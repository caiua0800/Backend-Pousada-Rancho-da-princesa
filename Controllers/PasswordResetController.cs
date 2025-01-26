using DotnetBackend.Services;
using DotnetBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace DotnetBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PasswordResetController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly PasswordResetService _passwordResetService;
        private readonly TokenService _tokenService;
        private readonly ClientService _clientService;
        private readonly AuthService _authService;

        public PasswordResetController(EmailService emailService, PasswordResetService passwordResetService, TokenService tokenService, ClientService clientService, AuthService authService)
        {
            _emailService = emailService;
            _passwordResetService = passwordResetService;
            _tokenService = tokenService;
            _clientService = clientService;
            _authService = authService;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
            {
                return BadRequest("Email é obrigatório.");
            }

            await _passwordResetService.SendPasswordResetEmail(request.Email);
            return Ok("Email enviado para redefinição de senha.");
        }

    }
}