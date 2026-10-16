using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventInterestsAndListingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalWebsiteUrl",
                table: "Events",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumPrice",
                table: "Events",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "EventInterests",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "int", nullable: false),
                    InterestCategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventInterests", x => new { x.EventId, x.InterestCategoryId });
                    table.ForeignKey(
                        name: "FK_EventInterests_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventInterests_InterestCategories_InterestCategoryId",
                        column: x => x.InterestCategoryId,
                        principalTable: "InterestCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_City",
                table: "Events",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Status_StartsAt",
                table: "Events",
                columns: new[] { "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EventInterests_InterestCategoryId",
                table: "EventInterests",
                column: "InterestCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventInterests");

            migrationBuilder.DropIndex(
                name: "IX_Events_City",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_Status_StartsAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ExternalWebsiteUrl",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "MinimumPrice",
                table: "Events");
        }
    }
}
