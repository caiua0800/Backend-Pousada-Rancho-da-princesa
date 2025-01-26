using MongoDB.Driver;
using DotnetBackend.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace DotnetBackend.Services;

public class ChaleService
{
    private readonly IMongoCollection<Chale> _chales;

    public ChaleService(MongoDbService mongoDbService)
    {
        _chales = mongoDbService.GetCollection<Chale>("Chales");
    }

    public async Task<List<Chale>> GetAllChalesAsync()
    {
        return await _chales.Find(_ => true).ToListAsync();
    }

    public async Task<bool> DeleteChaleAsync(string name)
    {
        var deleteResult = await _chales.DeleteOneAsync(c => c.Name == name);
        return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
    }

    public async Task<Chale?> GetChaleByIdAsync(string id)
    {
        var normalizedId = id.Trim();
        return await _chales.Find(c => c.Name == normalizedId).FirstOrDefaultAsync();
    }

    public async Task<Chale> CreateChaleAsync(Chale chale)
    {
        if (string.IsNullOrWhiteSpace(chale.Name))
        {
            throw new ArgumentException("O Nome do chalé deve ser fornecido.");
        }

        var existingUser = await _chales.Find(a => a.Name == chale.Name).FirstOrDefaultAsync();
        if (existingUser != null)
        {
            throw new InvalidOperationException("Já existe um administrador com este ID.");
        }

        await _chales.InsertOneAsync(chale);
        return chale;
    }

    public async Task<bool> UpdateChaleCurrentPrice(string chaleName, decimal newValue)
    {
        if (chaleName == null)
        {
            throw new Exception("chaleName Nullo.");
        }

        var currentChale = await _chales.Find(c => c.Name == chaleName).FirstOrDefaultAsync();
        if (currentChale == null)
        {
            throw new Exception("chaleName não encontrado.");
        }

        currentChale.CurrentPrice = newValue;
        var updateDefinition = Builders<Chale>.Update.Set(c => c.CurrentPrice, newValue);

        var result = await _chales.UpdateOneAsync(c => c.Name == chaleName, updateDefinition);

        return result.ModifiedCount > 0;
    }
}
