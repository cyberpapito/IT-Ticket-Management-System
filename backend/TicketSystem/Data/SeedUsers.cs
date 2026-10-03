using TicketSystem.Models;

namespace TicketSystem.Data
{
    // Users the AddUsers migration inserts. Fixed ids so every database has the same ones.
    // Emails are placeholders: the repository is public.
    public static class SeedUsers
    {
        public static readonly Guid AdrianRodriguezId = new("a0000000-0000-0000-0000-000000000001");
        public static readonly Guid JohnDoeId = new("a0000000-0000-0000-0000-000000000002");

        // Tickets created before users existed point at made-up ids; the migration re-points
        // them here so every ticket can reference a real user.
        public static readonly Guid UnknownId = new("a0000000-0000-0000-0000-000000000000");

        public static readonly DateTimeOffset SeededAt = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);

        public static object[] All =>
        [
            new { Id = AdrianRodriguezId, Name = "Adrian Rodriguez", Email = "adrian.rodriguez@example.com", Role = UserRole.Technician, CreatedAt = SeededAt },
            new { Id = JohnDoeId, Name = "John Doe", Email = "john.doe@example.com", Role = UserRole.User, CreatedAt = SeededAt },
            new { Id = UnknownId, Name = "Unknown (pre-users)", Email = "unknown@example.com", Role = UserRole.User, CreatedAt = SeededAt },
        ];
    }
}
