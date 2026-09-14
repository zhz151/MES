using System.Net.Http.Headers;
using MES.Core.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 巡检单前端服务 — 含巡检/整改照片（按类型分存）上传/下载/删除
/// </summary>
public class InspectionPatrolService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.InspectionPatrol;

    /// <summary>每类照片张数上限（须与后端 InspectionPatrolPhotoTypes.MaxPerType 一致）</summary>
    public const int MaxAttachmentPerType = InspectionPatrolPhotoTypes.MaxPerType;

    public InspectionPatrolService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<InspectionPatrolDto>>> GetAllAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null,
        bool isDescending = true, string? filters = null,
        DateTime? patrolDateFrom = null, DateTime? patrolDateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (patrolDateFrom.HasValue) url += $"&patrolDateFrom={patrolDateFrom.Value:yyyy-MM-dd}";
            if (patrolDateTo.HasValue) url += $"&patrolDateTo={patrolDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<InspectionPatrolDto>>>(url)
                   ?? ApiResponse<PagedResult<InspectionPatrolDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<InspectionPatrolDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<InspectionPatrolDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<InspectionPatrolDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<InspectionPatrolDto>.Fail("获取详情失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<InspectionPatrolDto>> CreateAsync(CreateInspectionPatrolRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateInspectionPatrolRequest, ApiResponse<InspectionPatrolDto>>(BaseUrl, request)
                   ?? ApiResponse<InspectionPatrolDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<InspectionPatrolDto>> UpdateAsync(int id, UpdateInspectionPatrolRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateInspectionPatrolRequest, ApiResponse<InspectionPatrolDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<InspectionPatrolDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<object>> SetClosedAsync(int id, bool isClosed)
    {
        try
        {
            return await _http.PutAsJsonAsync<object, ApiResponse<object>>(
                       $"{BaseUrl}/{id}/closed?isClosed={isClosed.ToString().ToLower()}", new { })
                   ?? ApiResponse<object>.Fail("操作失败");
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

    public async Task<ApiResponse<InspectionPatrolLookupResultDto?>> LookupBatchAsync(string batchNo)
    {
        try
        {
            var url = $"{BaseUrl}/lookup-batch?batchNo={Uri.EscapeDataString(batchNo)}";
            return await _http.GetFromJsonAsync<ApiResponse<InspectionPatrolLookupResultDto?>>(url)
                   ?? ApiResponse<InspectionPatrolLookupResultDto?>.Ok(null, "查询成功");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolLookupResultDto?>.Ok(null, $"网络错误: {ex.Message}"); }
    }

    /// <summary>在产单位/在产设备号/在产操作人 候选（档案驱动）</summary>
    public async Task<ApiResponse<InspectionPatrolPositionOptionsDto>> GetPositionOptionsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<InspectionPatrolPositionOptionsDto>>($"{BaseUrl}/position-options")
                   ?? ApiResponse<InspectionPatrolPositionOptionsDto>.Fail("获取候选失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolPositionOptionsDto>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 附件 ==========

    public async Task<ApiResponse<List<InspectionPatrolAttachmentDto>>> GetAttachmentsAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<InspectionPatrolAttachmentDto>>>($"{BaseUrl}/{id}/attachments")
                   ?? ApiResponse<List<InspectionPatrolAttachmentDto>>.Fail("获取附件失败");
        }
        catch (Exception ex) { return ApiResponse<List<InspectionPatrolAttachmentDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>上传附件（base64 图片，前端已压缩；photoType 见 InspectionPatrolPhotoTypes）</summary>
    public async Task<ApiResponse<InspectionPatrolAttachmentDto>> UploadAttachmentAsync(
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
                       $"{BaseUrl}/{id}/attachments", multipart)
                   ?? ApiResponse<InspectionPatrolAttachmentDto>.Fail("上传失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionPatrolAttachmentDto>.Fail($"网络错误: {ex.Message}"); }
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
