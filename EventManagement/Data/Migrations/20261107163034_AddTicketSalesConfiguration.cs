using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSalesConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalTicketingUrl",
                table: "Events",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TicketSalesMode",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EventTicketTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    QuantityAvailable = table.Column<int>(type: "int", nullable: false),
                    SalesStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SalesEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTicketTypes", x => x.Id);
                    table.CheckConstraint("CK_EventTicketTypes_Price_NonNegative", "[Price] >= 0");
                    table.CheckConstraint("CK_EventTicketTypes_QuantityAvailable_NonNegative", "[QuantityAvailable] >= 0");
                    table.CheckConstraint("CK_EventTicketTypes_SalesWindow", "[SalesEndAt] > [SalesStartAt]");
                    table.ForeignKey(
                        name: "FK_EventTicketTypes_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventTicketTypes_EventId_IsEnabled",
                table: "EventTicketTypes",
                columns: new[] { "EventId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_EventTicketTypes_EventId_Name",
                table: "EventTicketTypes",
                columns: new[] { "EventId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventTicketTypes");

            migrationBuilder.DropColumn(
                name: "ExternalTicketingUrl",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TicketSalesMode",
                table: "Events");
        }
    }
}
