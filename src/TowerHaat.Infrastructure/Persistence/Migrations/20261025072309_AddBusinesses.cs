using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TowerHaat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OrderingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_BusinessTypes_BusinessTypeId",
                        column: x => x.BusinessTypeId,
                        principalTable: "BusinessTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Businesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SocietyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpiId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    FssaiNumber = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    WhatsAppPhone = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    MinOrderValue = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DeliveryCharge = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AcceptOrdersUntil = table.Column<TimeOnly>(type: "time", nullable: true),
                    ClosesAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Businesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Businesses_BusinessTypes_BusinessTypeId",
                        column: x => x.BusinessTypeId,
                        principalTable: "BusinessTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Businesses_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Businesses_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Businesses_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SocietyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPhotos_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessPhotos_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SocietyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsVeg = table.Column<bool>(type: "bit", nullable: true),
                    DailyLimit = table.Column<int>(type: "int", nullable: true),
                    IsAvailableToday = table.Column<bool>(type: "bit", nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    PhotoKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItems_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatalogItems_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "BusinessTypes",
                columns: new[] { "Id", "IsActive", "Name", "OrderingEnabled", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("01920000-0000-7000-8001-000000000001"), true, "Food", true, 1 },
                    { new Guid("01920000-0000-7000-8001-000000000002"), true, "Products", true, 2 },
                    { new Guid("01920000-0000-7000-8001-000000000003"), true, "Tuition", false, 3 },
                    { new Guid("01920000-0000-7000-8001-000000000004"), true, "Services", false, 4 },
                    { new Guid("01920000-0000-7000-8001-000000000005"), true, "Daycare", false, 5 },
                    { new Guid("01920000-0000-7000-8001-000000000006"), true, "Other", false, 6 }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "BusinessTypeId", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("01920000-0000-7000-8002-000000000101"), new Guid("01920000-0000-7000-8001-000000000001"), true, "Home kitchen", 1 },
                    { new Guid("01920000-0000-7000-8002-000000000102"), new Guid("01920000-0000-7000-8001-000000000001"), true, "Tiffin and meals", 2 },
                    { new Guid("01920000-0000-7000-8002-000000000103"), new Guid("01920000-0000-7000-8001-000000000001"), true, "Bakery and desserts", 3 },
                    { new Guid("01920000-0000-7000-8002-000000000104"), new Guid("01920000-0000-7000-8001-000000000001"), true, "Snacks and sweets", 4 },
                    { new Guid("01920000-0000-7000-8002-000000000201"), new Guid("01920000-0000-7000-8001-000000000002"), true, "Clothing and accessories", 1 },
                    { new Guid("01920000-0000-7000-8002-000000000202"), new Guid("01920000-0000-7000-8001-000000000002"), true, "Home and decor", 2 },
                    { new Guid("01920000-0000-7000-8002-000000000203"), new Guid("01920000-0000-7000-8001-000000000002"), true, "Homemade products", 3 },
                    { new Guid("01920000-0000-7000-8002-000000000301"), new Guid("01920000-0000-7000-8001-000000000003"), true, "Academic", 1 },
                    { new Guid("01920000-0000-7000-8002-000000000302"), new Guid("01920000-0000-7000-8001-000000000003"), true, "Music", 2 },
                    { new Guid("01920000-0000-7000-8002-000000000303"), new Guid("01920000-0000-7000-8001-000000000003"), true, "Dance", 3 },
                    { new Guid("01920000-0000-7000-8002-000000000304"), new Guid("01920000-0000-7000-8001-000000000003"), true, "Art and craft", 4 },
                    { new Guid("01920000-0000-7000-8002-000000000401"), new Guid("01920000-0000-7000-8001-000000000004"), true, "Beauty and wellness", 1 },
                    { new Guid("01920000-0000-7000-8002-000000000402"), new Guid("01920000-0000-7000-8001-000000000004"), true, "Tailoring", 2 },
                    { new Guid("01920000-0000-7000-8002-000000000403"), new Guid("01920000-0000-7000-8001-000000000004"), true, "Repairs", 3 },
                    { new Guid("01920000-0000-7000-8002-000000000404"), new Guid("01920000-0000-7000-8001-000000000004"), true, "Fitness and yoga", 4 },
                    { new Guid("01920000-0000-7000-8002-000000000501"), new Guid("01920000-0000-7000-8001-000000000005"), true, "Daycare", 1 },
                    { new Guid("01920000-0000-7000-8002-000000000601"), new Guid("01920000-0000-7000-8001-000000000006"), true, "Other", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_BusinessTypeId",
                table: "Businesses",
                column: "BusinessTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_CategoryId",
                table: "Businesses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_OwnerUserId",
                table: "Businesses",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_SocietyId",
                table: "Businesses",
                column: "SocietyId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_SocietyId_Status",
                table: "Businesses",
                columns: new[] { "SocietyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPhotos_BusinessId",
                table: "BusinessPhotos",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPhotos_SocietyId",
                table: "BusinessPhotos",
                column: "SocietyId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTypes_Name",
                table: "BusinessTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_BusinessId",
                table: "CatalogItems",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_SocietyId",
                table: "CatalogItems",
                column: "SocietyId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_BusinessTypeId_Name",
                table: "Categories",
                columns: new[] { "BusinessTypeId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessPhotos");

            migrationBuilder.DropTable(
                name: "CatalogItems");

            migrationBuilder.DropTable(
                name: "Businesses");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "BusinessTypes");
        }
    }
}
