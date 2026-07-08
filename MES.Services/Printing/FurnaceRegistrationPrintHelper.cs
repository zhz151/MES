using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Services.Printing;

public static class FurnaceRegistrationPrintHelper
{
    private static readonly Dictionary<string, Func<object?, string>> ValueResolvers = new()
    {
        ["RawMaterialType"] = v => v?.ToString() is { Length: > 0 } s && Enum.TryParse<RawMaterialType>(s, out var rmt)
            ? EnumHelper.GetDisplayName(rmt) : (v?.ToString() ?? "")
    };

    public static byte[] GenerateBatchPdf(List<FurnaceRegistrationDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("来料炉号登记列表", items, columns, ValueResolvers);
    }
}
