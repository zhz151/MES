using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class ProcessInspectionPrintHelper
{
    private static readonly Dictionary<string, Func<object?, string>> ValueResolvers = new()
    {
        ["DataSource"] = v => v?.ToString() switch
        {
            "SCAN" => "扫码报工",
            "MANUAL" => "手动录入",
            _ => v?.ToString() ?? ""
        }
    };

    public static byte[] GenerateBatchPdf(List<ProcessInspectionDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("过程检验列表", items, columns, ValueResolvers);
    }
}
