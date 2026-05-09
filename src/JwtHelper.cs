using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace InventoryManagementSystem
{
    public static class JwtHelper
    {
        public const string SecretKey = "THISISIT!!!heheeehmysecRet!!!key!!!!itsthESECretCKeyyyyyyyyheHEHEHEHEEHEEheeheeee";

        public static string GenerateToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(SecretKey);

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user.UserName),
                new(ClaimTypes.Role, user is Admin ? "Admin" : "Staff"),
                new("firstName", user.FirstName),
                new("lastName", user.LastName)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials =  new SigningCredentials(
                    new SymmetricSecurityKey(key){KeyId = "MVP-Symmetric-Key-2026"},
                    SecurityAlgorithms.HmacSha256
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}