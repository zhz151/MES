using System.Net.Http.Headers;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;

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
    /// 解析设备码，返回设备信息，用于扫码报修
    /// </summary>
    public async Task<ApiResponse<ScanEquipmentResolveResultDto>> ResolveEquipmentAsync(string code)
    {
        try
        {
            var url = $"{BaseUrl}/resolve-equipment?code={Uri.EscapeDataString(code)}";
            return await _http.GetFromJsonAsync<ApiResponse<ScanEquipmentResolveResultDto>>(url)
                   ?? ApiResponse<ScanEquipmentResolveResultDto>.Fail("请求失败");
        }
        catch (Exception ex) { return ApiResponse<ScanEquipmentResolveResultDto>.Fail($"网络错误: {ex.Message}"); }
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

    // ======================================================================
    // 扫码链质量（巡检 / 不合格反馈）— 走 api/scan-quality 仅登录端点
    // ======================================================================

    private const string QualityUrl = ApiEndpoints.ScanQuality;

    /// <summary>当前扫码人真实姓名（后端按登录账号解析，只读展示用）</summary>
    public async Task<ApiResponse<string>> GetCurrentOperatorNameAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<string>>($"{QualityUrl}/current-operator")
                   ?? ApiResponse<string>.Fail("获取操作人失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    // ---------- 巡检 ----------

    /// <summary>按「批次 + 工序组 + 工段」定位巡检单（未闭环优先）</summary>
    public async Task<ApiResponse<List<InspectionPatrolDto>>> GetPatrolsByKeyAsync(
        string batchNo, int processGroupId, string sectionName)
    {
        try
        {
            var url = $"{QualityUrl}/inspection-patrol/by-key?batchNo={Uri.EscapeDataString(batchNo)}"
                      + $"&processGroupId={processGroupId}&sectionName={Uri.EscapeDataString(sectionName)}";
            return await _http.GetFromJsonAsync<ApiResponse<List<InspectionPatrolDto>>>(url)
                   ?? ApiResponse<List<InspectionPatrolDto>>.Fail("查询失败");
        }
        catch (Exception ex) { return ApiResponse<List<InspectionPatrolDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>巡检单详情（含巡检明细与照片）</summary>
    public async Task<ApiResponse<InspectionPatrolDto>> GetPatrolAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<InspectionPatrolDto>>($"{QualityUrl}/inspection-patrol/{id}")
                   ?? ApiResponse<InspectionPatrolDto>.Fail("获取详情失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>按生产编号调取批次信息（工序组/工段下拉）</summary>
    public async Task<ApiResponse<InspectionPatrolLookupResultDto?>> LookupPatrolBatchAsync(string batchNo)
    {
        try
        {
            var url = $"{QualityUrl}/inspection-patrol/lookup-batch?batchNo={Uri.EscapeDataString(batchNo)}";
            return await _http.GetFromJsonAsync<ApiResponse<InspectionPatrolLookupResultDto?>>(url)
                   ?? ApiResponse<InspectionPatrolLookupResultDto?>.Ok(null, "查询成功");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolLookupResultDto?>.Ok(null, $"网络错误: {ex.Message}"); }
    }

    /// <summary>在产单位/车间 候选</summary>
    public async Task<ApiResponse<InspectionPatrolPositionOptionsDto>> GetPatrolPositionOptionsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<InspectionPatrolPositionOptionsDto>>(
                       $"{QualityUrl}/inspection-patrol/position-options")
                   ?? ApiResponse<InspectionPatrolPositionOptionsDto>.Fail("获取候选失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolPositionOptionsDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>扫码新建巡检单</summary>
    public async Task<ApiResponse<InspectionPatrolDto>> CreatePatrolAsync(CreateInspectionPatrolRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateInspectionPatrolRequest, ApiResponse<InspectionPatrolDto>>(
                       $"{QualityUrl}/inspection-patrol", request)
                   ?? ApiResponse<InspectionPatrolDto>.Fail("提交失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>扫码整改回填（不动巡检明细）</summary>
    public async Task<ApiResponse<InspectionPatrolDto>> RectifyPatrolAsync(int id, InspectionPatrolRectifyRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<InspectionPatrolRectifyRequest, ApiResponse<InspectionPatrolDto>>(
                       $"{QualityUrl}/inspection-patrol/{id}/rectify", request)
                   ?? ApiResponse<InspectionPatrolDto>.Fail("回填失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>上传巡检照片（base64 已在前端压缩）</summary>
    public async Task<ApiResponse<InspectionPatrolAttachmentDto>> UploadPatrolAttachmentAsync(
        int id, string photoType, byte[] content, string fileName, string contentType)
    {
        try
        {
            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(photoType), "photoType");
            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            multipart.Add(fileContent, "file", fileName);

            return await _http.PostMultipartAsync<ApiResponse<InspectionPatrolAttachmentDto>>(
                       $"{QualityUrl}/inspection-patrol/{id}/attachments", multipart)
                   ?? ApiResponse<InspectionPatrolAttachmentDto>.Fail("上传失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolAttachmentDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>读取巡检照片字节（缩略图/大图预览）</summary>
    public async Task<byte[]?> GetPatrolAttachmentBytesAsync(int id, int attachmentId)
    {
        try
        {
            return await _http.GetByteArrayAsync($"{QualityUrl}/inspection-patrol/{id}/attachments/{attachmentId}/file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取巡检照片失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>删除巡检照片</summary>
    public async Task<ApiResponse<object>> DeletePatrolAttachmentAsync(int id, int attachmentId)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>(
                       $"{QualityUrl}/inspection-patrol/{id}/attachments/{attachmentId}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ---------- 不合格反馈 ----------

    /// <summary>按生产编号调取批次信息（工序组/工段下拉）</summary>
    public async Task<ApiResponse<NonconformingFeedbackLookupResultDto?>> LookupFeedbackBatchAsync(string batchNo)
    {
        try
        {
            var url = $"{QualityUrl}/nonconforming-feedback/lookup-batch?batchNo={Uri.EscapeDataString(batchNo)}";
            return await _http.GetFromJsonAsync<ApiResponse<NonconformingFeedbackLookupResultDto?>>(url)
                   ?? ApiResponse<NonconformingFeedbackLookupResultDto?>.Ok(null, "查询成功");
        }
        catch (Exception ex) { return ApiResponse<NonconformingFeedbackLookupResultDto?>.Ok(null, $"网络错误: {ex.Message}"); }
    }

    /// <summary>扫码新建不合格反馈单</summary>
    public async Task<ApiResponse<NonconformingFeedbackDto>> CreateFeedbackAsync(CreateNonconformingFeedbackRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateNonconformingFeedbackRequest, ApiResponse<NonconformingFeedbackDto>>(
                       $"{QualityUrl}/nonconforming-feedback", request)
                   ?? ApiResponse<NonconformingFeedbackDto>.Fail("提交失败");
        }
        catch (Exception ex) { return ApiResponse<NonconformingFeedbackDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>上传问题照片</summary>
    public async Task<ApiResponse<NonconformingFeedbackAttachmentDto>> UploadFeedbackAttachmentAsync(
        int id, byte[] content, string fileName, string contentType)
    {
        try
        {
            using var multipart = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            multipart.Add(fileContent, "file", fileName);

            return await _http.PostMultipartAsync<ApiResponse<NonconformingFeedbackAttachmentDto>>(
                       $"{QualityUrl}/nonconforming-feedback/{id}/attachments", multipart)
                   ?? ApiResponse<NonconformingFeedbackAttachmentDto>.Fail("上传失败");
        }
        catch (Exception ex) { return ApiResponse<NonconformingFeedbackAttachmentDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>读取问题照片字节</summary>
    public async Task<byte[]?> GetFeedbackAttachmentBytesAsync(int id, int attachmentId)
    {
        try
        {
            return await _http.GetByteArrayAsync($"{QualityUrl}/nonconforming-feedback/{id}/attachments/{attachmentId}/file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取问题照片失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>删除问题照片</summary>
    public async Task<ApiResponse<object>> DeleteFeedbackAttachmentAsync(int id, int attachmentId)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>(
                       $"{QualityUrl}/nonconforming-feedback/{id}/attachments/{attachmentId}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }
}
