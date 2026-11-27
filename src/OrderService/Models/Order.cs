namespace OrderService.Models;

public enum OrderStatus
{
    Placed = 1
}

public class Order
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public record PlaceOrderRequest(int ProductId, int Quantity);