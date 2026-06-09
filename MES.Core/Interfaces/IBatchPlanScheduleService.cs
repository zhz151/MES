using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 批次计划薄表服务接口 — 计划员手工编辑批次计划
/// </summary>
public interface IBatchPlanScheduleService
{
    /// <summary>获取所有批次计划</summary>
    Task<List<BatchPlanScheduleDto>> GetAllAsync();

    /// <summary>保存单条批次计划</summary>
    Task<bool> SaveAsync(BatchPlanScheduleDto dto);

    /// <summary>计划安排：按当前视图计算并填充前7个字段</summary>
    Task<bool> PlanAllAsync(string? sectionTab);
}
