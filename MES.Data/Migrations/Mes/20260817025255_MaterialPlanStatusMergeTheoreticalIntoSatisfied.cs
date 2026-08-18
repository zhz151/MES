using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations.Mes
{
    /// <inheritdoc />
    public partial class MaterialPlanStatusMergeTheoreticalIntoSatisfied : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 取消"理论满足"(原值2)并入"满足"：存量 int 值 >2 减 1（原2理论满足→2满足、原3满足→2满足、原4超量→3超量）
            migrationBuilder.Sql("""
                UPDATE [WorkOrder] SET [MaterialPlanStatus] = CASE WHEN [MaterialPlanStatus] > 2 THEN [MaterialPlanStatus] - 1 ELSE [MaterialPlanStatus] END;
                UPDATE [WorkOrderListSummary] SET
                    [MaterialPlanStatus] = CASE WHEN [MaterialPlanStatus] > 2 THEN [MaterialPlanStatus] - 1 ELSE [MaterialPlanStatus] END,
                    [MainNoMaterialPlanStatus] = CASE WHEN [MainNoMaterialPlanStatus] > 2 THEN [MainNoMaterialPlanStatus] - 1 ELSE [MainNoMaterialPlanStatus] END,
                    [OrderMaterialPlanStatus] = CASE WHEN [OrderMaterialPlanStatus] > 2 THEN [OrderMaterialPlanStatus] - 1 ELSE [OrderMaterialPlanStatus] END;
                UPDATE [WorkOrderExecutionSummary] SET
                    [MaterialPlanStatus] = CASE WHEN [MaterialPlanStatus] > 2 THEN [MaterialPlanStatus] - 1 ELSE [MaterialPlanStatus] END,
                    [MainNoMaterialPlanStatus] = CASE WHEN [MainNoMaterialPlanStatus] > 2 THEN [MainNoMaterialPlanStatus] - 1 ELSE [MainNoMaterialPlanStatus] END;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向：int 值 >1 加 1 恢复旧编号体系（0未计划/1部分/2空置/3满足/4超量）；"理论满足"档位已取消，无法还原语义
            migrationBuilder.Sql("""
                UPDATE [WorkOrder] SET [MaterialPlanStatus] = CASE WHEN [MaterialPlanStatus] > 1 THEN [MaterialPlanStatus] + 1 ELSE [MaterialPlanStatus] END;
                UPDATE [WorkOrderListSummary] SET
                    [MaterialPlanStatus] = CASE WHEN [MaterialPlanStatus] > 1 THEN [MaterialPlanStatus] + 1 ELSE [MaterialPlanStatus] END,
                    [MainNoMaterialPlanStatus] = CASE WHEN [MainNoMaterialPlanStatus] > 1 THEN [MainNoMaterialPlanStatus] + 1 ELSE [MainNoMaterialPlanStatus] END,
                    [OrderMaterialPlanStatus] = CASE WHEN [OrderMaterialPlanStatus] > 1 THEN [OrderMaterialPlanStatus] + 1 ELSE [OrderMaterialPlanStatus] END;
                UPDATE [WorkOrderExecutionSummary] SET
                    [MaterialPlanStatus] = CASE WHEN [MaterialPlanStatus] > 1 THEN [MaterialPlanStatus] + 1 ELSE [MaterialPlanStatus] END,
                    [MainNoMaterialPlanStatus] = CASE WHEN [MainNoMaterialPlanStatus] > 1 THEN [MainNoMaterialPlanStatus] + 1 ELSE [MainNoMaterialPlanStatus] END;
            """);
        }
    }
}
