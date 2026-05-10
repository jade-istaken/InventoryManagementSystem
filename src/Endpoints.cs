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
    public static class UsersEndpoints
    {
        public static void MapUsersEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/users", async (InventoryDbContext db, HttpContext http) =>
            {
                var requestingUser = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (requestingUser == null) return Results.Unauthorized();
                if (requestingUser is not Admin) return Results.Forbid();
                
                var users = await db.Users
                    .Select(u => new {
                        userName = u.UserName,
                        firstName = u.FirstName,
                        lastName = u.LastName,
                        role = u is Admin ? "Admin" : "Staff"
                    })
                    .ToListAsync();
                
                return Results.Ok(users);
            });

            app.MapPost("/api/users", async (
                UserCreateDto dto, 
                IUserService userService, 
                IPasswordHasher passwordHasher,
                InventoryDbContext db, 
                HttpContext http) =>
            {
                var requestingUser = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (requestingUser == null) return Results.Unauthorized();
                if (requestingUser is not Admin) return Results.Forbid();
                
                // Validate input
                if (string.IsNullOrWhiteSpace(dto.UserName) || 
                    string.IsNullOrWhiteSpace(dto.FirstName) || 
                    string.IsNullOrWhiteSpace(dto.LastName))
                    return Results.BadRequest(new { error = "Username, first name, and last name are required" });
                
                if (string.IsNullOrWhiteSpace(dto.Password))
                    return Results.BadRequest(new { error = "Password is required for new users" });
                
                // Check if username already exists (case-insensitive)
                var existing = await db.Users.FindAsync(dto.UserName.ToLower());
                if (existing != null)
                    return Results.Conflict(new { error = "Username already exists" });
                
                // Hash password using your existing hasher
                var passwordHash = passwordHasher.Hash(dto.Password);
                
                // Create user entity
                User newUser;
                if (dto.Role == "Admin")
                {
                    newUser = new Admin
                    {
                        UserName = dto.UserName.ToLower(),
                        FirstName = dto.FirstName.Trim(),
                        LastName = dto.LastName.Trim(),
                        HashedPass = passwordHash
                    };
                }
                else
                {
                    newUser = new Staff
                    {
                        UserName = dto.UserName.ToLower(),
                        FirstName = dto.FirstName.Trim(),
                        LastName = dto.LastName.Trim(),
                        HashedPass = passwordHash
                    };
                }
                
                db.Users.Add(newUser);
                await db.SaveChangesAsync();
                
                Console.WriteLine($"👤 User created: {newUser.UserName} ({dto.Role})");
                return Results.Created($"/api/users/{newUser.UserName}", DtoMappers.MapUserToDto(newUser));
            });

            app.MapPut("/api/users/{userName}", async (
                string userName, 
                UserUpdateDto dto, 
                IPasswordHasher passwordHasher,
                InventoryDbContext db, 
                HttpContext http) =>
            {
                var requestingUser = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (requestingUser == null) return Results.Unauthorized();
                if (requestingUser is not Admin) return Results.Forbid();
                
                // Cannot change your own role or delete yourself via update
                if (userName.ToLower() == requestingUser.UserName.ToLower() && dto.Role != null)
                    return Results.BadRequest(new { error = "You cannot change your own role" });
                
                var user = await db.Users.FindAsync(userName);
                if (user == null) return Results.NotFound();
                
                // Update basic fields
                if (!string.IsNullOrWhiteSpace(dto.FirstName))
                    user.FirstName = dto.FirstName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.LastName))
                    user.LastName = dto.LastName.Trim();
                
                // Hash new password if provided
                if (!string.IsNullOrWhiteSpace(dto.Password))
                {
                    user.HashedPass = passwordHasher.Hash(dto.Password);
                }
                
                // Handle role change (Staff ↔ Admin) - requires entity type change
                if (!string.IsNullOrWhiteSpace(dto.Role) && dto.Role != (user is Admin ? "Admin" : "Staff"))
                {
                    var oldRole = user is Admin ? "Admin" : "Staff";
                    var oldHash = user.HashedPass;
                    var oldFirst = user.FirstName;
                    var oldLast = user.LastName;
                    var oldUser = userName;
                    
                    // Remove old entity
                    db.Users.Remove(user);
                    await db.SaveChangesAsync();
                    
                    // Create new entity with correct type
                    if (dto.Role == "Admin")
                    {
                        user = new Admin
                        {
                            UserName = oldUser,
                            FirstName = oldFirst,
                            LastName = oldLast,
                            HashedPass = oldHash
                        };
                    }
                    else
                    {
                        user = new Staff
                        {
                            UserName = oldUser,
                            FirstName = oldFirst,
                            LastName = oldLast,
                            HashedPass = oldHash
                        };
                    }
                    db.Users.Add(user);
                    Console.WriteLine($"👤 User role changed: {userName} {oldRole} → {dto.Role}");
                }
                
                await db.SaveChangesAsync();
                return Results.Ok(DtoMappers.MapUserToDto(user));
            });

            // DELETE /api/users/{userName} - Delete user (Admin only)
            app.MapDelete("/api/users/{userName}", async (
                string userName, 
                InventoryDbContext db, 
                HttpContext http) =>
            {
                var requestingUser = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (requestingUser == null) return Results.Unauthorized();
                if (requestingUser is not Admin) return Results.Forbid();
                
                // Cannot delete yourself
                if (userName.ToLower() == requestingUser.UserName.ToLower())
                    return Results.BadRequest(new { error = "You cannot delete your own account" });
                
                var user = await db.Users.FindAsync(userName);
                if (user == null) return Results.NotFound();
                
                db.Users.Remove(user);
                await db.SaveChangesAsync();
                
                Console.WriteLine($"👤 User deleted: {userName}");
                return Results.NoContent();
            });
        }
    }
    public static class ActivityEndpoints
    {
        public static void MapActivityEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/activity", async (
                InventoryDbContext db, 
                HttpContext http, 
                int limit = 50,
                string? type = null) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                
                // MVP: Only Admins can view full activity feed
                if (user is not Admin) return Results.Forbid();
                
                    Console.WriteLine($"Fetching activity: limit={limit}, type={type ?? "all"}");
    
                // Initialize result lists
                var recentOrders = new List<Order>();
                var recentSales = new List<Sale>();
                var recentAdjustments = new List<Adjustment>();
                
                // Fetch only the requested type(s) to save DB calls
                if (string.IsNullOrWhiteSpace(type) || type.Equals("order", StringComparison.OrdinalIgnoreCase))
                {
                    recentOrders = await db.Orders
                        .Include(o => o.Product).Include(o => o.User)
                        .OrderByDescending(o => o.CreatedAt)
                        .Take(limit)
                        .ToListAsync();
                }
                
                if (string.IsNullOrWhiteSpace(type) || type.Equals("sale", StringComparison.OrdinalIgnoreCase))
                {
                    recentSales = await db.Sales
                        .Include(s => s.Product).Include(s => s.User)
                        .OrderByDescending(s => s.CreatedAt)
                        .Take(limit)
                        .ToListAsync();
                }
                
                if (string.IsNullOrWhiteSpace(type) || type.Equals("adjustment", StringComparison.OrdinalIgnoreCase))
                {
                    recentAdjustments = await db.Adjustments
                        .Include(a => a.Product).Include(a => a.User)
                        .OrderByDescending(a => a.CreatedAt)
                        .Take(limit)
                        .ToListAsync();
                }
                
                // Map to unified ActivityItem format
                var orderItems = recentOrders.Select(o => new ActivityItem(
                    "order", o.OrderID, o.SKU, o.Product?.Name ?? "", o.UserName,
                    string.IsNullOrWhiteSpace(o.User?.FirstName) ? o.UserName : $"{o.User.FirstName} {o.User.LastName}".Trim(),
                    o.Amount, o.Cost, o.CreatedAt, ""
                ));
                
                var saleItems = recentSales.Select(s => new ActivityItem(
                    "sale", s.SaleID, s.SKU, s.Product?.Name ?? "", s.UserName,
                    string.IsNullOrWhiteSpace(s.User?.FirstName) ? s.UserName : $"{s.User.FirstName} {s.User.LastName}".Trim(),
                    s.Amount, s.Income, s.CreatedAt, ""
                ));
                
                var adjustmentItems = recentAdjustments.Select(a => new ActivityItem(
                    "adjustment", a.AdjustmentID, a.SKU, a.Product?.Name ?? "", a.UserName,
                    string.IsNullOrWhiteSpace(a.User?.FirstName) ? a.UserName : $"{a.User.FirstName} {a.User.LastName}".Trim(),
                    a.NewPrice - a.OldPrice, a.NewPrice, a.CreatedAt, a.Reason ?? ""
                ));
                
                // Merge all sources, sort by timestamp (true chronological order), apply final limit
                var allActivity = orderItems
                    .Concat(saleItems)
                    .Concat(adjustmentItems)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(limit)
                    .ToList();
                
                Console.WriteLine($"Returned {allActivity.Count} activity items");
                return Results.Ok(allActivity);
            });
        }
    }
}