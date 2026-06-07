using MES.Core.DTOs;
using MES.Shared.Constants;
using System.Net.Http.Json;

namespace MES.Blazor.Services;

/// <summary>
/// 成检看板 Blazor 前端服务
/// </summary>
public class FinalInspectionKanbanService
{
    private readonly HttpClient _http;

    public FinalInspectionKanbanService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<FinalInspectionKanbanDto>> GetKanbanAsync()
    {
        var url = $"{ApiEndpoints.FinalInspectionKanban}/kanban";
        var response = await _http.GetFromJsonAsync<KanbanResponse>(url);
        return response?.Data ?? new List<FinalInspectionKanbanDto>();
    }

    private class KanbanResponse
    {
        public bool Success { get; set; }
        public List<FinalInspectionKanbanDto> Data { get; set; } = new();
    }
}
