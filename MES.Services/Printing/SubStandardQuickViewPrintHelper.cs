
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.ProductionStandard;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using SubStandardQuickViewDto = MES.Core.DTOs.ProductionStandard.SubStandardQuickViewDto;
namespace MES.Services.Printing;

public static class SubStandardQuickViewPrintHelper
{
    public static byte[] GenerateBatchPdf(List<SubStandardQuickViewDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("子标准速览列表", items, columns);
    }
}
