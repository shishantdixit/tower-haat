using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TowerHaat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SocietyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PlacedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreferredTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Tower = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Flat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DeliveryCharge = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Users_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SocietyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PriceSnapshot = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsVegSnapshot = table.Column<bool>(type: "bit", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SocietyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusChanges_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderStatusChanges_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_CatalogItemId",
                table: "OrderItems",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_SocietyId",
                table: "OrderItems",
                column: "SocietyId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BusinessId_OrderNo",
                table: "Orders",
                columns: new[] { "BusinessId", "OrderNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BusinessId_Status",
                table: "Orders",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerUserId_IdempotencyKey",
                table: "Orders",
                columns: new[] { "CustomerUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SocietyId",
                table: "Orders",
                column: "SocietyId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_PlacedAt",
                table: "Orders",
                columns: new[] { "Status", "PlacedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusChanges_OrderId",
                table: "OrderStatusChanges",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusChanges_SocietyId",
                table: "OrderStatusChanges",
                column: "SocietyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "OrderStatusChanges");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
