using MES.Core.Models;

namespace MES.Blazor.Components;

/// <summary>
/// 用于 CascadingValue 传递筛选回调，绕开 MudTable 内 EventCallback 不生效的问题
/// </summary>
public class FilterCallback
{
    public Func<FilterDescriptor?, Task>? Handler { get; set; }
}
