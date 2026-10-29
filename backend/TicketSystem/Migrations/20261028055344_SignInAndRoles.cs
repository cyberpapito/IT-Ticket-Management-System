using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketSystem.Migrations
{
    /// <inheritdoc />
    public partial class SignInAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ActorUserId",
                table: "TicketActivities",
                type: "uniqueidentifier",
                nullable: true);

            // Users made through the API before this migration get a random stamp instead of "".
            // None has a password yet, so none can sign in until an admin sets one.
            migrationBuilder.Sql(
                "UPDATE Users SET SecurityStamp = REPLACE(CONVERT(nvarchar(36), NEWID()), '-', '') WHERE SecurityStamp = ''");

            // Who did the older entries: on Created (0) and Comment (6) the person the entry names
            // did it. The other steps had no sign-in behind them and stay null ("before sign-in").
            migrationBuilder.Sql(
                "UPDATE TicketActivities SET ActorUserId = UserId WHERE Type IN (0, 6)");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000000"),
                columns: new[] { "PasswordHash", "SecurityStamp" },
                values: new object[] { null, "seed-unknown" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                columns: new[] { "PasswordHash", "SecurityStamp" },
                values: new object[] { null, "seed-adrian-rodriguez" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                columns: new[] { "PasswordHash", "SecurityStamp" },
                values: new object[] { null, "seed-john-doe" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_ActorUserId",
                table: "TicketActivities",
                column: "ActorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketActivities_Users_ActorUserId",
                table: "TicketActivities",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketActivities_Users_ActorUserId",
                table: "TicketActivities");

            migrationBuilder.DropIndex(
                name: "IX_TicketActivities_ActorUserId",
                table: "TicketActivities");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "TicketActivities");
        }
    }
}
