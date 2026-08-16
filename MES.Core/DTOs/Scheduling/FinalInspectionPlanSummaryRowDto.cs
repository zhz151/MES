namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 成检计划「待检批支重汇总」行（行=检验项，列=待到料/待检验/检验中/汇总数据）。
/// 口径：某检验项统计「要求该检验项（Req=true）且 本成检类型尚未完成该检验（对应检验日期为空）」的看板批次，
/// 按看板档位（待到料/待检验/检验中）分列；已检完该检验项的批次（含「完成检验待入库」档）不计入；
/// 每列三值 = 批次数/生产支数/生产重量(kg)，预+正式合并、按批次去重；汇总数据列 = 三档之和。
/// </summary>
public class FinalInspectionPlanSummaryRowDto
{
    /// <summary>检验项中文名（PMI检验/表检/…/端口着色）</summary>
    public string InspectionItemName { get; set; } = string.Empty;

    // ========== 待到料（无检验记录、无到料） ==========
    public int WaitingMaterialCount { get; set; }
    public int WaitingMaterialQuantity { get; set; }
    public decimal WaitingMaterialWeight { get; set; }

    // ========== 待检验（无检验记录、已有到料） ==========
    public int WaitingInspectionCount { get; set; }
    public int WaitingInspectionQuantity { get; set; }
    public decimal WaitingInspectionWeight { get; set; }

    // ========== 检验中（有本类型检验记录、要求项未全检；仅该检验项未检者计入） ==========
    public int InspectingCount { get; set; }
    public int InspectingQuantity { get; set; }
    public decimal InspectingWeight { get; set; }

    // ========== 汇总数据（三档之和） ==========
    public int TotalCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalWeight { get; set; }
}
