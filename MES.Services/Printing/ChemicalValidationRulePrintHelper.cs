using MES.Core.DTOs;

namespace MES.Services.Printing;

public static class ChemicalValidationRulePrintHelper
{
    public static byte[] GenerateBatchPdf(List<ChemicalValidationRuleDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("牌号验证规则列表", items, columns);
    }
}
