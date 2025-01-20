using MongoDB.Driver;
using DotnetBackend.Models;
using Microsoft.AspNetCore.Identity; // Para usar PasswordHasher
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualBasic;


namespace DotnetBackend.Services
{
    public class ClientService
    {
        private readonly IMongoCollection<Client> _clients;
        private readonly EmailService _emailService; 
        private readonly ExtractService _extractService;
        private readonly BalanceHistoryService _balanceHistoryService;


        public ClientService(MongoDbService mongoDbService, EmailService emailService,
         ExtractService extractService, BalanceHistoryService balanceHistoryService) 
        {
            _clients = mongoDbService.GetCollection<Client>("Clients");
            _emailService = emailService; 
            _extractService = extractService;
            _balanceHistoryService = balanceHistoryService;
        }

        public async Task<Client> CreateClientAsync(Client client, string password)
        {
            if (string.IsNullOrWhiteSpace(client.Id))
            {
                throw new ArgumentException("O CPF (Id) deve ser fornecido.");
            }

            var existingClient = await _clients.Find(c => c.Id == client.Id).FirstOrDefaultAsync();
            if (existingClient != null)
            {
                throw new InvalidOperationException("Já existe um cliente com este CPF.");
            }

            var passwordHasher = new PasswordHasher<Client>();
            client.Password = passwordHasher.HashPassword(client, password);
            client.ClientProfit = 1.5;
            client.DateCreated = DateTime.UtcNow;
            client.Status = 1;
            Console.WriteLine($"Data de Criação antes da inserção: {client.DateCreated}");
            await _clients.InsertOneAsync(client);
            BalanceHistory balll = new BalanceHistory();
            balll.ClientId = client.Id;
            balll.Current = 0;
            await _balanceHistoryService.CreateBalanceHistoryAsync(balll);

            try
            {
                await _emailService.SendEmailAsync(
                    client.Email,
                    "Cadastro realizado com sucesso",
                    "Obrigado por se cadastrar!",
                    "<strong>Obrigado por se cadastrar!</strong>"
                );
                return client;
            }
            catch (System.Exception)
            {
                return client;
            }
        }

        public async Task<List<Client>> GetAllClientsAsync()
        {
            return await _clients.Find(_ => true).ToListAsync();
        }

        public async Task<List<Client>> GetAllClientsByConsultorIdAsync(string consultorId)
        {
            return await _clients.Find(client => client.ConsultorId == consultorId).ToListAsync();
        }

        public async Task<List<Client>> GetAllClientsWithCreationDateFilterAsync(int dateFilter)
        {
            if (dateFilter < 0)
            {
                throw new ArgumentException("O filtro de data deve ser um número positivo representando a quantidade de dias.");
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-dateFilter);

            return await _clients.Find(client => client.DateCreated >= cutoffDate).ToListAsync();
        }

        public async Task<List<Client>> GetAllClientsWithCreationDateAndConsultorFilterAsync(int dateFilter, string consultorId)
        {
            if (dateFilter < 0)
            {
                throw new ArgumentException("O filtro de data deve ser um número positivo representando a quantidade de dias.");
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-dateFilter);

            return await _clients.Find(client => client.DateCreated >= cutoffDate && client.ConsultorId == consultorId).ToListAsync();
        }

        public async Task<bool> DeleteClientAsync(string id)
        {
            var deleteResult = await _clients.DeleteOneAsync(c => c.Id == id);
            return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
        }

        public async Task<Client?> GetClientByIdAsync(string id)
        {
            var normalizedId = id.Trim();
            return await _clients.Find(c => c.Id == normalizedId).FirstOrDefaultAsync();
        }

        public async Task<Client?> GetClientByEmailAsync(string email)
        {
            var normalizedEmail = email.Trim();
            return await _clients.Find(c => c.Email == normalizedEmail).FirstOrDefaultAsync();
        }

        public async Task<string> UploadProfilePictureAsync(IFormFile file)
        {
            var bucketName = "oscar-plataforma";
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);

