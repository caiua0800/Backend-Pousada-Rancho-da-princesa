using Microsoft.AspNetCore.Mvc;
using DotnetBackend.Models;
using DotnetBackend.Services;
using Microsoft.AspNetCore.Authorization;

namespace DotnetBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAdmin([FromBody] User user)
        {
            if (user == null || string.IsNullOrEmpty(user.Password))
            {
                return BadRequest("User ou a senha está nula.");
            }

            var createdAdmin = await _userService.CreateUserAsync(user, user.Password);
            return CreatedAtAction(nameof(CreateAdmin), new { id = createdAdmin.Id }, createdAdmin);
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllUsersAsync();
            Console.WriteLine("Admins no banco de dados: ");
            foreach (var user in users)
            {
                Console.WriteLine($"Id (CPF): {user.Id}, Name: {user.Name}");
            }
            return Ok(users);
        }


        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserById(string id)
        {
            Console.WriteLine($"Buscando cliente com CPF: '{id}'");
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                Console.WriteLine($"Cliente com CPF '{id}' não encontrado.");
                return NotFound(); 
            }
            return Ok(user); 
        }


        [HttpDelete("{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _userService.DeleteUserAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

    }
}
