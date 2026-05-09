using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

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

            var app = builder.Build();

            // === MIDDLEWARE ===
            app.UseCors();
            app.UseStaticFiles(); // ← Serve index.html, app.js, styles.css from wwwroot/
            app.UseRouting();

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

            // Authentication API
            app.MapPost("/api/auth/login", async (
                InventoryDbContext db, 
                IPasswordHasher hasher,
                LoginRequest request) => // ← New DTO class (see below)
            {
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.UserName == request.UserName);
                
                if (user is null || !hasher.Verify(request.Password, user.HashedPass))
                    return Results.Unauthorized();
                
                // For production: use JWT token generation here
                // For MVP: return user object with role (insecure but functional for coupling demo)
                return Results.Ok(new { 
                    userName = user.UserName, 
                    firstName = user.FirstName, 
                    lastName = user.LastName, 
                    role = user is Admin ? "Admin" : user is Staff ? "Staff" : "Unknown"
                });
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
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var admin = await context.Users.FirstOrDefaultAsync(u => u.UserName == "admin");
                
                if (admin != null)
                {
                    Console.WriteLine($"🔍 Admin PasswordHash length: {admin.HashedPass?.Length ?? 0}");
                    Console.WriteLine($"🔍 Admin PasswordHash preview: {admin.HashedPass?.Substring(0, Math.Min(30, admin.HashedPass.Length))}...");
                    Console.WriteLine($"🔍 Expected bcrypt prefix: $2a$, $2b$, or $2y$");
                }
            }

            Console.WriteLine("API running at http://localhost:5000");
            Console.WriteLine("Frontend served at http://localhost:5000/");
            app.Run(); // ← Keep server alive!
        }
    }

    // === NEW: Request DTO for login ===
    public record LoginRequest(string UserName, string Password);
}