using Microsoft.EntityFrameworkCore;
using TicketSystem.Data;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Tests
{
    // Who may see and do what (TicketPermissions), through TicketService: a ticket the user may
    // not see is null (404), a step they may not take throws NotAllowedException (403), and
    // nothing is saved when they're refused.
    public class TicketPermissionTests
    {
        private static readonly Guid Reporter = SeedUsers.JohnDoeId;
        private static readonly Guid Technician = SeedUsers.AdrianRodriguezId;

        private readonly DbContextOptions<AppDbContext> _options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private readonly Guid _admin;
        private readonly Guid _otherUser;
        private readonly Guid _otherTechnician;

        public TicketPermissionTests()
        {
            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
            _admin = TestSupport.AddUser(_options, UserRole.Admin).GetAwaiter().GetResult();
            _otherUser = TestSupport.AddUser(_options, UserRole.User, "Pat Lee").GetAwaiter().GetResult();
            _otherTechnician = TestSupport.AddUser(_options, UserRole.Technician, "Tech Two").GetAwaiter().GetResult();
        }

        private async Task<T> Service<T>(Func<TicketService, Task<T>> act)
        {
            using var context = new AppDbContext(_options);
            return await act(new TicketService(context));
        }

        private Task<Ticket> NewTicket(Guid reporter) =>
            Service(s => s.CreateTicket("VPN drops", "Every 10 minutes", TicketPriority.Medium, reporter));

        private async Task<Ticket> Stored(Guid id)
        {
            using var context = new AppDbContext(_options);
            return await context.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        }

        [Fact]
        public async Task APlainUserSeesOnlyTheTicketsTheyReported()
        {
            var mine = await NewTicket(Reporter);
            var theirs = await NewTicket(_otherUser);

            Assert.Equal(mine.Id, Assert.Single(await Service(s => s.GetAllTickets(Reporter))).Id);
            Assert.Null(await Service(s => s.GetTicketById(theirs.Id, Reporter)));
            Assert.Null(await Service(s => s.GetActivity(theirs.Id, Reporter)));
            Assert.Null(await Service(s => s.AddComment(theirs.Id, Reporter, "Me too")));

            Assert.Equal(2, (await Service(s => s.GetAllTickets(Technician))).Count);
            Assert.Equal(2, (await Service(s => s.GetAllTickets(_admin))).Count);
        }

        [Fact]
        public async Task APlainUserCantAssignWorkOrDelete()
        {
            var ticket = await NewTicket(Reporter);

            await Assert.ThrowsAsync<NotAllowedException>(() => Service(s => s.AssignTicket(ticket.Id, Technician, Reporter)));
            await Assert.ThrowsAsync<NotAllowedException>(() => Service(s => s.StartWork(ticket.Id, Reporter)));
            await Assert.ThrowsAsync<NotAllowedException>(() => Service(s => s.ResolveTicket(ticket.Id, "Fixed it myself", Reporter)));
            await Assert.ThrowsAsync<NotAllowedException>(() => Service(s => s.SoftDeleteTicket(ticket.Id, Reporter)));

            var stored = await Stored(ticket.Id);
            Assert.Null(stored.AssignedToUserId);
            Assert.Equal(TicketStatus.Open, stored.Status);
            Assert.Null(stored.DeletedAt);
        }

        [Fact]
        public async Task OnlyTheAssignedTechnicianOrAnAdminWorksTheTicket()
        {
            var ticket = await NewTicket(Reporter);
            await Service(s => s.AssignTicket(ticket.Id, Technician, _otherTechnician));  // any staff can assign

            var refused = await Assert.ThrowsAsync<NotAllowedException>(() => Service(s => s.StartWork(ticket.Id, _otherTechnician)));
            Assert.Equal("Only the assigned technician or an admin can start work.", refused.Message);
            await Assert.ThrowsAsync<NotAllowedException>(() => Service(s => s.ResolveTicket(ticket.Id, "Done", _otherTechnician)));
            await Assert.ThrowsAsync<NotAllowedException>(() => Service(s => s.SoftDeleteTicket(ticket.Id, Technician)));

            Assert.Equal(TicketStatus.InProgress, (await Service(s => s.StartWork(ticket.Id, Technician)))!.Status);
            Assert.Equal(TicketStatus.Resolved, (await Service(s => s.ResolveTicket(ticket.Id, "New cert", _admin)))!.Status);
        }

        [Fact]
        public async Task TheReporterCanCloseTheirResolvedTicketButNotSomeoneElses()
        {
            var ticket = await NewTicket(Reporter);
            await Service(s => s.AssignTicket(ticket.Id, Technician, Technician));
            await Service(s => s.ResolveTicket(ticket.Id, "Updated the driver", Technician));

            await Assert.ThrowsAsync<NotAllowedException>(() => Service(s => s.CloseTicket(ticket.Id, _otherTechnician)));
            Assert.Null(await Service(s => s.CloseTicket(ticket.Id, _otherUser)));  // can't even see it
            Assert.Equal(TicketStatus.Closed, (await Service(s => s.CloseTicket(ticket.Id, Reporter)))!.Status);
        }

        [Fact]
        public async Task AnAdminCanBeAssignedAndDeletes()
        {
            var ticket = await NewTicket(Reporter);

            Assert.Equal(_admin, (await Service(s => s.AssignTicket(ticket.Id, _admin, _admin)))!.AssignedToUserId);
            Assert.NotNull(await Service(s => s.SoftDeleteTicket(ticket.Id, _admin)));

            using var check = new AppDbContext(_options);
            var history = await check.TicketActivities.Where(a => a.TicketId == ticket.Id).OrderBy(a => a.At).ToListAsync();
            Assert.Equal([Reporter, _admin, _admin], history.Select(a => a.ActorUserId!.Value));
        }

        // The rules on their own, one row per role and relation to the ticket.
        [Theory]
        //           role,                reporter, assigned, view,  assign, work,  close, delete
        [InlineData(UserRole.User,       true,     false,    true,  false,  false, true,  false)]
        [InlineData(UserRole.User,       false,    false,    false, false,  false, false, false)]
        [InlineData(UserRole.Technician, false,    true,     true,  true,   true,  true,  false)]
        [InlineData(UserRole.Technician, false,    false,    true,  true,   false, false, false)]
        [InlineData(UserRole.Admin,      false,    false,    true,  true,   true,  true,  true)]
        public void PermissionTable(UserRole role, bool reporter, bool assigned,
            bool view, bool assign, bool work, bool close, bool delete)
        {
            var user = User.Create("Someone", "someone@example.com", role);
            var ticket = Ticket.Create("Printer", "", TicketPriority.Low, reporter ? user.Id : Guid.NewGuid());
            if (assigned)
                ticket.AssignTo(user, user.Id);

            Assert.Equal(view, TicketPermissions.CanView(user, ticket));
            Assert.Equal(view, TicketPermissions.CanComment(user, ticket));
            Assert.Equal(assign, TicketPermissions.CanAssign(user, ticket));
            Assert.Equal(work, TicketPermissions.CanWork(user, ticket));
            Assert.Equal(close, TicketPermissions.CanClose(user, ticket));
            Assert.Equal(delete, TicketPermissions.CanDelete(user, ticket));
        }
    }
}
