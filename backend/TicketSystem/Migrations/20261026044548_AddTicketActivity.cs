using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketActivities_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketActivities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_TicketId_At",
                table: "TicketActivities",
                columns: new[] { "TicketId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_UserId",
                table: "TicketActivities",
                column: "UserId");

            // Backfill the history of existing tickets from what they recorded: when they were
            // created (and by whom), resolved (with the summary) and deleted. Assign, start and
            // close left no timestamp, so those can't be recovered. Types: Created 0, Resolved 3,
            // Deleted 5. Statuses: Open 0, Resolved 2.
            migrationBuilder.Sql("""
                INSERT INTO TicketActivities (Id, TicketId, Type, At, UserId, Note, FromStatus, ToStatus)
                SELECT NEWID(), Id, 0, CreatedAt, CreatedByUserId, NULL, NULL, 0 FROM Tickets;

                INSERT INTO TicketActivities (Id, TicketId, Type, At, UserId, Note, FromStatus, ToStatus)
                SELECT NEWID(), Id, 3, ResolvedAt, NULL, ResolutionSummary, NULL, 2 FROM Tickets
                WHERE ResolvedAt IS NOT NULL;

                INSERT INTO TicketActivities (Id, TicketId, Type, At, UserId, Note, FromStatus, ToStatus)
                SELECT NEWID(), Id, 5, DeletedAt, NULL, NULL, NULL, NULL FROM Tickets
                WHERE DeletedAt IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketActivities");
        }
    }
}
