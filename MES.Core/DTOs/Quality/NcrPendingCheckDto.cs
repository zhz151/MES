using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// NCR 待处理批次卡片 DTO — 分析过程检验和成品检验数据
/// 识别出需要创建不合格报告的批次
/// </summary>
public class NcrPendingCheckDto
{
    /// <summary>生产编号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>工单号</summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>牌号</summary>
    public string? PlantGrade { get; set; }

    /// <summary>规格（取自检验记录）</summary>
    public string? Specification { get; set; }

    /// <summary>检验日期（取检验记录的检验日期）</summary>
    public DateTime ReportDate { get; set; }

    /// <summary>来源类型：ProcessInspection / FinalInspection</summary>
    public string SourceType { get; set; } = null!;

    /// <summary>检验项目</summary>
    public string? InspectionItem { get; set; }

    /// <summary>工序名称（过程检验用，判断钢管类别）</summary>
    public string? ProcessName { get; set; }

    /// <summary>物料名称（成品检验用，对应钢管类别）</summary>
    public string? MaterialName { get; set; }

    /// <summary>检验员（→反馈人）</summary>
    public string? Inspector { get; set; }

    /// <summary>次品情况描述（→问题描述）</summary>
    public string? DefectDescription { get; set; }

    /// <summary>处置方式：Rework / WarehouseEntry / Scrap</summary>
    public DisposalMethod DisposalMethod { get; set; }
    public string DisposalMethodDisplay => EnumHelper.GetDisplayName(DisposalMethod);

    /// <summary>次品支数（触发条件的次品数量）</summary>
    public int DefectQuantity { get; set; }

    /// <summary>次品重量(kg；成品检验取实际重量、过程检验取理论重量，对应触发处置方式)</summary>
    public int? DefectiveWeight { get; set; }

    /// <summary>总检验支数</summary>
    public int TotalQuantity { get; set; }

    /// <summary>不合格占比（百分比，如 8.5 表示 8.5%）</summary>
    public decimal Percentage { get; set; }
}
