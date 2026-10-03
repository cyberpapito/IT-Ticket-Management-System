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

        private AppDbContext NewContext() => new(_options);

        private async Task<Ticket> Seed(string title = "Laptop won't boot")
        {
            using var context = NewContext();
            return await new TicketService(context)
                .CreateTicket(title, "Black screen after update", TicketPriority.High, Guid.NewGuid());
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

        [Fact]
        public async Task SoftDeleteTicket_StampsDeletedAtAndKeepsRow()
        {
            var created = await Seed();

            using (var context = NewContext())
            {
                var deleted = await new TicketService(context).SoftDeleteTicket(created.Id);
                Assert.NotNull(deleted);
            }

            using var check = NewContext();
            var stored = await check.Tickets.SingleAsync();
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
        public async Task SoftDeleteTicket_TwiceThrowsAndKeepsOriginalTimestamp()
        {
            var created = await Seed();
            DateTimeOffset? original;
            using (var context = NewContext())
            {
                original = (await new TicketService(context).SoftDeleteTicket(created.Id))!.DeletedAt;
            }

            using (var context = NewContext())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    new TicketService(context).SoftDeleteTicket(created.Id));
            }

            using var check = NewContext();
            Assert.Equal(original, (await check.Tickets.SingleAsync()).DeletedAt);
        }
    }
}