            try
            {
                using (var client = new AmazonS3Client(Amazon.RegionEndpoint.SAEast1)) // Escolha a região apropriada
                {
                    var fileTransferUtility = new TransferUtility(client);
                    await fileTransferUtility.UploadAsync(file.OpenReadStream(), bucketName, fileName);
                }

                return $"https://{bucketName}.s3.amazonaws.com/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar a imagem para o S3: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> UpdatePasswordAsync(string clientId, string newPassword)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null) throw new InvalidOperationException("Cliente não encontrado.");

            var passwordHasher = new PasswordHasher<Client>();
            client.Password = passwordHasher.HashPassword(client, newPassword);

            var updateDefinition = Builders<Client>.Update.Set(c => c.Password, client.Password);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddToBalanceAsync(string clientId, decimal amount)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            client.AddToBalance(amount);
            var updateDefinition = Builders<Client>.Update.Set(c => c.Balance, client.Balance);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            Console.WriteLine("Valor adicionado ao Balance do cliente");
            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddToExtraBalanceAsync(string clientId, decimal amount)
        {
            var client = await GetClientByIdAsync(clientId);


            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            client.AddToExtraBalance(amount);
            var updateDefinition = Builders<Client>.Update.Set(c => c.ExtraBalance, client.ExtraBalance);
            var extract = new Extract($"Valor de R${amount} adicionado ao Extra Balance do cliente com id: ${clientId}", amount, clientId);
            await _extractService.CreateExtractAsync(extract);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddToBlockedBalanceAsync(string clientId, decimal amount)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            client.AddToBlockedBalance(amount);
            Console.WriteLine($"Chamando função no Service para adicionar valor ao BlockedBalance");
            var updateDefinition = Builders<Client>.Update.Set(c => c.BlockedBalance, client.BlockedBalance);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> WithdrawFromBalanceAsync(string clientId, decimal amount)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            client.WithdrawFromBalance(amount);

            var updateDefinition = Builders<Client>.Update.Set(c => c.Balance, client.Balance);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> WithdrawFromExtraBalanceAsync(string clientId, decimal amount)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            client.WithdrawFromExtraBalance(amount);

            var extract = new Extract($"Valor de R$${amount} retirado do Extra Balance do cliente com id: ${clientId}. CANCELAMENTO INDICAÇÃO", amount, clientId);
            await _extractService.CreateExtractAsync(extract);
            var updateDefinition = Builders<Client>.Update.Set(c => c.ExtraBalance, client.ExtraBalance);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }


        public async Task<bool> WithdrawFromBlockedBalanceAsync(string clientId, decimal amount)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            client.WithdrawFromBlockedBalance(amount);

            var updateDefinition = Builders<Client>.Update.Set(c => c.BlockedBalance, client.BlockedBalance);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateClientAsync(string id, Client updatedClient)
        {
            if (updatedClient.Id != id)
            {
                throw new ArgumentException("O ID do cliente atualizado deve coincidir com o ID do cliente que está sendo atualizado.");
            }

            var currentClient = await _clients.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (currentClient == null)
            {
                throw new Exception("Cliente não encontrado.");
            }

            var updateDefinition = Builders<Client>.Update;
            var updateFields = new List<UpdateDefinition<Client>>();

            Console.WriteLine(updatedClient.Password);

            if (updatedClient.Name != currentClient.Name)
            {
                updateFields.Add(updateDefinition.Set(c => c.Name, updatedClient.Name));
            }
            if (updatedClient.Email != currentClient.Email)
            {
                updateFields.Add(updateDefinition.Set(c => c.Email, updatedClient.Email));
            }
            if (updatedClient.Phone != currentClient.Phone)
            {
                updateFields.Add(updateDefinition.Set(c => c.Phone, updatedClient.Phone));
            }
            if (updatedClient.SponsorId != currentClient.SponsorId)
            {
                updateFields.Add(updateDefinition.Set(c => c.SponsorId, updatedClient.SponsorId));
            }
            if (updatedClient.ClientProfit != currentClient.ClientProfit)
            {
                updateFields.Add(updateDefinition.Set(c => c.ClientProfit, updatedClient.ClientProfit));
            }

            if (updatedClient.Address != null)
            {
                if (updatedClient.Address.Street != currentClient.Address.Street)
                {
                    updateFields.Add(updateDefinition.Set(c => c.Address.Street, updatedClient.Address.Street));
                }
                if (updatedClient.Address.Number != currentClient.Address.Number)
                {
                    updateFields.Add(updateDefinition.Set(c => c.Address.Number, updatedClient.Address.Number));
                }
                if (updatedClient.Address.Zipcode != currentClient.Address.Zipcode)
                {
                    updateFields.Add(updateDefinition.Set(c => c.Address.Zipcode, updatedClient.Address.Zipcode));
                }
                if (updatedClient.Address.Neighborhood != currentClient.Address.Neighborhood)
                {
                    updateFields.Add(updateDefinition.Set(c => c.Address.Neighborhood, updatedClient.Address.Neighborhood));
                }
                if (updatedClient.Address.City != currentClient.Address.City)
                {
                    updateFields.Add(updateDefinition.Set(c => c.Address.City, updatedClient.Address.City));
                }
                if (updatedClient.Address.State != currentClient.Address.State)
                {
                    updateFields.Add(updateDefinition.Set(c => c.Address.State, updatedClient.Address.State));
                }
                if (updatedClient.Address.Country != currentClient.Address.Country)
                {
                    updateFields.Add(updateDefinition.Set(c => c.Address.Country, updatedClient.Address.Country));
                }
            }

            if (updatedClient.PlatformId != currentClient.PlatformId)
            {
                updateFields.Add(updateDefinition.Set(c => c.PlatformId, updatedClient.PlatformId));
            }
            if (updatedClient.Balance != currentClient.Balance)
            {
                updateFields.Add(updateDefinition.Set(c => c.Balance, updatedClient.Balance));
            }
            if (updatedClient.DateCreated != currentClient.DateCreated)
            {
                updateFields.Add(updateDefinition.Set(c => c.DateCreated, updatedClient.DateCreated));
            }

            if (updateFields.Count > 0)
            {
                var updateResult = await _clients.UpdateOneAsync(c => c.Id == id, Builders<Client>.Update.Combine(updateFields));
                return updateResult.ModifiedCount > 0;
            }

            return false;
        }

        public async Task<bool> AddPurchaseAsync(string clientId, string purchaseId)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            if (client.WalletExtract == null)
            {
                client.WalletExtract = new WalletExtract();
            }

            client.WalletExtract.Purchases.Add(purchaseId);

            var updateDefinition = Builders<Client>.Update.Set(c => c.WalletExtract.Purchases, client.WalletExtract.Purchases);

            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddWithdrawalAsync(string clientId, string withdrawalId)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            if (client.WalletExtract == null)
            {
                client.WalletExtract = new WalletExtract();
            }

            client.WalletExtract.Withdrawals.Add(withdrawalId);

            var updateDefinition = Builders<Client>.Update.Set(c => c.WalletExtract.Withdrawals, client.WalletExtract.Withdrawals);

            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> ExcludeAccount(string clientId)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }


