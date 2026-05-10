using InventoryManagementSystem.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace InventoryManagementSystem
{
    // These classes are going to be single responsibility classes for each endpoint
    //Each endpoint counts kind of as a command if you think about it it's the command pattern i think?

    public static class ProductsEndpoints
    {
        public static void MapProductsEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/products", async (InventoryDbContext db) => 
                await db.Products.ToListAsync());
            
            app.MapPost("/api/products", async (InventoryDbContext db, Product product) =>
            {
                try
                {
                    Console.WriteLine($"Creating product: SKU={product.SKU}, Name={product.Name}, Category={product.Category}");
        
                    // Check for duplicate SKU before adding
                    var existing = await db.Products.FindAsync(product.SKU);
                    if (existing != null)
                    {
                        Console.WriteLine($" Duplicate SKU: {product.SKU}");
                        return Results.BadRequest(new { error = "SKU already exists" });
                    }
                    db.Products.Add(product);
                    await db.SaveChangesAsync();

                    Console.WriteLine($"Product created: {product.SKU}");
                    return Results.Created($"/api/products/{product.SKU}", product);
                }
                    catch (Exception ex)
                {
                    Console.WriteLine($"Product creation failed: {ex.GetType().Name} - {ex.Message}");
                    Console.WriteLine($"   Stack: {ex.StackTrace}");
                    return Results.BadRequest(new { error = ex.Message });
                }

            }); // ← Only authenticated users

            app.MapPut("/api/products/{sku}", async (InventoryDbContext db, string sku, Product updates) =>
            {
                var product = await db.Products.FindAsync(sku);
                if (product is null) return Results.NotFound();
                
                // Update only allowed fields (simple example)
                product.Name = updates.Name;
                product.Price = updates.Price;
                product.Quantity = updates.Quantity;
                product.ReorderLevel = updates.ReorderLevel;
                
                await db.SaveChangesAsync();
                return Results.Ok(product);
            });

            app.MapDelete("/api/products/{sku}", async (InventoryDbContext db, string sku) =>
            {
                var product = await db.Products.FindAsync(sku);
                if (product is null) return Results.NotFound();
                db.Products.Remove(product);
                await db.SaveChangesAsync();
                return Results.NoContent();
            });
        }
    }

    public static class AdjustmentsEndpoints
    {
        public static void MapAdjustmentsEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/adjustments", async (InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var adjustments = await db.Adjustments
                    .Include(a => a.Product)
                    .Include(a => a.User)
                    .OrderByDescending(a => a.AdjustmentID)
                    .ToListAsync();
                
                return Results.Ok(adjustments.Select(DtoMappers.MapAdjustmentToDto));
            });

            // POST /api/adjustments - Record a price adjustment (Admin only)
            app.MapPost("/api/adjustments", async (AdjustmentCreateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();  // Price changes = admin-only
                
                // Validate product exists
                var product = await db.Products.FindAsync(dto.SKU);
                if (product == null)
                    return Results.BadRequest(new { error = "Product not found" });
                
                // Validate input
                if (dto.NewPrice < 0)
                    return Results.BadRequest(new { error = "Price cannot be negative" });
                if (string.IsNullOrWhiteSpace(dto.Reason))
                    return Results.BadRequest(new { error = "Reason is required" });
                
                // Create adjustment record
                var adjustment = new Adjustment
                {
                    SKU = dto.SKU,
                    UserName = user.UserName,
                    OldPrice = product.Price,      // Capture price BEFORE change
                    NewPrice = dto.NewPrice,
                    Reason = dto.Reason.Trim(),
                    CreatedAt = DateTime.UtcNow 
                };
                
                //Update the product's actual price
                product.Price = dto.NewPrice;
                
                db.Adjustments.Add(adjustment);
                await db.SaveChangesAsync();
                
                Console.WriteLine($"rice adjustment: {product.Name} ${product.Price} → ${dto.NewPrice} ({dto.Reason})");
                return Results.Created($"/api/adjustments/{adjustment.AdjustmentID}", DtoMappers.MapAdjustmentToDto(adjustment));
            });

            // GET /api/adjustments/product/{sku} - Get adjustments for a specific product
            app.MapGet("/api/adjustments/product/{sku}", async (string sku, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                
                var adjustments = await db.Adjustments
                    .Include(a => a.Product)
                    .Include(a => a.User)
                    .Where(a => a.SKU == sku)
                    .OrderByDescending(a => a.AdjustmentID)
                    .ToListAsync();
                
                return Results.Ok(adjustments.Select(DtoMappers.MapAdjustmentToDto));
            });

            // DELETE /api/adjustments/{id} - Delete an adjustment record (Admin only)
            // Note: Does NOT revert the product price - adjustments are audit trail
            app.MapDelete("/api/adjustments/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var adjustment = await db.Adjustments.FindAsync(id);
                if (adjustment == null) return Results.NotFound();
                
                db.Adjustments.Remove(adjustment);
                await db.SaveChangesAsync();
                
                return Results.NoContent();
            });
        }
    }
    public static class OrdersEndpoints
    {
        public static void MapOrdersEndpoints(this IEndpointRouteBuilder app)
        {
            // GET /api/orders - List all orders (Admin only)
            app.MapGet("/api/orders", async (InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var orders = await db.Orders
                    .Include(o => o.Product)
                    .Include(o => o.User)
                    .ToListAsync();
                
                return Results.Ok(orders.Select(DtoMappers.MapOrderToDto));
            })
            ;

            // GET /api/orders/{id} - Get single order
            app.MapGet("/api/orders/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                
                var order = await db.Orders
                    .Include(o => o.Product)
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(o => o.OrderID == id);
                
                if (order == null) return Results.NotFound();
                
                // Staff can only view their own orders
                if (user is not Admin && order.UserName != user.UserName)
                    return Results.Forbid();
                
                return Results.Ok(DtoMappers.MapOrderToDto(order));
            })
            ;

            app.MapPost("/api/orders", async (OrderCreateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();

                //check if the product actually exists
                var product = await db.Products.FindAsync(dto.SKU);
                if (product == null)
                    return Results.BadRequest(new {error = "Product not found"});
                
                //validate the input
                if (dto.Amount <= 0)
                    return Results.BadRequest(new {error = "Amount must be positive"});
                if(dto.Cost < 0)
                    return Results.BadRequest(new {error = "Cost cannot be negative"});
                
                //create the order object
                var order = new Order
                {
                    SKU = dto.SKU,
                    UserName = user.UserName,
                    Amount = dto.Amount,
                    Cost = dto.Cost,
                    CreatedAt = DateTime.UtcNow
                };

                //update the inventory status
                product.Quantity += dto.Amount;

                db.Orders.Add(order);
                await db.SaveChangesAsync();

                return Results.Created($"/api/orders/{order.OrderID}", DtoMappers.MapOrderToDto(order));
            });


            // PUT /api/orders/{id} - Update order
            app.MapPut("/api/orders/{id:int}", async (int id, OrderUpdateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var order = await db.Orders
                    .Include(o => o.Product)
                    .FirstOrDefaultAsync(o => o.OrderID == id);
                
                if (order == null) return Results.NotFound();
                
                // Calculate inventory adjustment if amount changed
                if (order.Amount != dto.Amount && order.Product != null)
                {
                    var quantityDiff = dto.Amount - order.Amount;
                    order.Product.Quantity += quantityDiff;
                }
                
                order.SKU = dto.SKU;
                order.Amount = dto.Amount;
                order.Cost = dto.Cost;
                
                await db.SaveChangesAsync();
                
                return Results.Ok(DtoMappers.MapOrderToDto(order));
            });

            // DELETE /api/orders/{id} - Delete order (Admin only)
            app.MapDelete("/api/orders/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var order = await db.Orders
                    .Include(o => o.Product)
                    .FirstOrDefaultAsync(o => o.OrderID == id);
                
                if (order == null) return Results.NotFound();
                
                // Reverse inventory change when deleting order
                if (order.Product != null)
                {
                    order.Product.Quantity -= order.Amount;
                }
                
                db.Orders.Remove(order);
                await db.SaveChangesAsync();
                
                return Results.NoContent();
            });

            // GET /api/orders/user/{userName} - Get orders by user
            app.MapGet("/api/orders/user/{userName}", async (string userName, InventoryDbContext db, HttpContext http) =>
            {
                var requestingUser = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (requestingUser == null) return Results.Unauthorized();
                
                // Users can only view their own orders unless admin
                if (requestingUser is not Admin && requestingUser.UserName != userName)
                    return Results.Forbid();
                
                var orders = await db.Orders
                    .Include(o => o.Product)
                    .Include(o => o.User)
                    .Where(o => o.UserName == userName)
                    .ToListAsync();
                
                return Results.Ok(orders.Select(DtoMappers.MapOrderToDto));
            });
        }
    }
    public static class SalesEndpoints
    {
        public static void MapSalesEndpoints(this IEndpointRouteBuilder app)
        {
            // GET /api/sales - List all sales (Admin only)
            app.MapGet("/api/sales", async (InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var sales = await db.Sales
                    .Include(s => s.Product)
                    .Include(s => s.User)
                    .ToListAsync();
                return Results.Ok(sales.Select(DtoMappers.MapSaleToDto));
            });

            // GET /api/sales/{id} - Get single sale
            app.MapGet("/api/sales/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                
                var sale = await db.Sales
                    .Include(s => s.Product)
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.SaleID == id);
                if (sale == null) return Results.NotFound();
                
                // Staff can only view their own sales
                if (user is not Admin && sale.UserName != user.UserName)
                    return Results.Forbid();
                
                return Results.Ok(DtoMappers.MapSaleToDto(sale));
            });

            // POST /api/sales - Create a new sale (Admin or Staff)
            app.MapPost("/api/sales", async (SaleCreateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                
                // Validate product exists
                var product = await db.Products.FindAsync(dto.SKU);
                if (product == null)
                    return Results.BadRequest(new { error = "Product not found" });
                
                // Validate input
                if (dto.Amount <= 0)
                    return Results.BadRequest(new { error = "Amount must be positive" });
                if (dto.Income < 0)
                    return Results.BadRequest(new { error = "Income cannot be negative" });
                
                // Check sufficient stock for sale
                if (product.Quantity < dto.Amount)
                    return Results.BadRequest(new { 
                        error = "Insufficient stock", 
                        available = product.Quantity, 
                        requested = dto.Amount 
                    });
                
                // Create sale record
                var sale = new Sale
                {
                    SKU = dto.SKU,
                    UserName = user.UserName,
                    Amount = dto.Amount,
                    Income = dto.Income,
                    CreatedAt = DateTime.UtcNow 
                };
                
                // Decrease inventory
                product.Quantity -= dto.Amount;
                
                db.Sales.Add(sale);
                await db.SaveChangesAsync();
                
                return Results.Created($"/api/sales/{sale.SaleID}", DtoMappers.MapSaleToDto(sale));
            });

            // PUT /api/sales/{id} - Update sale (Admin only)
            app.MapPut("/api/sales/{id:int}", async (int id, SaleUpdateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var sale = await db.Sales
                    .Include(s => s.Product)
                    .FirstOrDefaultAsync(s => s.SaleID == id);
                if (sale == null) return Results.NotFound();
                
                // Calculate inventory adjustment if amount changed
                if (sale.Amount != dto.Amount && sale.Product != null)
                {
                    var quantityDiff = sale.Amount - dto.Amount; // Reverse of order logic
                    sale.Product.Quantity += quantityDiff; // Restore difference
                    
                    // Validate new amount doesn't exceed stock
                    if (sale.Product.Quantity < 0)
                        return Results.BadRequest(new { error = "Update would result in negative inventory" });
                }
                
                sale.SKU = dto.SKU;
                sale.Amount = dto.Amount;
                sale.Income = dto.Income;
                
                await db.SaveChangesAsync();
                return Results.Ok(DtoMappers.MapSaleToDto(sale));
            });

            // DELETE /api/sales/{id} - Delete sale (Admin only)
            app.MapDelete("/api/sales/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var sale = await db.Sales
                    .Include(s => s.Product)
                    .FirstOrDefaultAsync(s => s.SaleID == id);
                if (sale == null) return Results.NotFound();
                
                // Restore inventory when deleting sale
                if (sale.Product != null)
                {
                    sale.Product.Quantity += sale.Amount; // Return sold items to stock
                }
                
                db.Sales.Remove(sale);
                await db.SaveChangesAsync();
                return Results.NoContent();
            });

            // GET /api/sales/user/{userName} - Get sales by user
            app.MapGet("/api/sales/user/{userName}", async (string userName, InventoryDbContext db, HttpContext http) =>
            {
                var requestingUser = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (requestingUser == null) return Results.Unauthorized();
                
                // Users can only view their own sales unless admin
                if (requestingUser is not Admin && requestingUser.UserName != userName)
                    return Results.Forbid();
                
                var sales = await db.Sales
                    .Include(s => s.Product)
                    .Include(s => s.User)
                    .Where(s => s.UserName == userName)
                    .ToListAsync();
                return Results.Ok(sales.Select(DtoMappers.MapSaleToDto));
            });
        }
    }
    
}