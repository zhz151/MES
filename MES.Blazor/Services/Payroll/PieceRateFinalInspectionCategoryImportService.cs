using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>
/// 成检计件类别专用导入器（2026-09-03）——模板/导出下载 + 预览 + 事务导入。
/// kind = category（类别定义）| tier（维档系数），与后端 PieceRateImportKinds 对应。
/// </summary>
public class PieceRateFinalInspectionCategoryImportService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PieceRateFinalInspectionCategory;

    public PieceRateFinalInspectionCategoryImportService(AuthHttpClient http) => _http = http;

    /// <summary>下载全量导出</summary>
    public async Task<byte[]> ExportAllAsync()
    {
        return await _http.GetByteArrayAsync($"{BaseUrl}/export-all");
    }

    /// <summary>下载单 sheet 空模板（kind=category|tier）</summary>
    public async Task<byte[]> GenerateTemplateAsync(string kind)
    {
        return await _http.GetByteArrayAsync($"{BaseUrl}/import/template?kind={Uri.EscapeDataString(kind)}");
    }

    /// <summary>上传 xlsx → 服务端解析 + 校验 + 统计（预览与导入同口径）</summary>
    public async Task<ApiResponse<MES.Core.Models.ImportPreviewResult>> PreviewImportAsync(string kind, byte[] fileData, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileData), "file", fileName);
            var response = await _http.PostMultipartAsync<ApiResponse<MES.Core.Models.ImportPreviewResult>>(
                $"{BaseUrl}/import/preview?kind={Uri.EscapeDataString(kind)}", content);
            return response ?? ApiResponse<MES.Core.Models.ImportPreviewResult>.Fail("预览失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<MES.Core.Models.ImportPreviewResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>事务导入（覆盖更新；服务端任一数据行无效 → 整体拒绝）</summary>
    public async Task<ApiResponse<MES.Core.Models.ImportResult>> ImportAsync(string kind, byte[] fileData, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileData), "file", fileName);
            var response = await _http.PostMultipartAsync<ApiResponse<MES.Core.Models.ImportResult>>(
                $"{BaseUrl}/import?kind={Uri.EscapeDataString(kind)}", content);
            return response ?? ApiResponse<MES.Core.Models.ImportResult>.Fail("导入失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<MES.Core.Models.ImportResult>.Fail($"网络错误: {ex.Message}");
        }
    }
}
