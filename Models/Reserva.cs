using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace DotnetBackend.Models;
public class Reserva
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string? ReservaId { get; set; }

    [BsonElement("clientId")]
    public required string ClientId { get; set; }

    [BsonElement("clientName")]
    public string? ClientName { get; set; }

    [BsonElement("personQtt")]
    public required int PersonQtt { get; set; }

    [BsonElement("totalPrice")]
    public decimal TotalPrice { get; set; }

    [BsonElement("amountPaid")]
    public decimal AmountPaid { get; set; }

    [BsonElement("discount")]
    public decimal? Discount { get; set; }

    [BsonElement("dateCreated")]
    public DateTime? DateCreated { get; set; }

    [BsonElement("status")]
    public int Status { get; set; } = 1;

    [BsonElement("checkin")]
    public DateTime? Checkin { get; set; }

    [BsonElement("checkout")]
    public DateTime? Checkout { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; } = "";

    [BsonElement("ticketPayment")]
    public string? TicketPayment { get; set; } = "";

    [BsonElement("expirationDate")]
    public string? ExpirationDate { get; set; } = "";

    [BsonElement("ticketId")]
    public string? TicketId { get; set; } = "";

    [BsonElement("qrCode")]
    public string? QrCode { get; set; } = "";

    [BsonElement("chaleIds")]
    public List<string>? ChaleIds { get; set; } = new List<string>();
    public Reserva() { }

    public Reserva(string clientId, int personQtt, decimal totalPrice, DateTime checkin, DateTime checkout)
    {
        ClientId = clientId;
        PersonQtt = personQtt;
        TotalPrice = totalPrice;
        Checkin = checkin;
        Checkout = checkout;
    }
}