            client.Status = 2;

            var updateDefinition = Builders<Client>.Update.Set(c => c.Status, client.Status);

            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemovePurchaseAsync(string clientId, string purchaseId)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }

            if (client.WalletExtract.Purchases.Contains(purchaseId))
            {
                client.WalletExtract.Purchases.Remove(purchaseId);
            }

            var updateDefinition = Builders<Client>.Update.Set(c => c.WalletExtract.Purchases, client.WalletExtract.Purchases);

            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }


        public async Task<bool> VerifyPasswordAsync(string clientId, string password)
        {
            var client = await GetClientByIdAsync(clientId);
            if (client == null || string.IsNullOrEmpty(client.Password))
            {
                Console.WriteLine($"Cliente não encontrado ou senha não definida para ID: {clientId}");
                return false;
            }

            var passwordHasher = new PasswordHasher<Client>();
            var result = passwordHasher.VerifyHashedPassword(client, client.Password, password);

            Console.WriteLine($"Hash Armazenado: {client.Password}");
            Console.WriteLine($"Resultado da verificação: {result}");

            return result == PasswordVerificationResult.Success;
        }


        public async Task<bool> UpdateClientName(string clientId, string newValue)
        {
            if (clientId == null)
            {
                throw new Exception("Id Nullo.");
            }

            var currentClient = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
            if (currentClient == null)
            {
                throw new Exception("Cliente não encontrado.");
            }

            currentClient.Name = newValue;
            var updateDefinition = Builders<Client>.Update.Set(c => c.Name, newValue);

            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateClientEmail(string clientId, string newValue)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentException("O ID do cliente não pode ser nulo ou vazio.");
            }

            var currentClient = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
            if (currentClient == null)
            {
                throw new Exception("Cliente não encontrado.");
            }

            var responseIfExists = await _clients.Find(c => c.Email == newValue).FirstOrDefaultAsync();

            if (responseIfExists != null)
            {
                throw new Exception("Email já existente.");
            }

            var updateDefinition = Builders<Client>.Update.Set(c => c.Email, newValue);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateClientConsultor(string clientId, string newValue)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentException("O ID do cliente não pode ser nulo ou vazio.");
            }

            var currentClient = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
            if (currentClient == null)
            {
                throw new Exception("Cliente não encontrado.");
            }

            var updateDefinition = Builders<Client>.Update.Set(c => c.ConsultorId, newValue);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateClientSponsor(string clientId, string newValue)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentException("O ID do cliente não pode ser nulo ou vazio.");
            }

            var currentClient = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
            if (currentClient == null)
            {
                throw new Exception("Cliente não encontrado.");
            }

            var updateDefinition = Builders<Client>.Update.Set(c => c.SponsorId, newValue);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateClientClientProfit(string clientId, double newValue)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentException("O ID do cliente não pode ser nulo ou vazio.");
            }

            var currentClient = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
            if (currentClient == null)
            {
                throw new Exception("Cliente não encontrado.");
            }

            var updateDefinition = Builders<Client>.Update.Set(c => c.ClientProfit, newValue);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateClientAddress(string clientId, Address newAddress)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentException("O ID do cliente não pode ser nulo ou vazio.");
            }

            var currentClient = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
            if (currentClient == null)
            {
                throw new Exception("Cliente não encontrado.");
            }

            var updateDefinition = Builders<Client>.Update
                .Set(c => c.Address.Street, newAddress.Street)
                .Set(c => c.Address.Number, newAddress.Number)
                .Set(c => c.Address.Zipcode, newAddress.Zipcode)
                .Set(c => c.Address.Neighborhood, newAddress.Neighborhood)
                .Set(c => c.Address.City, newAddress.City)
                .Set(c => c.Address.State, newAddress.State)
                .Set(c => c.Address.Country, newAddress.Country);

            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateClientSponsorId(string clientId, string newValue)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentException("O ID do cliente não pode ser nulo ou vazio.");
            }

            var currentClient = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
            if (currentClient == null)
            {
                throw new Exception("Cliente não encontrado.");
            }

            var updateDefinition = Builders<Client>.Update.Set(c => c.SponsorId, newValue);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateClientPhone(string clientId, string newValue)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentException("O ID do cliente não pode ser nulo ou vazio.");
            }

            var currentClient = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
            if (currentClient == null)
            {
                throw new Exception("Cliente não encontrado.");
            }

            var updateDefinition = Builders<Client>.Update.Set(c => c.Phone, newValue);
            var result = await _clients.UpdateOneAsync(c => c.Id == clientId, updateDefinition);
            return result.ModifiedCount > 0;
        }
    }
}
