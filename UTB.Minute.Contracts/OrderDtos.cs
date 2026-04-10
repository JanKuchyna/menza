namespace UTB.Minute.Contracts;

public record OrderDto(int Id, MenuItemDto MenuItem, string StudentId, string Status, DateTime CreatedAt);

public record CreateOrderDto(int MenuItemId, string StudentId);

public record UpdateOrderStatusDto(string Status);