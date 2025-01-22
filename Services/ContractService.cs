using MongoDB.Driver;
using DotnetBackend.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace DotnetBackend.Services
{
    public class ContractService
    {
        private readonly IMongoCollection<ContractModel> _contractModels;
        private readonly CounterService _counterService;

        public ContractService(MongoDbService mongoDbService, CounterService counterService)
        {
            _contractModels = mongoDbService.GetCollection<ContractModel>("ContractModels");
            _counterService = counterService;
        }

        public async Task<ContractModel> CreateContractAsync(ContractModel contractModel)
        {
            int nextSequence = await _counterService.GetNextSequenceAsync("purchaseModels");
            contractModel.Id = "Model" + nextSequence;

            await _contractModels.InsertOneAsync(contractModel);
            return contractModel;
        }

        public async Task<bool> DeleteContractAsync(string id)
        {
            var deleteResult = await _contractModels.DeleteOneAsync(c => c.Id == id);
            return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0; // Retorna true se a exclusão for bem-sucedida
        }

        public async Task<ContractModel?> GetContractByIdAsync(string id)
        {
            return await _contractModels.Find(c => c.Id == id).FirstOrDefaultAsync(); // Busca o contrato pelo ID
        }

        public async Task<bool> ReplaceContractAsync(string id, ContractModel updatedModel)
        {
            var result = await _contractModels.ReplaceOneAsync(c => c.Id == id, updatedModel);
            return result.ModifiedCount > 0;
        }

        public async Task<List<ContractModel>> GetAllContractsAsync()
        {
            return await _contractModels.Find(_ => true).ToListAsync();
        }

        public async Task<bool> UpdateContractAsync(string id, ContractModel contractModel)
        {
            contractModel.Id = id;
            var existingContract = await _contractModels.Find(c => c.Id == id).FirstOrDefaultAsync();
            Console.WriteLine("editando");
            if (existingContract != null)
            {
                var deleteResult = await _contractModels.DeleteOneAsync(c => c.Id == id);
                if (!deleteResult.IsAcknowledged)
                {
                    return false;
                }
            }
            await _contractModels.InsertOneAsync(contractModel);
            return true;
        }
    }
}
