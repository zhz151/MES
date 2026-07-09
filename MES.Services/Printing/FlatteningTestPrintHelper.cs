using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class FlatteningTestPrintHelper
{
    public static byte[] GenerateBatchPdf(List<FlatteningTestDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("压扁检验列表", items, columns, null);
    }
}
