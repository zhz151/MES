using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 批次跟踪可视化DTO — 前端进度图展示用
/// </summary>
public class BatchTrackingVisualDto
{
    public int BatchId { get; set; }
    public string BatchNo { get; set; } = null!;

    // ===== 整体进度 =====
    public int TotalSectionCount { get; set; }
    public int CompletedSectionCount { get; set; }
    public double ProgressPercent => TotalSectionCount > 0
        ? Math.Round((double)CompletedSectionCount / TotalSectionCount * 100, 1)
        : 0;

    // ===== 当前执行摘要 =====
    public string? CurrentGroupName { get; set; }
    public string? CurrentSectionName { get; set; }
    public string? CurrentEquipmentName { get; set; }
    public string? CurrentOutsource { get; set; }
    public string? CurrentSpec { get; set; }
    public string? NextSectionName { get; set; }
    public string? NextProcess { get; set; }

    /// <summary>当前工段仓库入库明细（仅入库工段有值）</summary>
    public List<WarehouseDetailDto>? CurrentWarehouseDetails { get; set; }

    // ===== 投料与目标统计 =====

    /// <summary>投料支数 = 批次领料支数</summary>
    public int? InputQuantity { get; set; }
    /// <summary>投料重量(kg) = 批次领料重量</summary>
    public int? InputWeight { get; set; }
    /// <summary>目标支数 = 投料支数 × 制成倍数</summary>
    public int? TargetQuantity { get; set; }
    /// <summary>目标重量(kg) = 投料重量 × 工序组折扣系数</summary>
    public int? TargetWeight { get; set; }

    // ===== 工序组列表（含工段级数据） =====
    public List<ProcessGroupVisualDto> ProcessGroups { get; set; } = new();

    // ===== 成品检验 9 项（横向流程最右端追加展示，正式成检为主/预检仅角标） =====
    public List<FinalInspectionItemVisualDto> FinalInspectionItems { get; set; } = new();
}

/// <summary>
/// 工序组可视化DTO
/// </summary>
public class ProcessGroupVisualDto
{
    public int Id { get; set; }
    public int SequenceNumber { get; set; }
    public string ProcessName { get; set; } = null!;
    public string? ManufacturingSpec { get; set; }

    /// <summary>组内工段总数</summary>
    public int TotalSections { get; set; }
    /// <summary>组内已完成工段数</summary>
    public int CompletedSections { get; set; }

    /// <summary>组内工段列表（按执行序号排序）</summary>
    public List<SectionVisualDto> Sections { get; set; } = new();
}

/// <summary>
/// 工段可视化DTO
/// </summary>
public class SectionVisualDto
{
    public string SectionName { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public int ProcessGroupId { get; set; }

    /// <summary>状态: Completed / InProgress / Outsource / Next / Pending</summary>
    public SectionStatus Status { get; set; } = SectionStatus.Pending;

    // ===== 若有生产记录 =====
    /// <summary>主执行日期：生产=最近报工日、去油酸洗=入缸日、委外=发出日、过程检验=检验日、成品检验(成检到料)=到料日、入库=入库日</summary>
    public DateTime? ExecDate { get; set; }
    public string? EquipmentName { get; set; }
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public string? Operator { get; set; }

    // ===== 多日期/区间（执行进度卡片的日期区扩展） =====
    /// <summary>首日期标签：入缸/发出；普通执行与检验无标签为 null</summary>
    public string? DateLabel { get; set; }
    /// <summary>次日期标签：出缸/回收（仅双节点工段）</summary>
    public string? DateLabel2 { get; set; }
    /// <summary>第二日期：去油酸洗=出缸日、委外=回收日（未发生则 null）</summary>
    public DateTime? ExecDate2 { get; set; }
    /// <summary>起始日期：同一工段多次报工跨日时区间起点（=ExecDate 单日时为 null）</summary>
    public DateTime? ExecDateStart { get; set; }
    /// <summary>次执行人：去油酸洗出缸操作人（与 Operator=入缸操作人区分）</summary>
    public string? Operator2 { get; set; }

    // ===== 委外信息 =====
    public string? OutsourceVendor { get; set; }
    public decimal? OutsourceProgress { get; set; }
    /// <summary>正常回收支数合计（RecoveryQuantity，不含未加工退回）</summary>
    public int? RecoveryQuantity { get; set; }
    /// <summary>正常回收重量合计(kg)（RecoveryWeight，不含未加工退回）</summary>
    public decimal? RecoveryWeight { get; set; }

    // ===== 检验 4 值（仅过程检验工段有值：合格支/合格重、次品支/次品重） =====
    public int? QualifiedQuantity { get; set; }
    public decimal? QualifiedWeight { get; set; }
    public int? DefectQuantity { get; set; }
    public decimal? DefectWeight { get; set; }

    // ===== 成品检验（成检到料）卡片：仅标注"检验到料日" =====
    /// <summary>true=该"检验"工段为成检到料语义，卡片只显示到料日，不展示操作人/数量</summary>
    public bool IsReceiveOnly { get; set; }

    // ===== 仓库入库信息（仅入库工段有值） =====
    public List<WarehouseDetailDto>? WarehouseDetails { get; set; }
}

/// <summary>
/// 成品检验 9 项可视化 DTO — 批次详情横向流程最右端"成品检验"卡片组
/// 数据源 FinalInspection：展示以正式成检为主，预成检仅作"预"角标；
/// 某项无正式记录但存在预检记录时降级展示预检数据（PreOnly=true）。
/// </summary>
public class FinalInspectionItemVisualDto
{
    /// <summary>检验项目（InspectionItem 枚举：PMI检验/表检/尺寸/内窥/水压/水下气压/涡流/超声波/端口着色）</summary>
    public InspectionItem InspectionItem { get; set; }

    /// <summary>正式成检必检（来源=此订单技术要求 ProductRequirement，无要求记录时兜底 PMI+表检+尺寸）</summary>
    public bool IsRequired { get; set; }

    /// <summary>是否存在预检记录（卡片加"预"角标）</summary>
    public bool HasPre { get; set; }

    /// <summary>无正式记录且存在预检记录 → 降级展示预检数据，卡片标注"预"阶段</summary>
    public bool PreOnly { get; set; }

    /// <summary>检验日期（正式优先，无正式取预检）</summary>
    public DateTime? InspectionDate { get; set; }

    /// <summary>检验员（正式优先，无正式取预检）</summary>
    public string? Inspector { get; set; }

    /// <summary>合格支数（正式成检累计）</summary>
    public int? QualifiedQuantity { get; set; }
    /// <summary>合格重量(kg)（正式成检累计）</summary>
    public int? QualifiedWeight { get; set; }
    /// <summary>次品支数 = 返整+入库+报废（正式成检累计）</summary>
    public int? DefectQuantity { get; set; }
    /// <summary>次品重量(kg) = 返整+入库+报废重量（正式成检累计）</summary>
    public int? DefectWeight { get; set; }
}

/// <summary>
/// 仓库入库明细DTO
/// </summary>
public class WarehouseDetailDto
{
    public string WarehouseName { get; set; } = null!;
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public DateTime? InboundDate { get; set; }
}
