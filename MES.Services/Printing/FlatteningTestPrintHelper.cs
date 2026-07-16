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

public static class FlatteningTestPrintHelper
{
    public static byte[] GenerateBatchPdf(List<FlatteningTestDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("压扁检验列表", items, columns, null);
    }
}
