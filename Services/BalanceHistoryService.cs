using MongoDB.Driver;
using DotnetBackend.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualBasic;


namespace DotnetBackend.Services;

public class BalanceHistoryService
{
    private readonly IMongoCollection<BalanceHistory> _balanceHistories;


    public BalanceHistoryService(MongoDbService mongoDbService)
    {
        _balanceHistories = mongoDbService.GetCollection<BalanceHistory>("BalanceHistories");
    }

    public async Task<BalanceHistory> CreateBalanceHistoryAsync(BalanceHistory balanceHistory)
    {
        if (string.IsNullOrWhiteSpace(balanceHistory.ClientId))
        {
            throw new ArgumentException("O Client ID deve ser fornecido.");
        }

        var existingBalanceHistory = await _balanceHistories.Find(c => c.ClientId == balanceHistory.ClientId).FirstOrDefaultAsync();
        if (existingBalanceHistory != null)
        {
            throw new InvalidOperationException("Já existe um existingBalanceHistory com este Id.");
        }

        await _balanceHistories.InsertOneAsync(balanceHistory);

        return balanceHistory;
    }

    public async Task<List<BalanceHistory>> GetAllBalanceHistoriesAsync()
    {
        return await _balanceHistories.Find(_ => true).ToListAsync();
    }

    public async Task<BalanceHistory?> GetBalanceHistoryByIdAsync(string id)
    {
        var normalizedId = id.Trim();
        return await _balanceHistories.Find(c => c.ClientId == id).FirstOrDefaultAsync();
    }

    public async Task<bool> AddNewHistory(string clientId, decimal newValue)
    {
        var balanceHistory = await GetBalanceHistoryByIdAsync(clientId);

        if (balanceHistory == null)
        {
            throw new InvalidOperationException("balanceHistory não encontrado.");
        }

        if (balanceHistory.Items == null)
        {
            balanceHistory.Items = new List<BalanceHistoryItem>();
        }

        var currentDate = DateTime.UtcNow;

        var existingItem = balanceHistory.Items
            .FirstOrDefault(item => item.DateCreated.HasValue
                                    && item.DateCreated.Value.Year == currentDate.Year
                                    && item.DateCreated.Value.Month == currentDate.Month);

        if (existingItem != null)
        {
            existingItem.Value = newValue;
        }
        else
        {
            balanceHistory.Items.Add(new BalanceHistoryItem(currentDate, newValue));
        }

        var updateDefinition = Builders<BalanceHistory>.Update.Set(c => c.Items, balanceHistory.Items);

        var result = await _balanceHistories.UpdateOneAsync(c => c.ClientId == clientId, updateDefinition);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> UpdateBalanceHistoryCurrent(string clientId, decimal newValue)
    {
        if (clientId == null)
        {
            throw new Exception("Id Nullo.");
        }

        var currentClient = await _balanceHistories.Find(c => c.ClientId == clientId).FirstOrDefaultAsync();
        if (currentClient == null)
        {
            throw new Exception("Balance History não encontrado.");
        }

        currentClient.Current = newValue;
        var updateDefinition = Builders<BalanceHistory>.Update.Set(c => c.Current, newValue);

        var result = await _balanceHistories.UpdateOneAsync(c => c.ClientId == clientId, updateDefinition);

        return result.ModifiedCount > 0;
    }


}

