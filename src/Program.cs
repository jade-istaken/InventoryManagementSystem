using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Routing;

namespace InventoryManagementSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            });
            builder.WebHost.UseUrls("http://localhost:5000"); // ← Explicit port

            // === SERVICES ===
            var dbPath = Path.Combine(AppContext.BaseDirectory, "inventory.db");

            builder.Services.AddDbContext<InventoryDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));
            
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IPasswordHasher, BcryptHasher>();         
            builder.Services.AddControllers(); // ← Enable API controllers
            builder.Services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
                          .AllowAnyMethod()
                          .AllowAnyHeader())); // ← Allow frontend origin

            builder.Services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.ASCII.GetBytes(JwtHelper.SecretKey)),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        NameClaimType = ClaimTypes.Name,
                        RoleClaimType = ClaimTypes.Role
                    };
                });
            builder.Services.AddAuthorization();

            var app = builder.Build();

            // === MIDDLEWARE ===
            app.UseCors();
            app.UseStaticFiles(); // Serve index.html, app.js, styles.css from wwwroot/
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            // === API ENDPOINTS (Minimal API style for brevity) ===
            
            // Health check
            app.MapGet("/api/health", () => new { status = "ok", timestamp = DateTime.UtcNow });

            // Products API
            app.MapGet("/api/products", async (InventoryDbContext db) => 
                await db.Products.ToListAsync());
            
            app.MapPost("/api/products", async (InventoryDbContext db, Product product) =>
            {
                db.Products.Add(product);
                await db.SaveChangesAsync();
                return Results.Created($"/api/products/{product.SKU}", product);
            }).RequireAuthorization(); // ← Only authenticated users

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
            }).RequireAuthorization();

            app.MapDelete("/api/products/{sku}", async (InventoryDbContext db, string sku) =>
            {
                var product = await db.Products.FindAsync(sku);
                if (product is null) return Results.NotFound();
                db.Products.Remove(product);
                await db.SaveChangesAsync();
                return Results.NoContent();
            }).RequireAuthorization();

            //Orders API

            // GET /api/orders - List all orders (Admin only)
            app.MapGet("/api/orders", async (InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var orders = await db.Orders
                    .Include(o => o.Product)
                    .Include(o => o.User)
                    .ToListAsync();
                
                return Results.Ok(orders.Select(MapOrderToDto));
            })
            .RequireAuthorization();

            // GET /api/orders/{id} - Get single order
            app.MapGet("/api/orders/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                
                var order = await db.Orders
                    .Include(o => o.Product)
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(o => o.OrderID == id);
                
                if (order == null) return Results.NotFound();
                
                // Staff can only view their own orders
                if (user is not Admin && order.UserName != user.UserName)
                    return Results.Forbid();
                
                return Results.Ok(MapOrderToDto(order));
            })
            .RequireAuthorization();

            app.MapPost("/api/orders", async (OrderCreateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
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
                    Cost = dto.Cost
                };

                //update the inventory status
                product.Quantity += dto.Amount;

                db.Orders.Add(order);
                await db.SaveChangesAsync();

                return Results.Created($"/api/orders/{order.OrderID}", MapOrderToDto(order));
            }).RequireAuthorization();


            // PUT /api/orders/{id} - Update order
            app.MapPut("/api/orders/{id:int}", async (int id, OrderUpdateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
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
                
                return Results.Ok(MapOrderToDto(order));
            })
            .RequireAuthorization();

            // DELETE /api/orders/{id} - Delete order (Admin only)
            app.MapDelete("/api/orders/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
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
            })
            .RequireAuthorization();

            // GET /api/orders/user/{userName} - Get orders by user
            app.MapGet("/api/orders/user/{userName}", async (string userName, InventoryDbContext db, HttpContext http) =>
            {
                var requestingUser = await GetAuthenticatedUserAsync(http, db);
                if (requestingUser == null) return Results.Unauthorized();
                
                // Users can only view their own orders unless admin
                if (requestingUser is not Admin && requestingUser.UserName != userName)
                    return Results.Forbid();
                
                var orders = await db.Orders
                    .Include(o => o.Product)
                    .Include(o => o.User)
                    .Where(o => o.UserName == userName)
                    .ToListAsync();
                
                return Results.Ok(orders.Select(MapOrderToDto));
            })
            .RequireAuthorization();

            //Sales API

            // GET /api/sales - List all sales (Admin only)
            app.MapGet("/api/sales", async (InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var sales = await db.Sales
                    .Include(s => s.Product)
                    .Include(s => s.User)
                    .ToListAsync();
                return Results.Ok(sales.Select(MapSaleToDto));
            })
            .RequireAuthorization();

            // GET /api/sales/{id} - Get single sale
            app.MapGet("/api/sales/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                
                var sale = await db.Sales
                    .Include(s => s.Product)
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.SaleID == id);
                if (sale == null) return Results.NotFound();
                
                // Staff can only view their own sales
                if (user is not Admin && sale.UserName != user.UserName)
                    return Results.Forbid();
                
                return Results.Ok(MapSaleToDto(sale));
            })
            .RequireAuthorization();

            // POST /api/sales - Create a new sale (Admin or Staff)
            app.MapPost("/api/sales", async (SaleCreateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
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
                    Income = dto.Income
                };
                
                // Decrease inventory
                product.Quantity -= dto.Amount;
                
                db.Sales.Add(sale);
                await db.SaveChangesAsync();
                
                return Results.Created($"/api/sales/{sale.SaleID}", MapSaleToDto(sale));
            })
            .RequireAuthorization();

            // PUT /api/sales/{id} - Update sale (Admin only)
            app.MapPut("/api/sales/{id:int}", async (int id, SaleUpdateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
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
                return Results.Ok(MapSaleToDto(sale));
            })
            .RequireAuthorization();

            // DELETE /api/sales/{id} - Delete sale (Admin only)
            app.MapDelete("/api/sales/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
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
            })
            .RequireAuthorization();

            // GET /api/sales/user/{userName} - Get sales by user
            app.MapGet("/api/sales/user/{userName}", async (string userName, InventoryDbContext db, HttpContext http) =>
            {
                var requestingUser = await GetAuthenticatedUserAsync(http, db);
                if (requestingUser == null) return Results.Unauthorized();
                
                // Users can only view their own sales unless admin
                if (requestingUser is not Admin && requestingUser.UserName != userName)
                    return Results.Forbid();
                
                var sales = await db.Sales
                    .Include(s => s.Product)
                    .Include(s => s.User)
                    .Where(s => s.UserName == userName)
                    .ToListAsync();
                return Results.Ok(sales.Select(MapSaleToDto));
            })
            .RequireAuthorization();


            // Authentication API
            app.MapPost("/api/auth/login", async (LoginDto credentials, IUserService userService, HttpContext http) =>
            {
                if (await userService.ValidateCredialsAsync(credentials.UserName, credentials.Password))
                {
                    var user = await userService.GetUserAsync(credentials.UserName);
                    if (user != null)
                    {
                        var token = JwtHelper.GenerateToken(user);
                        return Results.Ok(new 
                        { 
                            token = token,
                            role = user is Admin ? "Admin" : "Staff",
                            userName = user.UserName,                   
                            firstName = user.FirstName,                 
                            lastName = user.LastName                    
                        });
                    }
                }
                return Results.Unauthorized();
            });

            // Temporary debug endpoint in Program.cs
            app.MapPost("/api/test-bcrypt", (IPasswordHasher hasher) =>
            {
                const string pwd = "Test123!";
                try 
                {
                    var hash = hasher.Hash(pwd);
                    var valid = hasher.Verify(pwd, hash);
                    var invalid = hasher.Verify("Wrong", hash);
                    
                    return Results.Json(new {
                        hash = hash,
                        length = hash?.Length,
                        prefix = hash?.Substring(0, 4),
                        verifyCorrect = valid,
                        verifyWrong = !invalid,
                        success = valid && !invalid && hash?.Length == 60
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error: {ex.GetType().Name} - {ex.Message}");
                }
            });

            // === FALLBACK: Serve SPA for client-side routing ===
            app.MapFallbackToFile("index.html");

            // === INITIALIZE DB & SEED ===
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                context.InitializeDatabase();
                
                var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                await userService.EnsureDefaultAdminAsync("SecureAdmin@2026!");
                
            }

            Console.WriteLine("API running at http://localhost:5000");
            Console.WriteLine("Frontend served at http://localhost:5000/");
            app.Run(); // ← Keep server alive!

            // funny little helper functions
            async Task<User?> GetAuthenticatedUserAsync(HttpContext http, InventoryDbContext db)
            {
                var authHeader = http.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                    return null;
                
                var token = authHeader["Bearer ".Length..].Trim();
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(token);
                    var userName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

                    return userName != null ? await db.Users.FindAsync(userName) : null;
                }
                catch
                {
                    return null;
                }
            }

            object MapOrderToDto(Order order) => new
            {
                order.OrderID,
                order.SKU,
                order.UserName,
                order.Amount,
                order.Cost,
                ProductName = order.Product?.Name,
                UserFirstName = order.User?.FirstName,
                UserLastName = order.User?.LastName
            };

            object MapSaleToDto(Sale sale) => new
            {
                orderId = sale.SaleID,
                sku = sale.SKU,
                userName = sale.UserName,
                amount = sale.Amount,
                income = sale.Income,
                ProductName = sale.Product?.Name,
                UserFirstName = sale.User?.FirstName,
                UserLastName = sale.User?.LastName
            };
        }
    }

    // DTOS
    public record OrderCreateDto(string SKU, int Amount, decimal Cost);
    public record OrderUpdateDto(string SKU, int Amount, decimal Cost);
    public record SaleCreateDto(string SKU, int Amount, decimal Income);
    public record SaleUpdateDto(string SKU, int Amount, decimal Income);
    public record LoginDto(string UserName, string Password);
    public record LoginRequest(string UserName, string Password);
}