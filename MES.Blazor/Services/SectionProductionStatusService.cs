using System.Text.Json;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 生产工段待产量现况前端服务
/// </summary>
public class SectionProductionStatusService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.SectionProductionStatus;

    public SectionProductionStatusService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<SectionProductionStatusDto>>> GetStatusAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<SectionProductionStatusDto>>>(BaseUrl);
            return response ?? ApiResponse<List<SectionProductionStatusDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SectionProductionStatusDto>>.Fail($"网络错误: {ex.Message}");
        }
    }
}
