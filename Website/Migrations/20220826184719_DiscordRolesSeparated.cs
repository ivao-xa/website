using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Website.Migrations
{
    public partial class DiscordRolesSeparated : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Discord",
                table: "Users",
                newName: "DiscordSnowflake");

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Snowflake = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Roles = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Snowflake);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DiscordSnowflake",
                table: "Users",
                column: "DiscordSnowflake");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_DiscordSnowflake",
                table: "Users",
                column: "DiscordSnowflake",
                principalTable: "Roles",
                principalColumn: "Snowflake");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_DiscordSnowflake",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_DiscordSnowflake",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "DiscordSnowflake",
                table: "Users",
                newName: "Discord");
        }
    }
}
