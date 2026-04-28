using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMaterialPlanStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MaterialPlanStatus 枚举数值变更：
            // 旧: NotPlanned=0 Partial=1 Satisfied=2 Excess=3
            // 新: NotPlanned=0 Partial=1 TheoreticalSatisfied=2 Satisfied=3 Excess=4
            // 需将原 Satisfied(2) → 3, 原 Excess(3) → 4
            migrationBuilder.Sql("UPDATE WorkOrder SET MaterialPlanStatus = 3 WHERE MaterialPlanStatus = 2");
            migrationBuilder.Sql("UPDATE WorkOrder SET MaterialPlanStatus = 4 WHERE MaterialPlanStatus = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：将 3→2(Satisfied), 4→3(Excess)
            migrationBuilder.Sql("UPDATE WorkOrder SET MaterialPlanStatus = 2 WHERE MaterialPlanStatus = 3");
            migrationBuilder.Sql("UPDATE WorkOrder SET MaterialPlanStatus = 3 WHERE MaterialPlanStatus = 4");
        }
    }
}
