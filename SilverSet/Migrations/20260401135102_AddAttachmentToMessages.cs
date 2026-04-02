using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SilverSet.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "TicketMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Word",
                table: "BadWords",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedAt",
                table: "Tickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IsOpen",
                table: "Tickets",
                column: "IsOpen");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IsSpam",
                table: "Tickets",
                column: "IsSpam");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_SentAt",
                table: "TicketMessages",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_BadWords_Word",
                table: "BadWords",
                column: "Word",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_CreatedAt",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IsOpen",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IsSpam",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketMessages_SentAt",
                table: "TicketMessages");

            migrationBuilder.DropIndex(
                name: "IX_BadWords_Word",
                table: "BadWords");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "TicketMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Word",
                table: "BadWords",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
