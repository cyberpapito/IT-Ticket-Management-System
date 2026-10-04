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

        // Loads the users the AddUsers migration seeds (Adrian Rodriguez, John Doe, Unknown).
        public TicketsControllerTests()
        {
            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        private static CreateTicketRequest Request(string title) => new()
        {
            Title = title,
            Description = "desc",
            Priority = TicketPriority.Low,
            CreatedByUserId = SeedUsers.JohnDoeId
        };

        [Fact]
        public async Task CreateTicket_UnknownCreatorReturns400()
        {
            using var context = new AppDbContext(_options);
            var controller = new TicketsController(new TicketService(context));
            var request = Request("Monitor flickering");
            request.CreatedByUserId = Guid.NewGuid();

            var result = await controller.CreateTicket(request);

            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<BadRequestObjectResult>(result.Result).Value);
            Assert.StartsWith("No user with id", problem.Detail);
        }

        [Fact]
        public async Task CreateTicket_ValidRequestReturns201()
        {
            using var context = new AppDbContext(_options);
            var controller = new TicketsController(new TicketService(context));

            var result = await controller.CreateTicket(Request("Monitor flickering"));

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(TicketsController.GetTicketById), created.ActionName);
            Assert.Equal("Monitor flickering", Assert.IsType<Ticket>(created.Value).Title);
        }

        [Fact]
        public async Task CreateTicket_OverlongTitleReturns400AndSavesNothing()
        {
            using (var context = new AppDbContext(_options))
            {
                var controller = new TicketsController(new TicketService(context));

                var result = await controller.CreateTicket(Request(new string('x', 300)));

                var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
                var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
                Assert.Equal(400, problem.Status);
                Assert.Equal($"Title cannot be longer than {Ticket.TitleMaxLength} characters", problem.Detail);
            }

            using var check = new AppDbContext(_options);
            Assert.Empty(await check.Tickets.IgnoreQueryFilters().ToListAsync());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateTicket_BlankTitleReturns400AndSavesNothing(string title)
        {
            using (var context = new AppDbContext(_options))
            {
                var controller = new TicketsController(new TicketService(context));

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
