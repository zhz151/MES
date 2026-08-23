using MES.Core.DTOs.Scheduling;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace MES.Blazor.Pages.Scheduling;

public partial class WorkOrderLoadOverview : ComponentBase
{
    [Inject] private Services.ProductionOverviewService OverviewService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Parameter] public string Title { get; set; } = "订单负荷总量";

    private ProductionOverviewDto? _data;
    private bool _loading;
    private string? _errorMessage;
    private DateTime _lastRefresh;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        _errorMessage = null;
        try
        {
            var response = await OverviewService.GetOverviewAsync();
            if (response.Success && response.Data != null)
            {
                _data = response.Data;
                _lastRefresh = response.Data.GeneratedTime;
            }
            else
            {
                _errorMessage = response.Message ?? "获取数据失败";
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏按表格列实际宽度对齐（复用 table-nav.js initGroupHeaders 测量逻辑）
        // 表格异步加载完成后才存在 DOM，因此每次渲染都调用（initGroupHeaders 内部防重复注册监听）
        await JS.InvokeVoidAsync("initGroupHeaders", "#workorder-load-overview-list-table");
    }

    private async Task PrintTable()
    {
        // 打印仅输出表格本身；分组标题栏因打印窗口按 11px 字号重新布局，与实测像素宽度无法对齐，故不打印（2026-08-19 用户决策）
        var table = await JS.InvokeAsync<string>("getTableHtml", "#workorder-overview-table");
        if (!string.IsNullOrEmpty(table))
            await JS.InvokeVoidAsync("printRawHtml", table, Title);
        else
            Console.Error.WriteLine("WorkOrderLoadOverview: 未找到可打印的表格");
    }

    private static string FormatTons(decimal? tons)
    {
        // 吨单位页内不显示；0 值显示 "-"
        return tons.HasValue && tons.Value > 0 ? $"{tons.Value:0}" : "-";
    }

    /// <summary>
    /// 待产量列格式：生产汇总行（按批次去重口径）数值加「(现周转)」后缀防误导；
    /// 冷轧5060/2030/三辊/冷拔行按待生产产类拆分附加量（如「230（中100/成130）」）；
    /// 其余行纯数值
    /// </summary>
    private static string FormatRemainingTons(OverviewRowDto row, decimal? tons)
    {
        if (!tons.HasValue || tons.Value <= 0) return "-";
        var val = $"{tons.Value:0}";
        if (row.IsSummary && row.Category == "投料-在产") return $"{val}(现周转)";
        if (row.PendingInProgressTons.HasValue || row.PendingFinishedTons.HasValue)
            return $"{val}（中{row.PendingInProgressTons!.Value:0}/成{row.PendingFinishedTons!.Value:0}）";
        return val;
    }

    private static string FormatDays(int? days)
    {
        return days.HasValue ? $"{days.Value}天" : "-";
    }

    private static string FormatDate(DateTime? date)
    {
        // 加 2 位简化年份（2026-08-19 用户决策），如 2026-11-10 → 26/11/10
        return date.HasValue ? date.Value.ToString("yy/M/d") : "-";
    }

    /// <summary>
    /// 日期桶格值：延期分类行显示「主值/料副值」斜杠式（如 80/料16），延期量行显示「主值[*副值]」括号式（如 87[*18]，星号红色标注超1周量），其余行单值；0 显示 "-"
    /// subOnly=true（订单延期-原料/在产/成检，2026-08-23 用户决策）：仅显示副值（如 118/待料89 → 89），去掉主值与前缀
    /// </summary>
    private static string FormatBucketTons(decimal tons, decimal? sub, string? prefix, bool parenFormat = false, bool subOnly = false)
    {
        if (subOnly)
            return sub.HasValue && sub.Value > 0 ? $"{sub.Value:0}" : "-";
        var main = tons > 0 ? $"{tons:0}" : "-";
        if (sub.HasValue && sub.Value > 0)
            return parenFormat
                ? $"{main}[<span style=\"color:#D32F2F;\">*</span>{sub:0}]"
                : $"{main}/{prefix}{sub:0}";
        return main;
    }

    /// <summary>层级序号（大类-行，如 1-1/2-3）；汇总行与总估算行留空</summary>
    private static string DisplaySeq(OverviewRowDto row)
    {
        if (row.IsSummary || row.CategoryNo == 0) return "";
        return $"{row.CategoryNo}-{row.RowNo}";
    }

    /// <summary>汇总行单元格样式：加粗 + 分类底色（原料浅蓝/投料-在产浅绿/投料-成检浅橙）</summary>
    private static string CellClass(OverviewRowDto row, string baseClass)
    {
        if (!row.IsSummary) return baseClass;
        var summaryClass = row.Category switch
        {
            "原料" => "summary-raw",
            "投料-在产" => "summary-prod",
            "投料-成检" => "summary-fi",
            _ => "summary-cell"
        };
        return string.IsNullOrEmpty(baseClass)
            ? $"summary-cell {summaryClass}"
            : $"{baseClass} summary-cell {summaryClass}";
    }
}
