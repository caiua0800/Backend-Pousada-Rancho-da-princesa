using Microsoft.AspNetCore.Mvc;
using DotnetBackend.Models;
using DotnetBackend.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualBasic;


namespace DotnetBackend.Controllers;
[ApiController]
[Route("api/[controller]")]
public class BalanceHistoryController : ControllerBase
{
    private readonly BalanceHistoryService _balanceHistoryService;
    private readonly AuthService _authService;

    public BalanceHistoryController(BalanceHistoryService balanceHistoryService, AuthService authService)
    {
        _balanceHistoryService = balanceHistoryService;
        _authService = authService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BalanceHistory balanceService)
    {
        if (balanceService == null)
        {
            return BadRequest("Balance Service não pode ser nulo.");
        }

        var createdHistoryBalanceService = await _balanceHistoryService.CreateBalanceHistoryAsync(balanceService);
        return CreatedAtAction(nameof(GetBalanceHistoryById), new { id = createdHistoryBalanceService.ClientId }, createdHistoryBalanceService);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBalanceHistoryById(string id)
    {
        var contract = await _balanceHistoryService.GetBalanceHistoryByIdAsync(id);
        if (contract == null)
        {
            return NotFound();
        }
        return Ok(contract);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllBalanceHistories()
    {
        var contracts = await _balanceHistoryService.GetAllBalanceHistoriesAsync();
        return Ok(contracts);
    }

    [HttpPut("{clientId}/history/{newHistoryValue}")]
    public async Task<IActionResult> AddHistory(string clientId, decimal newHistoryValue)
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var result = await _balanceHistoryService.AddNewHistory(clientId, newHistoryValue);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }


    [HttpPut("{clientId}/{newCurrent}")]
    public async Task<IActionResult> UpdateCurrent(string clientId, decimal newCurrent)
    {

        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var result = await _balanceHistoryService.UpdateBalanceHistoryCurrent(clientId, newCurrent);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }
}
