namespace MES.Core.DTOs.Batch;

/// <summary>
/// 批量物料匹配请求项
/// </summary>
public class BatchMaterialMatchItem
{
    public string Category { get; set; } = null!;
    public string Grade { get; set; } = null!;
    public string Spec { get; set; } = null!;
}
