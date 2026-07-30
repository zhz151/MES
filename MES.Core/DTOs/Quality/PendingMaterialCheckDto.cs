namespace MES.Core.DTOs.Quality;

/// <summary>
/// 待检验到料批次（成品检验阶段且无检验到料记录）
/// </summary>
public class PendingMaterialCheckDto
{
    public int BatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public string? WorkOrderNo { get; set; }
    public string? Salesman { get; set; }
    public string? TagNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public int CurrentValidWeight { get; set; }
    public DateTime? CurrentExecDate { get; set; }
    public string? CurrentSectionName { get; set; }

    // ========== 匹配的工序组信息 ==========

    /// <summary>匹配的工序组ID（优先非"成检附加"）</summary>
    public int ProcessGroupId { get; set; }

    /// <summary>匹配的工序组名称</summary>
    public string? ProcessGroupName { get; set; }
}
