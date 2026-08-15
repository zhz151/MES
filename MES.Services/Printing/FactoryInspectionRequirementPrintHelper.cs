
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.StandardRegister;
using FactoryInspectionRequirementDto = MES.Core.DTOs.StandardRegister.FactoryInspectionRequirementDto;
namespace MES.Services.Printing;

public static class FactoryInspectionRequirementPrintHelper
{
    public static byte[] GenerateBatchPdf(List<FactoryInspectionRequirementDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("工厂检验项要求列表", items, columns);
    }
}
