using Microsoft.AspNetCore.Mvc;
using DotnetBackend.Models;
using DotnetBackend.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DotnetBackend.Queries;
using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace DotnetBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController : ControllerBase
    {
        private readonly ClientService _clientService;
        private readonly ClientQueries _clientQueries;
        private readonly PurchaseService _purchaseService;
        private readonly ExtractService _extractService;
        private readonly WithdrawalService _withdrawalService;
        private readonly AuthService _authService;
        private readonly VendaService _vendaService;
        private readonly WebSocketHandler _webSocketHandler;

        public ClientController(ClientService clientService, ClientQueries clientQueries,
            PurchaseService purchaseService, ExtractService extractService,
            VendaService vendaService, WithdrawalService withdrawalService, WebSocketHandler webSocketHandler, AuthService authService)
        {
            _clientService = clientService;
            _clientQueries = clientQueries;
            _purchaseService = purchaseService;
            _extractService = extractService;
            _vendaService = vendaService;
            _webSocketHandler = webSocketHandler;
            _withdrawalService = withdrawalService;
            _authService = authService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(Client client)
        {
            Console.WriteLine(client.Name);

            if (client == null || string.IsNullOrEmpty(client.Password))
            {
                return BadRequest("Client ou a senha está nula.");
            }

            var createdClient = await _clientService.CreateClientAsync(client, client.Password);

            Console.WriteLine($"Cliente criado: {createdClient.Id}, {createdClient.Name}");

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

        [HttpGet("consultor/{consultorId}")]
        public async Task<IActionResult> GetAllByConsultorId(string consultorId)
        {
            var authorizationHeader = HttpContext.Request.Headers["Authorization"];
            var token = authorizationHeader.ToString().Replace("Bearer ", "");
            if (!_authService.VerifyIfAdminToken(token))
            {
                return Forbid("Você não é ela");
            }
            var clients = await _clientService.GetAllClientsByConsultorIdAsync(consultorId);
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

        [HttpGet("details/{id}")]
        public async Task<IActionResult> GetClientDetails(string id)
        {

            var authorizationHeader = HttpContext.Request.Headers["Authorization"];
            var token = authorizationHeader.ToString().Replace("Bearer ", "");
            if (!_authService.VerifyIfIsReallyTheClient(id, token))
            {
                return Forbid("Você não é ela");
            }

            var client = await _clientService.GetClientByIdAsync(id);
            if (client == null)
            {
                return NotFound();
            }

            var purchaseDetails = new List<Purchase>();
            decimal amountAvailableToWithdraw = 0;
            decimal amountTotalAvaliableFromContracts = 0;
            decimal dailyIncome = 0;

            var dailyIncomes = new List<IncomeByPurchase>();


            if (client.WalletExtract.Purchases != null)
            {
                foreach (var purchaseId in client.WalletExtract.Purchases)
                {
                    var purchase = await _purchaseService.GetPurchaseByIdAsync(purchaseId);
                    if (purchase != null)
                    {
                        purchaseDetails.Add(purchase);

                        int? daysToWithdrawDB = purchase.DaysToFirstWithdraw;
                        int daysToWithdraw = 90;
                        if (daysToWithdrawDB != null)
                        {
                            daysToWithdraw = (int)daysToWithdrawDB;
                        }

                        if ((purchase.FirstIncreasement.HasValue && (DateTime.UtcNow - purchase.FirstIncreasement.Value).TotalDays >= daysToWithdraw) || purchase.FreeWithdraw == true)
                        {
                            amountAvailableToWithdraw += purchase.CurrentIncome - purchase.AmountWithdrawn;
                        }

                        if (purchase.Status == 1 || purchase.Status == 2)
                        {
                            amountTotalAvaliableFromContracts += (purchase.CurrentIncome - purchase.AmountWithdrawn);
                        }

                        if (purchase.Status == 2)
                        {
                            TimeSpan? difference = purchase.EndContractDate - purchase.FirstIncreasement;

                            if (difference.HasValue && difference.Value.TotalDays > 0)
                            {
                                decimal aux = (purchase.FinalIncome - purchase.CurrentIncome) / (decimal)Math.Ceiling(difference.Value.TotalDays);
                                dailyIncome += aux;

                                dailyIncomes.Add(new IncomeByPurchase(purchase.PurchaseId, aux, purchase.CurrentIncome));
                            }
                            else
                            {
                                Console.WriteLine("A data de término do contrato deve ser posterior à data do primeiro incremento.");
                            }
                        }
                    }
                }
            }


            amountAvailableToWithdraw += (decimal)client.ExtraBalance;

            var withdrawalDetails = new List<Withdrawal>();
            if (client.WalletExtract.Withdrawals != null)
            {
                foreach (var withdrawalId in client.WalletExtract.Withdrawals)
                {
                    var withdrawal = await _withdrawalService.GetWithdrawalByIdAsync(withdrawalId);
                    if (withdrawal != null)
                    {
                        withdrawalDetails.Add(withdrawal);
                    }
                }
            }

            var clientDetails = new
            {
                Client = new
                {
                    client.Id,
                    client.Name,
                    client.Email,
                    client.Phone,
                    client.ExtraBalance,
                    client.Address,
                    client.Balance,
                    client.BlockedBalance,
                    client.ClientProfit,
                    client.DateCreated,
                    client.PlatformId,
                    client.WithdrawDate,
                    client.SponsorId,
                    client.Status,
                    AmountAvailableToWithdraw = amountAvailableToWithdraw,
                    DailyIncome = dailyIncome,
                    DailyIncomes = dailyIncomes,
                    WalletExtract = new
                    {
                        Purchases = purchaseDetails,
                        Withdrawals = withdrawalDetails
                    }
                }
            };

            return Ok(clientDetails);
        }

        [HttpPost("{id}/purchases")]
        public async Task<IActionResult> AddPurchase(string id, [FromBody] string purchaseId)
        {
            if (string.IsNullOrEmpty(purchaseId))
            {
                return BadRequest("ID da compra não pode ser nulo ou vazio.");
            }

            var result = await _clientService.AddPurchaseAsync(id, purchaseId);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("{id}/uploadProfilePicture")]
        public async Task<IActionResult> UploadProfilePicture(string id, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Arquivo não pode estar vazio.");
            }

            try
            {
                var imageUrl = await _clientService.UploadProfilePictureAsync(file);

                var client = await _clientService.GetClientByIdAsync(id);
                if (client == null)
                {
                    return NotFound();
                }

                client.ProfilePictureUrl = imageUrl;

                await _clientService.UpdateClientAsync(id, client);

                return Ok(new { Url = imageUrl });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao fazer upload da imagem: {ex.Message}");
                return StatusCode(500, "Erro ao fazer upload da imagem. Detalhes: " + ex.ToString());
            }
        }

        [HttpPut("{id}/exclude")]
        public async Task<IActionResult> ExcludeAccount(string id)
        {

            var authorizationHeader = HttpContext.Request.Headers["Authorization"];
            var token = authorizationHeader.ToString().Replace("Bearer ", "");
            if (!_authService.VerifyIfIsReallyTheClient(id, token))
            {
                return Forbid("Você não é ela");
            }

            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("ID do cliente inexistente.");
            }

            var purchases = await _purchaseService.GetPurchasesByClientIdAsync(id);

            if (purchases != null && purchases.Count > 0)
            {
                foreach (var purchase in purchases)
                {
                    purchase.Status = 4;
                    await _purchaseService.UpdatePurchaseAsync(purchase.PurchaseId, purchase);
                }
            }

            var result = await _clientService.ExcludeAccount(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
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

        [HttpPost("{id}/add/{amount}")]
        public async Task<IActionResult> AddMoney(string id, decimal amount)
        {

            var authorizationHeader = HttpContext.Request.Headers["Authorization"];
            var token = authorizationHeader.ToString().Replace("Bearer ", "");
            if (!_authService.VerifyIfAdminToken(token))
            {
                return Forbid("Você não é ela");
            }

            if (amount <= 0 || string.IsNullOrEmpty(id))
            {
                return BadRequest("O Amount e o Id não pode ser vazio.");
            }

            try
            {
                var result = await _clientService.AddToExtraBalanceAsync(id, amount);
                if (!result)
                    return NotFound("O cliente não foi encontrado");
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao adicionar saldo: {ex.Message}");
            }
        }

        [HttpPut("{id}/sponsor/{newSponsor}")]
        public async Task<IActionResult> UpdateClientSponsor(string id, string newSponsor)
        {
            var authorizationHeader = HttpContext.Request.Headers["Authorization"];
            var token = authorizationHeader.ToString().Replace("Bearer ", "");
            if (!_authService.VerifyIfAdminToken(token))
            {
                return Forbid("Você não é ela");
            }

            if (string.IsNullOrEmpty(newSponsor))
            {
                return BadRequest("O Sponsor não pode ser vazio.");
            }

            try
            {
                var result = await _clientService.UpdateClientSponsor(id, newSponsor);
                if (!result)
                    return NotFound("O cliente não foi encontrado");
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar o sponsor: {ex.Message}");
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

        [HttpPut("{id}/consultor/{newPhone}")]
        public async Task<IActionResult> UpdateClientConsultor(string id, string newPhone)
        {
            if (string.IsNullOrEmpty(newPhone))
            {
                return BadRequest("O telefone não pode ser vazio.");
            }

            try
            {
                var result = await _clientService.UpdateClientConsultor(id, newPhone);
                if (!result)
                {
                    return NotFound($"Cliente com ID '{id}' não encontrado.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar o consultor: {ex.Message}");
            }
        }

        [HttpPut("{id}/sponsorId")]
        public async Task<IActionResult> UpdateClientSponsorId(string id, [FromBody] string sponsorId)
        {
            if (string.IsNullOrEmpty(sponsorId))
            {
                return BadRequest("O SponsorId não pode ser vazio.");
            }

            try
            {
                var result = await _clientService.UpdateClientSponsorId(id, sponsorId);
                if (!result)
                {
                    return NotFound($"Cliente com ID '{id}' não encontrado.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar o SponsorId: {ex.Message}");
            }
        }

        [HttpPut("{id}/address")]
        public async Task<IActionResult> UpdateClientAddress(string id, [FromBody] Address newAddress)
        {
            if (newAddress == null)
            {
                return BadRequest("O endereço não pode ser nulo.");
            }

            try
            {
                var result = await _clientService.UpdateClientAddress(id, newAddress);
                if (!result)
                {
                    return NotFound($"Cliente com ID '{id}' não encontrado.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar o endereço: {ex.Message}");
            }
        }

        [HttpPut("{id}/clientProfit")]
        public async Task<IActionResult> UpdateClientClientProfit(string id, [FromBody] double clientProfit)
        {
            try
            {
                var result = await _clientService.UpdateClientClientProfit(id, clientProfit);
                if (!result)
                {
                    return NotFound($"Cliente com ID '{id}' não encontrado.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar o ClientProfit: {ex.Message}");
            }
        }

    }
}
