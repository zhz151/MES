using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Services.Printing;

public static class EmployeePrintHelper
{
    public static byte[] GenerateBatchPdf(List<EmployeeDto> items, List<PrintColumnDef> columns)
    {
        // SectionName/GroupName 逗号分隔英文 Key 串 → 中文；成检项目为逗号分隔检验项目枚举名串 → 中文；
        // 成检到料为布尔开关 → 是/否
        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["SectionName"] = v => v is string s && !string.IsNullOrWhiteSpace(s)
                ? string.Join("、", s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => SectionKeys.ToChinese(x.Trim()) ?? x.Trim()))
                : string.Empty,
            ["GroupName"] = v => v is string s && !string.IsNullOrWhiteSpace(s)
                ? string.Join("、", s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => ProcessKeys.ToChinese(x.Trim()) ?? x.Trim()))
                : string.Empty,
            ["InspectionItems"] = v => v is string s ? FormatInspectionItemList(s) : string.Empty,
            ["MaterialReceiveCheckItems"] = v => v is bool b ? (b ? "是" : "否") : string.Empty
        };
        return TablePrintHelper.GeneratePdf("员工信息列表", items, columns, resolvers);
    }

    /// <summary>InspectionItem 枚举名逗号串 → 中文（"、 " 连接），未识别项原样保留</summary>
    private static string FormatInspectionItemList(string value)
        => string.Join("、", value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Enum.TryParse<InspectionItem>(x.Trim(), out var item) ? EnumHelper.GetDisplayName(item) : x.Trim()));
}
