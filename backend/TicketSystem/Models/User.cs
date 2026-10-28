using System.Text.Json.Serialization;

namespace TicketSystem.Models
{
    public class User
    {
        // Column sizes in the database (AppDbContext uses these), enforced here like Ticket's.
        public const int NameMaxLength = 100;
        public const int EmailMaxLength = 255;
        public const int PasswordHashMaxLength = 256;
        public const int SecurityStampMaxLength = 64;

        // NIST SP 800-63B asks for at least 8 characters and no composition rules; 12 here. A long
        // passphrase is fine, but not unbounded (the hash has to process all of it).
        public const int PasswordMinLength = 12;
        public const int PasswordMaxLength = 128;

        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public UserRole Role { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        // Null until a password is set: such a user exists (tickets can name them) but can't sign in.
        [JsonIgnore]
        public string? PasswordHash { get; private set; }

        // Changes whenever the password or role does. The sign-in cookie carries the value it was
        // issued with, so a password reset or demotion ends every existing session.
        [JsonIgnore]
        public string SecurityStamp { get; private set; } = NewStamp();

        // Technicians and admins work tickets; plain users report them.
        [JsonIgnore]
        public bool IsStaff => Role is UserRole.Technician or UserRole.Admin;

        public static void ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < PasswordMinLength)
                throw new ArgumentException($"A password needs at least {PasswordMinLength} characters.");

            if (password.Length > PasswordMaxLength)
                throw new ArgumentException($"A password cannot be longer than {PasswordMaxLength} characters.");
        }

        // The hash comes from ASP.NET Core's PasswordHasher (UserService); the entity only stores it.
        public void SetPasswordHash(string passwordHash)
        {
            PasswordHash = passwordHash;
            SecurityStamp = NewStamp();
        }

        // The same password, re-hashed with current settings at sign-in: not a new password, so
        // the stamp (and the session being created) stays.
        public void UpgradePasswordHash(string passwordHash)
        {
            PasswordHash = passwordHash;
        }

        public void ChangeRole(UserRole role)
        {
            if (!Enum.IsDefined(role))
                throw new ArgumentException($"Unknown role {(int)role}");

            if (role == Role)
                return;

            Role = role;
            SecurityStamp = NewStamp();
        }

        private static string NewStamp() => Guid.NewGuid().ToString("N");

        // Factory method - only way to create a new user. Email is stored lowercased so the
        // unique index treats Adrian@Example.com and adrian@example.com as the same person.
        public static User Create(string name, string email, UserRole role)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty");

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty");

            name = name.Trim();
            email = email.Trim().ToLowerInvariant();

            if (name.Length > NameMaxLength)
                throw new ArgumentException($"Name cannot be longer than {NameMaxLength} characters");

            if (email.Length > EmailMaxLength)
                throw new ArgumentException($"Email cannot be longer than {EmailMaxLength} characters");

            // A sanity check, not full validation: one @ with something on both sides.
            var at = email.IndexOf('@');
            if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
                throw new ArgumentException("Email must look like name@domain");

            if (!Enum.IsDefined(role))
                throw new ArgumentException($"Unknown role {(int)role}");

            return new User
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                Role = role,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
