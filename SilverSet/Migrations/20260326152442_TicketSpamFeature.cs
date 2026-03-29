using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SilverSet.Migrations
{
    /// <inheritdoc />
    public partial class TicketSpamFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSpam",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpamReason",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSpam",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SpamReason",
                table: "Tickets");
        }
    }
}
