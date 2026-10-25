using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TowerHaat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSocieties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Societies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Societies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Towers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SocietyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Towers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Towers_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Societies_Name",
                table: "Societies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Towers_SocietyId",
                table: "Towers",
                column: "SocietyId");

            migrationBuilder.CreateIndex(
                name: "IX_Towers_SocietyId_Name",
                table: "Towers",
                columns: new[] { "SocietyId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Towers");

            migrationBuilder.DropTable(
                name: "Societies");
        }
    }
}
