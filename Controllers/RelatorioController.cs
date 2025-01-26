using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using DotnetBackend.Services;
using MongoDB.Driver;

namespace DotnetBackend.Controllers;
[ApiController]
[Route("api/[controller]")]
public class RelatorioController : ControllerBase
{
    private readonly RelatorioService _relatorioService;
    private readonly ClientService _clientService;

    public RelatorioController(RelatorioService relatorioService, ClientService clientService)
    {
        _relatorioService = relatorioService;
        _clientService = clientService;
    }

    [HttpGet("clients/")]
    public async Task<IActionResult> GetClientReportPdf()
    {
        var clients = await _clientService.GetAllClientsAsync();
        var pdfStream = await _relatorioService.GenerateClientReportPdfAsync(clients);
        return File(pdfStream.ToArray(), "application/pdf", "relatorio_clientes.pdf");
    }


    [HttpGet("clients/noPdf")]
    public async Task<IActionResult> GetClientReportNoPdf()
    {
        var clients = await _clientService.GetAllClientsAsync();
        return Ok(clients);
    }

    [HttpGet("clients/creationDate/{qttDays}")]
    public async Task<IActionResult> GetClientFilteredReportPdf(int qttDays)
    {
        var clients = await _clientService.GetAllClientsWithCreationDateFilterAsync(qttDays);
        var pdfStream = await _relatorioService.GenerateClientReportPdfAsync(clients);
        return File(pdfStream.ToArray(), "application/pdf", "relatorio_clientes_filtrados.pdf");
    }

    [HttpGet("clients/creationDate/{qttDays}/noPdf")]
    public async Task<IActionResult> GetClientFilteredReportNoPdf(int qttDays)
    {
        var clients = await _clientService.GetAllClientsWithCreationDateFilterAsync(qttDays);
        return Ok(clients);
    }

    [HttpGet("clients/status/{status}")]
    public async Task<IActionResult> GetClientsFilteredStatusReportPdf(int status)
    {
        var clients = await _clientService.GetAllClientsAsync();
        var filteredClients = clients.Where(p => p.Status == status).ToList();
        if (!filteredClients.Any())
        {
            return NotFound("Nenhum cliente encontrada com o status especificado.");
        }

        var pdfStream = await _relatorioService.GenerateClientReportPdfAsync(filteredClients);
        return File(pdfStream.ToArray(), "application/pdf", "relatorio_clientes_filtrados.pdf");
    }

    [HttpGet("clients/status/{status}/noPdf")]
    public async Task<IActionResult> GetClientsFilteredStatusReportNoPdf(int status)
    {
        var clients = await _clientService.GetAllClientsAsync();
        var filteredClients = clients.Where(p => p.Status == status).ToList();
        if (!filteredClients.Any())
        {
            return NotFound("Nenhum cliente encontrada com o status especificado.");
        }

        return Ok(filteredClients);
    }


    [HttpGet("clients/creationDate/{qttDays}/status/{status}")]
    public async Task<IActionResult> GetClientsFilteredStatusAndDateReportPdf(int status, int qttDays)
    {
        var clients = await _clientService.GetAllClientsWithCreationDateFilterAsync(qttDays);
        var filteredClients = clients.Where(p => p.Status == status).ToList();
        if (!filteredClients.Any())
        {
            return NotFound("Nenhum cliente encontrada com o status especificado.");
        }

        var pdfStream = await _relatorioService.GenerateClientReportPdfAsync(filteredClients);
        return File(pdfStream.ToArray(), "application/pdf", "relatorio_clientes_filtrados.pdf");
    }

    [HttpGet("clients/creationDate/{qttDays}/status/{status}/noPdf")]
    public async Task<IActionResult> GetClientsFilteredStatusAndDateReportNoPdf(int status, int qttDays)
    {
        var clients = await _clientService.GetAllClientsWithCreationDateFilterAsync(qttDays);
        var filteredClients = clients.Where(p => p.Status == status).ToList();
        if (!filteredClients.Any())
        {
            return NotFound("Nenhum cliente encontrada com o status especificado.");
        }

        return Ok(filteredClients);
    }

    [HttpGet("consultor/creationDate/{qttDays}/noPdf")]
    public async Task<IActionResult> GetConsultorFilteredReportNoPdf(int qttDays)
    {
        var consultores = await _clientService.GetAllClientsWithCreationDateFilterAsync(qttDays);
        return Ok(consultores);
    }
}
