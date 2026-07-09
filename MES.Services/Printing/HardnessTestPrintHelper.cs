using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class HardnessTestPrintHelper
{
    public static byte[] GenerateBatchPdf(List<HardnessTestDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("硬度检验列表", items, columns, null);
    }
}
