using System.Net.Http.Headers;
using System.Text.Json;
using MES.Core.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 不合格反馈单前端服务 — 含问题照片附件上传/下载/删除
/// </summary>
public class NonconformingFeedbackService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.NonconformingFeedback;

    /// <summary>附件张数上限（单一出口 QualityPhotoLimits.PerRecord，与后端同源）</summary>
    public const int MaxAttachmentCount = QualityPhotoLimits.PerRecord;

    public NonconformingFeedbackService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<NonconformingFeedbackDto>>> GetAllAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null,
        bool isDescending = true, string? filters = null,
        DateTime? reportDateFrom = null, DateTime? reportDateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (reportDateFrom.HasValue) url += $"&reportDateFrom={reportDateFrom.Value:yyyy-MM-dd}";
            if (reportDateTo.HasValue) url += $"&reportDateTo={reportDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<NonconformingFeedbackDto>>>(url)
                   ?? ApiResponse<PagedResult<NonconformingFeedbackDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<NonconformingFeedbackDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<NonconformingFeedbackDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<NonconformingFeedbackDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<NonconformingFeedbackDto>.Fail("获取详情失败");
        }
        catch (Exception ex) { return ApiResponse<NonconformingFeedbackDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<NonconformingFeedbackDto>> CreateAsync(CreateNonconformingFeedbackRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateNonconformingFeedbackRequest, ApiResponse<NonconformingFeedbackDto>>(BaseUrl, request)
                   ?? ApiResponse<NonconformingFeedbackDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<NonconformingFeedbackDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<NonconformingFeedbackDto>> UpdateAsync(int id, UpdateNonconformingFeedbackRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateNonconformingFeedbackRequest, ApiResponse<NonconformingFeedbackDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<NonconformingFeedbackDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<NonconformingFeedbackDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<NonconformingFeedbackLookupResultDto?>> LookupBatchAsync(string batchNo)
    {
        try
        {
            var url = $"{BaseUrl}/lookup-batch?batchNo={Uri.EscapeDataString(batchNo)}";
            return await _http.GetFromJsonAsync<ApiResponse<NonconformingFeedbackLookupResultDto?>>(url)
                   ?? ApiResponse<NonconformingFeedbackLookupResultDto?>.Ok(null, "查询成功");
        }
        catch (Exception ex) { return ApiResponse<NonconformingFeedbackLookupResultDto?>.Ok(null, $"网络错误: {ex.Message}"); }
    }

    // ========== 附件 ==========

    public async Task<ApiResponse<List<NonconformingFeedbackAttachmentDto>>> GetAttachmentsAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<NonconformingFeedbackAttachmentDto>>>($"{BaseUrl}/{id}/attachments")
                   ?? ApiResponse<List<NonconformingFeedbackAttachmentDto>>.Fail("获取附件失败");
        }
        catch (Exception ex) { return ApiResponse<List<NonconformingFeedbackAttachmentDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>上传附件（base64 图片，前端已压缩）</summary>
    public async Task<ApiResponse<NonconformingFeedbackAttachmentDto>> UploadAttachmentAsync(
        int id, byte[] content, string fileName, string contentType)
    {
        try
        {
            using var multipart = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            multipart.Add(fileContent, "file", fileName);

            return await _http.PostMultipartAsync<ApiResponse<NonconformingFeedbackAttachmentDto>>(
                       $"{BaseUrl}/{id}/attachments", multipart)
                   ?? ApiResponse<NonconformingFeedbackAttachmentDto>.Fail("上传失败");
        }
        catch (Exception ex) { return ApiResponse<NonconformingFeedbackAttachmentDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>读取附件字节（用于缩略图/大图预览）</summary>
    public async Task<byte[]?> GetAttachmentBytesAsync(int id, int attachmentId)
    {
        try
        {
            return await _http.GetByteArrayAsync($"{BaseUrl}/{id}/attachments/{attachmentId}/file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取附件失败: {ex.Message}");
            return null;
        }
    }

    public async Task<ApiResponse<object>> DeleteAttachmentAsync(int id, int attachmentId)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}/attachments/{attachmentId}")
                   ?? ApiResponse<object>.Fail("删除附件失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }
}
