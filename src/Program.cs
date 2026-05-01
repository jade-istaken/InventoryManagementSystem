using System;
namespace InventoryManagementSystem
{
    class Program
    {
        static async Task Main()
        {
            string dbPath = "inventory.db";
        
            using var context = new InventoryDbContext(dbPath);
            context.InitializeDatabase(); // Creates DB & schema if missing

            var hasher = new BcryptHasher();
            var userService = new UserService(context, hasher);

            // Seed default admin if DB is brand new
            await userService.EnsureDefaultAdminAsync("SecureAdmin@2026!");

            // Continue with normal app flow...
            Console.WriteLine("Inventory system ready.");
        }
    }
}