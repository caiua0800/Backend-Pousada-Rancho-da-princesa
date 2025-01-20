
using MongoDB.Bson.Serialization.Attributes;

public class BalanceHistoryItem
{
    [BsonElement("dateCreated")]
    public DateTime? DateCreated { get; set; }

    [BsonElement("value")]
    public decimal Value { get; set; }

    public BalanceHistoryItem(DateTime? dateCreated, decimal value)
    {
        DateCreated = dateCreated;
        Value = value;
    }
}