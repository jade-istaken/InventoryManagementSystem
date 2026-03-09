using System;
namespace InventoryManagementSystem
{
    internal abstract class Product
    {
        public string Name {get;set;}
        public int SKU {get;}
        public Category Category {get;}
        public double Price {get;set;}
        
        public int Quantity {get;set;}
        public int ReorderLevel {get;set;}
        public string Manufacturer {get;}
        protected Product(string name, string manufacturer)
        {
            this.Name = name;
            this.Manufacturer = manufacturer;
            this.Quantity = 1;
            this.ReorderLevel = 1;
            this.Price = 0.00;
        }
        
    }

    enum Category
    {
        Material,
        Finished
    }
}