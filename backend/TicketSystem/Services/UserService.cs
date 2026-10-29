using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Data;
using TicketSystem.Models;

namespace TicketSystem.Services
{
    public class UserService
    {
        private readonly AppDbContext _dbContext;
        private readonly IPasswordHasher<User> _hasher;

        public UserService(AppDbContext dbContext, IPasswordHasher<User> hasher)
        {
            _dbContext = dbContext;
            _hasher = hasher;
        }

        public async Task<User> CreateUser(string name, string email, UserRole role, string password)
        {
            var user = User.Create(name, email, role);
            User.ValidatePassword(password);

            // The unique index would also refuse it, but as a database error (500); this makes it a 400.
            if (await _dbContext.Users.AnyAsync(u => u.Email == user.Email))
                throw new ArgumentException($"A user with email {user.Email} already exists.");

            user.SetPasswordHash(_hasher.HashPassword(user, password));
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            return user;
        }

        // Null for an unknown user, and for one the viewer may not see (see GetUsers).
        public async Task<User?> GetUserById(Guid userId, Guid viewerId)
        {
            var viewer = await _dbContext.Users.FindAsync(viewerId);
            var user = await _dbContext.Users.FindAsync(userId);
            if (viewer is null || user is null)
                return null;

            return viewer.IsStaff || user.IsStaff || user.Id == viewer.Id ? user : null;
        }

        // Staff see everyone. A plain user sees themselves and the staff: everyone who can appear
        // on their own tickets (they reported them; only staff are assigned or comment on others').
        public async Task<List<User>> GetUsers(UserRole? role, Guid viewerId)
        {
            var viewer = await _dbContext.Users.FindAsync(viewerId);
            if (viewer is null)
                return [];

            var users = _dbContext.Users.AsQueryable();
            if (!viewer.IsStaff)
                users = users.Where(u => u.Id == viewer.Id || u.Role == UserRole.Technician || u.Role == UserRole.Admin);
            if (role is not null)
                users = users.Where(u => u.Role == role);
            return await users.OrderBy(u => u.Name).ToListAsync();
        }

        // Null for an unknown user. Refuses (409) to demote the last admin who can sign in, which
        // would leave nobody able to manage users.
        public async Task<User?> ChangeRole(Guid userId, UserRole role)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user is null)
                return null;

            if (user.Role == UserRole.Admin && role != UserRole.Admin
                && !await _dbContext.Users.AnyAsync(u => u.Id != userId && u.Role == UserRole.Admin && u.PasswordHash != null))
                throw new InvalidOperationException("This is the only admin who can sign in. Make someone else an admin first.");

            user.ChangeRole(role);
            await _dbContext.SaveChangesAsync();
            return user;
        }

        // An admin's password reset. Ends the user's existing sessions (the security stamp changes).
        public async Task<User?> SetPassword(Guid userId, string password)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user is null)
                return null;

            User.ValidatePassword(password);
            user.SetPasswordHash(_hasher.HashPassword(user, password));
            await _dbContext.SaveChangesAsync();
            return user;
        }

        // The user, or null for a wrong email or password (the caller can't tell which), or for an
        // account with no password yet.
        public async Task<User?> Authenticate(string email, string password)
        {
            var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == normalized);
            if (user?.PasswordHash is null || string.IsNullOrEmpty(password))
            {
                // Hash anyway, so an unknown email takes as long to refuse as a wrong password.
                _hasher.HashPassword(DummyUser, password ?? string.Empty);
                return null;
            }

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
                return null;

            // The hash was made with older settings (fewer iterations): upgrade it now that we
            // have the password.
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.UpgradePasswordHash(_hasher.HashPassword(user, password));
                await _dbContext.SaveChangesAsync();
            }

            return user;
        }

        // The first admin, from configuration (user secrets in development, an environment
        // variable when deployed). Does nothing once any admin can sign in, so a later restart
        // never resets a password someone changed. Returns what it did, for the startup log.
        public async Task<string?> EnsureBootstrapAdmin(string email, string name, string password)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Role == UserRole.Admin && u.PasswordHash != null))
                return null;

            User.ValidatePassword(password);
            var normalized = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == normalized);
            if (user is null)
            {
                await CreateUser(name, email, UserRole.Admin, password);
                return $"Created admin {normalized}.";
            }

            user.ChangeRole(UserRole.Admin);
            user.SetPasswordHash(_hasher.HashPassword(user, password));
            await _dbContext.SaveChangesAsync();
            return $"Made {normalized} an admin and set its password.";
        }

        private static readonly User DummyUser = User.Create("Nobody", "nobody@example.com", UserRole.User);
    }
}
