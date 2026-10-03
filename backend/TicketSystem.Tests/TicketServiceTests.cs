using Microsoft.EntityFrameworkCore;
using TicketSystem.Data;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Tests
{
    // Runs TicketService against EF Core's in-memory provider, so no SQL Server is needed.
    // Each test gets its own database name, and reads go through a fresh context so they
    // check what was saved, not what the change tracker still holds.
    public class TicketServiceTests
    {
        private readonly DbContextOptions<AppDbContext> _options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        // Loads the users the AddUsers migration seeds (Adrian Rodriguez, John Doe, Unknown).
        public TicketServiceTests()
        {
            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        private AppDbContext NewContext() => new(_options);

        private async Task<Ticket> Seed(string title = "Laptop won't boot")
        {
            using var context = NewContext();
            return await new TicketService(context)
                .CreateTicket(title, "Black screen after update", TicketPriority.High, SeedUsers.JohnDoeId);
        }

        [Fact]
        public async Task SeededUsersAreThere()
        {
            using var context = NewContext();
            var users = await context.Users.OrderBy(u => u.Name).ToListAsync();

            Assert.Equal(["Adrian Rodriguez", "John Doe", "Unknown (pre-users)"], users.Select(u => u.Name));
            Assert.Equal(UserRole.Technician, users.Single(u => u.Id == SeedUsers.AdrianRodriguezId).Role);
            Assert.Equal(UserRole.User, users.Single(u => u.Id == SeedUsers.JohnDoeId).Role);
        }

        [Fact]
        public async Task CreateTicket_UnknownCreatorThrowsAndSavesNothing()
        {
            using (var context = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                    new TicketService(context).CreateTicket("Title", "desc", TicketPriority.Low, Guid.NewGuid()));
                Assert.StartsWith("No user with id", ex.Message);
            }

            using var check = NewContext();
            Assert.Empty(await check.Tickets.IgnoreQueryFilters().ToListAsync());
        }

        [Fact]
        public async Task AssignTicket_OnlyToAnExistingTechnician()
        {
            var created = await Seed();

            using (var context = NewContext())
            {
                var service = new TicketService(context);
                await Assert.ThrowsAsync<ArgumentException>(() => service.AssignTicket(created.Id, Guid.NewGuid()));
                await Assert.ThrowsAsync<ArgumentException>(() => service.AssignTicket(created.Id, SeedUsers.JohnDoeId));
                Assert.Null(await service.AssignTicket(Guid.NewGuid(), SeedUsers.AdrianRodriguezId));  // unknown ticket
            }

            using (var context = NewContext())
            {
                var assigned = await new TicketService(context).AssignTicket(created.Id, SeedUsers.AdrianRodriguezId);
                Assert.Equal(SeedUsers.AdrianRodriguezId, assigned!.AssignedToUserId);
            }
        }

        [Fact]
        public async Task CreateTicket_PersistsTicket()
        {
            var created = await Seed();

            using var context = NewContext();
            var stored = await context.Tickets.SingleAsync();

            Assert.Equal(created.Id, stored.Id);
            Assert.Equal("Laptop won't boot", stored.Title);
            Assert.Equal(TicketPriority.High, stored.Priority);
            Assert.Equal(TicketStatus.Open, stored.Status);
        }

        [Fact]
        public async Task CreateTicket_BlankTitleSavesNothing()
        {
            using (var context = NewContext())
            {
                var service = new TicketService(context);
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    service.CreateTicket(" ", "desc", TicketPriority.Low, Guid.NewGuid()));
            }

            using var check = NewContext();
            Assert.Empty(await check.Tickets.ToListAsync());
        }

        [Fact]
        public async Task GetTicketById_ReturnsStoredTicket()
        {
            var created = await Seed();

            using var context = NewContext();
            var found = await new TicketService(context).GetTicketById(created.Id);

            Assert.NotNull(found);
            Assert.Equal(created.Title, found!.Title);
        }

        [Fact]
        public async Task GetTicketById_ReturnsNullForUnknownId()
        {
            await Seed();

            using var context = NewContext();
            Assert.Null(await new TicketService(context).GetTicketById(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetAllTickets_EmptyDatabaseReturnsEmptyList()
        {
            using var context = NewContext();
            var tickets = await new TicketService(context).GetAllTickets();

            Assert.NotNull(tickets);
            Assert.Empty(tickets);
        }

        [Fact]
        public async Task GetAllTickets_ReturnsEveryTicket()
        {
            var a = await Seed("Ticket A");
            var b = await Seed("Ticket B");

            using var context = NewContext();
            var ids = (await new TicketService(context).GetAllTickets()).Select(t => t.Id);

            Assert.Equal(new[] { a.Id, b.Id }.OrderBy(i => i), ids.OrderBy(i => i));
        }

        private async Task Delete(Guid id)
        {
            using var context = NewContext();
            Assert.NotNull(await new TicketService(context).SoftDeleteTicket(id));
        }

        [Fact]
        public async Task SoftDeleteTicket_StampsDeletedAtAndKeepsRow()
        {
            var created = await Seed();

            await Delete(created.Id);

            using var check = NewContext();
            var stored = await check.Tickets.IgnoreQueryFilters().SingleAsync();
            Assert.Equal(created.Id, stored.Id);
            Assert.NotNull(stored.DeletedAt);
        }

        [Fact]
        public async Task SoftDeleteTicket_ReturnsNullForUnknownId()
        {
            using var context = NewContext();
            Assert.Null(await new TicketService(context).SoftDeleteTicket(Guid.NewGuid()));
        }

        [Fact]
        public async Task SoftDeleteTicket_TwiceReturnsNullAndKeepsOriginalTimestamp()
        {
            var created = await Seed();
            await Delete(created.Id);
            DateTimeOffset? original;
            using (var context = NewContext())
            {
                original = (await context.Tickets.IgnoreQueryFilters().SingleAsync()).DeletedAt;
            }

            using (var context = NewContext())
            {
                Assert.Null(await new TicketService(context).SoftDeleteTicket(created.Id));
            }

            using var check = NewContext();
            Assert.Equal(original, (await check.Tickets.IgnoreQueryFilters().SingleAsync()).DeletedAt);
        }

        [Fact]
        public async Task GetTicketById_ReturnsNullForDeletedTicket()
        {
            var created = await Seed();
            await Delete(created.Id);

            using var context = NewContext();
            Assert.Null(await new TicketService(context).GetTicketById(created.Id));
        }

        [Fact]
        public async Task GetAllTickets_ExcludesDeletedTickets()
        {
            var kept = await Seed("Keep me");
            var deleted = await Seed("Delete me");
            await Delete(deleted.Id);

            using var context = NewContext();
            var tickets = await new TicketService(context).GetAllTickets();

            Assert.Equal(kept.Id, Assert.Single(tickets).Id);
        }
    }
}
