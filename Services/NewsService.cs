using DotnetBackend.Models;
using MongoDB.Driver;

namespace DotnetBackend.Services;

public class NewsService
{
    private readonly IMongoCollection<News> _news;
    private readonly CounterService _counterService;

    public NewsService(MongoDbService mongoDbService, CounterService counterService)
    {
        _news = mongoDbService.GetCollection<News>("News");
        _counterService = counterService;
    }

    public async Task<News> CreateNewsAsync(News newNews)
    {

        var existingNews = await _news.Find(news => news.Text == newNews.Text).FirstOrDefaultAsync();

        if (existingNews != null)
        {
            throw new InvalidOperationException("Já existe uma notícia com esse texto");
        }

        newNews.Id = "N" + await _counterService.GetNextSequenceAsync("News");
        newNews.DateCreated = DateTime.UtcNow;

        _news.InsertOneAsync(newNews);

        return newNews;
    }

    public async Task<List<News>> GetAllNewsAsync()
    {
        return await _news.Find(_ => true).ToListAsync();
    }

    public async Task<bool> DeleteOneAsync(string id)
    {
        var deleteResult = await _news.DeleteOneAsync(n => n.Id == id);
        return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
    }
}