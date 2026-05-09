namespace InventoryManagementSystem
{
    // DTOS
    public record OrderCreateDto(string SKU, int Amount, decimal Cost);
    public record OrderUpdateDto(string SKU, int Amount, decimal Cost);
    public record SaleCreateDto(string SKU, int Amount, decimal Income);
    public record SaleUpdateDto(string SKU, int Amount, decimal Income);
    public record LoginDto(string UserName, string Password);
    public record LoginRequest(string UserName, string Password);
    public record AdjustmentCreateDto(string SKU, decimal NewPrice, string Reason);
    public record AdjustmentDto(
        int AdjustmentID, 
        string SKU, 
        string UserName, 
        decimal OldPrice, 
        decimal NewPrice, 
        string Reason,
        DateTime CreatedAt,
        string ProductName,
        string UserFirstName,
        string UserLastName);

    public record ActivityItem(
        string Type,              // "order", "sale", or "adjustment"
        int Id,                   // OrderID, SaleID, or AdjustmentID
        string SKU,
        string ProductName,
        string UserName,
        string UserDisplayName,   // "FirstName LastName" or fallback to UserName
        decimal Amount,           // Quantity for orders/sales; price delta for adjustments
        decimal Value,            // Cost/Income/NewPrice depending on type
        DateTime Timestamp,       // CreatedAt from database
        string Reason);
    
    public record UserCreateDto(
        string UserName, 
        string FirstName, 
        string LastName, 
        string Password, 
        string Role  // "Admin" or "Staff"
    );

    public record UserUpdateDto(
        string? FirstName, 
        string? LastName, 
        string? Password,  // Optional: only set if changing
        string? Role       // Optional: only set if changing role
    );
    public record UserDto(
        string UserName,
        string FirstName,
        string LastName,
        string Role  // "Admin" or "Staff"
    );
}