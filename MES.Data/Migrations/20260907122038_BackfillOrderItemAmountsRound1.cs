using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOrderItemAmountsRound1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 项次金额字段上线前存量收敛（应用层已收敛为 1 位小数，本次校正存量数据）：
            // 1) 米数/合同重量/理算重量 统一四舍五入保留 1 位小数；
            // 2) 总价按计价单位取量重算 = ROUND(单价 × 取量, 2)（单价为空的行不重算）。
            //    PerKg→合同重量、PerMeter→米数、PerPiece→支数（数量为整数）。
            migrationBuilder.Sql(@"
UPDATE ""OrderItem"" SET
    ""Meters"" = ROUND(""Meters"", 1),
    ""ContractWeight"" = ROUND(""ContractWeight"", 1),
    ""TheoreticalWeight"" = ROUND(""TheoreticalWeight"", 1);

UPDATE ""OrderItem"" SET ""TotalPrice"" = CASE
    WHEN ""UnitPrice"" IS NULL THEN ""TotalPrice""
    WHEN ""PricingUnit"" = 'PerMeter' THEN ROUND(""UnitPrice"" * ROUND(COALESCE(""Meters"", 0), 1), 2)
    WHEN ""PricingUnit"" = 'PerPiece' THEN ROUND(""UnitPrice"" * COALESCE(""Quantity"", 0), 2)
    ELSE ROUND(""UnitPrice"" * ROUND(""ContractWeight"", 1), 2)
END
WHERE ""UnitPrice"" IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
