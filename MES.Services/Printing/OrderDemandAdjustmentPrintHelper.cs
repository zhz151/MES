using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;
namespace MES.Services.Printing;

public static class OrderDemandAdjustmentPrintHelper
{
    public static byte[] GeneratePdf(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["SettlementMethod"] = v => EnumHelper.GetDisplayName<SettlementMethod>(v?.ToString()),
            ["LengthStatus"] = v => EnumHelper.GetDisplayName<LengthStatus>(v?.ToString()),
        };
        return TablePrintHelper.GeneratePdf(title, items, columns, resolvers);
    }
}
