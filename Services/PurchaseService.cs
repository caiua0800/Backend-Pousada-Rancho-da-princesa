using MongoDB.Driver;
using DotnetBackend.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;


namespace DotnetBackend.Services
{
    public class PurchaseService
    {
        private readonly IMongoCollection<Purchase> _purchases;
        private readonly CounterService _counterService;
        private readonly ExtractService _extractService;
        private readonly ClientService _clientService;
        private readonly ContractService _contractService;
        private readonly BankAccountService _bankAccountService;
        public PurchaseService(MongoDbService mongoDbService, ClientService clientService, CounterService counterService, ExtractService extractService, ContractService contractService, BankAccountService bankAccountService)
        {
            _purchases = mongoDbService.GetCollection<Purchase>("Purchases");
            _counterService = counterService;
            _extractService = extractService;
            _clientService = clientService;
            _contractService = contractService;
            _bankAccountService = bankAccountService;
        }

        public async Task<Purchase> CreatePurchaseAsync(Purchase purchase)
        {
            purchase.PurchaseId = "A" + await _counterService.GetNextSequenceAsync("purchases");
            var contractId = "Model" + purchase.Type;
            var contract = await _contractService.GetContractByIdAsync(contractId);
            Client client = await _clientService.GetClientByIdAsync(purchase.ClientId);

            decimal value;
            string descrip;
            string title;
            double gain;

            Console.WriteLine(purchase.ProductName);

            if (purchase.Type == 0)
            {
                value = purchase.UnityPrice;
                descrip = "Contrato Personalizado";
                title = "Contrato Personalizado";

                if (purchase.PercentageProfit > 0)
                {
                    gain = purchase.PercentageProfit;
                }
                else if (client.ClientProfit > 0)
                {
                    gain = (double)client.ClientProfit;
                }
                else
                {
                    gain = 1.5;
                }
            }
            else
            {
                value = (decimal)contract.Value;
                descrip = contract.Description;
                title = contract.Title;
                gain = contract.Gain;
            }

            purchase.TotalPrice = (purchase.Quantity * value);
            purchase.AmountPaid = (purchase.Quantity * value) - ((purchase.Quantity * value) * purchase.Discount);
            purchase.FinalIncome = purchase.Quantity * value * (decimal)gain;
            purchase.DaysToFirstWithdraw = purchase.DaysToFirstWithdraw;
            purchase.CurrentIncome = 0;
            purchase.AmountWithdrawn = 0;
            purchase.UnityPrice = value;
            purchase.Description = descrip;
            TimeZoneInfo brtZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            DateTime currentBrasiliaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, brtZone);
            purchase.PurchaseDate = currentBrasiliaTime;
            purchase.ProductName = purchase.ProductName;
            purchase.Status = 1;

            // Chamada à API PIX
            using (var httpClient = new HttpClient())
            {
                var requestPayload = new
                {
                    transaction_amount = purchase.AmountPaid,
                    description = "Pagamento de Teste",
                    paymentMethodId = "pix",
                    email = client.Email,
                    identificationType = "CPF",
                    number = client.Id
                };

                var jsonContent = JsonSerializer.Serialize(requestPayload);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // HttpResponseMessage response = await httpClient.PostAsync("http://servidoroscar.modelodesoftwae.com:3030/pix", httpContent);
                HttpResponseMessage response = await httpClient.PostAsync("http://localhost:3030/pix", httpContent);


                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var pixResponse = JsonSerializer.Deserialize<PixResponse>(responseBody);

                    purchase.TicketPayment = pixResponse?.PointOfInteraction.TransactionData.TicketUrl;
                    purchase.QrCode = pixResponse?.PointOfInteraction.TransactionData.QrCode;
                    purchase.QrCodeBase64 = pixResponse?.PointOfInteraction.TransactionData.QrCodeBase64;
                    purchase.TicketId = pixResponse?.Id?.ToString();
                    purchase.ExpirationDate = pixResponse?.PointOfInteraction.TransactionData.ExpirationDate;
                    Console.WriteLine($"Ticket gerado com sucesso, id: {pixResponse?.Id}");
                }
                else
                {
                    throw new Exception($"Falha na requisição PIX: {response.ReasonPhrase}");
                }
            }

