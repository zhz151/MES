using System.Net.Http.Headers;
using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Quality;

namespace MES.Blazor.Services;

public class ProcessInspectionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.ProcessInspection;

    /// <summary>照片张数上限（单一出口 QualityPhotoLimits.PerRecord，与后端同源）</summary>
    public const int MaxAttachmentCount = QualityPhotoLimits.PerRecord;

    public ProcessInspectionService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<ProcessInspectionDto>>> GetAllAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null, string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (inspectionDateFrom.HasValue) url += $"&inspectionDateFrom={inspectionDateFrom.Value:yyyy-MM-dd}";
            if (inspectionDateTo.HasValue) url += $"&inspectionDateTo={inspectionDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProcessInspectionDto>>>(url)
                   ?? ApiResponse<PagedResult<ProcessInspectionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ProcessInspectionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<ProcessInspectionDto>>> BatchCreateAsync(List<CreateProcessInspectionRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateProcessInspectionRequest>, ApiResponse<List<ProcessInspectionDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<ProcessInspectionDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<ProcessInspectionDto>>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<ProcessInspectionDto>> UpdateAsync(int id, UpdateProcessInspectionRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateProcessInspectionRequest, ApiResponse<ProcessInspectionDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<ProcessInspectionDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<ProcessInspectionDto>.Fail($"网络错误: {ex.Message}"); }
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

    // ========== 照片附件 ==========

    public async Task<ApiResponse<List<ProcessInspectionAttachmentDto>>> GetAttachmentsAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<ProcessInspectionAttachmentDto>>>($"{BaseUrl}/{id}/attachments")
                   ?? ApiResponse<List<ProcessInspectionAttachmentDto>>.Fail("获取照片失败");
        }
        catch (Exception ex) { return ApiResponse<List<ProcessInspectionAttachmentDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>上传照片（base64 转字节，前端已压缩）</summary>
    public async Task<ApiResponse<ProcessInspectionAttachmentDto>> UploadAttachmentAsync(
        int id, byte[] content, string fileName, string contentType)
    {
        try
        {
            using var multipart = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            multipart.Add(fileContent, "file", fileName);

            return await _http.PostMultipartAsync<ApiResponse<ProcessInspectionAttachmentDto>>(
                       $"{BaseUrl}/{id}/attachments", multipart)
                   ?? ApiResponse<ProcessInspectionAttachmentDto>.Fail("上传失败");
        }
        catch (Exception ex) { return ApiResponse<ProcessInspectionAttachmentDto>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>读取照片字节（用于缩略图/大图预览）</summary>
    public async Task<byte[]?> GetAttachmentBytesAsync(int id, int attachmentId)
    {
        try
        {
            return await _http.GetByteArrayAsync($"{BaseUrl}/{id}/attachments/{attachmentId}/file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取照片失败: {ex.Message}");
            return null;
        }
    }

    public async Task<ApiResponse<object>> DeleteAttachmentAsync(int id, int attachmentId)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}/attachments/{attachmentId}")
                   ?? ApiResponse<object>.Fail("删除照片失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }
}
