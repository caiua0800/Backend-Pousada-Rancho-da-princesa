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
public class ClientController : ControllerBase
{
    private readonly ClientService _clientService;
    private readonly ClientQueries _clientQueries;
    private readonly ExtractService _extractService;
    private readonly ReservaService _reservaService;
    private readonly AuthService _authService;
    private readonly WebSocketHandler _webSocketHandler;

    public ClientController(ClientService clientService,
    ClientQueries clientQueries, ExtractService extractService,
     WebSocketHandler webSocketHandler, AuthService authService, ReservaService reservaService)
    {
        _clientService = clientService;
        _clientQueries = clientQueries;
        _extractService = extractService;
        _webSocketHandler = webSocketHandler;
        _authService = authService;
        _reservaService = reservaService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(Client client)
    {
        Console.WriteLine(client.Name);

        if (client == null)
        {
            return BadRequest("Client está nulo.");
        }

        var createdClient = await _clientService.CreateClientAsync(client);

        await _webSocketHandler.SendNewClientAsync(createdClient);

        return CreatedAtAction(nameof(Create), new { id = createdClient.Id }, createdClient);
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
        var clients = await _clientService.GetAllClientsAsync();
        return Ok(clients);
    }


    [HttpDelete("{id}")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> Delete(string id)
    {

        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var result = await _clientService.DeleteClientAsync(id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetClientById(string id)
    {

        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var client = await _clientService.GetClientByIdAsync(id);
        if (client == null)
        {
            Console.WriteLine($"Cliente com CPF '{id}' não encontrado.");
            return NotFound();
        }
        return Ok(client);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Client updatedClient)
    {
        if (updatedClient == null)
        {
            return BadRequest("O cliente atualizado não pode ser nulo.");
        }

        var result = await _clientService.UpdateClientAsync(id, updatedClient);
        if (!result)
        {
            return NotFound($"Cliente com id '{id}' não encontrado.");
        }

        var client = await _clientService.GetClientByIdAsync(id);
        if (client == null)
        {
            return NotFound($"Cliente com id '{id}' não encontrado após atualização.");
        }

        return Ok(client);
    }


    [HttpPut("{id}/email")]
    public async Task<IActionResult> UpdateClientEmail(string id, [FromBody] string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest("O Email não pode ser vazio.");
        }

        try
        {
            var result = await _clientService.UpdateClientEmail(id, email);
            if (!result)
                return NotFound("O cliente não foi encontrado");
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro ao atualizar o email: {ex.Message}");
        }
    }

    [HttpPut("{id}/name/{newName}")]
    public async Task<IActionResult> UpdateClientName(string id, string newName)
    {
        if (string.IsNullOrEmpty(newName))
        {
            return BadRequest("O Novo Nome não pode ser vazio.");
        }

        try
        {
            var result = await _clientService.UpdateClientName(id, newName);
            var result2 = await _reservaService.EditarClientNameByClientIdAsync(id, newName);
            if (!result || result2 != "Ok")
                return NotFound("O cliente não foi encontrado");
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro ao atualizar o nome: {ex.Message}");
        }
    }

    [HttpPut("{id}/phone/{newPhone}")]
    public async Task<IActionResult> UpdateClientPhone(string id, string newPhone)
    {
        if (string.IsNullOrEmpty(newPhone))
        {
            return BadRequest("O telefone não pode ser vazio.");
        }

        try
        {
            var result = await _clientService.UpdateClientPhone(id, newPhone);
            if (!result)
            {
                return NotFound($"Cliente com ID '{id}' não encontrado.");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro ao atualizar o telefone: {ex.Message}");
        }
    }


}

