using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace DotnetBackend.Models;

public class BalanceHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string ClientId { get; set; }

    [BsonElement("current")]
    public decimal? Current { get; set; }

    public List<BalanceHistoryItem>? Items { get; set; }

}

