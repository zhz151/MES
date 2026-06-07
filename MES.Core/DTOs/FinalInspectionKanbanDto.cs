namespace MES.Core.DTOs;

/// <summary>
/// 成检看板 DTO
/// </summary>
public class FinalInspectionKanbanDto
{
    public int ProductionBatchId { get; set; }

    // ========== G1 批次信息 ==========
    public string? BatchNo { get; set; }              // 生产编号
    public string? TagNo { get; set; }                 // 挂牌号
    public string? PlantGrade { get; set; }            // 原料钢号
    public decimal? CurrentValidWeight { get; set; }   // 重量(kg)

    // ========== G2 关联工单 ==========
    public string? WorkOrderNo { get; set; }           // 工单号
    public string? Salesman { get; set; }              // 业务员
    public DateTime? DeliveryDate { get; set; }        // 交货日期
    public string? Specification { get; set; }         // 成品规格
    public string? LengthStatus { get; set; }          // 长度状态
    public decimal? MinLength { get; set; }            // 最小长度
    public decimal? MaxLength { get; set; }            // 最大长度

    // ========== G12 排程信息（WorkOrderExecutionSummary） ==========
    public int? ScheduleStage { get; set; }            // 关注状态
    public string? UrgencyLevel { get; set; }          // 工单计划性

    // ========== 到料 ==========
    public DateTime? ReceiveDate { get; set; }         // 到料日期

    // ========== 检验进度 ==========
    public DateTime? MaxInspectionDate { get; set; }   // 最大检验日期

    // ========== 档位标识（前端显示用） ==========
    public string KanbanStage { get; set; } = "";       // 待到料/待检验/检验中
}
