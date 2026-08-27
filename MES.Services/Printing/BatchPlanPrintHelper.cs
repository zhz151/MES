using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;
namespace MES.Services.Printing;

public static class BatchPlanPrintHelper
{
    public static byte[] GeneratePdf(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["LengthStatus"] = v => EnumHelper.GetDisplayName<LengthStatus>(v?.ToString()),
        };
        // 批次计划打印样式：按数据内容自适应列宽、整页宽度铺满、表头行数不限、数据居中
        return TablePrintHelper.GeneratePdf(title, items, columns, resolvers,
            autoWidth: true, alignCenter: true, headerMaxLines: 0);
    }
}
