using TicketSystem.Models;

namespace TicketSystem.Tests
{
    public class TicketTests
    {
        // Who takes the steps in these tests (the entity only records the id).
        private static readonly Guid Actor = Guid.NewGuid();

        private static readonly Guid Creator = Guid.NewGuid();
        private static readonly User Technician = User.Create("Tech One", "tech1@example.com", UserRole.Technician);

        private static User OtherTechnician() =>
            User.Create("Tech Two", "tech2@example.com", UserRole.Technician);

        private static Ticket NewTicket() =>
            Ticket.Create("Printer offline", "3rd floor printer shows offline", TicketPriority.Medium, Creator);

        private static Ticket ResolvedTicket()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician, Actor);
            ticket.MarkResolved("Power-cycled the printer", Actor);
            return ticket;
        }

        // Create

        [Fact]
        public void Create_StartsOpenAndUnassigned()
        {
            var before = DateTimeOffset.UtcNow;
            var ticket = NewTicket();

            Assert.NotEqual(Guid.Empty, ticket.Id);
            Assert.Equal(TicketStatus.Open, ticket.Status);
            Assert.Equal(TicketPriority.Medium, ticket.Priority);
            Assert.Equal(Creator, ticket.CreatedByUserId);
            Assert.Null(ticket.AssignedToUserId);
            Assert.Null(ticket.ResolvedAt);
            Assert.Null(ticket.ResolutionSummary);
            Assert.Null(ticket.DeletedAt);
            Assert.InRange(ticket.CreatedAt, before, DateTimeOffset.UtcNow);
        }

        [Fact]
        public void Create_GivesEachTicketItsOwnId()
        {
            Assert.NotEqual(NewTicket().Id, NewTicket().Id);
        }

        [Fact]
        public void Create_TrimsTitleAndDescription()
        {
            var ticket = Ticket.Create("  VPN down  ", "  cannot connect  ", TicketPriority.High, Creator);

            Assert.Equal("VPN down", ticket.Title);
            Assert.Equal("cannot connect", ticket.Description);
        }

        [Fact]
        public void Create_TreatsNullDescriptionAsEmpty()
        {
            var ticket = Ticket.Create("VPN down", null!, TicketPriority.High, Creator);

            Assert.Equal(string.Empty, ticket.Description);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Create_RejectsBlankTitle(string? title)
        {
            Assert.Throws<ArgumentException>(() =>
                Ticket.Create(title!, "description", TicketPriority.Low, Creator));
        }

        [Fact]
        public void Create_AcceptsTitleAtMaxLength()
        {
            var title = new string('a', Ticket.TitleMaxLength);

            Assert.Equal(title, Ticket.Create(title, "desc", TicketPriority.Low, Creator).Title);
        }

        [Fact]
        public void Create_RejectsTitleOverMaxLength()
        {
            var title = new string('a', Ticket.TitleMaxLength + 1);

            var ex = Assert.Throws<ArgumentException>(() => Ticket.Create(title, "desc", TicketPriority.Low, Creator));
            Assert.Equal($"Title cannot be longer than {Ticket.TitleMaxLength} characters", ex.Message);
        }

        [Fact]
        public void Create_MeasuresTitleAfterTrimming()
        {
            var title = "  " + new string('a', Ticket.TitleMaxLength) + "  ";

            Assert.Equal(Ticket.TitleMaxLength, Ticket.Create(title, "desc", TicketPriority.Low, Creator).Title.Length);
        }

        [Fact]
        public void Create_AcceptsDescriptionAtMaxLengthAndRejectsLonger()
        {
            var atMax = new string('d', Ticket.DescriptionMaxLength);
            Assert.Equal(atMax, Ticket.Create("Title", atMax, TicketPriority.Low, Creator).Description);

            var tooLong = atMax + "d";
            var ex = Assert.Throws<ArgumentException>(() => Ticket.Create("Title", tooLong, TicketPriority.Low, Creator));
            Assert.Equal($"Description cannot be longer than {Ticket.DescriptionMaxLength} characters", ex.Message);
        }

        // AssignTo

        [Fact]
        public void AssignTo_SetsTechnicianAndLeavesTicketOpen()
        {
            var ticket = NewTicket();

            ticket.AssignTo(Technician, Actor);

            Assert.Equal(Technician.Id, ticket.AssignedToUserId);
            Assert.Equal(TicketStatus.Open, ticket.Status);
        }

        [Fact]
        public void AssignTo_RejectsSomeoneWhoIsNotATechnician()
        {
            var ticket = NewTicket();
            var requester = User.Create("John Doe", "john.doe@example.com", UserRole.User);

            var ex = Assert.Throws<ArgumentException>(() => ticket.AssignTo(requester, Actor));
            Assert.Contains("not a technician", ex.Message);
            Assert.Null(ticket.AssignedToUserId);
        }

        [Fact]
        public void AssignTo_CanReassignAnOpenTicket()
        {
            var ticket = NewTicket();
            var other = OtherTechnician();

            ticket.AssignTo(Technician, Actor);
            ticket.AssignTo(other, Actor);

            Assert.Equal(other.Id, ticket.AssignedToUserId);
        }

        [Fact]
        public void AssignTo_RejectsInProgressTicket()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician, Actor);
            ticket.StartWork(Actor);

            Assert.Throws<InvalidOperationException>(() => ticket.AssignTo(OtherTechnician(), Actor));
            Assert.Equal(Technician.Id, ticket.AssignedToUserId);
        }

        [Fact]
        public void AssignTo_RejectsResolvedTicket()
        {
            var ticket = ResolvedTicket();

            Assert.Throws<InvalidOperationException>(() => ticket.AssignTo(OtherTechnician(), Actor));
        }

        // StartWork

        [Fact]
        public void StartWork_MovesAssignedTicketToInProgress()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician, Actor);

            ticket.StartWork(Actor);

            Assert.Equal(TicketStatus.InProgress, ticket.Status);
        }

        [Fact]
        public void StartWork_RejectsUnassignedTicket()
        {
            var ticket = NewTicket();

            Assert.Throws<InvalidOperationException>(() => ticket.StartWork(Actor));
            Assert.Equal(TicketStatus.Open, ticket.Status);
        }

        [Fact]
        public void StartWork_RejectsTicketAlreadyInProgress()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician, Actor);
            ticket.StartWork(Actor);

            Assert.Throws<InvalidOperationException>(() => ticket.StartWork(Actor));
        }

        // MarkResolved

        [Fact]
        public void MarkResolved_RecordsTrimmedSummaryAndTime()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician, Actor);
            ticket.StartWork(Actor);
            var before = DateTimeOffset.UtcNow;

            ticket.MarkResolved("  Replaced toner  ", Actor);

            Assert.Equal(TicketStatus.Resolved, ticket.Status);
            Assert.Equal("Replaced toner", ticket.ResolutionSummary);
            Assert.NotNull(ticket.ResolvedAt);
            Assert.InRange(ticket.ResolvedAt!.Value, before, DateTimeOffset.UtcNow);
        }

        [Fact]
        public void MarkResolved_AllowsAssignedOpenTicketWithoutStartingWork()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician, Actor);

            ticket.MarkResolved("Fixed over the phone", Actor);

            Assert.Equal(TicketStatus.Resolved, ticket.Status);
        }

        [Fact]
        public void MarkResolved_RejectsUnassignedTicket()
        {
            var ticket = NewTicket();

            Assert.Throws<InvalidOperationException>(() => ticket.MarkResolved("Fixed", Actor));
            Assert.Equal(TicketStatus.Open, ticket.Status);
            Assert.Null(ticket.ResolvedAt);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void MarkResolved_RequiresSummary(string? summary)
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician, Actor);

            Assert.Throws<ArgumentException>(() => ticket.MarkResolved(summary!, Actor));
            Assert.Equal(TicketStatus.Open, ticket.Status);
            Assert.Null(ticket.ResolutionSummary);
        }

        [Fact]
        public void MarkResolved_RejectsClosedTicket()
        {
            var ticket = ResolvedTicket();
            ticket.Close(Actor);

            Assert.Throws<InvalidOperationException>(() => ticket.MarkResolved("Again", Actor));
            Assert.Equal(TicketStatus.Closed, ticket.Status);
            Assert.Equal("Power-cycled the printer", ticket.ResolutionSummary);
        }

        [Fact]
        public void MarkResolved_RejectsResolvedTicketAndKeepsOriginalResolution()
        {
            var ticket = ResolvedTicket();
            var resolvedAt = ticket.ResolvedAt;

            Assert.Throws<InvalidOperationException>(() => ticket.MarkResolved("Different story", Actor));
            Assert.Equal(TicketStatus.Resolved, ticket.Status);
            Assert.Equal("Power-cycled the printer", ticket.ResolutionSummary);
            Assert.Equal(resolvedAt, ticket.ResolvedAt);
        }

        // Close

        [Fact]
        public void Close_ClosesResolvedTicket()
        {
            var ticket = ResolvedTicket();

            ticket.Close(Actor);

            Assert.Equal(TicketStatus.Closed, ticket.Status);
        }

        [Fact]
        public void Close_RejectsOpenTicket()
        {
            var ticket = NewTicket();

            Assert.Throws<InvalidOperationException>(() => ticket.Close(Actor));
            Assert.Equal(TicketStatus.Open, ticket.Status);
        }

        [Fact]
        public void Close_RejectsInProgressTicket()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician, Actor);
            ticket.StartWork(Actor);

            Assert.Throws<InvalidOperationException>(() => ticket.Close(Actor));
        }

        [Fact]
        public void Close_RejectsClosedTicket()
        {
            var ticket = ResolvedTicket();
            ticket.Close(Actor);

            Assert.Throws<InvalidOperationException>(() => ticket.Close(Actor));
        }

        // MarkDeleted

        [Fact]
        public void MarkDeleted_StampsDeletedAtWithoutChangingStatus()
        {
            var ticket = NewTicket();
            var before = DateTimeOffset.UtcNow;

            ticket.MarkDeleted(Actor);

            Assert.NotNull(ticket.DeletedAt);
            Assert.InRange(ticket.DeletedAt!.Value, before, DateTimeOffset.UtcNow);
            Assert.Equal(TicketStatus.Open, ticket.Status);
        }

        [Fact]
        public void MarkDeleted_TwiceKeepsOriginalTimestamp()
        {
            var ticket = NewTicket();
            ticket.MarkDeleted(Actor);
            var original = ticket.DeletedAt;

            Assert.Throws<InvalidOperationException>(() => ticket.MarkDeleted(Actor));
            Assert.Equal(original, ticket.DeletedAt);
        }

        // Full lifecycle

        [Fact]
        public void Lifecycle_OpenToClosed()
        {
            var ticket = NewTicket();

            ticket.AssignTo(Technician, Actor);
            ticket.StartWork(Actor);
            ticket.MarkResolved("Reinstalled driver", Actor);
            ticket.Close(Actor);

            Assert.Equal(TicketStatus.Closed, ticket.Status);
            Assert.Equal(Technician.Id, ticket.AssignedToUserId);
            Assert.Equal("Reinstalled driver", ticket.ResolutionSummary);
            Assert.NotNull(ticket.ResolvedAt);
        }
    }
}
