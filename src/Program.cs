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
using InventoryManagementSystem.Helpers;
using InventoryManagementSystem.Extensions;

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
            builder.Services.AddAppServices(dbPath);            

            var app = builder.Build();

            // === MIDDLEWARE ===
            app.UseCors();
            app.UseStaticFiles(); // Serve index.html, app.js, styles.css from wwwroot/
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapProductsEndpoints();
            app.MapAdjustmentsEndpoints();
            app.MapOrdersEndpoints();
            app.MapSalesEndpoints();

            // === API ENDPOINTS (Minimal API style for brevity) ===
            
            // Health check
            app.MapGet("/api/health", () => new { status = "ok", timestamp = DateTime.UtcNow });

            

            //Orders API


            //Sales API

            // Adjustments API
            

            //Activity Feed
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
                
                Console.WriteLine($"✅ Returned {allActivity.Count} activity items");
                return Results.Ok(allActivity);
            });

            //User APIs
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
            app.Run(); // Keep server alive!
        }
    }
}