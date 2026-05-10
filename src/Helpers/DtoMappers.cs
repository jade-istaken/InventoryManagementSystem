namespace InventoryManagementSystem.Helpers
{
    //Another static helper class that helps us to isolate concerns, as it doesn't really make sense to keep this in Program.cs either
    public static class DtoMappers
    {
        public static object MapOrderToDto(Order order) => new
            {
                order.OrderID,
                order.SKU,
                order.UserName,
                order.Amount,
                order.Cost,
                createdAt = order.CreatedAt,
                ProductName = order.Product?.Name,
                UserFirstName = order.User?.FirstName,
                UserLastName = order.User?.LastName
            };

        public static object MapSaleToDto(Sale sale) => new
            {
                orderId = sale.SaleID,
                sku = sale.SKU,
                userName = sale.UserName,
                amount = sale.Amount,
                income = sale.Income,
                createdAt = sale.CreatedAt,
                ProductName = sale.Product?.Name,
                UserFirstName = sale.User?.FirstName,
                UserLastName = sale.User?.LastName
            };

        public static AdjustmentDto MapAdjustmentToDto(Adjustment adj) => new(
                adj.AdjustmentID,
                adj.SKU,
                adj.UserName,
                adj.OldPrice,
                adj.NewPrice,
                adj.Reason,
                adj.CreatedAt, 
                adj.Product?.Name ?? "",
                adj.User?.FirstName ?? "",
                adj.User?.LastName ?? ""
            );

        public static UserDto MapUserToDto(User user) => new(
                user.UserName,
                user.FirstName,
                user.LastName,
                user is Admin ? "Admin" : "Staff"
            );
    }
}