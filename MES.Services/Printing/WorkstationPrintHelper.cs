using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Shared;

namespace MES.Services.Printing;

public static class WorkstationPrintHelper
{
    public static byte[] GenerateBatchPdf(List<WorkstationDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("工位信息列表", items, columns);
    }
}
