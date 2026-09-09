using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseAndSubcontractPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PricingUnit",
                table: "SubcontractReturnItem",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingUnit",
                table: "PurchaseOrder",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // 幂等回填存量（仅当单价为空，不覆盖手输价）：
            // - 采购：计价单位=元/Kg；荒管族(RoughTube/DefectRoughTube)=18 元/kg；其余成品族=26 元/kg；
            //   总价=ROUND(重量×单价,2)（元/kg 口径，非支数）。
            // - 委外明细：计价单位=元/Kg；加工单价=1.2 元/kg（圆棒穿孔）；加工总价=ROUND(需求重量×1.2,2)。
            // 默认价常量与 MES.Core/Helpers/MaterialPricingDefaults.cs 人工一致，勿单改一处。
            migrationBuilder.Sql(@"
UPDATE ""PurchaseOrder"" SET
    ""PricingUnit"" = 'PerKg',
    ""UnitPrice"" = CASE
        WHEN ""MaterialCategory"" IN ('RoughTube','DefectRoughTube') THEN 18
        WHEN ""MaterialCategory"" IN ('Finished','OrderFinished','CriticalFinished','DefectFinished','SpecialDeliveryStatus') THEN 26
        ELSE ""UnitPrice""
    END,
    ""TotalAmount"" = ROUND(
        CAST(""Weight"" AS decimal(18,3)) * CASE
            WHEN ""MaterialCategory"" IN ('RoughTube','DefectRoughTube') THEN 18
            WHEN ""MaterialCategory"" IN ('Finished','OrderFinished','CriticalFinished','DefectFinished','SpecialDeliveryStatus') THEN 26
            ELSE ""UnitPrice""
        END, 2)
WHERE ""UnitPrice"" IS NULL;

UPDATE ""SubcontractReturnItem"" SET
    ""PricingUnit"" = 'PerKg',
    ""ProcessUnitPrice"" = 1.2,
    ""ProcessTotalAmount"" = ROUND(
        CAST(COALESCE(""RequiredWeight"", 0) AS decimal(18,4)) * 1.2, 2)
WHERE ""ProcessUnitPrice"" IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PricingUnit",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "PricingUnit",
                table: "PurchaseOrder");
        }
    }
}
