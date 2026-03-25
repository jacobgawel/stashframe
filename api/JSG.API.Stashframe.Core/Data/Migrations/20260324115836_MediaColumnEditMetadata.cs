using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JSG.API.Stashframe.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class MediaColumnEditMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "media",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "media",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "media",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "media");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "media");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "media");
        }
    }
}
