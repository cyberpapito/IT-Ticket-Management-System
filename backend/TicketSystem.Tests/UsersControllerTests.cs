using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Controllers;
using TicketSystem.Data;
using TicketSystem.DTOs;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Tests
{
    public class UsersControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        // Loads the users the AddUsers migration seeds (Adrian Rodriguez, John Doe, Unknown).
        public UsersControllerTests()
        {
            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        private async Task<T> WithController<T>(Func<UsersController, Task<T>> act)
        {
            using var context = new AppDbContext(_options);
            return await act(new UsersController(new UserService(context)));
        }

        [Fact]
        public async Task ListsUsersAndFiltersByRole()
        {
            var all = Assert.IsType<List<User>>(Assert.IsType<OkObjectResult>((await WithController(c => c.GetUsers(null))).Result).Value);
            Assert.Equal(["Adrian Rodriguez", "John Doe", "Unknown (pre-users)"], all.Select(u => u.Name));

            var technicians = Assert.IsType<List<User>>(
                Assert.IsType<OkObjectResult>((await WithController(c => c.GetUsers(UserRole.Technician))).Result).Value);
            Assert.Equal(SeedUsers.AdrianRodriguezId, Assert.Single(technicians).Id);
        }

        [Fact]
        public async Task GetsAUserById()
        {
            var john = Assert.IsType<User>(Assert.IsType<OkObjectResult>((await WithController(c => c.GetUserById(SeedUsers.JohnDoeId))).Result).Value);
            Assert.Equal("John Doe", john.Name);
            Assert.IsType<NotFoundResult>((await WithController(c => c.GetUserById(Guid.NewGuid()))).Result);
        }

        [Fact]
        public async Task CreatesAUser()
        {
            var result = await WithController(c => c.CreateUser(new CreateUserRequest { Name = "Jane Smith", Email = "Jane@Example.com", Role = UserRole.Technician }));

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var jane = Assert.IsType<User>(created.Value);
            Assert.Equal("jane@example.com", jane.Email);
            using var context = new AppDbContext(_options);
            Assert.Equal(UserRole.Technician, (await context.Users.SingleAsync(u => u.Id == jane.Id)).Role);
        }

        [Fact]
        public async Task DuplicateEmailOrBadInputIs400()
        {
            var duplicate = await WithController(c => c.CreateUser(new CreateUserRequest { Name = "Another John", Email = "JOHN.DOE@example.com", Role = UserRole.User }));
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<BadRequestObjectResult>(duplicate.Result).Value);
            Assert.Equal("A user with email john.doe@example.com already exists.", problem.Detail);

            var blank = await WithController(c => c.CreateUser(new CreateUserRequest { Name = " ", Email = "x@y.com" }));
            Assert.IsType<BadRequestObjectResult>(blank.Result);

            using var context = new AppDbContext(_options);
            Assert.Equal(3, await context.Users.CountAsync());
        }
    }
}
