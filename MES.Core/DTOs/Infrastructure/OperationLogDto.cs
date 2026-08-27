namespace MES.Core.DTOs.Infrastructure;

public class OperationLogDto
{
    public int Id { get; set; }
    public string OperationType { get; set; } = null!;
    public string? Detail { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset CreatedTime { get; set; }
}
