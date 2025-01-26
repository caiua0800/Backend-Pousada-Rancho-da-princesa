using MongoDB.Driver;
using DotnetBackend.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotnetBackend.Services;

namespace DotnetBackend.Queries
{
    public class ClientQueries
    {
        private readonly IMongoCollection<Client> _clients;

        public ClientQueries(MongoDbService mongoDbService)
        {
            _clients = mongoDbService.GetCollection<Client>("Clients");
        }

        // Método para pegar todos os clientes em ordem alfabética
        public async Task<List<Client>> GetAllClientsSortedAsync()
        {
            var clients = await _clients.Find(_ => true).ToListAsync();
            return clients.OrderBy(c => c.Name).ToList(); // Ordena por nome
        }
    }
}
