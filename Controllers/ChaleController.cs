using Microsoft.AspNetCore.Mvc;
using DotnetBackend.Models;
using DotnetBackend.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DotnetBackend.Queries;
using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace DotnetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChaleController : ControllerBase
{
    private readonly ClientService _clientService;
    private readonly AuthService _authService;
    private readonly ChaleService _chaleService;

    public ChaleController(ClientService clientService,
     ExtractService extractService, AuthService authService, ChaleService chaleService)
    {
        _clientService = clientService;
        _authService = authService;
        _chaleService = chaleService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(Chale chale)
    {
        Console.WriteLine(chale.Name);

        if (chale == null)
        {
            return BadRequest("Client está nulo.");
        }

        var createdChale = await _chaleService.CreateChaleAsync(chale);


        return CreatedAtAction(nameof(Create), new { id = createdChale.Name }, createdChale);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }
        var chales = await _chaleService.GetAllChalesAsync();
        return Ok(chales);
    }


    [HttpDelete("{id}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Delete(string id)
    {

        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var result = await _chaleService.DeleteChaleAsync(id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetChaleById(string name)
    {

        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var chale = await _chaleService.GetChaleByIdAsync(name);
        if (chale == null)
        {
            Console.WriteLine($"Cliente com nome '{name}' não encontrado.");
            return NotFound();
        }
        return Ok(chale);
    }

    [HttpPut("{name}/currentPrice/{newPrice}")]
    public async Task<IActionResult> UpdateClientEmail(string name, decimal newPrice)
    {
        if (string.IsNullOrEmpty(name))
        {
            return BadRequest("O Nome não pode ser vazio.");
        }

        try
        {
            var result = await _chaleService.UpdateChaleCurrentPrice(name, newPrice);
            if (!result)
                return NotFound("O chale não foi encontrado");
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro ao atualizar o chale: {ex.Message}");
        }
    }

}

