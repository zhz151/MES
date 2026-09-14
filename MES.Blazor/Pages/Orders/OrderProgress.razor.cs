using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MES.Core.DTOs.Order;
using MES.Blazor.Services;

namespace MES.Blazor.Pages.Orders;

/// <summary>
/// 订单进度页 /orders/progress：按 ?salesOrderNo= 取数 + 页头动作（打印 / 返回订单列表）。
/// 树本体（含折叠与「投料产出」派生）已抽为 <c>Shared/OrderProgressTree.razor</c>，与首页「订单进度查询」卡共用；
/// 本页传 <c>Compact=false</c>，保留原「非完结主号默认展开、完结主号默认折叠」的行为。
/// </summary>
public partial class OrderProgress
{
    [Inject] private OrderProgressService Service { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private string? _orderNo;
    private OrderProgressTreeDto? _tree;
    private bool _loading = true;

    /// <summary>打印页脚日期（仅打印可见，点「打印」时刷新为当天）</summary>
    private DateTime _printDate = DateTime.Today;

    protected override async Task OnInitializedAsync()
    {
        _orderNo = ReadQuery("salesOrderNo");
        if (!string.IsNullOrWhiteSpace(_orderNo))
        {
            var resp = await Service.GetAsync(_orderNo!);
            if (resp.Success)
                _tree = resp.Data;
        }

        _loading = false;
    }

    private void Back() => Nav.NavigateTo("/orders");

    /// <summary>
    /// 打印当前所见（用户 2026-09-14 拍板）：就地 window.print + 页面内 @media print 规则，
    /// 折叠/展开状态原样输出（折叠主号的展开区在 DOM 中不存在，打印自然不会出现）。
    /// 打印前刷新页脚日期（.op-print-footer「打印日期：yyyy-MM-dd」，取打印当天）。
    /// </summary>
    private async Task OnPrintAsync()
    {
        _printDate = DateTime.Today;
        StateHasChanged();
        await Task.Yield();   // 让页脚日期先落到 DOM，再触发浏览器打印
        await JS.InvokeVoidAsync("window.print");
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
}
