namespace UTB.Library.Db;

public class Order
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public required string StudentId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Preparing;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
