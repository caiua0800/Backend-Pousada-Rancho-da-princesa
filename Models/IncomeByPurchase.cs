public class IncomeByPurchase
{
    public string Id { get; set; }
    public decimal Price { get; set; }
    public decimal TotalPrice { get; set; }

    public IncomeByPurchase(string id, decimal price, decimal totalPrice)
    {
        Id = id;
        Price = price;
        TotalPrice = totalPrice;
    }
}