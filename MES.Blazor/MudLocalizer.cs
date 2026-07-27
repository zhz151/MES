using Microsoft.Extensions.Localization;
using MudBlazor;

namespace MES.Blazor;

/// <summary>
/// MudBlazor 组件中文本地化
/// </summary>
public class MESMudLocalizer : MudLocalizer
{
    private readonly Dictionary<string, string> _localizations = new()
    {
        { "MudTablePager.RowsPerPage", "每页行数" },
    };

    public override LocalizedString this[string key] =>
        _localizations.TryGetValue(key, out var value)
            ? new LocalizedString(key, value, true)
            : base[key];
}
