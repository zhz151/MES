using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 扫码执行前端服务
/// </summary>
public class ScanService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/scan";

    public ScanService(AuthHttpClient http) => _http = http;

    /// <summary>
    /// 解析二维码内容
    /// </summary>
    public async Task<ApiResponse<ScanResolveResultDto>> ResolveAsync(string batchNo, int processGroupId)
    {
        try
        {
            var url = $"{BaseUrl}/resolve?batchNo={Uri.EscapeDataString(batchNo)}&processGroupId={processGroupId}";
            return await _http.GetFromJsonAsync<ApiResponse<ScanResolveResultDto>>(url)
                   ?? ApiResponse<ScanResolveResultDto>.Fail("请求失败");
        }
        catch (Exception ex) { return ApiResponse<ScanResolveResultDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>
    /// 创建生产记录（扫码报工提交）
    /// </summary>
    public async Task<ApiResponse<ProductionRecordDto>> CreateProductionRecordAsync(CreateProductionRecordRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateProductionRecordRequest, ApiResponse<ProductionRecordDto>>("api/production-record/record", request)
                   ?? ApiResponse<ProductionRecordDto>.Fail("提交失败");
        }
        catch (Exception ex) { return ApiResponse<ProductionRecordDto>.Fail($"网络错误: {ex.Message}"); }
    }
}
