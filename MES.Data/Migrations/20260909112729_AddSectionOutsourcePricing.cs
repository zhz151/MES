using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionOutsourcePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PricingUnit",
                table: "SectionOutsource",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "SectionOutsource",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "SectionOutsource",
                type: "decimal(18,4)",
                nullable: true);

            // 幂等回填存量（仅当单价为空，不覆盖手输价）：
            // - 厂外行（IsInternal=0）：计价单位=元/Kg；单价按工段默认——冷轧拔(ColdRollDraw)=1.4 元/Kg，其余工段=0.8 元/Kg；
            //   总价=ROUND(重量×单价,2)，仅当发出重量>0 才计算（无重量留空）。
            // - 厂内行（IsInternal=1，虚拟发外本厂车间）：无价，三列保持 NULL。
            // 默认价常量与 MES.Core/Helpers/MaterialPricingDefaults.cs 人工一致（ColdRollDrawSectionUnitPrice/OtherSectionUnitPrice），勿单改一处。
            migrationBuilder.Sql(@"
UPDATE ""SectionOutsource"" SET
    ""PricingUnit"" = 'PerKg',
    ""UnitPrice"" = CASE WHEN ""SectionName"" = 'ColdRollDraw' THEN 1.4 ELSE 0.8 END,
    ""TotalAmount"" = CASE
        WHEN ""SendWeight"" > 0 THEN ROUND(
            CAST(""SendWeight"" AS decimal(18,3)) * (CASE WHEN ""SectionName"" = 'ColdRollDraw' THEN 1.4 ELSE 0.8 END), 2)
        ELSE NULL
    END
WHERE ""IsInternal"" = 0 AND ""UnitPrice"" IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PricingUnit",
                table: "SectionOutsource");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "SectionOutsource");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "SectionOutsource");
        }
    }
}
