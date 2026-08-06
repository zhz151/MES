using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Constants;
using MES.Core.Helpers;

namespace MES.Services.Printing;

public static class ProcessInspectionPrintHelper
{
    private static readonly Dictionary<string, Func<object?, string>> ValueResolvers = new()
    {
        ["DataSource"] = v => StringEnumDisplayHelper.GetDataSourceText(v?.ToString())
    };

    public static byte[] GenerateBatchPdf(List<ProcessInspectionDto> items, List<PrintColumnDef> columns,
        IReadOnlyDictionary<string, string>? processNameMap = null)
    {
        var resolvers = new Dictionary<string, Func<object?, string>>(ValueResolvers);
        if (processNameMap != null)
            resolvers["ProcessName"] = v => ProcessDisplayText(v?.ToString(), processNameMap);
        return TablePrintHelper.GeneratePdf("过程检验列表", items, columns, resolvers);
    }

    /// <summary>工序 Key/中文 → 打印显示中文（配置表 map 优先，ProcessKeys 兜底）</summary>
    private static string ProcessDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? processNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && processNameMap != null && processNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return ProcessKeys.ToChinese(keyOrName) ?? "";
    }
}
