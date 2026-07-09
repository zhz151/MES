using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class ChemicalAnalysisPrintHelper
{
    public static byte[] GenerateBatchPdf(List<ChemicalAnalysisDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("化学检验列表", items, columns, null);
    }
}
