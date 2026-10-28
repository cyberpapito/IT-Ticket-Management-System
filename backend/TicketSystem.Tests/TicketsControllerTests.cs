using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Controllers;
using TicketSystem.Data;
using TicketSystem.DTOs;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Tests
{
    public class TicketsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private readonly Guid _admin;

        // Loads the users the AddUsers migration seeds (Adrian Rodriguez, John Doe, Unknown), plus
        // an admin for deletes.
        public TicketsControllerTests()
        {
            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
            _admin = TestSupport.AddUser(_options, UserRole.Admin).GetAwaiter().GetResult();
        }

        private static CreateTicketRequest Request(string title) => new()
        {
            Title = title,
            Description = "desc",
            Priority = TicketPriority.Low
        };

        private static TicketsController Controller(AppDbContext context, Guid userId) =>
            new TicketsController(new TicketService(context)).SignedInAs(userId);

        // The creator is whoever is signed in; the request has no say.
        [Fact]
        public async Task CreateTicket_ValidRequestReturns201AsTheSignedInUser()
        {
            using var context = new AppDbContext(_options);
            var controller = Controller(context, SeedUsers.JohnDoeId);

            var result = await controller.CreateTicket(Request("Monitor flickering"));

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(TicketsController.GetTicketById), created.ActionName);
            var ticket = Assert.IsType<Ticket>(created.Value);
            Assert.Equal("Monitor flickering", ticket.Title);
            Assert.Equal(SeedUsers.JohnDoeId, ticket.CreatedByUserId);
        }

        [Fact]
        public async Task CreateTicket_OverlongTitleReturns400AndSavesNothing()
        {
            using (var context = new AppDbContext(_options))
            {
                var controller = Controller(context, SeedUsers.JohnDoeId);

                var result = await controller.CreateTicket(Request(new string('x', 300)));

                var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
                var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
                Assert.Equal(400, problem.Status);
                Assert.Equal($"Title cannot be longer than {Ticket.TitleMaxLength} characters", problem.Detail);
            }

            using var check = new AppDbContext(_options);
            Assert.Empty(await check.Tickets.IgnoreQueryFilters().ToListAsync());
        }

        [Fact]
        public async Task GetDeletedTickets_Returns200WithDeletedTicketsOnly()
        {
            using var context = new AppDbContext(_options);
            var controller = Controller(context, _admin);
            var kept = Assert.IsType<Ticket>(Assert.IsType<CreatedAtActionResult>(
                (await controller.CreateTicket(Request("Keep me"))).Result).Value);
            var gone = Assert.IsType<Ticket>(Assert.IsType<CreatedAtActionResult>(
                (await controller.CreateTicket(Request("Delete me"))).Result).Value);
            Assert.IsType<NoContentResult>(await controller.DeleteTicket(gone.Id));

            var result = await controller.GetDeletedTickets();

            var deleted = Assert.IsType<List<Ticket>>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(gone.Id, Assert.Single(deleted).Id);
            Assert.DoesNotContain(deleted, t => t.Id == kept.Id);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateTicket_BlankTitleReturns400AndSavesNothing(string title)
        {
            using (var context = new AppDbContext(_options))
            {
                var controller = Controller(context, SeedUsers.JohnDoeId);

                var result = await controller.CreateTicket(Request(title));

                var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
                var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
                Assert.Equal(400, problem.Status);
                Assert.Equal("Title cannot be empty", problem.Detail);
            }

            using var check = new AppDbContext(_options);
            Assert.Empty(await check.Tickets.IgnoreQueryFilters().ToListAsync());
        }
    }
}
