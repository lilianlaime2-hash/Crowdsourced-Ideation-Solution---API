using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIS.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddIdeaStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "ideas",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                table: "ideas");
        }
    }
}
