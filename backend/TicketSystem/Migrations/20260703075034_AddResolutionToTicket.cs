using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddResolutionToTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionSummary",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolutionSummary",
                table: "Tickets");
        }
    }
}
