using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using DotnetBackend.Models;
using MongoDB.Driver;
namespace DotnetBackend.Models;

public class User
{
    [BsonId] 
    [BsonRepresentation(BsonType.String)] 
    public required string Id { get; set; } 

    [BsonElement("adminName")]
    public required string Name { get; set; }

    [BsonElement("password")]
    public required string Password { get; set; }

    [BsonElement("email")]
    public required string Email { get; set; }

    [BsonElement("phone")]
    public required string Phone { get; set; }


    public User(string id, string name, string email, string phone, string password)
    {
        Id = id;
        Name = name;
        Email = email;
        Phone = phone;
        Password = password;
    }
}
