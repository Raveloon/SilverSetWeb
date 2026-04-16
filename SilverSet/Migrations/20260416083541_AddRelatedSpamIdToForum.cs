using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SilverSet.Migrations
{
    /// <inheritdoc />
    public partial class AddRelatedSpamIdToForum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RelatedSpamId",
                table: "ForumSpamLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumSpamLogs_RelatedSpamId",
                table: "ForumSpamLogs",
                column: "RelatedSpamId");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_ForumSpamLogs_RelatedSpamId",
                table: "ForumSpamLogs",
                column: "RelatedSpamId",
                principalTable: "ForumSpamLogs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_ForumSpamLogs_RelatedSpamId",
                table: "ForumSpamLogs");

            migrationBuilder.DropIndex(
                name: "IX_ForumSpamLogs_RelatedSpamId",
                table: "ForumSpamLogs");

            migrationBuilder.DropColumn(
                name: "RelatedSpamId",
                table: "ForumSpamLogs");
        }
    }
}
