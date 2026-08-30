namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 批次计划工段筛选 Tab 选项（配置驱动，前后端共享）。
/// 组装：冷轧冷拔类 = ProcessDefinitions 启用工序（Key=ProcessKey、Display=ProcessName），
/// 普通工段 = StandardWorkDays 启用工段（Key=SectionKey、Display=SectionName，扣除冷轧拔/检验/入库），
/// 末尾固定「荒管检」「在制检」（产类维度，Key=Display=中文）。
/// 前端 Tab 渲染用 Display、筛选传 Key；委外在产列排序（Display→序）亦由此构建。
/// </summary>
public class BatchPlanSectionTabDto
{
    /// <summary>稳定 Key（冷轧=ProcessKey、普通=SectionKey、检验=固定中文），筛选传此值</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>显示名（冷轧=工序 ProcessName、普通=工段 SectionName、检验=固定中文）</summary>
    public string Display { get; set; } = string.Empty;

    /// <summary>分组：cold=冷轧冷拔工序 / section=普通工段 / fixed=固定检验</summary>
    public string Group { get; set; } = string.Empty;
}
