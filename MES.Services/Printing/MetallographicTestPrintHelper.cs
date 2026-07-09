using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class MetallographicTestPrintHelper
{
    public static byte[] GenerateBatchPdf(List<MetallographicTestDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("金相检验列表", items, columns, null);
    }
}
