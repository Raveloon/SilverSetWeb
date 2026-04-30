using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SilverSet.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDisplayOrderFromCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "ForumCategories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "ForumCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
