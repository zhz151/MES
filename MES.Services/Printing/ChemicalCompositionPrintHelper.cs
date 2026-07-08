using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class ChemicalCompositionPrintHelper
{
    public static byte[] GenerateBatchPdf(List<ChemicalCompositionDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("牌号化学成分列表", items, columns);
    }
}
