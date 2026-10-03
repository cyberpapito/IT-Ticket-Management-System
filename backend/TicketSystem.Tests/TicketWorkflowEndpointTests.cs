using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Controllers;
using TicketSystem.Data;
using TicketSystem.DTOs;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Tests
{
    // The assign / start / resolve / close endpoints, called on the controller with the in-memory
    // provider. Each test reads back through a fresh context to check what was actually saved.
    public class TicketWorkflowEndpointTests
    {
        private static readonly Guid Technician = Guid.NewGuid();

        private readonly DbContextOptions<AppDbContext> _options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private async Task<T> WithController<T>(Func<TicketsController, Task<T>> act)
        {
            using var context = new AppDbContext(_options);
            return await act(new TicketsController(new TicketService(context)));
        }

        private async Task<Guid> NewTicket()
        {
            using var context = new AppDbContext(_options);
            var ticket = await new TicketService(context)
                .CreateTicket("Laptop won't boot", "Black screen", TicketPriority.High, Guid.NewGuid());
            return ticket.Id;
        }

        private async Task<Ticket> Stored(Guid id)
        {
            using var context = new AppDbContext(_options);
            return await context.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        }

        private static Ticket OkTicket(ActionResult<Ticket> result) =>
            Assert.IsType<Ticket>(Assert.IsType<OkObjectResult>(result.Result).Value);

        private static ProblemDetails Problem<TResult>(ActionResult<Ticket> result, int status) where TResult : ObjectResult
        {
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<TResult>(result.Result).Value);
            Assert.Equal(status, problem.Status);
            return problem;
        }

        [Fact]
        public async Task FullLifecycleThroughTheEndpoints()
        {
            var id = await NewTicket();

            var assigned = OkTicket(await WithController(c => c.AssignTicket(id, new AssignTicketRequest { TechnicianId = Technician })));
            Assert.Equal(Technician, assigned.AssignedToUserId);

            Assert.Equal(TicketStatus.InProgress, OkTicket(await WithController(c => c.StartWork(id))).Status);

            var resolved = OkTicket(await WithController(c => c.ResolveTicket(id, new ResolveTicketRequest { ResolutionSummary = "  Reseated RAM  " })));
            Assert.Equal(TicketStatus.Resolved, resolved.Status);
            Assert.Equal("Reseated RAM", resolved.ResolutionSummary);

            Assert.Equal(TicketStatus.Closed, OkTicket(await WithController(c => c.CloseTicket(id))).Status);

            var stored = await Stored(id);
            Assert.Equal(TicketStatus.Closed, stored.Status);
            Assert.Equal(Technician, stored.AssignedToUserId);
            Assert.NotNull(stored.ResolvedAt);
        }

        [Fact]
        public async Task UnknownOrDeletedTicketIs404()
        {
            var missing = Guid.NewGuid();
            Assert.IsType<NotFoundResult>((await WithController(c => c.StartWork(missing))).Result);
            Assert.IsType<NotFoundResult>((await WithController(c => c.CloseTicket(missing))).Result);

            var id = await NewTicket();
            await WithController(c => c.DeleteTicket(id));
            Assert.IsType<NotFoundResult>(
                (await WithController(c => c.AssignTicket(id, new AssignTicketRequest { TechnicianId = Technician }))).Result);
        }

        [Fact]
        public async Task StepNotAllowedInCurrentStateIs409AndSavesNothing()
        {
            var id = await NewTicket();

            var problem = Problem<ConflictObjectResult>(await WithController(c => c.StartWork(id)), 409);
            Assert.Contains("unassigned", problem.Detail);
            Problem<ConflictObjectResult>(await WithController(c => c.CloseTicket(id)), 409);
            Problem<ConflictObjectResult>(
                await WithController(c => c.ResolveTicket(id, new ResolveTicketRequest { ResolutionSummary = "Done" })), 409);

            var stored = await Stored(id);
            Assert.Equal(TicketStatus.Open, stored.Status);
            Assert.Null(stored.ResolutionSummary);
        }

        [Fact]
        public async Task ResolvingTwiceIs409AndKeepsTheFirstSummary()
        {
            var id = await NewTicket();
            await WithController(c => c.AssignTicket(id, new AssignTicketRequest { TechnicianId = Technician }));
            await WithController(c => c.ResolveTicket(id, new ResolveTicketRequest { ResolutionSummary = "First fix" }));

            Problem<ConflictObjectResult>(
                await WithController(c => c.ResolveTicket(id, new ResolveTicketRequest { ResolutionSummary = "Second story" })), 409);
            Assert.Equal("First fix", (await Stored(id)).ResolutionSummary);
        }

        [Fact]
        public async Task ReassigningAfterWorkStartedIs409()
        {
            var id = await NewTicket();
            await WithController(c => c.AssignTicket(id, new AssignTicketRequest { TechnicianId = Technician }));
            await WithController(c => c.StartWork(id));

            Problem<ConflictObjectResult>(
                await WithController(c => c.AssignTicket(id, new AssignTicketRequest { TechnicianId = Guid.NewGuid() })), 409);
            Assert.Equal(Technician, (await Stored(id)).AssignedToUserId);
        }

        [Fact]
        public async Task BadInputIs400AndSavesNothing()
        {
            var id = await NewTicket();

            var problem = Problem<BadRequestObjectResult>(
                await WithController(c => c.AssignTicket(id, new AssignTicketRequest { TechnicianId = Guid.Empty })), 400);
            Assert.Equal("A technician id is required.", problem.Detail);
            Assert.Null((await Stored(id)).AssignedToUserId);

            await WithController(c => c.AssignTicket(id, new AssignTicketRequest { TechnicianId = Technician }));
            Problem<BadRequestObjectResult>(
                await WithController(c => c.ResolveTicket(id, new ResolveTicketRequest { ResolutionSummary = "   " })), 400);
            Assert.Equal(TicketStatus.Open, (await Stored(id)).Status);
        }
    }
}
