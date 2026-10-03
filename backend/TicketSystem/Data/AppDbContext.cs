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

                // Soft-deleted tickets are hidden from every query; use IgnoreQueryFilters()
                // when the audit history is actually needed.
                entity.HasQueryFilter(t => t.DeletedAt == null);
            });
        }
    }
}