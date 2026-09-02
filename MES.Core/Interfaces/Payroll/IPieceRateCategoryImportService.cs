using MES.Core.Models;

namespace MES.Core.Interfaces.Payroll;

/// <summary>
/// 生产计件类别专用批量导入/导出服务（2026-09-02，工资结算数据维护闭环）。
/// 定位键 = 工段 × 工序/产类/作业阶段三约束归一组（空=该维全选）；冲突策略 = 覆盖更新：
///   Category 模板：定位命中类别 → 整组更新主属性+三约束成员（绝不清空既有档行）；未命中 → 新建（无档行）。
///   Tier 模板：定位未命中 → 整行报错「请先导入类别定义」；命中 → 该类别 Tiers 整组替换为文件行。
/// 职责分离：类别模板不动档行、维档模板不动主属性，杜绝单模板误清空。
/// </summary>
public interface IPieceRateCategoryImportService
{
    /// <summary>导出全量类别标准 → xlsx（Sheet「类别」+「维档」双表），用于备份/改后再导。</summary>
    Task<byte[]> ExportAsync();

    /// <summary>生成单 sheet 空模板（kind=category|tier，中文表头 + 1 示例行）。</summary>
    Task<byte[]> GenerateTemplateAsync(string kind);

    /// <summary>解析 + 校验 + 统计（复用 DataTool ImportPreviewResult 契约，预览与导入同口径）。</summary>
    Task<ImportPreviewResult> PreviewImportAsync(string kind, byte[] fileData);

    /// <summary>事务内覆盖更新；任一数据行无效则整体拒绝（组级原子性）。</summary>
    Task<ImportResult> ImportAsync(string kind, byte[] fileData);
}
