using MES.Core.Models;

using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.StandardRegister;

/// <summary>
/// 牌号对照打印选中项请求
/// </summary>
public class GradeMappingPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 牌号对照打印全部请求
/// </summary>
public class GradeMappingPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}
