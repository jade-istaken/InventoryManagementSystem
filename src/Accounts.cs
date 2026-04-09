using System;
using System.Diagnostics.CodeAnalysis;
namespace InventoryManagementSystem
{
    public enum UserRole {Admin, Staff}

    //This is strategy pattern i think? where like the actual password hasher is just an interface, not something concrete 
    public interface IPasswordHasher
    {
        string Hash(string plainText);
        bool Verify(string hashed, string plainText);
    }

    //this service is gonna act as a facade for having to interact with the EFCore part
    public interface IUserService
    {
        User CreateUser(string userName, string firstName, string lastName, string plainPass, UserRole role);
        Task<User?> GetUserAsync(string UserName);
        Task<bool> ValidateCredialsAsync(string userName, string plainPass);
    }

    public class UserService : IUserService{
        private readonly InventoryDbContext _context;
        private readonly IPasswordHasher _hasher;

        public UserService(InventoryDbContext context, IPasswordHasher hasher)
        {
            _context = context;
            _hasher = hasher;
        }

        //this is kinda a factory because like. we have the product is just something that implements the User abstract class
        //and then the actual instantiation is branched
        public User CreateUser(string userName, string firstName, string lastName, string plainPass, UserRole role)
        {
            User user = role switch{
                UserRole.Admin => new Admin(),
                UserRole.Staff => new Staff(),
                _ => throw new ArgumentException("Rnvalid role: ", nameof(role))
            };

            user.UserName = userName;
            user.HashedPass = _hasher.Hash(plainPass);
            user.FirstName = firstName;
            user.LastName = lastName;

            _context.Users.Add(user);
            _context.SaveChanges();

            return user;
        }

        public async Task<User?> GetUserAsync(string UserName)
        {
            return await _context.Users.FindAsync(UserName);
        }

        public async Task<bool> ValidateCredialsAsync(string userName, string plainPass)
        {
            var user = await GetUserAsync(userName);
            return user != null && _hasher.Verify(user.HashedPass, plainPass);
        }
    }
}