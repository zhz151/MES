using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Shared;

namespace MES.Services.Printing;

public static class EmployeePrintHelper
{
    public static byte[] GenerateBatchPdf(List<EmployeeDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("员工信息列表", items, columns);
    }
}
