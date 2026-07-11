namespace MES.Core.DTOs.Batch;

/// <summary>
/// 批次操作日志DTO
/// </summary>
public class BatchOperationLogDto
{
    public int Id { get; set; }
    public string OperationType { get; set; } = null!;
    public string? Detail { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset CreatedTime { get; set; }
}
