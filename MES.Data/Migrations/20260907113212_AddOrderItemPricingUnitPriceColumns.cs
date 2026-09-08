using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemPricingUnitPriceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PricingUnit",
                table: "OrderItem",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPrice",
                table: "OrderItem",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "OrderItem",
                type: "decimal(18,2)",
                nullable: true);

            // 回填存量模拟数据（仅仿真，非业务口径）：计价单位=元/Kg；
            // 单价按工厂牌号规则（含"316"→30、以"2"开头→35、其它→25）；
            // 总价=ROUND(单价×合同重量, 2)。新建项次由人工填写，不套用本规则。
            migrationBuilder.Sql(@"
UPDATE ""OrderItem"" SET
    ""PricingUnit"" = 'PerKg',
    ""UnitPrice"" = CASE
        WHEN ""PlantGrade"" LIKE '%316%' THEN 30
        WHEN ""PlantGrade"" LIKE '2%' THEN 35
        ELSE 25
    END,
    ""TotalPrice"" = ROUND(
        CAST(""ContractWeight"" AS decimal(18,3)) * CASE
            WHEN ""PlantGrade"" LIKE '%316%' THEN 30
            WHEN ""PlantGrade"" LIKE '2%' THEN 35
            ELSE 25
        END, 2);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PricingUnit",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "TotalPrice",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "OrderItem");
        }
    }
}
