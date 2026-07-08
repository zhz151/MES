using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Services.Printing;

public static class FinalInspectionPrintHelper
{
    private static readonly Dictionary<string, Func<object?, string>> ValueResolvers = new()
    {
        ["InspectionItem"] = v => v switch
        {
            InspectionItem.PMIInspection => "PMI检验",
            InspectionItem.VisualInspection => "表检",
            InspectionItem.Dimension => "尺寸",
            InspectionItem.Endoscopy => "内窥",
            InspectionItem.HydrostaticPressure => "水压",
            InspectionItem.UnderwaterPneumatic => "水下气压",
            InspectionItem.EddyCurrent => "涡流",
            InspectionItem.Ultrasonic => "超声波",
            InspectionItem.PortColoring => "端口着色",
            _ => v?.ToString() ?? ""
        },
        ["ProductionType"] = v => v?.ToString() is { Length: > 0 } s && Enum.TryParse<ProductionType>(s, out var pt)
            ? EnumHelper.GetDisplayName(pt) : (v?.ToString() ?? ""),
        ["DataSource"] = v => v?.ToString() switch
        {
            "SCAN" => "扫码报工",
            "MANUAL" => "手动录入",
            _ => v?.ToString() ?? ""
        }
    };

    public static byte[] GenerateBatchPdf(List<FinalInspectionDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("成品检验列表", items, columns, ValueResolvers);
    }
}
