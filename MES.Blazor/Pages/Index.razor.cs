using Microsoft.AspNetCore.Components;

namespace MES.Blazor.Pages;

public partial class Index
{
    /// <summary>手机端壳（MobileLayout）会向页面下传 IsMobile=true；桌面端 MainLayout 不提供 → 默认 false。</summary>
    [CascadingParameter(Name = "IsMobile")]
    private bool IsMobile { get; set; }
}
