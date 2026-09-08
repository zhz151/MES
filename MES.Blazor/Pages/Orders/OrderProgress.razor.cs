using Microsoft.AspNetCore.Components;
using MES.Core.Constants;
using MES.Core.DTOs.Order;
using MES.Core.Helpers;
using MES.Blazor.Helpers;
using MES.Blazor.Services;

namespace MES.Blazor.Pages.Orders;

/// <summary>
/// 订单进度树：一级=订单号，二级=订单号+主号，三级=四阶段分支，四级=叶子重量(kg)
/// 头部信息两行化：订单头=订单号/签订/业务员/客户/交期截止/延期罚款/总重/项次数；
/// 主号头行1=主号号(X01)+规格要素，行2=执行关注/紧急性/预计完成；二级按主号从小到大混排（含完结）。
/// </summary>
public partial class OrderProgress
{
    [Inject] private OrderProgressService Service { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private string? _orderNo;
    private OrderProgressTreeDto? _tree;
    private bool _loading = true;

    /// <summary>已折叠主号集合（默认仅完结主号折叠，其余展开；点击主号标题行切换）</summary>
    private readonly HashSet<string> _collapsedMains = new(StringComparer.Ordinal);

    protected override async Task OnInitializedAsync()
    {
        _orderNo = ReadQuery("salesOrderNo");
        if (!string.IsNullOrWhiteSpace(_orderNo))
        {
            var resp = await Service.GetAsync(_orderNo!);
            if (resp.Success)
                _tree = resp.Data;
        }

        // 执行关注=已完结（ScheduleStage==1，IsCompleted）的主号默认折叠，避免刷屏（用户拍板）
        if (_tree?.MainNos is { } mains)
        {
            foreach (var main in mains.Where(m => m.IsCompleted))
                _collapsedMains.Add(main.ProductionMainNo);
        }

        _loading = false;
    }

    private void Back() => Nav.NavigateTo("/orders");

    private bool IsCollapsed(string mainNo) => _collapsedMains.Contains(mainNo);

    private void ToggleMain(string mainNo)
    {
        if (!_collapsedMains.Add(mainNo))
            _collapsedMains.Remove(mainNo);
    }

    private string? ReadQuery(string key)
    {
        var query = Nav.ToAbsoluteUri(Nav.Uri).Query;
        if (string.IsNullOrEmpty(query))
            return null;
        var pair = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .FirstOrDefault(a => a.Length == 2 && string.Equals(a[0], key, StringComparison.OrdinalIgnoreCase));
        return pair is null ? null : Uri.UnescapeDataString(pair[1]);
    }

    /// <summary>主号分支按固定语义序：原料锁定 → 生产执行 → 成品检验 → 成品入库（完结主号仅成品入库）</summary>
    private static IEnumerable<MainProgressBranchDto> BranchesOf(OrderMainProgressDto main)
    {
        if (main.RawMaterialLock != null) yield return main.RawMaterialLock;
        if (main.Production != null) yield return main.Production;
        if (main.FinalInspection != null) yield return main.FinalInspection;
        if (main.Warehousing != null) yield return main.Warehousing;
    }

    private static string StageText(int stage) => IntStatusDisplayHelper.GetScheduleStageText(stage);

    private static string? UrgencyText(string? key) => UrgencyLevelKeys.ToChinese(key);

    /// <summary>执行关注色 class（语义沿用原 chip 配色：暂停红/原料锁橙/生产蓝/成检靛）</summary>
    private static string StageTextClass(int stage) => stage switch
    {
        0 => "op-v-pause",
        2 => "op-v-lock",
        3 => "op-v-prod",
        4 => "op-v-inspect",
        _ => "",
    };

    /// <summary>紧急性色 class（A+红/A橙/B蓝）</summary>
    private static string UrgencyClass(string? key) => key switch
    {
        UrgencyLevelKeys.APlusUrgent => "op-v-u-aplus",
        UrgencyLevelKeys.AUrgent => "op-v-u-a",
        UrgencyLevelKeys.BOrder => "op-v-u-b",
        _ => "",
    };

    /// <summary>长度状态中文（存枚举名字符串）</summary>
    private static string LengthText(string? lengthStatus) => DisplayHelper.GetLengthStatusText(lengthStatus);

    /// <summary>交货状态中文（存枚举名字符串）</summary>
    private static string DeliveryStateText(string? deliveryState) => DisplayHelper.GetDeliveryStateText(deliveryState);

    /// <summary>日期 yyyy-MM-dd</summary>
    private static string FmtDate(DateTime? value) => DisplayHelper.FormatNullableDate(value);

    /// <summary>延期罚款 有/无</summary>
    private static string PenaltyText(bool value) => value ? "有" : "无";

    /// <summary>重量整数化显示（去小数位，列表页 §10.7 同款截断口径）</summary>
    private static string FmtKg(decimal kg) => DisplayHelper.FormatDecimalAsInt(kg);
}
