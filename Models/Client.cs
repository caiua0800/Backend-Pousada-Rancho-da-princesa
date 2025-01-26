using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace DotnetBackend.Models
{
    public class Client
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public required string Id { get; set; }

        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("email")]
        public required string Email { get; set; }

        [BsonElement("phone")]
        public required string Phone { get; set; }

        [BsonElement("address")]
        public required Address Address { get; set; }

        [BsonElement("balance")]
        public decimal? Balance { get; set; }

        [BsonElement("dateCreated")]
        public DateTime DateCreated { get; set; }

        [BsonElement("status")]
        public int? Status { get; set; }

        public Client(string id, string name, string email, string phone, Address address)
        {
            Id = id;
            Name = name;
            Email = email;
            Phone = phone;
            Address = address;
            Balance = 0;
            DateCreated = DateTime.UtcNow;
            Status = 1;
        }

        public void AddToBalance(decimal amount)
        {
            if (amount < 0)
            {
                throw new ArgumentException("O valor a ser adicionado deve ser positivo.");
            }
            Balance += amount;
        }

        public void WithdrawFromBalance(decimal amount)
        {
            if (amount < 0)
            {
                throw new ArgumentException("O valor a ser sacado deve ser positivo.");
            }
            if ((Balance - amount) > 0)
            {
                throw new InvalidOperationException($"Saldo bloqueado, Balance = {Balance}, amount = {amount}");
            }
            if (amount > Balance)
            {
                throw new InvalidOperationException("Saldo insuficiente para realizar o saque.");
            }
            Balance -= amount;
        }
    }

}
