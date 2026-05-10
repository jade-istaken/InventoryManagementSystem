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
            app.MapUsersEndpoints();

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