using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MES.Core.DTOs.Order;
using MES.Blazor.Services;

namespace MES.Blazor.Shared;

/// <summary>
/// 首页「订单进度查询」卡：卡内输入订单号 → 就地渲染订单进度树（非链接跳转）。
/// 数据源 <c>GET api/order/progress?salesOrderNo=</c>，该端点自 2026-09-14 起仅需登录（不带 OrderView 档），
/// 以满足「首页人人可查」（首页本身只有 [Authorize]，若挂角色档会让无订单权限的用户一查就 403）。
/// </summary>
public partial class OrderProgressQueryCard
{
    /// <summary>手机端壳（MobileLayout）下传 IsMobile=true；桌面端 MainLayout 不提供 → 默认 false。</summary>
    [CascadingParameter(Name = "IsMobile")]
    private bool IsMobile { get; set; }

    [Inject] private OrderProgressService Service { get; set; } = null!;

    private string? _orderNo;
    private OrderProgressTreeDto? _tree;
    private bool _loading;
    private bool _searched;

    private string CardClass => IsMobile ? "mh-card pa-3" : "pa-4 home-stack-card";
    private int CardElevation => IsMobile ? 0 : 1;
    private Typo TitleTypo => IsMobile ? Typo.subtitle1 : Typo.h6;

    /// <summary>「清除」按钮可用性：输入框有内容或已查询过即有事可清，避免空态下按钮常亮。</summary>
    private bool CanClear => _searched || !string.IsNullOrEmpty(_orderNo);

    /// <summary>回车即查（与 Attendance/MonthlyWages 的 OnKeywordEnter 同款）</summary>
    private async Task OnOrderNoEnter(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await QueryAsync();
    }

    /// <summary>
    /// 取消查询：清空输入框与结果区，回到「未查询」空态（纯前端复位，不触后端、不影响 /orders/progress 页面）。
    /// </summary>
    private void Clear()
    {
        _orderNo = null;
        _tree = null;
        _searched = false;
    }

    private async Task QueryAsync()
    {
        var orderNo = _orderNo?.Trim();
        if (string.IsNullOrEmpty(orderNo))
            return;

        _loading = true;
        _searched = true;
        _tree = null;   // 换单先清旧树，避免查询失败时残留上一单结果
        try
        {
            var resp = await Service.GetAsync(orderNo);
            _tree = resp.Success ? resp.Data : null;
        }
        finally
        {
            _loading = false;
        }
    }
}
