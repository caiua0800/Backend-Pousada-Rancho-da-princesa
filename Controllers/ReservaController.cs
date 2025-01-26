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
public class ReservaController : ControllerBase
{
    private readonly ClientService _clientService;
    private readonly AuthService _authService;
    private readonly ChaleService _chaleService;
    private readonly ReservaService _reservaService;
    private readonly ExtractService _extractService;
    private readonly CounterService _counterService;

    public ReservaController(ClientService clientService,
     ExtractService extractService, AuthService authService,
     ChaleService chaleService, ReservaService reservaService, CounterService counterService)
    {
        _clientService = clientService;
        _authService = authService;
        _chaleService = chaleService;
        _reservaService = reservaService;
        _extractService = extractService;
        _counterService = counterService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(Reserva reserva)
    {

        if (reserva == null)
        {
            return BadRequest("Reserva está nulo.");
        }

        reserva.ReservaId = "R" + await _counterService.GetNextSequenceAsync("Reserva");
        reserva.DateCreated = DateTime.UtcNow;

        if(reserva.AmountPaid > 0)
            reserva.Status = 2;

        var createdReserva = await _reservaService.CreateReservaAsync(reserva);

        return CreatedAtAction(nameof(Create), new { id = createdReserva.ReservaId }, createdReserva);
    }

    [HttpPost("saldo")]
    public async Task<IActionResult> CreateSaldo(Reserva reserva)
    {

        if (reserva == null)
        {
            return BadRequest("Reserva está nulo.");
        }

        reserva.ReservaId = "R" + await _counterService.GetNextSequenceAsync("Reserva");
        reserva.DateCreated = DateTime.UtcNow;

        var client = await _clientService.GetClientByIdAsync(reserva.ClientId);
        reserva.AmountPaid = (decimal)reserva.AmountPaid + (decimal)client.Balance;
        reserva.Status = 2;
        await _clientService.WithdrawFromBalanceAsync(client.Id, (decimal)client.Balance);

        var createdReserva = await _reservaService.CreateReservaAsync(reserva);

        return CreatedAtAction(nameof(Create), new { id = createdReserva.ReservaId }, createdReserva);
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
        var reservas = await _reservaService.GetAllReservasAsync();
        return Ok(reservas);
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

        var result = await _reservaService.DeleteReservaAsync(id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetReservaById(string name)
    {

        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var chale = await _reservaService.GetReservaByIdAsync(name);
        if (chale == null)
        {
            Console.WriteLine($"Cliente com nome '{name}' não encontrado.");
            return NotFound();
        }
        return Ok(chale);
    }

    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll()
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var result = await _reservaService.DeleteAllReservasAsync();
        if (!result)
        {
            return BadRequest("Nenhuma reserva foi deletada ou ocorreu um erro.");
        }

        return NoContent();
    }

    [HttpGet("period")]
    public async Task<IActionResult> GetReservasByPeriod([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var reservas = await _reservaService.GetReservasByPeriodAsync(startDate, endDate);
        if (reservas == null || reservas.Count == 0)
        {
            return NotFound("Nenhuma reserva encontrada para o período especificado.");
        }
        return Ok(reservas);
    }

    [HttpGet("available-chales")]
    public async Task<IActionResult> GetAvailableChales([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {

        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var chalesDisponiveis = await _reservaService.GetChalesDisponiveisAsync(startDate, endDate);

        if (chalesDisponiveis == null || chalesDisponiveis.Count == 0)
        {
            return Ok(new List<Chale>());
        }

        return Ok(chalesDisponiveis);
    }


    [HttpPut("{id}/status/{newStatus}")]
    public async Task<IActionResult> UpdateStatus(string id, int newStatus)
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        try
        {
            var result = await _reservaService.UpdateReservaStatusAsync(id, newStatus);
            if (!result)
            {
                return NotFound("Reserva não encontrada ou status não alterado.");
            }

            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/cancel-addToClient")]
    public async Task<IActionResult> CancelAndAdd(string id)
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        try
        {
            var reserva = await _reservaService.GetReservaByIdAsync(id);

            if (reserva == null)
                return NotFound("Reserva não encontrada.");

            var result1 = await _clientService.AddToBalanceAsync(reserva.ClientId, reserva.TotalPrice);
            var result2 = await _reservaService.UpdateReservaStatusAsync(id, 4);

            if (!result2)
            {
                return NotFound("Reserva não encontrada.");
            }

            if (!result1)
            {
                return NotFound("Não foi possível adicionar o valor ao cliente.");
            }

            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/addPayment/{payment}")]
    public async Task<IActionResult> AddPayment(string id, decimal payment)
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        try
        {
            var result = await _reservaService.AddPayment(id, payment);
            if (!result)
            {
                return NotFound("Reserva não encontrada ou pagamento nullo.");
            }

            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/totalPrice/{payment}")]
    public async Task<IActionResult> EditTotalPrice(string id, decimal payment)
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        try
        {
            var result = await _reservaService.EditarValorTotal(id, payment);
            if (!result)
            {
                return NotFound("Reserva não encontrada ou pagamento nullo.");
            }

            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("reserved-dates")]
    public async Task<IActionResult> GetReservedDates([FromQuery] int year, [FromQuery] int month)
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var reservedDates = await _reservaService.GetReservedDatesByMonthAsync(year, month);
        if (reservedDates == null || reservedDates.Count == 0)
        {
            return NotFound("Nenhuma data reservada encontrada para o mês e ano especificados.");
        }

        return Ok(reservedDates);
    }

    [HttpGet("reserved-dates-day")]
    public async Task<IActionResult> GetReservedDatesByDayMonthAndYear([FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var reservedDates = await _reservaService.GetReservedDatesByMonthYearAndDayAsync(year, month, day);
        if (reservedDates == null || reservedDates.Count == 0)
        {
            return NotFound("Nenhuma reserva encontrada para o dia especificado.");
        }

        return Ok(reservedDates); // Retorna as reservas
    }

    [HttpGet("total-value/month")]
    public async Task<IActionResult> GetTotalValueByMonth([FromQuery] int year, [FromQuery] int month)
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var totalValue = await _reservaService.GetTotalValueByMonthAsync(year, month);
        return Ok(new { TotalValue = totalValue });
    }

    [HttpGet("total-value/current-year")]
    public async Task<IActionResult> GetTotalValueForCurrentYear()
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");
        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var totalValue = await _reservaService.GetTotalValueForCurrentYearAsync();
        return Ok(new { TotalValue = totalValue });
    }

    [HttpGet("counts")]
    public async Task<IActionResult> GetReservaCounts()
    {
        var authorizationHeader = HttpContext.Request.Headers["Authorization"];
        var token = authorizationHeader.ToString().Replace("Bearer ", "");

        if (!_authService.VerifyIfAdminToken(token))
        {
            return Forbid("Você não é ela");
        }

        var counts = await _reservaService.GetReservaCountsAsync();
        return Ok(counts);
    }
}

