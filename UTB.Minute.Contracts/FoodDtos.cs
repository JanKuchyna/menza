namespace UTB.Minute.Contracts;

// for sending
public record FoodDto(int Id, string Name, string? Description, decimal Price, bool IsActive);

// getting for new food
public record CreateFoodDto(string Name, string? Description, decimal Price);

// getting for update food
public record UpdateFoodDto(string Name, string? Description, decimal Price);