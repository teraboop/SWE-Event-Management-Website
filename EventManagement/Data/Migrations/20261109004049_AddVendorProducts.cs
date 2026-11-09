using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorProfileId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    QuantityInStock = table.Column<int>(type: "int", nullable: false),
                    IsListed = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ImageId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorProducts", x => x.Id);
                    table.CheckConstraint("CK_VendorProducts_Price_Positive", "[Price] > 0");
                    table.CheckConstraint("CK_VendorProducts_QuantityInStock_NonNegative", "[QuantityInStock] >= 0");
                    table.ForeignKey(
                        name: "FK_VendorProducts_EventImages_ImageId",
                        column: x => x.ImageId,
                        principalTable: "EventImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorProducts_VendorProfiles_VendorProfileId",
                        column: x => x.VendorProfileId,
                        principalTable: "VendorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorProducts_ImageId",
                table: "VendorProducts",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorProducts_IsListed_ArchivedAt_CreatedAt",
                table: "VendorProducts",
                columns: new[] { "IsListed", "ArchivedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorProducts_VendorProfileId_ArchivedAt",
                table: "VendorProducts",
                columns: new[] { "VendorProfileId", "ArchivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorProducts");
        }
    }
}
