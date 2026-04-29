using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class WarehouseService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/warehouse";

    public WarehouseService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<WarehouseDto>>> GetAllAsync(bool onlyActive = true)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<WarehouseDto>>>($"{BaseUrl}/all?onlyActive={onlyActive}");
            return response ?? ApiResponse<List<WarehouseDto>>.Fail("获取仓库列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<WarehouseDto>>.Fail($"网络错误: {ex.Message}");
        }
    }
}
