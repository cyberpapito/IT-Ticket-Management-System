using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Controllers;
using TicketSystem.Data;
using TicketSystem.DTOs;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Tests
{
    // The Admin-only endpoints are also Admin-only in the pipeline ([Authorize(Roles)]); that part
    // is covered by SignInPipelineTests. Here the controller is called directly.
    public class UsersControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private readonly Guid _admin;

        // Loads the users the AddUsers migration seeds (Adrian Rodriguez, John Doe, Unknown), plus an admin.
        public UsersControllerTests()
        {
            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
            _admin = TestSupport.AddUser(_options, UserRole.Admin).GetAwaiter().GetResult();
        }

        private async Task<T> As<T>(Guid userId, Func<UsersController, Task<T>> act)
        {
            using var context = new AppDbContext(_options);
            return await act(new UsersController(TestSupport.UserService(context)).SignedInAs(userId));
        }

        private static List<User> Users(ActionResult<List<User>> result) =>
            Assert.IsType<List<User>>(Assert.IsType<OkObjectResult>(result.Result).Value);

        private async Task<User> Stored(Guid id)
        {
            using var context = new AppDbContext(_options);
            return await context.Users.SingleAsync(u => u.Id == id);
        }

        [Fact]
        public async Task StaffSeeEveryoneAndCanFilterByRole()
        {
            var all = Users(await As(SeedUsers.AdrianRodriguezId, c => c.GetUsers(null)));
            Assert.Equal(["Ada Admin", "Adrian Rodriguez", "John Doe", "Unknown (pre-users)"], all.Select(u => u.Name));

            var technicians = Users(await As(_admin, c => c.GetUsers(UserRole.Technician)));
            Assert.Equal(SeedUsers.AdrianRodriguezId, Assert.Single(technicians).Id);
        }

        // Everyone who can appear on their own tickets: themselves and the staff.
        [Fact]
        public async Task APlainUserSeesThemselvesAndTheStaff()
        {
            var seen = Users(await As(SeedUsers.JohnDoeId, c => c.GetUsers(null)));
            Assert.Equal(["Ada Admin", "Adrian Rodriguez", "John Doe"], seen.Select(u => u.Name));

            Assert.IsType<OkObjectResult>((await As(SeedUsers.JohnDoeId, c => c.GetUserById(SeedUsers.AdrianRodriguezId))).Result);
            Assert.IsType<NotFoundResult>((await As(SeedUsers.JohnDoeId, c => c.GetUserById(SeedUsers.UnknownId))).Result);
        }

        [Fact]
        public async Task GetsAUserById()
        {
            var john = Assert.IsType<User>(Assert.IsType<OkObjectResult>((await As(_admin, c => c.GetUserById(SeedUsers.JohnDoeId))).Result).Value);
            Assert.Equal("John Doe", john.Name);
            Assert.IsType<NotFoundResult>((await As(_admin, c => c.GetUserById(Guid.NewGuid()))).Result);
        }

        [Fact]
        public async Task CreatesAUserWithAHashedPassword()
        {
            var result = await As(_admin, c => c.CreateUser(new CreateUserRequest
            {
                Name = "Jane Smith", Email = "Jane@Example.com", Role = UserRole.Technician, Password = TestSupport.Password
            }));

            var jane = Assert.IsType<User>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
            Assert.Equal("jane@example.com", jane.Email);
            var stored = await Stored(jane.Id);
            Assert.Equal(UserRole.Technician, stored.Role);
            Assert.NotNull(stored.PasswordHash);
            Assert.DoesNotContain(TestSupport.Password, stored.PasswordHash);
        }

        // The hash and the stamp never leave the API.
        [Fact]
        public void PasswordHashAndStampAreNotInTheJson()
        {
            var user = User.Create("Jane Smith", "jane@example.com", UserRole.User);
            user.SetPasswordHash("AQAAAAIAAYagAAAAE-not-a-real-hash");

            var json = JsonSerializer.Serialize(user, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.DoesNotContain("passwordHash", json);
            Assert.DoesNotContain("securityStamp", json);
            Assert.DoesNotContain("isStaff", json);
            Assert.Contains("\"email\":\"jane@example.com\"", json);
        }

        [Fact]
        public async Task DuplicateEmailShortPasswordOrBadInputIs400()
        {
            var duplicate = await As(_admin, c => c.CreateUser(new CreateUserRequest
            {
                Name = "Another John", Email = "JOHN.DOE@example.com", Role = UserRole.User, Password = TestSupport.Password
            }));
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<BadRequestObjectResult>(duplicate.Result).Value);
            Assert.Equal("A user with email john.doe@example.com already exists.", problem.Detail);

            var shortPassword = await As(_admin, c => c.CreateUser(new CreateUserRequest
            {
                Name = "Jane Smith", Email = "jane@example.com", Role = UserRole.User, Password = "hunter2"
            }));
            problem = Assert.IsType<ProblemDetails>(Assert.IsType<BadRequestObjectResult>(shortPassword.Result).Value);
            Assert.Equal($"A password needs at least {User.PasswordMinLength} characters.", problem.Detail);

            var blank = await As(_admin, c => c.CreateUser(new CreateUserRequest { Name = " ", Email = "x@y.com", Password = TestSupport.Password }));
            Assert.IsType<BadRequestObjectResult>(blank.Result);

            using var context = new AppDbContext(_options);
            Assert.Equal(4, await context.Users.CountAsync());
        }

        [Fact]
        public async Task ChangingARoleEndsThatUsersSessions()
        {
            var before = (await Stored(SeedUsers.JohnDoeId)).SecurityStamp;

            var result = await As(_admin, c => c.ChangeRole(SeedUsers.JohnDoeId, new ChangeRoleRequest { Role = UserRole.Technician }));

            Assert.Equal(UserRole.Technician, Assert.IsType<User>(Assert.IsType<OkObjectResult>(result.Result).Value).Role);
            Assert.NotEqual(before, (await Stored(SeedUsers.JohnDoeId)).SecurityStamp);
            Assert.IsType<NotFoundResult>((await As(_admin, c => c.ChangeRole(Guid.NewGuid(), new ChangeRoleRequest { Role = UserRole.User }))).Result);
        }

        [Fact]
        public async Task TheLastAdminWhoCanSignInCantBeDemoted()
        {
            var refused = await As(_admin, c => c.ChangeRole(_admin, new ChangeRoleRequest { Role = UserRole.User }));
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ConflictObjectResult>(refused.Result).Value);
            Assert.StartsWith("This is the only admin who can sign in", problem.Detail);
            Assert.Equal(UserRole.Admin, (await Stored(_admin)).Role);

            // With a second admin, the first can step down.
            var second = await TestSupport.AddUser(_options, UserRole.Admin, "Second Admin");
            Assert.IsType<OkObjectResult>((await As(second, c => c.ChangeRole(_admin, new ChangeRoleRequest { Role = UserRole.User }))).Result);
        }

        [Fact]
        public async Task APasswordResetLetsASeededUserSignInAndEndsOldSessions()
        {
            using (var context = new AppDbContext(_options))
                Assert.Null(await TestSupport.UserService(context).Authenticate("john.doe@example.com", TestSupport.Password));
            var before = (await Stored(SeedUsers.JohnDoeId)).SecurityStamp;

            Assert.IsType<NoContentResult>(await As(_admin, c => c.SetPassword(SeedUsers.JohnDoeId, new SetPasswordRequest { Password = TestSupport.Password })));

            using (var context = new AppDbContext(_options))
                Assert.Equal(SeedUsers.JohnDoeId, (await TestSupport.UserService(context).Authenticate(" John.Doe@example.com ", TestSupport.Password))?.Id);
            Assert.NotEqual(before, (await Stored(SeedUsers.JohnDoeId)).SecurityStamp);
            Assert.IsType<BadRequestObjectResult>(await As(_admin, c => c.SetPassword(SeedUsers.JohnDoeId, new SetPasswordRequest { Password = "short" })));
        }

        [Fact]
        public async Task AuthenticateRefusesAWrongPasswordAndAnUnknownEmail()
        {
            using var context = new AppDbContext(_options);
            var service = TestSupport.UserService(context);
            Assert.NotNull(await service.Authenticate("ada.admin@example.com", TestSupport.Password));
            Assert.Null(await service.Authenticate("ada.admin@example.com", TestSupport.Password + "!"));
            Assert.Null(await service.Authenticate("nobody@example.com", TestSupport.Password));
            Assert.Null(await service.Authenticate("ada.admin@example.com", ""));
        }

        [Fact]
        public async Task BootstrapAdminPromotesTheConfiguredUserOnceThenDoesNothing()
        {
            var fresh = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            using (var context = new AppDbContext(fresh))
                context.Database.EnsureCreated();

            using (var context = new AppDbContext(fresh))
                Assert.Equal("Made adrian.rodriguez@example.com an admin and set its password.",
                    await TestSupport.UserService(context).EnsureBootstrapAdmin("Adrian.Rodriguez@example.com", "ignored", TestSupport.Password));

            using (var context = new AppDbContext(fresh))
            {
                var service = TestSupport.UserService(context);
                Assert.Equal(UserRole.Admin, (await service.Authenticate("adrian.rodriguez@example.com", TestSupport.Password))?.Role);
                Assert.Null(await service.EnsureBootstrapAdmin("someone.else@example.com", "Someone", TestSupport.Password + "x"));
                Assert.False(await context.Users.AnyAsync(u => u.Email == "someone.else@example.com"));
            }
        }
    }
}
