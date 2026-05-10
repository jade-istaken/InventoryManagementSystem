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

    public static class AdjustmentsEndpoints
    {
        public static void MapAdjustmentsEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/adjustments", async (InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var adjustments = await db.Adjustments
                    .Include(a => a.Product)
                    .Include(a => a.User)
                    .OrderByDescending(a => a.AdjustmentID)
                    .ToListAsync();
                
                return Results.Ok(adjustments.Select(DtoMappers.MapAdjustmentToDto));
            });

            // POST /api/adjustments - Record a price adjustment (Admin only)
            app.MapPost("/api/adjustments", async (AdjustmentCreateDto dto, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();  // Price changes = admin-only
                
                // Validate product exists
                var product = await db.Products.FindAsync(dto.SKU);
                if (product == null)
                    return Results.BadRequest(new { error = "Product not found" });
                
                // Validate input
                if (dto.NewPrice < 0)
                    return Results.BadRequest(new { error = "Price cannot be negative" });
                if (string.IsNullOrWhiteSpace(dto.Reason))
                    return Results.BadRequest(new { error = "Reason is required" });
                
                // Create adjustment record
                var adjustment = new Adjustment
                {
                    SKU = dto.SKU,
                    UserName = user.UserName,
                    OldPrice = product.Price,      // Capture price BEFORE change
                    NewPrice = dto.NewPrice,
                    Reason = dto.Reason.Trim(),
                    CreatedAt = DateTime.UtcNow 
                };
                
                //Update the product's actual price
                product.Price = dto.NewPrice;
                
                db.Adjustments.Add(adjustment);
                await db.SaveChangesAsync();
                
                Console.WriteLine($"rice adjustment: {product.Name} ${product.Price} → ${dto.NewPrice} ({dto.Reason})");
                return Results.Created($"/api/adjustments/{adjustment.AdjustmentID}", DtoMappers.MapAdjustmentToDto(adjustment));
            });

            // GET /api/adjustments/product/{sku} - Get adjustments for a specific product
            app.MapGet("/api/adjustments/product/{sku}", async (string sku, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                
                var adjustments = await db.Adjustments
                    .Include(a => a.Product)
                    .Include(a => a.User)
                    .Where(a => a.SKU == sku)
                    .OrderByDescending(a => a.AdjustmentID)
                    .ToListAsync();
                
                return Results.Ok(adjustments.Select(DtoMappers.MapAdjustmentToDto));
            });

            // DELETE /api/adjustments/{id} - Delete an adjustment record (Admin only)
            // Note: Does NOT revert the product price - adjustments are audit trail
            app.MapDelete("/api/adjustments/{id:int}", async (int id, InventoryDbContext db, HttpContext http) =>
            {
                var user = await AuthHelper.GetAuthenticatedUserAsync(http, db);
                if (user == null) return Results.Unauthorized();
                if (user is not Admin) return Results.Forbid();
                
                var adjustment = await db.Adjustments.FindAsync(id);
                if (adjustment == null) return Results.NotFound();
                
                db.Adjustments.Remove(adjustment);
                await db.SaveChangesAsync();
                
                return Results.NoContent();
            });
        }
    }
}