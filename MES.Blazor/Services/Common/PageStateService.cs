using System.Text.Json;
using Blazored.LocalStorage;
using MES.Blazor.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 列表页状态持久化服务（基于 localStorage）
/// 保存排序、筛选、分页、关键字等页面状态
/// </summary>
public class PageStateService
{
    private readonly ILocalStorageService _storage;
    private const string Prefix = "page_state";

    public PageStateService(ILocalStorageService storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// 保存页面状态
    /// </summary>
    public async Task SaveAsync(string pageKey, PageState state)
    {
        var key = BuildKey(pageKey);
        await _storage.SetItemAsync(key, state);
    }

    /// <summary>
    /// 加载页面状态，不存在时返回默认值
    /// </summary>
    public async Task<PageState?> LoadAsync(string pageKey)
    {
        var key = BuildKey(pageKey);
        try
        {
            return await _storage.GetItemAsync<PageState>(key);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 清除页面状态
    /// </summary>
    public async Task ClearAsync(string pageKey)
    {
        var key = BuildKey(pageKey);
        await _storage.RemoveItemAsync(key);
    }

    private static string BuildKey(string pageKey) => $"{Prefix}_{pageKey}";
}
