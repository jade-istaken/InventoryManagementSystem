using System;
using System.Data.SqlTypes;
namespace InventoryManagementSystem
{
    internal abstract class Product
    {
        public string Name {get;set;}
        public string SKU {get;}
        public Category Category {get; set;}
        public double Price {get;set;}
        
        public int Quantity {get;set;}
        public int ReorderLevel {get;set;}
        public string Manufacturer {get;}
        protected Product(string name)
        {
            Name = name;
            Manufacturer = "SELF";
            Quantity = 0;
            ReorderLevel = 0;
            Price = 0.00;
            SKU = Guid.NewGuid().ToString();
            //This is the default constructor really, there's no way to make a product without at least a name
            //Without a manufacturer supplied, we assume that it's produced by the company itself
            //Additionally just randomly generate a SKU
        }
        protected Product(string name, string manufacturer)
        {
            Name = name;
            Manufacturer = manufacturer;
            Quantity = 0;
            ReorderLevel = 0;
            Price = 0.00;
            SKU = Guid.NewGuid().ToString();
        }
        protected Product(string name, double price)
        {
            Name = name;
            Manufacturer = "SELF";
            Quantity = 0;
            ReorderLevel = 0;
            Price = price;
            SKU = Guid.NewGuid().ToString();
        }
        protected Product(string name, string manufacturer, double price)
        {
            Name = name;
            Manufacturer = manufacturer;
            Quantity = 0;
            ReorderLevel = 0;
            Price = price;
            SKU = Guid.NewGuid().ToString();
        }
    }

    internal class RawProduct : Product
    {
        public RawProduct(string name) : base(name)
        {
            Category = Category.RawMaterial;
        }

        public RawProduct(string name, string manufacturer) : base(name, manufacturer)
        {
            Category = Category.RawMaterial;
        }

        public RawProduct(string name, double price) : base(name, price)
        {
            Category = Category.RawMaterial;
        }

        public RawProduct(string name, string manufacturer, double price) : base(name, manufacturer, price)
        {
            Category = Category.RawMaterial;
        }
    }

    internal class FinishedProduct : Product
    {
        public FinishedProduct(string name) : base(name)
        {
            Category = Category.Finished;
        }

        public FinishedProduct(string name, string manufacturer) : base(name, manufacturer)
        {
            Category = Category.Finished;
        }

        public FinishedProduct(string name, double price) : base(name, price)
        {
            Category = Category.Finished;
        }

        public FinishedProduct(string name, string manufacturer, double price) : base(name, manufacturer, price)
        {
            Category = Category.Finished;
        }
    }

    enum Category
    {
        RawMaterial,
        Finished
    }
}