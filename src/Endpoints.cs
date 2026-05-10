using InventoryManagementSystem.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace InventoryManagementSystem
{
    // These classes are going to be single responsibility classes for each endpoint
    //Each endpoint counts kind of as a command 

    public static class ProductsEndpoints
    {
        public static void MapProductsEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/products", async (InventoryDbContext db) => 
                await db.Products.ToListAsync());
            
            app.MapPost("/api/products", async (InventoryDbContext db, Product product) =>
            {
                try
                {
                    Console.WriteLine($"Creating product: SKU={product.SKU}, Name={product.Name}, Category={product.Category}");
        
                    // Check for duplicate SKU before adding
                    var existing = await db.Products.FindAsync(product.SKU);
                    if (existing != null)
                    {
                        Console.WriteLine($" Duplicate SKU: {product.SKU}");
                        return Results.BadRequest(new { error = "SKU already exists" });
                    }
                    db.Products.Add(product);
                    await db.SaveChangesAsync();

                    Console.WriteLine($"Product created: {product.SKU}");
                    return Results.Created($"/api/products/{product.SKU}", product);
                }
                    catch (Exception ex)
                {
                    Console.WriteLine($"Product creation failed: {ex.GetType().Name} - {ex.Message}");
                    Console.WriteLine($"   Stack: {ex.StackTrace}");
                    return Results.BadRequest(new { error = ex.Message });
                }

            }); // ← Only authenticated users

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
            });

            app.MapDelete("/api/products/{sku}", async (InventoryDbContext db, string sku) =>
            {
                var product = await db.Products.FindAsync(sku);
                if (product is null) return Results.NotFound();
                db.Products.Remove(product);
                await db.SaveChangesAsync();
                return Results.NoContent();
            });
        }
    }
}