using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using MES.Blazor.Services;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Reports;

/// <summary>
/// 物料进出存报表：行=库房×物料类型（库房合并单元格），列=期初 + 12月 + 进出汇总/实时库存。
/// 4 报表切换（入库/出库/库存/物料进出存），同一数据集仅展示列不同；
/// 结存为真实库存余额（全口径）。入库报表按「入库来源」展开、出库报表按「出库类型」展开，
/// 均为 库房×物料类型×来源/类型 粒度，并附「物料汇总(t)」合并列。
/// 当前月份之后的月份尚未发生，单元格留空；物料进出存(inout) 每月格=彩带入x/出y（绿入/红出），
/// 末尾两列：进出汇总（全年入/出流量）+ 实时库存（只显示当前库存量）。
/// </summary>
public partial class MonthlyStock
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;

    private bool _loading;

    /// <summary>当前报表：in=入库 / out=出库 / stock=库存 / inout=物料进出存</summary>
    private string _reportType = "inout";

    /// <summary>当前月份 0 基索引（1月=0，8月=7）；后续月份尚未发生，单元格留空</summary>
    private readonly int _currentMonthIndex = DateTime.Today.Month - 1;

    private MonthlyStockSummaryResultDto? _result;
    private List<MonthlyStockRowDto> _rows = new();

    /// <summary>行对齐库房合并单元格 rowspan：>0=该库房首行（rowspan 值），0=被合并隐藏</summary>
    private List<int> _warehouseRowspans = new();

    /// <summary>行对齐物料类型合并单元格 rowspan（入库/出库报表粒度行专用）：>0=该物料首行，0=被合并隐藏</summary>
    private List<int> _materialRowspans = new();

    /// <summary>行对齐物料类型汇总值（入库/出库报表粒度行专用）：仅该物料首行非 0，=该物料类型全年合计（入/出）</summary>
    private List<decimal> _materialTotals = new();

    private static readonly List<(string Value, string Label)> ReportOptions = new()
    {
        ("in", "入库报表"),
        ("out", "出库报表"),
        ("stock", "库存报表"),
        ("inout", "物料进出存报表")
    };

    private string CurrentTitle => ReportOptions.First(o => o.Value == _reportType).Label;

    /// <summary>末尾合计列表头（物料进出存报表不走此列——表头渲染「进出汇总」+「实时库存」两列）</summary>
    private string TotalHeader => _reportType switch
    {
        "in" => "来源汇总(t)",
        "out" => "出库类型汇总(t)",
        "stock" => "实时结存",
        _ => "实时结存"
    };

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        StateHasChanged();
        try
        {
            var response = await InventorySvc.GetMonthlyStockSummaryAsync();
            if (response.Success && response.Data != null)
            {
                _result = response.Data;
                ApplyRows();
            }
            else
            {
                _result = null;
                ResetRows();
                Snackbar.Add(response.Message ?? "加载失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _result = null;
            ResetRows();
            Snackbar.Add($"加载异常: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private void OnReportTypeChanged(string v)
    {
        _reportType = v;
        if (_result != null) ApplyRows();
        StateHasChanged();
    }

    /// <summary>按当前报表类型切换数据源（in→来源粒度 / out→类型粒度 / 其余→全口径）并重算合并单元格</summary>
    private void ApplyRows()
    {
        _rows = _reportType switch
        {
            "in" => _result?.InboundSourceRows ?? new(),
            "out" => _result?.OutboundTypeRows ?? new(),
            _ => _result?.Rows ?? new()
        };
        ComputeRowspans();
    }

    private void ResetRows()
    {
        _rows = new();
        _warehouseRowspans = new();
        _materialRowspans = new();
        _materialTotals = new();
    }

    /// <summary>
    /// 计算合并单元格 rowspan（后端已按库房固定顺序→物料类型固定顺序→来源/类型固定顺序排序，同组相邻）。
    /// 全口径行仅库房合并；入库/出库粒度行再叠加物料类型合并 + 物料类型全年汇总值（首行非 0）。
    /// </summary>
    private void ComputeRowspans()
    {
        _warehouseRowspans = new List<int>(new int[_rows.Count]);
        _materialRowspans = new List<int>(new int[_rows.Count]);
        _materialTotals = new List<decimal>(new decimal[_rows.Count]);

        var granular = _reportType == "in" || _reportType == "out";
        var i = 0;
        while (i < _rows.Count)
        {
            var whName = _rows[i].WarehouseName;
            var whCount = 1;
            while (i + whCount < _rows.Count
                   && string.Equals(_rows[i + whCount].WarehouseName, whName, StringComparison.OrdinalIgnoreCase))
                whCount++;
            _warehouseRowspans[i] = whCount;

            if (granular)
            {
                var j = i;
                var whEnd = i + whCount;
                while (j < whEnd)
                {
                    var mat = _rows[j].MaterialType;
                    var matCount = 1;
                    while (j + matCount < whEnd
                           && string.Equals(_rows[j + matCount].MaterialType, mat, StringComparison.OrdinalIgnoreCase))
                        matCount++;
                    _materialRowspans[j] = matCount;
                    decimal matTotal = 0m;
                    for (var k = j; k < j + matCount; k++)
                        matTotal += _reportType == "in" ? _rows[k].TotalIn : _rows[k].TotalOut;
                    _materialTotals[j] = matTotal;
                    j += matCount;
                }
            }

            i += whCount;
        }
    }

    // ========== 显示 ==========

    private static string DisplayMaterial(MonthlyStockRowDto r)
        => string.IsNullOrEmpty(r.MaterialType) ? "-" : DisplayHelper.GetMaterialTypeText(r.MaterialType);

    private static string DisplayInboundSource(MonthlyStockRowDto r)
        => string.IsNullOrEmpty(r.InboundSource) ? "-" : DisplayHelper.GetInboundSourceText(r.InboundSource);

    private static string DisplayOutboundType(MonthlyStockRowDto r)
        => string.IsNullOrEmpty(r.OutboundType) ? "-" : DisplayHelper.GetOutboundTypeText(r.OutboundType);

    /// <summary>单值重量格式化（kg/1000 显示 t，F1，0 值留空；负数仍显示）</summary>
    private static string FormatWeight(decimal kg)
        => kg != 0m ? (kg / 1000m).ToString("F1") : string.Empty;

    /// <summary>单月格按当前月份与报表类型渲染：未来月留空；inout 走彩带入x/出y，其余单值吨。</summary>
    private MarkupString MonthCell(int m, MonthlyStockRowDto r)
    {
        if (m > _currentMonthIndex) return new MarkupString(string.Empty);
        var mv = r.Months[m];
        return _reportType switch
        {
            "in" => new MarkupString(FormatWeight(mv.In)),
            "out" => new MarkupString(FormatWeight(mv.Out)),
            "stock" => new MarkupString(FormatWeight(mv.Closing)),
            _ => BuildFlowMarkup(mv.In, mv.Out)
        };
    }

    /// <summary>末尾合计列文本（非 inout 报表单值吨；inout 报表末尾为「进出汇总」彩带 +「实时库存」两列，不走此方法）</summary>
    private string FooterCellText(MonthlyStockRowDto r) => _reportType switch
    {
        "in" => FormatWeight(r.TotalIn),
        "out" => FormatWeight(r.TotalOut),
        "stock" => FormatWeight(r.ClosingWeight),
        _ => string.Empty
    };

    /// <summary>
    /// 彩带入x/出y（kg→t，F1；0 侧省略、两侧皆 0 返回空）。「入」浅绿底深绿字、「出」浅红底深红字，
    /// 内联样式保证打印（getTableHtml 抓取 DOM）与屏幕显示一致。例：入80/出15。
    /// </summary>
    private static MarkupString BuildFlowMarkup(decimal inKg, decimal outKg)
    {
        var inT = inKg > 0m ? (inKg / 1000m).ToString("F1") : null;
        var outT = outKg > 0m ? (outKg / 1000m).ToString("F1") : null;
        if (inT == null && outT == null) return new MarkupString(string.Empty);
        var html = string.Empty;
        if (inT != null)
            html += "<span style=\"color:#1e7e34;background:#e6f4ea;font-weight:600;border-radius:3px;padding:0 4px;\">入" + inT + "</span>";
        if (inT != null && outT != null)
            html += "<span style=\"color:#9e9e9e;padding:0 2px;\">/</span>";
        if (outT != null)
            html += "<span style=\"color:#c62828;background:#fdecea;font-weight:600;border-radius:3px;padding:0 4px;\">出" + outT + "</span>";
        return new MarkupString(html);
    }

    // ========== 打印 ==========

    private async Task Print()
    {
        if (_rows.Count == 0)
        {
            Snackbar.Add("暂无数据可打印", Severity.Warning);
            return;
        }
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#monthly-stock-table-wrap");
            if (!string.IsNullOrEmpty(html))
            {
                // 横向 A4 + 表格撑满页宽（table-layout:fixed + white-space:normal），列总宽度不超过单页界限
                var printHtml = "<style>" +
                    "table{width:100%!important;table-layout:fixed!important;font-size:12px!important;border-collapse:collapse!important;}" +
                    "th,td{white-space:normal!important;padding:3px 4px!important;text-align:center!important;border:1px solid #333!important;}" +
                    "</style>" + html;
                await JS.InvokeVoidAsync("printRawHtml", printHtml, CurrentTitle, "landscape");
            }
            else
            {
                Snackbar.Add("未找到可打印的库存报表表格", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }
}
