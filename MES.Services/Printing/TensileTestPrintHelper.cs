using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class TensileTestPrintHelper
{
    public static byte[] GenerateBatchPdf(List<TensileTestDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("室温拉伸检验列表", items, columns, null);
    }
}
