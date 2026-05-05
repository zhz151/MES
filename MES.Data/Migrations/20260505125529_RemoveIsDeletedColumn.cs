using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsDeletedColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_Supplier_Code",
                table: "SupplierProfile");

            migrationBuilder.DropIndex(
                name: "UK_OrderItem_Sequence_Active",
                table: "OrderItem");

            migrationBuilder.DropIndex(
                name: "UK_Material_Code",
                table: "Material");

            migrationBuilder.DropIndex(
                name: "UK_Material_Combo",
                table: "Material");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBatch_RemainingWeight",
                table: "InventoryBatch");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkOrder");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Warehouse");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SupplierProfile");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SubcontractOrder");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StandardGradeMapping");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SalesOrder");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PurchaseSemiPlan");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProductionStandard");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OrderChangeNotification");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Material");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "InventoryPlan");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "InventoryBatch");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CustomerProfile");

            migrationBuilder.CreateIndex(
                name: "UK_Supplier_Code",
                table: "SupplierProfile",
                column: "SupplierCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_OrderItem_Sequence_Active",
                table: "OrderItem",
                columns: new[] { "SalesOrderId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_Material_Code",
                table: "Material",
                column: "MaterialCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_Material_Combo",
                table: "Material",
                columns: new[] { "MaterialCategory", "PlantGrade", "Specification" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_RemainingWeight",
                table: "InventoryBatch",
                column: "RemainingWeight",
                filter: "[RemainingWeight] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_Supplier_Code",
                table: "SupplierProfile");

            migrationBuilder.DropIndex(
                name: "UK_OrderItem_Sequence_Active",
                table: "OrderItem");

            migrationBuilder.DropIndex(
                name: "UK_Material_Code",
                table: "Material");

            migrationBuilder.DropIndex(
                name: "UK_Material_Combo",
                table: "Material");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBatch_RemainingWeight",
                table: "InventoryBatch");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkOrder",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Warehouse",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SupplierProfile",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SubcontractReturnItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SubcontractOrder",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StandardGradeMapping",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SalesOrder",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RefreshToken",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PurchaseSemiPlan",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PurchaseOrder",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PurchaseFinishedPlan",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProductionStandard",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OrderItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OrderChangeNotification",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Material",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "InventoryPlan",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "InventoryBatch",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CustomerProfile",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "UK_Supplier_Code",
                table: "SupplierProfile",
                column: "SupplierCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_OrderItem_Sequence_Active",
                table: "OrderItem",
                columns: new[] { "SalesOrderId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_Material_Code",
                table: "Material",
                column: "MaterialCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_Material_Combo",
                table: "Material",
                columns: new[] { "MaterialCategory", "PlantGrade", "Specification" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_RemainingWeight",
                table: "InventoryBatch",
                column: "RemainingWeight",
                filter: "[RemainingWeight] > 0 AND [IsDeleted] = 0");
        }
    }
}
