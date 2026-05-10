using Microsoft.EntityFrameworkCore;


namespace InventoryManagementSystem.Extensions
{
    // [OO: Single Responsibility] This class handles ONLY demo data initialization
    // [OO: Dependency Injection] Accepts only what it needs via constructor
    public class DemoDataSeeder
    {
        private readonly InventoryDbContext _db;
        private readonly IPasswordHasher _hasher;

        public DemoDataSeeder(InventoryDbContext db, IPasswordHasher hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        // [OO: Idempotency] Safe to call multiple times; only seeds if empty
        public async Task SeedAsync()
        {
            if (await _db.Products.AnyAsync() || await _db.Users.AnyAsync())
                return; // Database already has data

            // === DEMO PRODUCTS ===
            // Note: Replace ProductCategory.RawMaterial/Finished with your actual enum names from Database.cs
            var products = new[]
            {
                new Product { SKU = "RM-0001", Name = "Cold-Rolled Steel Sheet", Manufacturer = "Nippon Steel", Category = Category.RawMaterial, Quantity = 1240, ReorderLevel = 300, Price = 18.75m },
                new Product { SKU = "RM-0002", Name = "Copper Wire 14AWG", Manufacturer = "Southwire", Category = Category.RawMaterial, Quantity = 85, ReorderLevel = 200, Price = 6.30m },
                new Product { SKU = "FG-0001", Name = "Servo Motor SM-400", Manufacturer = "SELF", Category = Category.Finished, Quantity = 44, ReorderLevel = 20, Price = 312.00m },
                new Product { SKU = "RM-0003", Name = "Silicone Gasket Ring", Manufacturer = "Parker Hannifin", Category = Category.RawMaterial, Quantity = 6800, ReorderLevel = 1500, Price = 1.10m },
                new Product { SKU = "FG-0002", Name = "PCB Assembly Rev.7", Manufacturer = "SELF", Category = Category.Finished, Quantity = 190, ReorderLevel = 50, Price = 67.50m },
                new Product { SKU = "FG-0003", Name = "Hydraulic Cylinder HC-20", Manufacturer = "Bosch Rexroth", Category = Category.Finished, Quantity = 12, ReorderLevel = 15, Price = 489.00m },
                new Product { SKU = "RM-0004", Name = "Aluminum Extrusion 6061", Manufacturer = "Alcoa", Category = Category.RawMaterial, Quantity = 520, ReorderLevel = 150, Price = 22.40m },
                new Product { SKU = "FG-0004", Name = "Power Supply Unit 24V", Manufacturer = "SELF", Category = Category.Finished, Quantity = 78, ReorderLevel = 25, Price = 145.00m }
            };

            _db.Products.AddRange(products);

            // === DEMO USERS ===
            // Uses your existing IPasswordHasher strategy (bcrypt) to match auth flow
            var users = new User[]
            {
                new Admin { UserName = "admin", FirstName = "Diana", LastName = "Kovacs", HashedPass = _hasher.Hash("admin123") },
                new Admin { UserName = "jwhitfield", FirstName = "James", LastName = "Whitfield", HashedPass = _hasher.Hash("admin123") },
                new Staff { UserName = "mobi", FirstName = "Marcus", LastName = "Obi", HashedPass = _hasher.Hash("staff123") },
                new Staff { UserName = "slindgren", FirstName = "Sarah", LastName = "Lindgren", HashedPass = _hasher.Hash("staff123") },
                new Staff { UserName = "pnair", FirstName = "Priya", LastName = "Nair", HashedPass = _hasher.Hash("staff123") }
            };

            _db.Users.AddRange(users);

            await _db.SaveChangesAsync();
        }
    }
}