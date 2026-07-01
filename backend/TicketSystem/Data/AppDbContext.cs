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
                    .HasMaxLength(255);

                entity.Property(t => t.Description)
                    .HasMaxLength(2000);

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
            });
        }
    }
}