using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Controllers;
using TicketSystem.Data;
using TicketSystem.DTOs;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Tests
{
    // A ticket's history: the entries the entity records, what the service saves, and the
    // activity and comment endpoints. Saved state is read back through a fresh context.
    public class TicketActivityTests
    {
        // Who takes the steps in these tests (the entity only records the id).
        private static readonly Guid Actor = Guid.NewGuid();

        private static readonly User Technician = User.Create("Tech One", "tech1@example.com", UserRole.Technician);
        private static readonly User Requester = User.Create("Pat Lee", "pat.lee@example.com", UserRole.User);

        private readonly DbContextOptions<AppDbContext> _options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private readonly Guid _admin;

        // Loads the users the AddUsers migration seeds (Adrian Rodriguez, John Doe, Unknown), plus
        // an admin for deletes.
        public TicketActivityTests()
        {
            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
            _admin = TestSupport.AddUser(_options, UserRole.Admin).GetAwaiter().GetResult();
        }

        private AppDbContext NewContext() => new(_options);

        private async Task<Guid> NewTicket()
        {
            using var context = NewContext();
            var ticket = await new TicketService(context)
                .CreateTicket("Laptop won't boot", "Black screen", TicketPriority.High, SeedUsers.JohnDoeId);
            return ticket.Id;
        }

        private static Ticket NewEntity() =>
            Ticket.Create("Printer offline", "3rd floor", TicketPriority.Medium, Requester.Id);

        // Entity

        [Fact]
        public void Create_RecordsCreatedByTheReporter()
        {
            var ticket = NewEntity();

            var created = Assert.Single(ticket.Activities);
            Assert.Equal(TicketActivityType.Created, created.Type);
            Assert.Equal(ticket.Id, created.TicketId);
            Assert.Equal(Requester.Id, created.UserId);
            Assert.Null(created.FromStatus);
            Assert.Equal(TicketStatus.Open, created.ToStatus);
            Assert.Equal(ticket.CreatedAt, created.At, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void FullLifecycle_RecordsEachStepInOrder()
        {
            var ticket = NewEntity();
            ticket.AssignTo(Technician, Actor);
            ticket.StartWork(Actor);
            ticket.MarkResolved("  Replaced the toner  ", Actor);
            ticket.Close(Actor);
            ticket.MarkDeleted(Actor);

            Assert.Equal(
                [TicketActivityType.Created, TicketActivityType.Assigned, TicketActivityType.Started,
                 TicketActivityType.Resolved, TicketActivityType.Closed, TicketActivityType.Deleted],
                ticket.Activities.Select(a => a.Type));

            var assigned = ticket.Activities[1];
            Assert.Equal(Technician.Id, assigned.UserId);
            Assert.Null(assigned.ToStatus);

            var started = ticket.Activities[2];
            Assert.Equal((TicketStatus.Open, TicketStatus.InProgress), (started.FromStatus!.Value, started.ToStatus!.Value));

            var resolved = ticket.Activities[3];
            Assert.Equal("Replaced the toner", resolved.Note);
            Assert.Equal((TicketStatus.InProgress, TicketStatus.Resolved), (resolved.FromStatus!.Value, resolved.ToStatus!.Value));

            var closed = ticket.Activities[4];
            Assert.Equal((TicketStatus.Resolved, TicketStatus.Closed), (closed.FromStatus!.Value, closed.ToStatus!.Value));
        }

        [Fact]
        public void ResolveWithoutStarting_RecordsOpenAsTheFromStatus()
        {
            var ticket = NewEntity();
            ticket.AssignTo(Technician, Actor);
            ticket.MarkResolved("Fixed over the phone", Actor);

            Assert.Equal(TicketStatus.Open, ticket.Activities[^1].FromStatus);
        }

        [Fact]
        public void RejectedStep_RecordsNothing()
        {
            var ticket = NewEntity();

            Assert.Throws<InvalidOperationException>(() => ticket.Close(Actor));
            Assert.Throws<InvalidOperationException>(() => ticket.StartWork(Actor));
            Assert.Throws<ArgumentException>(() => ticket.AssignTo(Requester, Actor));

            Assert.Single(ticket.Activities);
        }

        [Fact]
        public void AddComment_RecordsTheAuthorAndTrimmedText()
        {
            var ticket = NewEntity();

            var comment = ticket.AddComment(Technician, "  On my way with a spare cable  ");

            Assert.Same(comment, ticket.Activities[^1]);
            Assert.Equal(TicketActivityType.Comment, comment.Type);
            Assert.Equal(Technician.Id, comment.UserId);
            Assert.Equal("On my way with a spare cable", comment.Note);
            Assert.Null(comment.FromStatus);
            Assert.Null(comment.ToStatus);
            Assert.Equal(TicketStatus.Open, ticket.Status);
        }

        [Fact]
        public void AddComment_AllowedOnAClosedTicket()
        {
            var ticket = NewEntity();
            ticket.AssignTo(Technician, Actor);
            ticket.MarkResolved("Rebooted", Actor);
            ticket.Close(Actor);

            ticket.AddComment(Requester, "Broke again this morning");

            Assert.Equal(TicketActivityType.Comment, ticket.Activities[^1].Type);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void AddComment_EmptyTextIsRejected(string text)
        {
            var ticket = NewEntity();

            var ex = Assert.Throws<ArgumentException>(() => ticket.AddComment(Requester, text));
            Assert.Equal("A comment cannot be empty.", ex.Message);
            Assert.Single(ticket.Activities);
        }

        [Fact]
        public void AddComment_TooLongIsRejected()
        {
            var ticket = NewEntity();

            ticket.AddComment(Requester, new string('x', TicketActivity.NoteMaxLength));
            Assert.Throws<ArgumentException>(() =>
                ticket.AddComment(Requester, new string('x', TicketActivity.NoteMaxLength + 1)));
        }

        [Fact]
        public void AddComment_DeletedTicketIsRejected()
        {
            var ticket = NewEntity();
            ticket.MarkDeleted(Actor);

            Assert.Throws<InvalidOperationException>(() => ticket.AddComment(Requester, "Hello?"));
        }

        // Service

        [Fact]
        public async Task Workflow_SavesTheHistoryOfAnAlreadySavedTicket()
        {
            var id = await NewTicket();

            using (var context = NewContext())
            {
                var service = new TicketService(context);
                await service.AssignTicket(id, SeedUsers.AdrianRodriguezId, _admin);
                await service.StartWork(id, SeedUsers.AdrianRodriguezId);
                await service.AddComment(id, SeedUsers.JohnDoeId, "Thanks!");
                await service.ResolveTicket(id, "Reseated the RAM", SeedUsers.AdrianRodriguezId);
            }

            using (var context = NewContext())
            {
                var activity = await new TicketService(context).GetActivity(id, SeedUsers.JohnDoeId);

                Assert.NotNull(activity);
                Assert.Equal(
                    [TicketActivityType.Created, TicketActivityType.Assigned, TicketActivityType.Started,
                     TicketActivityType.Comment, TicketActivityType.Resolved],
                    activity.Select(a => a.Type));
                Assert.Equal(SeedUsers.JohnDoeId, activity[0].UserId);
                Assert.Equal(SeedUsers.AdrianRodriguezId, activity[1].UserId);
                Assert.Equal("Thanks!", activity[3].Note);
                // Who did each step: the reporter, the admin who assigned, the technician, the
                // commenter, the technician again.
                Assert.Equal(
                    [SeedUsers.JohnDoeId, _admin, SeedUsers.AdrianRodriguezId, SeedUsers.JohnDoeId, SeedUsers.AdrianRodriguezId],
                    activity.Select(a => a.ActorUserId!.Value));
            }
        }

        [Fact]
        public async Task GetActivity_UnknownOrDeletedTicketIsNull()
        {
            var id = await NewTicket();

            using (var context = NewContext())
            {
                var service = new TicketService(context);
                Assert.Null(await service.GetActivity(Guid.NewGuid(), _admin));

                await service.SoftDeleteTicket(id, _admin);
            }

            using (var context = NewContext())
            {
                Assert.Null(await new TicketService(context).GetActivity(id, _admin));

                // The Deleted entry is still saved, for the audit history.
                var stored = await context.TicketActivities.Where(a => a.TicketId == id).ToListAsync();
                Assert.Contains(stored, a => a.Type == TicketActivityType.Deleted);
            }
        }

        [Fact]
        public async Task AddComment_UnknownAuthorIsRefusedAndSavesNothing()
        {
            var id = await NewTicket();

            using (var context = NewContext())
            {
                var ex = await Assert.ThrowsAsync<NotAllowedException>(() =>
                    new TicketService(context).AddComment(id, Guid.NewGuid(), "Hello"));
                Assert.StartsWith("Your account no longer exists", ex.Message);
            }

            using (var context = NewContext())
            {
                Assert.Equal(1, await context.TicketActivities.CountAsync(a => a.TicketId == id));
            }
        }

        // Endpoints

        [Fact]
        public async Task CommentEndpoint_Returns201AndTheTimelineShowsIt()
        {
            var id = await NewTicket();

            using (var context = NewContext())
            {
                var result = await new TicketsController(new TicketService(context)).SignedInAs(SeedUsers.JohnDoeId)
                    .AddComment(id, new AddCommentRequest { Text = "Any update?" });

                var created = Assert.IsType<CreatedAtActionResult>(result.Result);
                var comment = Assert.IsType<TicketActivity>(created.Value);
                Assert.Equal("Any update?", comment.Note);
                Assert.Equal(SeedUsers.JohnDoeId, comment.UserId);
            }

            using (var context = NewContext())
            {
                var result = await new TicketsController(new TicketService(context)).SignedInAs(SeedUsers.JohnDoeId).GetActivity(id);

                var list = Assert.IsType<List<TicketActivity>>(Assert.IsType<OkObjectResult>(result.Result).Value);
                Assert.Equal([TicketActivityType.Created, TicketActivityType.Comment], list.Select(a => a.Type));
            }
        }

        [Fact]
        public async Task CommentEndpoint_BadInputIs400AndUnknownTicketIs404()
        {
            var id = await NewTicket();

            using var context = NewContext();
            var controller = new TicketsController(new TicketService(context)).SignedInAs(SeedUsers.JohnDoeId);

            var empty = await controller.AddComment(id, new AddCommentRequest { Text = " " });
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<BadRequestObjectResult>(empty.Result).Value);
            Assert.Equal("A comment cannot be empty.", problem.Detail);

            var noTicket = await controller.AddComment(Guid.NewGuid(), new AddCommentRequest { Text = "Hi" });
            Assert.IsType<NotFoundResult>(noTicket.Result);

            var noTimeline = await controller.GetActivity(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(noTimeline.Result);
        }
    }
}