            await _purchases.InsertOneAsync(purchase);
            var extract = new Extract($"Compra {purchase.ProductName}", purchase.TotalPrice, purchase.ClientId);
            Console.WriteLine($"Compra {purchase.ProductName}");
            await _extractService.CreateExtractAsync(extract);
            await _clientService.AddPurchaseAsync(purchase.ClientId, purchase.PurchaseId);

            return purchase;
        }

        public async Task<List<Purchase>> GetAllPurchasesAsync()
        {
            return await _purchases.Find(_ => true).ToListAsync();
        }

        public async Task<List<Purchase>> GetAllPurchasesFilteredAsync(int dateFilter)
        {
            if (dateFilter < 0)
            {
                throw new ArgumentException("O filtro de data deve ser um número positivo representando a quantidade de dias.");
            }
            var cutoffDate = DateTime.UtcNow.AddDays(-dateFilter);

            return await _purchases.Find(purchase => purchase.PurchaseDate >= cutoffDate).ToListAsync();
        }

        public async Task<List<Purchase>> GetLast50PurchasesAsync()
        {
            return await _purchases
                .Find(_ => true)
                .SortByDescending(e => e.PurchaseId)
                .Limit(50)
                .ToListAsync();
        }

        public string GetDescription(int type)
        {
            switch (type)
            {
                case 1:
                    return "Contrato de Minérios, configurado com lucro final de 150% no período de 3 anos.";
                case 2:
                    return "Contrato de Minérios, configurado com lucro final de 45% no período de 1 ano.";
                case 3:
                    return "Contrato Diamante, configurado com lucro final de 200% no período de 5 anos.";
                default:
                    return "";
            }
        }

        public String GetProductName(int type)
        {
            switch (type)
            {
                case 1:
                    return "Contrato de Minérios";
                case 2:
                    return "Contrato de Diamantes";
                case 3:
                    return "Contrato Cotas";
                default:
                    return "";
            }
        }
        public async Task<bool> DeletePurchaseAsync(string purchaseId)
        {
            var existingPurchase = await GetPurchaseByIdAsync(purchaseId);
            if (existingPurchase == null)
            {
                return false; // Retorna false se a compra não for encontrada
            }

            var deleteResult = await _purchases.DeleteOneAsync(p => p.PurchaseId == purchaseId);
            if (!deleteResult.IsAcknowledged || deleteResult.DeletedCount == 0)
            {
                return false; // Retorna false se a exclusão falhar
            }

            await _clientService.RemovePurchaseAsync(existingPurchase.ClientId, purchaseId); // Lógica adicional se necessário
            return true; // Retorna true se a exclusão foi bem-sucedida
        }

        public async Task<Purchase?> GetPurchaseByIdAsync(string id)
        {
            var normalizedId = id.Trim();
            return await _purchases.Find(p => p.PurchaseId == normalizedId).FirstOrDefaultAsync();
        }

        public async Task<List<Purchase>> GetPurchasesByClientIdAsync(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("Client ID must be provided.", nameof(clientId));
            }

            return await _purchases.Find(p => p.ClientId == clientId).ToListAsync();
        }

        public async Task<bool> VerifyPayment(string idPurchase, string ticketId)
        {
            var mpUrl = $"https://api.mercadopago.com/v1/payments/{ticketId}";
            var accessToken = "APP_USR-1375204330701481-073021-97be99fab97882aa55c07ffe1e81ec7e-246170016"; // Idealmente, mova isso para appsettings.json.

            using (var httpClient = new HttpClient())
            {
                try
                {
                    // Configura o cabeçalho com o Token de Acesso
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    // Faz a requisição GET para verificar o pagamento
                    var response = await httpClient.GetAsync(mpUrl);
                    response.EnsureSuccessStatusCode(); // Lança uma exceção se a resposta não for bem-sucedida

                    // Lê o conteúdo da resposta
                    var responseBody = await response.Content.ReadAsStringAsync();

                    Console.WriteLine("Resultado da Verificação do Pagamento:");
                    Console.WriteLine(responseBody);

                    // Deserializa a resposta para um objeto StatusResponse
                    var statusResult = JsonSerializer.Deserialize<StatusResponse>(responseBody);

                    if (statusResult != null && (statusResult.Status == "approved" || statusResult.Status == "authorized"))
                    {
                        await UpdateStatus(idPurchase, 2); // Atualiza o status para 2 se o pagamento for aprovado
                        return true;
                    }

                    Console.WriteLine(statusResult.Status);

                    return false; // Retorna false para qualquer outro status
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("ERRO NA VERIFICAÇÃO DO PAGAMENTO:");
                    Console.WriteLine(e.Message);
                    return false; // Retorna false em caso de erro
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine("Erro ao deserializar a resposta do pagamento:");
                    Console.WriteLine(jsonEx.Message);
                    return false; // Retorna false se ocorrer um erro de deserialização
                }
            }
        }

        public async Task<bool> WithdrawFromPurchaseAsync(string purchaseId, decimal amount)
        {
            Purchase purchase = await GetPurchaseByIdAsync(purchaseId);
            if (purchase == null)
            {
                throw new InvalidOperationException("Compra não encontrado.");
            }

            purchase.WithdrawFromPurchase(amount);

            var updateDefinition = Builders<Purchase>.Update.Set(c => c.AmountWithdrawn, purchase.AmountWithdrawn);
            var result = await _purchases.UpdateOneAsync(c => c.PurchaseId == purchaseId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdatePurchaseAsync(string purchaseId, Purchase newPurchase)
        {
            var existingPurchase = await GetPurchaseByIdAsync(purchaseId);
            if (existingPurchase == null)
            {
                return false;
            }

            newPurchase.PurchaseId = existingPurchase.PurchaseId;

            var replaceResult = await _purchases.ReplaceOneAsync(
                p => p.PurchaseId == purchaseId, newPurchase);

            return replaceResult.IsAcknowledged && replaceResult.ModifiedCount > 0;
        }
        public async Task<bool> AnticipateProfit(string purchaseId, decimal increasement)
        {
            Purchase? existingPurchase = await GetPurchaseByIdAsync(purchaseId);

            if (existingPurchase == null)
            {
                return false;
            }

            existingPurchase.CurrentIncome += increasement;

            var extract = new Extract($"Antecipação de lucro do contrato {purchaseId} no valor de R${increasement}", increasement, existingPurchase.ClientId);
            await _extractService.CreateExtractAsync(extract);

            await _clientService.AddToBalanceAsync(existingPurchase.ClientId, increasement);

            var updateDefinition = Builders<Purchase>.Update.Set(p => p.CurrentIncome, existingPurchase.CurrentIncome);
            var updateResult = await _purchases.UpdateOneAsync(p => p.PurchaseId == purchaseId, updateDefinition);

            return updateResult.ModifiedCount > 0;
        }


        public async Task<bool> AddIncrementToPurchaseAsync(string purchaseId, decimal amount)
        {
            Console.WriteLine($"Iniciando o incremento no contrato: {purchaseId} com valor de R${amount}");

            // Obtenha a compra existente
            Purchase existingPurchase = await GetPurchaseByIdAsync(purchaseId);
            if (existingPurchase == null)
            {
                Console.WriteLine($"Compra com o ID {purchaseId} não encontrada.");
                return false;
            }

            Console.WriteLine($"Compra encontrada: {existingPurchase}");

            Client existingClient = await _clientService.GetClientByIdAsync(existingPurchase.ClientId);
            if (existingClient == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            Console.WriteLine($"Cliente encontrado: {existingClient.Id} com saldo de R${existingClient.Balance} e saldo bloqueado de R${existingClient.BlockedBalance}");

            decimal availableBalance = (decimal)existingClient.Balance - (existingClient.BlockedBalance ?? 0);
            if (amount > availableBalance)
            {
                Console.WriteLine($"Saldo insuficiente para realizar o incremento. Saldo disponível: R${availableBalance}, Valor solicitado: R${amount}");

                // Tentar obter o saldo faltante de outras compras com status 2
                decimal remainingAmount = amount - availableBalance;
                var purchasesWithStatus2 = await _purchases.Find(p => p.Status == 2 && p.ClientId == existingClient.Id).ToListAsync();

                foreach (var purchase in purchasesWithStatus2)
                {
                    decimal withdrawableAmount = purchase.CurrentIncome - purchase.AmountWithdrawn;

                    if (withdrawableAmount > 0)
                    {
                        if (withdrawableAmount >= remainingAmount)
                        {
                            // Pode retirar o restante necessário
                            await WithdrawFromPurchaseAsync(purchase.PurchaseId, remainingAmount);
                            Console.WriteLine($"Retirado R${remainingAmount} da compra {purchase.PurchaseId}.");
                            remainingAmount = 0; // Fim da necessidade de retirada
                            break;
                        }
                        else
                        {
                            // Retirar o máximo possível
                            await WithdrawFromPurchaseAsync(purchase.PurchaseId, withdrawableAmount);
                            Console.WriteLine($"Retirado R${withdrawableAmount} da compra {purchase.PurchaseId}.");
                            remainingAmount -= withdrawableAmount; // Reduz a quantidade que ainda precisa ser retirada
                        }
                    }
                }

                if (remainingAmount > 0)
                {
                    Console.WriteLine($"Após verificar outras compras, ainda faltam R${remainingAmount} para completar o incremento.");
                    throw new InvalidOperationException("Saldo insuficiente mesmo após retirar de outras compras.");
                }
            }

            // Adicionando a compra normalmente agora que temos saldo suficiente
            existingPurchase.TotalPrice += amount;
            existingPurchase.AmountPaid += amount;
            existingPurchase.FinalIncome += amount * (decimal)existingPurchase.PercentageProfit;
            Console.WriteLine($"O valor do Final Income era {existingPurchase.FinalIncome - (amount * (decimal)existingPurchase.PercentageProfit)} e agora ficou {existingPurchase.FinalIncome}");

            // Atualizar a compra
            var updateDefinition = Builders<Purchase>.Update
                .Set(p => p.TotalPrice, existingPurchase.TotalPrice)
                .Set(p => p.AmountPaid, existingPurchase.AmountPaid)
                .Set(p => p.FinalIncome, existingPurchase.FinalIncome);

            await _purchases.UpdateOneAsync(p => p.PurchaseId == purchaseId, updateDefinition);
            Console.WriteLine($"Compra com ID {purchaseId} atualizada com sucesso.");

            var extract = new Extract($"Incremento no contrato {purchaseId} de R${amount}", amount, existingClient.Id);
            await _extractService.CreateExtractAsync(extract);
            Console.WriteLine($"Extrato criado para o cliente {existingClient.Id}.");

            // Retirando do saldo do cliente
            await _clientService.WithdrawFromBalanceAsync(existingClient.Id, amount / 2);
            Console.WriteLine($"Valor de R${amount / 2} retirado do saldo do cliente {existingClient.Id}.");

            // Adicionando metade do valor ao saldo bloqueado
            await _clientService.AddToBlockedBalanceAsync(existingClient.Id, amount / 2);
            Console.WriteLine($"Valor de R${amount / 2} adicionado ao saldo bloqueado do cliente {existingClient.Id}.");

            // Realiza a retirada da compra existente se houver saldo suficiente
            if (existingPurchase.CurrentIncome - existingPurchase.AmountWithdrawn >= amount)
            {
                await WithdrawFromPurchaseAsync(existingPurchase.PurchaseId, amount);
                Console.WriteLine($"Saque de R${amount} realizado na compra {existingPurchase.PurchaseId}.");
            }
            else
            {
                // Aqui, a lógica já foi feita anteriormente para buscar outras compras com status 2
            }

            return true;
        }

        public async Task<bool> RemoveSomeAmountWithdrawn(string purchaseId, decimal amount)
        {
            Purchase existingPurchase = await GetPurchaseByIdAsync(purchaseId);
            if (existingPurchase == null)
            {
                return false;
            }

            Client existingClient = await _clientService.GetClientByIdAsync(existingPurchase.ClientId);
            if (existingClient == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            existingPurchase.AmountWithdrawn = existingPurchase.AmountWithdrawn - amount;

            var updateDefinition = Builders<Purchase>.Update
                .Set(p => p.AmountWithdrawn, existingPurchase.AmountWithdrawn);

            await _purchases.UpdateOneAsync(p => p.PurchaseId == purchaseId, updateDefinition);
            await _clientService.AddToBalanceAsync(existingClient.Id, amount);

            Purchase existingPurchase2 = await GetPurchaseByIdAsync(purchaseId);


            return true;
        }

        public async Task<bool> UpdateStatus(string purchaseId, int newStatus)
        {
            var existingPurchase = await GetPurchaseByIdAsync(purchaseId);

            if (existingPurchase != null)
            {
                existingPurchase.Status = newStatus;

                var updateDefinition = Builders<Purchase>.Update.Set(p => p.Status, newStatus);
                await _purchases.UpdateOneAsync(p => p.PurchaseId == purchaseId, updateDefinition);

                Console.WriteLine($"Contrato encontrado: {existingPurchase.PurchaseId}, novo status {newStatus}");

                if (newStatus == 2)
                {
                    await _clientService.AddToBalanceAsync(existingPurchase.ClientId, existingPurchase.TotalPrice);
                    await _clientService.AddToBlockedBalanceAsync(existingPurchase.ClientId, existingPurchase.TotalPrice);
                    // await _bankAccountService.AddToBalanceAsync(purchaseId, existingPurchase.AmountPaid);
                    Console.WriteLine($"Saldo do cliente {existingPurchase.ClientId} atualizado com o valor {existingPurchase.TotalPrice}");
                }

                return true;
            }
            else
            {
                Console.WriteLine($"Erro ao encontrar contrato {purchaseId}");
                return false;
            }
        }

        public async Task<bool> CancelPaymentContract(string purchaseId)
        {
            var existingPurchase = await GetPurchaseByIdAsync(purchaseId);

            if (existingPurchase != null && existingPurchase.Status != 2)
            {
                existingPurchase.Status = 4;

                var updateDefinition = Builders<Purchase>.Update.Set(p => p.Status, 4);
                await _purchases.UpdateOneAsync(p => p.PurchaseId == purchaseId, updateDefinition);

                Console.WriteLine($"Contrato encontrado: {existingPurchase.PurchaseId}, novo status 4");

                return true;
            }
            else
            {
                Console.WriteLine($"Erro ao encontrar contrato {purchaseId}");
                return false;
            }
        }

        public async Task<bool> FreeWithdrawStatus(string purchaseId, bool newStatus)
        {
            var existingPurchase = await GetPurchaseByIdAsync(purchaseId);

            if (existingPurchase != null)
            {
                existingPurchase.FreeWithdraw = newStatus;

                var updateDefinition = Builders<Purchase>.Update.Set(p => p.FreeWithdraw, newStatus);
                await _purchases.UpdateOneAsync(p => p.PurchaseId == purchaseId, updateDefinition);

                Console.WriteLine($"Contrato encontrado: {existingPurchase.PurchaseId}, free Withdraw {newStatus}");

                return true;
            }
            else
            {
                Console.WriteLine($"Erro ao encontrar contrato {purchaseId}");
                return false;
            }
        }

        public async Task<bool> CancelPurchase(string purchaseId)
        {
            Purchase existingPurchase = await GetPurchaseByIdAsync(purchaseId);

            if (existingPurchase != null)
            {
                var updateDefinition = Builders<Purchase>.Update
                    .Set(p => p.Status, 4)
                    .Set(p => p.CurrentIncome, 0);

                await _purchases.UpdateOneAsync(p => p.PurchaseId == purchaseId, updateDefinition);

                Console.WriteLine($"Contrato #{existingPurchase.PurchaseId} cancelado com sucesso");

                await _clientService.WithdrawFromBalanceAsync(existingPurchase.ClientId, existingPurchase.TotalPrice);
                await _clientService.WithdrawFromBlockedBalanceAsync(existingPurchase.ClientId, existingPurchase.TotalPrice);
                Console.WriteLine($"Saldo do cliente {existingPurchase.ClientId} decrementado no valor de {existingPurchase.TotalPrice}");
                var extract = new Extract($"Contrato #{purchaseId} cancelado.", existingPurchase.TotalPrice, existingPurchase.ClientId);
                await _extractService.CreateExtractAsync(extract);
                return true;
            }
            else
            {
                Console.WriteLine($"Erro ao encontrar contrato {purchaseId}");
                return false;
            }
        }

        public static implicit operator PurchaseService(WithdrawalService v)
        {
            throw new NotImplementedException();
        }

    }

    public class StatusResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
}
