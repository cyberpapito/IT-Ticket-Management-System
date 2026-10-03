using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TicketSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "Name", "Role" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000000"), new DateTimeOffset(new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "unknown@example.com", "Unknown (pre-users)", 0 },
                    { new Guid("a0000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "adrian.rodriguez@example.com", "Adrian Rodriguez", 1 },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "john.doe@example.com", "John Doe", 0 }
                });

            // Tickets created before users existed point at made-up user ids, which the foreign
            // keys below would refuse. Re-point them at the seeded "Unknown (pre-users)" user so
            // every row (and its history) is kept. The technician is re-pointed rather than
            // cleared, because a resolved or closed ticket must keep one. Not undone by Down.
            migrationBuilder.Sql(
                "UPDATE Tickets SET CreatedByUserId = 'a0000000-0000-0000-0000-000000000000' " +
                "WHERE CreatedByUserId NOT IN (SELECT Id FROM Users);");
            migrationBuilder.Sql(
                "UPDATE Tickets SET AssignedToUserId = 'a0000000-0000-0000-0000-000000000000' " +
                "WHERE AssignedToUserId IS NOT NULL AND AssignedToUserId NOT IN (SELECT Id FROM Users);");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedToUserId",
                table: "Tickets",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedByUserId",
                table: "Tickets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AssignedToUserId",
                table: "Tickets",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_CreatedByUserId",
                table: "Tickets",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_AssignedToUserId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_CreatedByUserId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_AssignedToUserId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CreatedByUserId",
                table: "Tickets");
        }
    }
}
