using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderChangeNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderChangeNotification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    WorkOrderCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderChangeNotification", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderChangeNotification_CreatedTime",
                table: "OrderChangeNotification",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_OrderChangeNotification_IsRead",
                table: "OrderChangeNotification",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_OrderChangeNotification_OrderNumber",
                table: "OrderChangeNotification",
                column: "OrderNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderChangeNotification");
        }
    }
}
