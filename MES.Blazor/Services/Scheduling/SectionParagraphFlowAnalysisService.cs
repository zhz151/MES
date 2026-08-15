using System.Text.Json;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Scheduling;

namespace MES.Blazor.Services;

/// <summary>
/// 生产段落流转量分析前端服务
/// </summary>
public class SectionParagraphFlowAnalysisService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.SectionParagraphFlowAnalysis;

    public SectionParagraphFlowAnalysisService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<SectionParagraphFlowAnalysisDto>>> GetAnalysisAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<SectionParagraphFlowAnalysisDto>>>(BaseUrl);
            return response ?? ApiResponse<List<SectionParagraphFlowAnalysisDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SectionParagraphFlowAnalysisDto>>.Fail($"网络错误: {ex.Message}");
        }
    }
}
