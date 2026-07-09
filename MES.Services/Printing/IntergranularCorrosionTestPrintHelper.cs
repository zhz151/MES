using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class IntergranularCorrosionTestPrintHelper
{
    public static byte[] GenerateBatchPdf(List<IntergranularCorrosionTestDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("晶间腐蚀检验列表", items, columns, null);
    }
}
