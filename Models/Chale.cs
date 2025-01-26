using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace DotnetBackend.Models;
public class Chale
{

    [BsonId] 
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; } // id como string
    [BsonElement("name")]
    public string Name { get; set; }

    [BsonElement("personQtt")]
    public int PersonQtt { get; set; }

    [BsonElement("coupleBedNumber")]
    public int CoupleBedNumber { get; set; }

    [BsonElement("singleBedNumber")]
    public int SingleBedNumber { get; set; }

    [BsonElement("currentPrice")]
    public decimal CurrentPrice { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; } = "";
    public Chale() { }

    public Chale(string name, int personQtt, decimal currentPrice, int coupleBedNumber, int singleBedNumber)
    {
        Name = name;
        PersonQtt = personQtt;
        CoupleBedNumber = coupleBedNumber;
        SingleBedNumber = singleBedNumber;
        CurrentPrice = currentPrice;
    }
}
