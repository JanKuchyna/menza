namespace UTB.Minute.Db;

public class Food
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
