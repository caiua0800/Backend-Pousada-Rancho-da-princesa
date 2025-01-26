using MongoDB.Driver;
using DotnetBackend.Models;
using System.Threading.Tasks;

namespace DotnetBackend.Services
{
    public class CounterService
    {
        private readonly IMongoCollection<Counter> _counters;

        public CounterService(MongoDbService mongoDbService)
        {
            _counters = mongoDbService.GetCollection<Counter>("Counters");
        }

        public async Task<int> GetNextSequenceAsync(string counterName)
        {
            var filter = Builders<Counter>.Filter.Eq(c => c.Id, counterName);
            var update = Builders<Counter>.Update.Inc(c => c.SequenceValue, 1);
            var options = new FindOneAndUpdateOptions<Counter>
            {
                IsUpsert = true, 
                ReturnDocument = ReturnDocument.After
            };
            var counter = await _counters.FindOneAndUpdateAsync(filter, update, options);
            return counter.SequenceValue;
        }
    }
}
