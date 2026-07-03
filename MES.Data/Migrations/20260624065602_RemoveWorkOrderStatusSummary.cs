using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWorkOrderStatusSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderStatusSummary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkOrderStatusSummary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeliveryEnd = table.Column<DateTime>(type: "date", nullable: true),
                    DeliveryStart = table.Column<DateTime>(type: "date", nullable: true),
                    EndCustomer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HasDelayPenalty = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HasWorkOrder = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastChangeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    SalesOrderId = table.Column<int>(type: "int", nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalContractWeight = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WorkOrderCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    WorkOrderId = table.Column<int>(type: "int", nullable: true),
                    WorkOrderStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "NotGenerated")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderStatusSummary", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WOSS_CustomerName",
                table: "WorkOrderStatusSummary",
                column: "CustomerName");

            migrationBuilder.CreateIndex(
                name: "IX_WOSS_OrderNumber",
                table: "WorkOrderStatusSummary",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_WOSS_SignDate",
                table: "WorkOrderStatusSummary",
                column: "SignDate");

            migrationBuilder.CreateIndex(
                name: "IX_WOSS_WorkOrderStatus",
                table: "WorkOrderStatusSummary",
                column: "WorkOrderStatus");

            migrationBuilder.CreateIndex(
                name: "UK_WOSS_SalesOrderId",
                table: "WorkOrderStatusSummary",
                column: "SalesOrderId",
                unique: true);
        }
    }
}
