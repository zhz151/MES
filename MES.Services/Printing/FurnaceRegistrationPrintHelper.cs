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
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Services.Printing;

public static class FurnaceRegistrationPrintHelper
{
    private static readonly Dictionary<string, Func<object?, string>> ValueResolvers = new()
    {
        ["RawMaterialType"] = v => v is MaterialType rmt ? EnumHelper.GetDisplayName(rmt) : (v?.ToString() ?? "")
    };

    public static byte[] GenerateBatchPdf(List<FurnaceRegistrationDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("来料炉号登记列表", items, columns, ValueResolvers);
    }
}
