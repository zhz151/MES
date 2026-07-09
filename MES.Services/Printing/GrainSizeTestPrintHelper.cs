using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class GrainSizeTestPrintHelper
{
    public static byte[] GenerateBatchPdf(List<GrainSizeTestDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("晶粒度检验列表", items, columns, null);
    }
}
