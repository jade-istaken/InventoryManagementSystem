using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
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

            //Map the APIS by deleagting out to the static classes we got
            app.MapProductsEndpoints();
            app.MapAdjustmentsEndpoints();
            app.MapOrdersEndpoints();
            app.MapSalesEndpoints();
            app.MapUsersEndpoints();
            app.MapActivityEndpoints();
            app.MapAuthEndpoins();
            app.MapDebugEndpoints();

            // === FALLBACK: Serve SPA for client-side routing ===
            app.MapFallbackToFile("index.html");

            // === INITIALIZE DB & SEED ===
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                context.InitializeDatabase();
                
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                var seeder = new DemoDataSeeder(context, hasher);
                await seeder.SeedAsync();         
            }

            Console.WriteLine("API running at http://localhost:5000");
            Console.WriteLine("Frontend served at http://localhost:5000/");
            app.Run(); // Keep server alive!
        }
    }
}