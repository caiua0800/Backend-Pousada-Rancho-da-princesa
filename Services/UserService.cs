using MongoDB.Driver;
using DotnetBackend.Models;
using Microsoft.AspNetCore.Identity; // Para usar PasswordHasher
using System.Threading.Tasks;

namespace DotnetBackend.Services
{
    public class UserService
    {
        private readonly IMongoCollection<User> _users;
    
        public UserService(MongoDbService mongoDbService)
        {
            _users = mongoDbService.GetCollection<User>("Users");
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _users.Find(_ => true).ToListAsync();
        }

        public async Task<bool> DeleteUserAsync(string id)
        {
            var deleteResult = await _users.DeleteOneAsync(c => c.Id == id);
            return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
        }

        public async Task<User?> GetUserByIdAsync(string id)
        {
            var normalizedId = id.Trim();
            return await _users.Find(c => c.Id == normalizedId).FirstOrDefaultAsync();
        }

        public async Task<User> CreateUserAsync(User user, string password)
        {
            if (string.IsNullOrWhiteSpace(user.Id))
            {
                throw new ArgumentException("O ID do administrador deve ser fornecido.");
            }

            var existingUser = await _users.Find(a => a.Id == user.Id).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                throw new InvalidOperationException("Já existe um administrador com este ID.");
            }

            var passwordHasher = new PasswordHasher<User>();
            user.Password = passwordHasher.HashPassword(user, password);

            await _users.InsertOneAsync(user);
            return user;
        }

        public async Task<bool> VerifyPasswordAsync(string userId, string password)
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.Password))
            {
                Console.WriteLine($"Admin não encontrado ou senha não definida para ID: {userId}");
                return false;
            }

            Console.WriteLine($"Tentando verificar a senha para Admin ID: {userId}");
            Console.WriteLine($"Hash armazenado: {user.Password}");

            var passwordHasher = new PasswordHasher<User>();
            var result = passwordHasher.VerifyHashedPassword(user, user.Password, password);

            Console.WriteLine($"Resultado da verificação: {result}");
            Console.WriteLine($"Senha de entrada: {password}");

            return result == PasswordVerificationResult.Success;
        }


    }
}
