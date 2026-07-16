using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;

namespace MES.Services.Printing;

public static class ProcessInspectionPrintHelper
{
    private static readonly Dictionary<string, Func<object?, string>> ValueResolvers = new()
    {
        ["DataSource"] = v => v?.ToString() switch
        {
            "SCAN" => "扫码报工",
            "MANUAL" => "手动录入",
            _ => v?.ToString() ?? ""
        }
    };

    public static byte[] GenerateBatchPdf(List<ProcessInspectionDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("过程检验列表", items, columns, ValueResolvers);
    }
}
