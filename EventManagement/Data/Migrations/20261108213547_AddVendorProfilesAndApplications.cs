using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorProfilesAndApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsVendorApplications",
                table: "Events",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VendorProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProductsOffered = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorProfiles_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorProfileId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProductsOffered = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorApplications", x => x.Id);
                    table.CheckConstraint("CK_VendorApplications_EventMatchesKind", "([Kind] = 1 AND [EventId] IS NOT NULL) OR ([Kind] = 0 AND [EventId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_VendorApplications_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VendorApplications_VendorProfiles_VendorProfileId",
                        column: x => x.VendorProfileId,
                        principalTable: "VendorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorApplications_EventId_Status",
                table: "VendorApplications",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorApplications_Kind_Status",
                table: "VendorApplications",
                columns: new[] { "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorApplications_OnePending",
                table: "VendorApplications",
                columns: new[] { "VendorProfileId", "Kind", "EventId" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_VendorProfiles_OwnerUserId",
                table: "VendorProfiles",
                column: "OwnerUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorApplications");

            migrationBuilder.DropTable(
                name: "VendorProfiles");

            migrationBuilder.DropColumn(
                name: "AcceptsVendorApplications",
                table: "Events");
        }
    }
}
