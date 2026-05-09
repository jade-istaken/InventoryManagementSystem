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
            builder.WebHost.UseUrls("http://localhost:5000"); //  Explicit port

            // === SERVICES ===
            var dbPath = Path.Combine(AppContext.BaseDirectory, "inventory.db");

            builder.Services.AddDbContext<InventoryDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));
            
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IPasswordHasher, BcryptHasher>();         
            builder.Services.AddControllers(); //  Enable API controllers
            builder.Services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.WithOrigins(
                        "http://localhost:3000", 
                        "http://127.0.0.1:3000",
                        "http://localhost:5000",
                        "http://127.0.0.1:5000"
                        )
                          .AllowAnyMethod()
                          .AllowAnyHeader())); //  Allow frontend origin

            builder.Services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    var validationKey = new SymmetricSecurityKey(
                    Encoding.ASCII.GetBytes(JwtHelper.SecretKey))
                {
                    KeyId = "MVP-Symmetric-Key-2026"  // MUST MATCH JwtHelper exactly
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = validationKey,  //  Use the key with KeyId
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    ValidateLifetime = false,          // Skip expiration for MVP
                    ClockSkew = TimeSpan.FromMinutes(5),
                    RequireSignedTokens = true         // Keep validation, but KeyId fixes the issue
                };
                });
            builder.Services.AddAuthorization();

            builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            {
                options.SerializerOptions.PropertyNameCaseInsensitive = true;
                options.SerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                });

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
            ;

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
            ;

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
                    Cost = dto.Cost,
                    CreatedAt = DateTime.UtcNow
                };

                //update the inventory status
                product.Quantity += dto.Amount;

                db.Orders.Add(order);
                await db.SaveChangesAsync();

                return Results.Created($"/api/orders/{order.OrderID}", MapOrderToDto(order));
            });


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
            });

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
            });

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
            });

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
            });

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
            });

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
                    Income = dto.Income,
                    CreatedAt = DateTime.UtcNow 
                };
                
                // Decrease inventory
                product.Quantity -= dto.Amount;
                
                db.Sales.Add(sale);
                await db.SaveChangesAsync();
                
                return Results.Created($"/api/sales/{sale.SaleID}", MapSaleToDto(sale));
            });

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
            });

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
            });

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
            });

            // Adjustments API
            app.MapGet("/api/adjustments", async (InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var adjustments = await db.Adjustments
                    .Include(a => a.Product)
                    .Include(a => a.User)
                    .OrderByDescending(a => a.AdjustmentID)
                    .ToListAsync();
                
                return Results.Ok(adjustments.Select(MapAdjustmentToDto));
            });

            // POST /api/adjustments - Record a price adjustment (Admin only)
            app.MapPost("/api/adjustments", async (AdjustmentCreateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
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
                return Results.Created($"/api/adjustments/{adjustment.AdjustmentID}", MapAdjustmentToDto(adjustment));
            });

            // GET /api/adjustments/product/{sku} - Get adjustments for a specific product
            app.MapGet("/api/adjustments/product/{sku}", async (string sku, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                
                var adjustments = await db.Adjustments
                    .Include(a => a.Product)
                    .Include(a => a.User)
                    .Where(a => a.SKU == sku)
                    .OrderByDescending(a => a.AdjustmentID)
                    .ToListAsync();
                
                return Results.Ok(adjustments.Select(MapAdjustmentToDto));
            });

            // DELETE /api/adjustments/{id} - Delete an adjustment record (Admin only)
            // Note: Does NOT revert the product price - adjustments are audit trail
            app.MapDelete("/api/adjustments/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var adjustment = await db.Adjustments.FindAsync(id);
                if (adjustment == null) return Results.NotFound();
                
                db.Adjustments.Remove(adjustment);
                await db.SaveChangesAsync();
                
                return Results.NoContent();
            });

            //Activity Feed
            app.MapGet("/api/activity", async (
                InventoryDbContext db, 
                HttpContext http, 
                int limit = 50,
                string? type = null) =>
            {
                var user = await GetAuthenticatedUserAsync(http, db);
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
                
                Console.WriteLine($"✅ Returned {allActivity.Count} activity items");
                return Results.Ok(allActivity);
            });

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

            // TEMP: Debug token validation
            app.MapGet("/api/debug/validate", (HttpContext http) =>
            {
                var auth = http.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer "))
                    return Results.Ok(new { error = "No Bearer token" });
                
                var token = auth["Bearer ".Length..].Trim();
                var handler = new JwtSecurityTokenHandler();
                
                try 
                {
                    var jwt = handler.ReadJwtToken(token);  // Decode only
                    return Results.Ok(new { 
                        decoded = true,
                        algorithm = jwt.Header.Alg,  // ← See what algorithm the token claims
                        claims = jwt.Claims.Select(c => new { c.Type, c.Value })
                    });
                }
                catch (Exception ex)
                {
                    return Results.Ok(new { decoded = false, error = ex.Message });
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
                await userService.EnsureDefaultAdminAsync("admin123");
                
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
                    var userName = jwtToken.Claims.FirstOrDefault(c => 
                        c.Type == "unique_name" ||        // JWT short form
                        c.Type == ClaimTypes.Name         // .NET long form fallback
                    )?.Value;
                    Console.WriteLine($"Auth: Extracted userName='{userName}' from token");

                    if (userName == null)
                    {
                        Console.WriteLine("Auth: userName claim not found in token");
                        return null;
                    }

                    var user = await db.Users.FindAsync(userName);
                    Console.WriteLine($"Auth: Found user in DB: {user != null}");
                    return user;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Auth: Exception decoding token: {ex.GetType().Name} - {ex.Message}");
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
                createdAt = order.CreatedAt,
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
                createdAt = sale.CreatedAt,
                ProductName = sale.Product?.Name,
                UserFirstName = sale.User?.FirstName,
                UserLastName = sale.User?.LastName
            };

            AdjustmentDto MapAdjustmentToDto(Adjustment adj) => new(
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
        }
    }

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
}