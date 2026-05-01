using System;
namespace InventoryManagementSystem
{
    class Program
    {
        static void Main()
        {
            string dbPath = "inventory.db";
            
            using var context = new InventoryDbContext(dbPath);
            context.InitializeDatabase();

            Console.WriteLine($"Database initialized at: {Path.GetFullPath(dbPath)}");
            
            // Example: Check if a table exists by querying
            int userCount = context.Users.Count();
            Console.WriteLine($"Current user count: {userCount}");
        }
    }
}