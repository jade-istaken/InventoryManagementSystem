using System;
namespace InventoryManagementSystem
{
    internal interface IDatabaseObject
    {
        public void AddToDB();
        //this interface defines the contracts that all objects which eventually get stored in the main database must uphold
        //namely that you can call the associated method AddToDB() on them to write the information in their object instances to the database
        //this will be used for Users, Products, and anything else which eventually needs to be written to the DB
        //extensibility win!
    }

    internal abstract class DBObjectCreator
    {
        public abstract IDatabaseObject FactoryMethod();
        public void AddToDB()
        {
            var product = FactoryMethod();
            product.AddToDB();
        }
    } 
}