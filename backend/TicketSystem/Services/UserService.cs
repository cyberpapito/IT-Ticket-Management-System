using Microsoft.EntityFrameworkCore;
using TicketSystem.Data;
using TicketSystem.Models;

namespace TicketSystem.Services
{
    public class UserService
    {
        private readonly AppDbContext _dbContext;

        public UserService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User> CreateUser(string name, string email, UserRole role)
        {
            var user = User.Create(name, email, role);

            // The unique index would also refuse it, but as a database error (500); this makes it a 400.
            if (await _dbContext.Users.AnyAsync(u => u.Email == user.Email))
                throw new ArgumentException($"A user with email {user.Email} already exists.");

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            return user;
        }

        public async Task<User?> GetUserById(Guid userId)
        {
            return await _dbContext.Users.FindAsync(userId);
        }

        public async Task<List<User>> GetUsers(UserRole? role)
        {
            var users = _dbContext.Users.AsQueryable();
            if (role is not null)
                users = users.Where(u => u.Role == role);
            return await users.OrderBy(u => u.Name).ToListAsync();
        }
    }
}
