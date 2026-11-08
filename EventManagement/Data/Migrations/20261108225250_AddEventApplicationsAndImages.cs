using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventApplicationsAndImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackgroundImageId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicantUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EventCategoryId = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VenueName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ExternalWebsiteUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BackgroundImageId = table.Column<int>(type: "int", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    OrganizationName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedEventId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventApplications", x => x.Id);
                    table.CheckConstraint("CK_EventApplications_CreatedEventMatchesStatus", "([Status] = 1 AND [CreatedEventId] IS NOT NULL) OR ([Status] <> 1 AND [CreatedEventId] IS NULL)");
                    table.CheckConstraint("CK_EventApplications_EndsAfterStart", "[EndsAt] > [StartsAt]");
                    table.ForeignKey(
                        name: "FK_EventApplications_AspNetUsers_ApplicantUserId",
                        column: x => x.ApplicantUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventApplications_EventCategories_EventCategoryId",
                        column: x => x.EventCategoryId,
                        principalTable: "EventCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventApplications_EventImages_BackgroundImageId",
                        column: x => x.BackgroundImageId,
                        principalTable: "EventImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventApplications_Events_CreatedEventId",
                        column: x => x.CreatedEventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventApplicationInterests",
                columns: table => new
                {
                    EventApplicationId = table.Column<int>(type: "int", nullable: false),
                    InterestCategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventApplicationInterests", x => new { x.EventApplicationId, x.InterestCategoryId });
                    table.ForeignKey(
                        name: "FK_EventApplicationInterests_EventApplications_EventApplicationId",
                        column: x => x.EventApplicationId,
                        principalTable: "EventApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventApplicationInterests_InterestCategories_InterestCategoryId",
                        column: x => x.InterestCategoryId,
                        principalTable: "InterestCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_BackgroundImageId",
                table: "Events",
                column: "BackgroundImageId");

            migrationBuilder.CreateIndex(
                name: "IX_EventApplicationInterests_InterestCategoryId",
                table: "EventApplicationInterests",
                column: "InterestCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EventApplications_BackgroundImageId",
                table: "EventApplications",
                column: "BackgroundImageId");

            migrationBuilder.CreateIndex(
                name: "IX_EventApplications_CreatedEventId",
                table: "EventApplications",
                column: "CreatedEventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventApplications_EventCategoryId",
                table: "EventApplications",
                column: "EventCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EventApplications_OnePendingPerTitle",
                table: "EventApplications",
                columns: new[] { "ApplicantUserId", "Title" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EventApplications_Status_SubmittedAt",
                table: "EventApplications",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Events_EventImages_BackgroundImageId",
                table: "Events",
                column: "BackgroundImageId",
                principalTable: "EventImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_EventImages_BackgroundImageId",
                table: "Events");

            migrationBuilder.DropTable(
                name: "EventApplicationInterests");

            migrationBuilder.DropTable(
                name: "EventApplications");

            migrationBuilder.DropTable(
                name: "EventImages");

            migrationBuilder.DropIndex(
                name: "IX_Events_BackgroundImageId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "BackgroundImageId",
                table: "Events");
        }
    }
}
