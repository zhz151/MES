using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class PittingCorrosionTestPrintHelper
{
    public static byte[] GenerateBatchPdf(List<PittingCorrosionTestDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("点腐蚀检验列表", items, columns, null);
    }
}
