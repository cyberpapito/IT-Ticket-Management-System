namespace TicketSystem.Models
{
    public class User
    {
        // Column sizes in the database (AppDbContext uses these), enforced here like Ticket's.
        public const int NameMaxLength = 100;
        public const int EmailMaxLength = 255;

        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public UserRole Role { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

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
