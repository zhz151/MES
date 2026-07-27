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
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);

    // ===== 若有生产记录 =====
    public DateTime? ExecDate { get; set; }
    public string? EquipmentName { get; set; }
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public string? Operator { get; set; }

    // ===== 委外信息 =====
    public string? OutsourceVendor { get; set; }
    public decimal? OutsourceProgress { get; set; }

    // ===== 仓库入库信息（仅入库工段有值） =====
    public List<WarehouseDetailDto>? WarehouseDetails { get; set; }
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
