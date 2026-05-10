using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Json;

namespace InventoryManagementSystem.Extensions
{
    //The serviceExtensions class will act as a facade to the complex stuff that goes on in setting up service registration
    //this is going to make things os much easier

    public static class ServiceExtensions
    {
        public static void AddAppServices(this IServiceCollection services, string dbPath)
        {
            services.AddDbContext<InventoryDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPasswordHasher, BcryptHasher>();
            services.AddControllers();
            
            services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.WithOrigins(
                        "http://localhost:3000", "http://127.0.0.1:3000",
                        "http://localhost:5000", "http://127.0.0.1:5000")
                    .AllowAnyMethod().AllowAnyHeader()));

            services.AddAuthentication("Bearer").AddJwtBearer("Bearer", options =>
            {
                var key = new SymmetricSecurityKey(
                    Encoding.ASCII.GetBytes(JwtHelper.SecretKey))
                { KeyId = "MVP-Symmetric-Key-2026" };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    ValidateLifetime = false,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    RequireSignedTokens = true
                };
            });
            services.AddAuthorization();

            services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.PropertyNameCaseInsensitive = true;
                options.SerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
        }
    } 
}