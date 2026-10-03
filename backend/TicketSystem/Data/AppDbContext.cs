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