using TicketSystem.Models;

namespace TicketSystem.Tests
{
    public class UserTests
    {
        [Fact]
        public void Create_TrimsNameAndLowercasesEmail()
        {
            var user = User.Create("  Adrian Rodriguez  ", "  Adrian.Rodriguez@Example.COM ", UserRole.Technician);

            Assert.NotEqual(Guid.Empty, user.Id);
            Assert.Equal("Adrian Rodriguez", user.Name);
            Assert.Equal("adrian.rodriguez@example.com", user.Email);
            Assert.Equal(UserRole.Technician, user.Role);
        }

        [Theory]
        [InlineData("", "a@b.com")]
        [InlineData("   ", "a@b.com")]
        [InlineData("Name", "")]
        [InlineData("Name", "no-at-sign")]
        [InlineData("Name", "@nouser.com")]
        [InlineData("Name", "nodomain@")]
        [InlineData("Name", "two@@ats.com")]
        public void Create_RejectsBadNameOrEmail(string name, string email)
        {
            Assert.Throws<ArgumentException>(() => User.Create(name, email, UserRole.User));
        }

        [Fact]
        public void Create_EnforcesLengths()
        {
            Assert.Equal(User.NameMaxLength, User.Create(new string('n', User.NameMaxLength), "a@b.com", UserRole.User).Name.Length);
            Assert.Throws<ArgumentException>(() => User.Create(new string('n', User.NameMaxLength + 1), "a@b.com", UserRole.User));
            var longEmail = new string('e', User.EmailMaxLength - "@b.com".Length + 1) + "@b.com";
            Assert.Throws<ArgumentException>(() => User.Create("Name", longEmail, UserRole.User));
        }

        [Fact]
        public void Create_RejectsUnknownRole()
        {
            Assert.Throws<ArgumentException>(() => User.Create("Name", "a@b.com", (UserRole)7));
        }
    }
}
