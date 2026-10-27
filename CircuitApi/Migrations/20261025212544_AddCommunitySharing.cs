using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircuitApi.Migrations
{

    public partial class AddCommunitySharing : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Circuits",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SharedCircuits",
                columns: table => new
                {
                    SharedCircuitId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    JsonData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedCircuits", x => x.SharedCircuitId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Circuits_OwnerId_UpdatedAt",
                table: "Circuits",
                columns: new[] { "OwnerId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedCircuits_PublishedAt",
                table: "SharedCircuits",
                column: "PublishedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharedCircuits");

            migrationBuilder.DropIndex(
                name: "IX_Circuits_OwnerId_UpdatedAt",
                table: "Circuits");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Circuits");
        }
    }
}
