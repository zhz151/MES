using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MES.Blazor.Services;

namespace MES.Blazor.Shared;

/// <summary>
/// 首页「生产批次进度查询」卡：卡内输入生产编号 → 就地渲染「批次执行进度」（非链接跳转）。
/// 先按生产编号换出批次 Id（<c>GET api/batch/by-batch-no/{batchNo}</c>），再交给
/// <see cref="BatchProgressCard"/> 取 <c>GET api/batch/{id}/tracking</c>；两个端点自 2026-09-14 起均仅需登录
/// （不带 BatchView 档），以满足「首页人人可查」。
/// </summary>
public partial class BatchProgressQueryCard
{
    /// <summary>手机端壳（MobileLayout）下传 IsMobile=true；桌面端 MainLayout 不提供 → 默认 false。</summary>
    [CascadingParameter(Name = "IsMobile")]
    private bool IsMobile { get; set; }

    [Inject] private BatchService BatchService { get; set; } = null!;

    private string? _batchNo;
    private string? _resolvedBatchNo;
    private int _batchId;
    private bool _loading;
    private bool _searched;

    private string CardClass => IsMobile ? "mh-card pa-3" : "pa-4 home-stack-card";
    private int CardElevation => IsMobile ? 0 : 1;
    private Typo TitleTypo => IsMobile ? Typo.subtitle1 : Typo.h6;

    /// <summary>「清除」按钮可用性：输入框有内容或已查询过即有事可清，避免空态下按钮常亮。</summary>
    private bool CanClear => _searched || !string.IsNullOrEmpty(_batchNo);

    /// <summary>回车即查（与 Attendance/MonthlyWages 的 OnKeywordEnter 同款）</summary>
    private async Task OnBatchNoEnter(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await QueryAsync();
    }

    /// <summary>
    /// 取消查询：清空输入框与结果区，回到「未查询」空态（纯前端复位，不触后端、不影响批次详情页）。
    /// </summary>
    private void Clear()
    {
        _batchNo = null;
        _resolvedBatchNo = null;
        _batchId = 0;
        _searched = false;
    }

    private async Task QueryAsync()
    {
        var batchNo = _batchNo?.Trim();
        if (string.IsNullOrEmpty(batchNo))
            return;

        _loading = true;
        _searched = true;
        _batchId = 0;           // 换号先清旧批次，避免查询失败时残留上一批的进度
        _resolvedBatchNo = null;
        try
        {
            var resp = await BatchService.GetByBatchNoAsync(batchNo);
            if (resp.Success && resp.Data != null)
            {
                _batchId = resp.Data.Id;
                _resolvedBatchNo = resp.Data.BatchNo;
            }
        }
        finally
        {
            _loading = false;
        }
    }
}
