using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel;
using System.IO;

namespace InventoryManagementSystem
{
    #region Entities
    
    public abstract class User
    {
        public string UserName{ get; set;} = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string HashedPass{ get; set; } = string.Empty;
        public abstract bool CanModifyPrices();
        public abstract bool CanViewFinancialReports();
    }

    //let's be real pretty much all inheritence counts as using the template pattern
    public class Admin : User
    {
        public override bool CanModifyPrices() => true;
        public override bool CanViewFinancialReports() => true;
    }

    public class Staff : User
    {
        public override bool CanModifyPrices() => false;
        public override bool CanViewFinancialReports() => false;
    }

    public class Product
    {
        public string SKU { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public Category Category { get; set; }
        public int Quantity { get; set; }
        public int ReorderLevel { get; set; }
        public decimal Price { get; set; }
    }

    public class Order
    {
        public int OrderID { get; set; }
        public string SKU {get; set;} = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Amount { get; set; }
        public decimal Cost { get; set; }

        // Navigation properties
        public Product Product { get; set; } = null!;
        public User User { get; set; } = null!;
    }

    public class Sale
    {
        public int OrderID { get; set; }
        public string SKU {get; set;} = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Amount { get; set; }
        public decimal Income { get; set; }

        // Navigation properties
        public Product Product { get; set; } = null!;
        public User User { get; set; } = null!;
    }

    public class Adjustment
    {
        public int AdjustmentID { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string Reason { get; set; } = string.Empty;

        // Navigation properties
        public Product Product { get; set; } = null!;
        public User User { get; set; } = null!;
    }

    #endregion

    //the entire InventoryDbContext class is a facade for the many complex systems that go into an ORM. it makes it so much easier to interact with the database
    public class InventoryDbContext : DbContext
    {
        private readonly string _dbPath;
        public InventoryDbContext(string dbFilePath)
        {
            _dbPath = dbFilePath;
            
            // Ensure parent directory exists before EF tries to create the DB
            var directory = Path.GetDirectoryName(Path.GetFullPath(dbFilePath));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        //these DbSet generics are examples ofg the iterator pattern, they're extremely useful for efficiently storing the results of queries

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Sale> Sales { get; set; } = null!;
        public DbSet<Adjustment> Adjustments { get; set; } = null!;
        
        //the options builder and model builder are both examples of the builder pattern. 
        //the modelbuilder uses it more but it shows how the builder pattern is really useful for making complex constructors
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Users PK
            modelBuilder.Entity<User>()
                .HasDiscriminator<string>("PrivilegeLevel")
                    .HasValue<Admin>("Admin")
                    .HasValue<Staff>("Staff");

            // Products PK
            modelBuilder.Entity<Product>().HasKey(p => p.SKU);

            // Orders PK & FKs
            modelBuilder.Entity<Order>().HasKey(o => o.OrderID);
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Product)
                .WithMany()
                .HasForeignKey(o => o.SKU)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserName)
                .OnDelete(DeleteBehavior.Restrict);

            // Sales PK & FKs
            modelBuilder.Entity<Sale>().HasKey(s => s.OrderID);
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.SKU)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserName)
                .OnDelete(DeleteBehavior.Restrict);

            // Adjustments PK & FKs
            modelBuilder.Entity<Adjustment>().HasKey(a => a.AdjustmentID);
            modelBuilder.Entity<Adjustment>()
                .HasOne(a => a.Product)
                .WithMany()
                .HasForeignKey(a => a.SKU)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Adjustment>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserName)
                .OnDelete(DeleteBehavior.Restrict);
        }

        /// <summary>
        /// Creates the database file and schema if they don't already exist.
        /// </summary>
        public void InitializeDatabase()
        {
            Database.EnsureCreated();
        }
    }

    public enum Privilege
    {
        Administrator,
        Staff,
        Guest
    }

    public enum Category
    {
        RawMaterial,
        Finished
    }
}