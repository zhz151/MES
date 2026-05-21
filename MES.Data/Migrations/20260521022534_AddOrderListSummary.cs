using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderListSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderListSummary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EndCustomer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeliveryStart = table.Column<DateTime>(type: "date", nullable: true),
                    DeliveryEnd = table.Column<DateTime>(type: "date", nullable: true),
                    HasDelayPenalty = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TotalContractWeight = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ItemCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    HasTechReqCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "int", maxLength: 20, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    LastChangeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstOrderItemId = table.Column<int>(type: "int", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderListSummary", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OLS_CustomerName",
                table: "OrderListSummary",
                column: "CustomerName");

            migrationBuilder.CreateIndex(
                name: "IX_OLS_DeliveryEnd",
                table: "OrderListSummary",
                column: "DeliveryEnd");

            migrationBuilder.CreateIndex(
                name: "IX_OLS_OrderNumber",
                table: "OrderListSummary",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_OLS_SignDate",
                table: "OrderListSummary",
                column: "SignDate");

            migrationBuilder.CreateIndex(
                name: "IX_OLS_Status",
                table: "OrderListSummary",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UK_OLS_OrderId",
                table: "OrderListSummary",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderListSummary");
        }
    }
}
