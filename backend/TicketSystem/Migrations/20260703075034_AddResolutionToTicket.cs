using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketSystem.Migrations
{
        public partial class AddResolutionToTicket : Migration
    {
       
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionSummary",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);
        }

                protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolutionSummary",
                table: "Tickets");
        }
    }
}
