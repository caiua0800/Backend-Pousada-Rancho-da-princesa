using System.Collections;
using DotnetBackend.Models;
using DotnetBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotnetBackend.Controllers;


[ApiController]
[Route("api/[controller]")]
public class NewsController : Controller
{
    private readonly NewsService _newsServices;

    public NewsController(NewsService newsService)
    {
        _newsServices = newsService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(News news)
    {
        if (news == null)
        {
            return BadRequest("Notícia Null.");
        }

        Console.WriteLine("Criando Notícia");
        var createdNews = await _newsServices.CreateNewsAsync(news);

        return CreatedAtAction(nameof(Create), new { id = createdNews.Id }, createdNews);
    }

    [HttpGet]
    public async Task<List<News>> GetAllNews()
    {
        return await _newsServices.GetAllNewsAsync();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNews(string id)
    {
        var result = await _newsServices.DeleteOneAsync(id);

         if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }
}