using Microsoft.EntityFrameworkCore;
using TicketSystem.Models;

namespace TicketSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<TicketActivity> TicketActivities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Ticket>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Title)
                    .IsRequired()
                    .HasMaxLength(Ticket.TitleMaxLength);

                entity.Property(t => t.Description)
                    .HasMaxLength(Ticket.DescriptionMaxLength);

                entity.Property(t => t.Priority)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(t => t.Status)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(t => t.CreatedAt)
                    .IsRequired();

                entity.Property(t => t.ResolvedAt)
                    .IsRequired(false);

                entity.Property(t => t.AssignedToUserId)
                    .IsRequired(false);

                // Every ticket's creator and technician must be a real user. Restrict: a user
                // with tickets can't be deleted out from under them.
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(t => t.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(t => t.AssignedToUserId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                // Soft-deleted tickets are hidden from every query; use IgnoreQueryFilters()
                // when the audit history is actually needed.
                entity.HasQueryFilter(t => t.DeletedAt == null);

                // The history is reached through the ticket; Ticket.Activities reads the field.
                entity.HasMany(t => t.Activities)
                    .WithOne()
                    .HasForeignKey(a => a.TicketId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Navigation(t => t.Activities)
                    .UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<TicketActivity>(entity =>
            {
                entity.HasKey(a => a.Id);

                // Ticket sets the id. Without this, EF would take an activity added to an
                // already-saved ticket for an existing row and try to update it.
                entity.Property(a => a.Id)
                    .ValueGeneratedNever();

                entity.Property(a => a.Type)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(a => a.At)
                    .IsRequired();

                // No length cap: a resolution summary has none. Comments are capped by the entity.
                entity.Property(a => a.Note)
                    .IsRequired(false);

                entity.Property(a => a.FromStatus)
                    .HasConversion<int?>();

                entity.Property(a => a.ToStatus)
                    .HasConversion<int?>();

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(a => a.UserId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                // No query filter of its own: the history is only read through its ticket, and
                // TicketService looks the ticket up first, so a deleted ticket's history stays hidden.
                entity.HasIndex(a => new { a.TicketId, a.At });
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);

                entity.Property(u => u.Name)
                    .IsRequired()
                    .HasMaxLength(User.NameMaxLength);

                entity.Property(u => u.Email)
                    .IsRequired()
                    .HasMaxLength(User.EmailMaxLength);

                entity.HasIndex(u => u.Email).IsUnique();

                entity.Property(u => u.Role)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(u => u.CreatedAt)
                    .IsRequired();

                entity.HasData(SeedUsers.All);
            });
        }
    }
}