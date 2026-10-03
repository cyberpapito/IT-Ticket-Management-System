using TicketSystem.Models;

namespace TicketSystem.Tests
{
    public class TicketTests
    {
        private static readonly Guid Creator = Guid.NewGuid();
        private static readonly Guid Technician = Guid.NewGuid();

        private static Ticket NewTicket() =>
            Ticket.Create("Printer offline", "3rd floor printer shows offline", TicketPriority.Medium, Creator);

        private static Ticket ResolvedTicket()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician);
            ticket.MarkResolved("Power-cycled the printer");
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

            ticket.AssignTo(Technician);

            Assert.Equal(Technician, ticket.AssignedToUserId);
            Assert.Equal(TicketStatus.Open, ticket.Status);
        }

        [Fact]
        public void AssignTo_RejectsEmptyTechnicianId()
        {
            var ticket = NewTicket();

            Assert.Throws<ArgumentException>(() => ticket.AssignTo(Guid.Empty));
            Assert.Null(ticket.AssignedToUserId);
        }

        [Fact]
        public void AssignTo_CanReassignAnOpenTicket()
        {
            var ticket = NewTicket();
            var other = Guid.NewGuid();

            ticket.AssignTo(Technician);
            ticket.AssignTo(other);

            Assert.Equal(other, ticket.AssignedToUserId);
        }

        [Fact]
        public void AssignTo_RejectsInProgressTicket()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician);
            ticket.StartWork();

            Assert.Throws<InvalidOperationException>(() => ticket.AssignTo(Guid.NewGuid()));
            Assert.Equal(Technician, ticket.AssignedToUserId);
        }

        [Fact]
        public void AssignTo_RejectsResolvedTicket()
        {
            var ticket = ResolvedTicket();

            Assert.Throws<InvalidOperationException>(() => ticket.AssignTo(Guid.NewGuid()));
        }

        // StartWork

        [Fact]
        public void StartWork_MovesAssignedTicketToInProgress()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician);

            ticket.StartWork();

            Assert.Equal(TicketStatus.InProgress, ticket.Status);
        }

        [Fact]
        public void StartWork_RejectsUnassignedTicket()
        {
            var ticket = NewTicket();

            Assert.Throws<InvalidOperationException>(() => ticket.StartWork());
            Assert.Equal(TicketStatus.Open, ticket.Status);
        }

        [Fact]
        public void StartWork_RejectsTicketAlreadyInProgress()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician);
            ticket.StartWork();

            Assert.Throws<InvalidOperationException>(() => ticket.StartWork());
        }

        // MarkResolved

        [Fact]
        public void MarkResolved_RecordsTrimmedSummaryAndTime()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician);
            ticket.StartWork();
            var before = DateTimeOffset.UtcNow;

            ticket.MarkResolved("  Replaced toner  ");

            Assert.Equal(TicketStatus.Resolved, ticket.Status);
            Assert.Equal("Replaced toner", ticket.ResolutionSummary);
            Assert.NotNull(ticket.ResolvedAt);
            Assert.InRange(ticket.ResolvedAt!.Value, before, DateTimeOffset.UtcNow);
        }

        [Fact]
        public void MarkResolved_AllowsAssignedOpenTicketWithoutStartingWork()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician);

            ticket.MarkResolved("Fixed over the phone");

            Assert.Equal(TicketStatus.Resolved, ticket.Status);
        }

        [Fact]
        public void MarkResolved_RejectsUnassignedTicket()
        {
            var ticket = NewTicket();

            Assert.Throws<InvalidOperationException>(() => ticket.MarkResolved("Fixed"));
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
            ticket.AssignTo(Technician);

            Assert.Throws<ArgumentException>(() => ticket.MarkResolved(summary!));
            Assert.Equal(TicketStatus.Open, ticket.Status);
            Assert.Null(ticket.ResolutionSummary);
        }

        [Fact]
        public void MarkResolved_RejectsClosedTicket()
        {
            var ticket = ResolvedTicket();
            ticket.Close();

            Assert.Throws<InvalidOperationException>(() => ticket.MarkResolved("Again"));
            Assert.Equal(TicketStatus.Closed, ticket.Status);
            Assert.Equal("Power-cycled the printer", ticket.ResolutionSummary);
        }

        [Fact]
        public void MarkResolved_RejectsResolvedTicketAndKeepsOriginalResolution()
        {
            var ticket = ResolvedTicket();
            var resolvedAt = ticket.ResolvedAt;

            Assert.Throws<InvalidOperationException>(() => ticket.MarkResolved("Different story"));
            Assert.Equal(TicketStatus.Resolved, ticket.Status);
            Assert.Equal("Power-cycled the printer", ticket.ResolutionSummary);
            Assert.Equal(resolvedAt, ticket.ResolvedAt);
        }

        // Close

        [Fact]
        public void Close_ClosesResolvedTicket()
        {
            var ticket = ResolvedTicket();

            ticket.Close();

            Assert.Equal(TicketStatus.Closed, ticket.Status);
        }

        [Fact]
        public void Close_RejectsOpenTicket()
        {
            var ticket = NewTicket();

            Assert.Throws<InvalidOperationException>(() => ticket.Close());
            Assert.Equal(TicketStatus.Open, ticket.Status);
        }

        [Fact]
        public void Close_RejectsInProgressTicket()
        {
            var ticket = NewTicket();
            ticket.AssignTo(Technician);
            ticket.StartWork();

            Assert.Throws<InvalidOperationException>(() => ticket.Close());
        }

        [Fact]
        public void Close_RejectsClosedTicket()
        {
            var ticket = ResolvedTicket();
            ticket.Close();

            Assert.Throws<InvalidOperationException>(() => ticket.Close());
        }

        // MarkDeleted

        [Fact]
        public void MarkDeleted_StampsDeletedAtWithoutChangingStatus()
        {
            var ticket = NewTicket();
            var before = DateTimeOffset.UtcNow;

            ticket.MarkDeleted();

            Assert.NotNull(ticket.DeletedAt);
            Assert.InRange(ticket.DeletedAt!.Value, before, DateTimeOffset.UtcNow);
            Assert.Equal(TicketStatus.Open, ticket.Status);
        }

        [Fact]
        public void MarkDeleted_TwiceKeepsOriginalTimestamp()
        {
            var ticket = NewTicket();
            ticket.MarkDeleted();
            var original = ticket.DeletedAt;

            Assert.Throws<InvalidOperationException>(() => ticket.MarkDeleted());
            Assert.Equal(original, ticket.DeletedAt);
        }

        // Full lifecycle

        [Fact]
        public void Lifecycle_OpenToClosed()
        {
            var ticket = NewTicket();

            ticket.AssignTo(Technician);
            ticket.StartWork();
            ticket.MarkResolved("Reinstalled driver");
            ticket.Close();

            Assert.Equal(TicketStatus.Closed, ticket.Status);
            Assert.Equal(Technician, ticket.AssignedToUserId);
            Assert.Equal("Reinstalled driver", ticket.ResolutionSummary);
            Assert.NotNull(ticket.ResolvedAt);
        }
    }
}
