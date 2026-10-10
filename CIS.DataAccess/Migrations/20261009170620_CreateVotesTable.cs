using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace CIS.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class CreateVotesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "votes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    idea_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_votes", x => x.id);
                    table.ForeignKey(
                        name: "FK_votes_ideas_idea_id",
                        column: x => x.idea_id,
                        principalTable: "ideas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ux_votes_idea_id_user_id",
                table: "votes",
                columns: new[] { "idea_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "votes");
        }
    }
}
