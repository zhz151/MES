using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 扫码执行前端服务
/// </summary>
public class ScanService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Scan;

    public ScanService(AuthHttpClient http) => _http = http;

    /// <summary>
    /// 解析二维码内容（批次号+工序组ID）
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
    /// 按批次号解析，返回批次信息和该批次下所有工序组选项
    /// </summary>
    public async Task<ApiResponse<ScanBatchResolveResultDto>> GetBatchProcessGroupsAsync(string batchNo)
    {
        try
        {
            var url = $"{BaseUrl}/batch-groups?batchNo={Uri.EscapeDataString(batchNo)}";
            return await _http.GetFromJsonAsync<ApiResponse<ScanBatchResolveResultDto>>(url)
                   ?? ApiResponse<ScanBatchResolveResultDto>.Fail("请求失败");
        }
        catch (Exception ex) { return ApiResponse<ScanBatchResolveResultDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>
    /// 创建生产记录（扫码报工提交）
    /// </summary>
    public async Task<ApiResponse<ProductionRecordDto>> CreateProductionRecordAsync(CreateProductionRecordRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateProductionRecordRequest, ApiResponse<ProductionRecordDto>>($"{ApiEndpoints.ProductionRecord}/record", request)
                   ?? ApiResponse<ProductionRecordDto>.Fail("提交失败");
        }
        catch (Exception ex) { return ApiResponse<ProductionRecordDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>
    /// 创建过程检验记录（扫码检验提交）
    /// </summary>
    public async Task<ApiResponse<List<ProcessInspectionDto>>> CreateInspectionAsync(CreateProcessInspectionRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateProcessInspectionRequest>, ApiResponse<List<ProcessInspectionDto>>>(
                $"{ApiEndpoints.ProcessInspection}/batch", new List<CreateProcessInspectionRequest> { request })
                   ?? ApiResponse<List<ProcessInspectionDto>>.Fail("提交失败");
        }
        catch (Exception ex) { return ApiResponse<List<ProcessInspectionDto>>.Fail($"网络错误: {ex.Message}"); }
    }
}
