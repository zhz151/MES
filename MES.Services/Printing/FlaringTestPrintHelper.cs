using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class FlaringTestPrintHelper
{
    public static byte[] GenerateBatchPdf(List<FlaringTestDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("扩口检验列表", items, columns, null);
    }
}
