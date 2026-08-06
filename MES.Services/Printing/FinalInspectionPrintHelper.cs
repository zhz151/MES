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
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Services.Printing;

public static class FinalInspectionPrintHelper
{
    private static readonly Dictionary<string, Func<object?, string>> ValueResolvers = new()
    {
        ["InspectionItem"] = v => v is InspectionItem item ? EnumHelper.GetDisplayName(item) : (v?.ToString() ?? ""),
        ["ProductionType"] = v => v is string s && !string.IsNullOrEmpty(s) && Enum.TryParse<ProductionType>(s, true, out var pt) ? EnumHelper.GetDisplayName(pt) : (v?.ToString() ?? ""),
        ["DataSource"] = v => StringEnumDisplayHelper.GetDataSourceText(v?.ToString()),
        ["LengthStatus"] = v => v is string s && !string.IsNullOrEmpty(s) && Enum.TryParse<LengthStatus>(s, true, out var ls) ? EnumHelper.GetDisplayName(ls) : (v?.ToString() ?? ""),
        ["DeliveryState"] = v => v is string s && !string.IsNullOrEmpty(s) && Enum.TryParse<DeliveryState>(s, true, out var ds) ? EnumHelper.GetDisplayName(ds) : (v?.ToString() ?? "")
    };

    public static byte[] GenerateBatchPdf(List<FinalInspectionDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("成品检验列表", items, columns, ValueResolvers);
    }
}
