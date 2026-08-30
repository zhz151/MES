namespace MES.Core.DTOs.Scheduling;

using MES.Core.Constants;

/// <summary>
/// 生产工段待产量现况 DTO — 按(工序组, 工段, 产类)三维汇总各批次的有效原料重量。
/// 产类由 <see cref="MES.Services.Helpers.ProductStatusHelper.Calculate"/> 逐批次判定。
/// </summary>
public class SectionProductionStatusDto
{
    /// <summary>工序组名称（英文 Key）</summary>
    public string ProcessGroupName { get; set; } = null!;

    /// <summary>工段名称（英文 Key）</summary>
    public string SectionName { get; set; } = null!;

    /// <summary>产类（ProductStatuses.RoughTube/InProgress/Finished，口径=ProductStatusHelper.Calculate）</summary>
    public string ProductStatus { get; set; } = ProductStatuses.RoughTube;

    /// <summary>生产中：批次的当前工序/工段匹配此维度且工段未完工的现有效原料重量汇总</summary>
    public decimal? InProduction { get; set; }

    /// <summary>待产量：批次的下一工序/工段匹配此维度且工段已完工的现有效原料重量汇总</summary>
    public decimal? PendingProduction { get; set; }

    /// <summary>工段生产与待产汇总量 = 生产中 + 待产量</summary>
    public decimal? Total { get; set; }

    /// <summary>计划流转量（批次计划中流转=是的现有效原料重量汇总，口径=批次计划 GetAllAsync(null)，单位 kg）</summary>
    public decimal? PlanFlowQuantity { get; set; }

    /// <summary>重点批重量（批次计划中流转=是且等级=急+的现有效原料重量汇总，口径=批次计划 GetAllAsync(null)，单位 kg）</summary>
    public decimal? PlanKeyWeight { get; set; }
}
