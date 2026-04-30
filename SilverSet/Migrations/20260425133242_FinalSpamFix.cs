using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SilverSet.Migrations
{
    /// <inheritdoc />
    public partial class FinalSpamFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_AspNetUsers_UserId",
                table: "ForumSpamLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_ForumPosts_PostId",
                table: "ForumSpamLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_ForumSpamLogs_RelatedSpamId",
                table: "ForumSpamLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_ForumTopics_TopicId",
                table: "ForumSpamLogs");

            migrationBuilder.AddColumn<int>(
                name: "ForumSpamLogId",
                table: "ForumSpamLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumSpamLogs_ForumSpamLogId",
                table: "ForumSpamLogs",
                column: "ForumSpamLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_AspNetUsers_UserId",
                table: "ForumSpamLogs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_ForumPosts_PostId",
                table: "ForumSpamLogs",
                column: "PostId",
                principalTable: "ForumPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_ForumSpamLogs_ForumSpamLogId",
                table: "ForumSpamLogs",
                column: "ForumSpamLogId",
                principalTable: "ForumSpamLogs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_ForumSpamLogs_RelatedSpamId",
                table: "ForumSpamLogs",
                column: "RelatedSpamId",
                principalTable: "ForumSpamLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_ForumTopics_TopicId",
                table: "ForumSpamLogs",
                column: "TopicId",
                principalTable: "ForumTopics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_AspNetUsers_UserId",
                table: "ForumSpamLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_ForumPosts_PostId",
                table: "ForumSpamLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_ForumSpamLogs_ForumSpamLogId",
                table: "ForumSpamLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_ForumSpamLogs_RelatedSpamId",
                table: "ForumSpamLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumSpamLogs_ForumTopics_TopicId",
                table: "ForumSpamLogs");

            migrationBuilder.DropIndex(
                name: "IX_ForumSpamLogs_ForumSpamLogId",
                table: "ForumSpamLogs");

            migrationBuilder.DropColumn(
                name: "ForumSpamLogId",
                table: "ForumSpamLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_AspNetUsers_UserId",
                table: "ForumSpamLogs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_ForumPosts_PostId",
                table: "ForumSpamLogs",
                column: "PostId",
                principalTable: "ForumPosts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_ForumSpamLogs_RelatedSpamId",
                table: "ForumSpamLogs",
                column: "RelatedSpamId",
                principalTable: "ForumSpamLogs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumSpamLogs_ForumTopics_TopicId",
                table: "ForumSpamLogs",
                column: "TopicId",
                principalTable: "ForumTopics",
                principalColumn: "Id");
        }
    }
}
