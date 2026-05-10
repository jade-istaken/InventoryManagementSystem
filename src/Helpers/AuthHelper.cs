using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
namespace InventoryManagementSystem.Helpers
{
    //This is a little singleton class that we're basically using only to handle authentication, so it's not in Program.cs anymore
    public static class AuthHelper
    {
        public static async Task<User?> GetAuthenticatedUserAsync(HttpContext http, InventoryDbContext db)
            {
                var authHeader = http.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                    return null;
                
                var token = authHeader["Bearer ".Length..].Trim();
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(token);
                    var userName = jwtToken.Claims.FirstOrDefault(c => 
                        c.Type == "unique_name" ||        // JWT short form
                        c.Type == ClaimTypes.Name         // .NET long form fallback
                    )?.Value;
                    Console.WriteLine($"Auth: Extracted userName='{userName}' from token");

                    if (userName == null)
                    {
                        Console.WriteLine("Auth: userName claim not found in token");
                        return null;
                    }

                    var user = await db.Users.FindAsync(userName);
                    Console.WriteLine($"Auth: Found user in DB: {user != null}");
                    return user;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Auth: Exception decoding token: {ex.GetType().Name} - {ex.Message}");
                    return null;
                }
            }
    }
}