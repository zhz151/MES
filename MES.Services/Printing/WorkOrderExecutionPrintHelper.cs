using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;
namespace MES.Services.Printing;

public static class WorkOrderExecutionPrintHelper
{
    // 列数超限校验统一在 TablePrintHelper.GeneratePdf（所有 Mode B 打印共用兜底）

    public static byte[] GeneratePdf(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["SettlementMethod"] = v => EnumHelper.GetDisplayName<SettlementMethod>(v?.ToString()),
            ["LengthStatus"] = v => EnumHelper.GetDisplayName<LengthStatus>(v?.ToString()),
            ["MaterialPlanStatus"] = v => EnumHelper.GetDisplayName<MaterialPlanStatus>(v?.ToString()),
            ["MainNoMaterialPlanStatus"] = v => EnumHelper.GetDisplayName<MaterialPlanStatus>(v?.ToString()),
        };
        return TablePrintHelper.GeneratePdf(title, items, columns, resolvers);
    }
}
