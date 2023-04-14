using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Website.Migrations
{
    public partial class Eventsscaffolding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Positions",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "Route",
                table: "Events",
                newName: "ForumUrl");

            migrationBuilder.AddColumn<string>(
                name: "BannerUrl",
                table: "Events",
                type: "longtext",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventSignups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Controller = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<string>(type: "longtext", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSignups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSignups_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventSignups_EventId",
                table: "EventSignups",
                column: "EventId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventSignups");

            migrationBuilder.DropColumn(
                name: "BannerUrl",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "ForumUrl",
                table: "Events",
                newName: "Route");

            migrationBuilder.AddColumn<string>(
                name: "Positions",
                table: "Events",
                type: "longtext",
                nullable: false);
        }
    }
}
