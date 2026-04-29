using System.Text.Json;
using System.Text.Json.Serialization;
using Blazored.LocalStorage;

namespace MES.Blazor.Services;

/// <summary>
/// 列定义（用于列选择器）
/// </summary>
public class ColumnDef
{
    [JsonPropertyName("k")]
    public string Key { get; set; } = "";
    [JsonPropertyName("l")]
    public string Label { get; set; } = "";
    [JsonPropertyName("v")]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 是否适用于当前仓库类型（仓库固有属性，不序列化到 localStorage）
    /// </summary>
    [JsonIgnore]
    public bool IsApplicable { get; set; } = true;
}

/// <summary>
/// 列偏好持久化服务
/// </summary>
public class ColumnPrefsService
{
    private readonly ILocalStorageService _storage;
    private const string Prefix = "col_prefs";

    public ColumnPrefsService(ILocalStorageService storage)
    {
        _storage = storage;
    }

    public async Task<List<ColumnDef>> LoadAsync(string pageType, string? warehouseCode)
    {
        var key = BuildKey(pageType, warehouseCode);
        try
        {
            var json = await _storage.GetItemAsStringAsync(key);
            if (!string.IsNullOrEmpty(json))
            {
                var defs = JsonSerializer.Deserialize<List<ColumnDef>>(json);
                if (defs != null && defs.Count > 0)
                    return defs;
            }
        }
        catch
        {
            // ignore deserialization errors, use defaults
        }
        return new List<ColumnDef>();
    }

    public async Task SaveAsync(string pageType, string? warehouseCode, List<ColumnDef> columns)
    {
        var key = BuildKey(pageType, warehouseCode);
        var json = JsonSerializer.Serialize(columns);
        await _storage.SetItemAsStringAsync(key, json);
    }

    public async Task<bool> ExistsAsync(string pageType, string? warehouseCode)
    {
        var key = BuildKey(pageType, warehouseCode);
        return await _storage.ContainKeyAsync(key);
    }

    private static string BuildKey(string pageType, string? warehouseCode) =>
        $"{Prefix}_{pageType}" + (string.IsNullOrEmpty(warehouseCode) ? "" : $"_{warehouseCode.ToLowerInvariant()}");
}
