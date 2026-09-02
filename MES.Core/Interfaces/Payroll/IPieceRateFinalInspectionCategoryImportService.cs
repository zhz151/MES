using MES.Core.Models;

namespace MES.Core.Interfaces.Payroll;

/// <summary>
/// 成检计件类别专用批量导入/导出服务（2026-09-03，工资结算数据维护闭环）。
/// 定位键 = 成检项目（InspectionItem，同项目启用唯一；类别定义与维档系数分离，职责各一）：
///   Category 模板：定位命中类别 → 整组更新主属性（基准价/单位/启停/备注，绝不清空既有档行）；未命中 → 新建（无档行）。
///   Tier 模板：定位未命中 → 整行报错「请先导入类别定义」；命中 → 该类别 Tiers 整组替换为文件行。
/// 任一数据行无效 → 整体拒绝入库（组级原子性，预览与导入同口径解析）。
/// 模板/导出列值全用中文域值（中英容忍）：成检项目/长度状态/特殊制造状态显示中文、导出→改→再导闭环。
/// </summary>
public interface IPieceRateFinalInspectionCategoryImportService
{
    /// <summary>导出全量成检类别标准 → xlsx（Sheet「类别」+「维档」双表），用于备份/改后再导。</summary>
    Task<byte[]> ExportAsync();

    /// <summary>生成单 sheet 空模板（kind=category|tier，中文表头 + 1 示例行）。</summary>
    Task<byte[]> GenerateTemplateAsync(string kind);

    /// <summary>解析 + 校验 + 统计（复用 DataTool ImportPreviewResult 契约，预览与导入同口径）。</summary>
    Task<ImportPreviewResult> PreviewImportAsync(string kind, byte[] fileData);

    /// <summary>事务内覆盖更新；任一数据行无效则整体拒绝（组级原子性）。</summary>
    Task<ImportResult> ImportAsync(string kind, byte[] fileData);
}
