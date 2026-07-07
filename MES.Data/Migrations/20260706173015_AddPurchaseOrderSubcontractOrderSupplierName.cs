using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderSubcontractOrderSupplierName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "SubcontractOrder",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "PurchaseOrder",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // 从 SupplierProfiles 回填现有数据的 SupplierName
            migrationBuilder.Sql(@"
UPDATE po SET po.SupplierName = sp.SupplierName
FROM PurchaseOrder po
INNER JOIN SupplierProfile sp ON po.SupplierId = sp.Id
WHERE po.SupplierName IS NULL");

            migrationBuilder.Sql(@"
UPDATE so SET so.SupplierName = sp.SupplierName
FROM SubcontractOrder so
INNER JOIN SupplierProfile sp ON so.SupplierId = sp.Id
WHERE so.SupplierName IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplierName",
                table: "SubcontractOrder");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                table: "PurchaseOrder");
        }
    }
}
