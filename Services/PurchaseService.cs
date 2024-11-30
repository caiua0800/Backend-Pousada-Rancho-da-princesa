using MongoDB.Driver;
using DotnetBackend.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DotnetBackend.Services
{
    public class PurchaseService
    {
        private readonly IMongoCollection<Purchase> _purchases;
        private readonly CounterService _counterService;
        private readonly ExtractService _extractService;
        private readonly ClientService _clientService;
        private readonly ContractService _contractService;
        public PurchaseService(MongoDbService mongoDbService, ClientService clientService, CounterService counterService, ExtractService extractService, ContractService contractService)
        {
            _purchases = mongoDbService.GetCollection<Purchase>("Purchases");
            _counterService = counterService;
            _extractService = extractService;
            _clientService = clientService;
            _contractService = contractService;
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

            if (purchase.Type == 0)
            {
                value = purchase.UnityPrice;
                descrip = "Contrato Personalizado";
                title = "Contrato Personalizado";

                if (purchase.PercentageProfit > 0)
                {
                    Console.WriteLine("O contrato possui rendimento personalizado");
                    gain = purchase.PercentageProfit;
                }
                else if (client.ClientProfit > 0)
                {
                    Console.WriteLine("O cliente possui rendimento padrão");
                    gain = (double)client.ClientProfit;
                }
                else
                {
                    Console.WriteLine("Rendimento do contrato setado com 150%");
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
            purchase.EndContractDate = purchase.EndContractDate;
            purchase.CurrentIncome = 0;
            purchase.AmountWithdrawn = 0;
            purchase.UnityPrice = value;
            purchase.Description = descrip;
            TimeZoneInfo brtZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            DateTime currentBrasiliaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, brtZone);
            purchase.PurchaseDate = currentBrasiliaTime; purchase.ProductName = title;
            purchase.Status = 1;

            await _purchases.InsertOneAsync(purchase);
            var extract = new Extract("Compra", purchase.TotalPrice, purchase.ClientId);
            await _extractService.CreateExtractAsync(extract);
            await _clientService.AddPurchaseAsync(purchase.ClientId, purchase.PurchaseId);

            return purchase;
        }

        private DateTime GetEndContractDate(int duration)
        {
            return DateTime.UtcNow.AddMonths(duration);
        }

        public async Task<List<Purchase>> GetAllPurchasesAsync()
        {
            return await _purchases.Find(_ => true).ToListAsync();
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

        public async Task<bool> UpdatePurchaseAsync(string purchaseId, decimal amountWithdrawn)
        {
            var existingPurchase = await GetPurchaseByIdAsync(purchaseId);
            if (existingPurchase == null)
            {
                return false; // Compra não encontrada
            }

            existingPurchase.AmountWithdrawn += amountWithdrawn;

            var replaceResult = await _purchases.ReplaceOneAsync(
                p => p.PurchaseId == purchaseId, existingPurchase);

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
            // Obtenha a compra existente
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

            if (amount > (existingClient.Balance - (existingClient.BlockedBalance ?? 0)))
            {
                throw new InvalidOperationException("Saldo insuficiente para realizar o incremento.");
            }

            existingPurchase.TotalPrice += amount;
            existingPurchase.AmountPaid += amount;
            Console.WriteLine($"O valor do Final Income era {existingPurchase.FinalIncome} e agora somando {(amount * (decimal)(existingPurchase.PercentageProfit))} ficou {existingPurchase.FinalIncome + (amount * (decimal)(existingPurchase.PercentageProfit / 100))}");
            existingPurchase.FinalIncome += (amount * ((decimal)(existingPurchase.PercentageProfit)));

            var updateDefinition = Builders<Purchase>.Update
                .Set(p => p.TotalPrice, existingPurchase.TotalPrice)
                .Set(p => p.AmountPaid, existingPurchase.AmountPaid)
                .Set(p => p.FinalIncome, existingPurchase.FinalIncome);

            await _purchases.UpdateOneAsync(p => p.PurchaseId == purchaseId, updateDefinition);

            var extract = new Extract($"Incremento no contrato {purchaseId} de R${amount}", amount, existingClient.Id);
            await _extractService.CreateExtractAsync(extract);
            await _clientService.WithdrawFromBalanceAsync(existingClient.Id, (amount / 2));
            await _clientService.AddToBlockedBalanceAsync(existingClient.Id, (amount / 2));
            await WithdrawFromPurchaseAsync(purchaseId, amount);
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
}
