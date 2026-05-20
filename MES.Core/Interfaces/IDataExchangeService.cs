using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 实体元数据
/// </summary>
public class EntityInfo
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 数据导入导出服务接口
/// </summary>
public interface IDataExchangeService
{
    /// <summary>
    /// 获取所有支持的实体类型列表
    /// </summary>
    Task<List<EntityInfo>> GetEntitiesAsync();

    /// <summary>
    /// 获取实体显示名称（用于文件名）
    /// </summary>
    string GetEntityDisplayName(string entityKey);

    /// <summary>
    /// 导出指定实体的全部数据为 Excel 字节数组
    /// </summary>
    Task<byte[]> ExportAsync(string entityKey);

    /// <summary>
    /// 生成导入模板（含示例行）
    /// </summary>
    Task<byte[]> GenerateTemplateAsync(string entityKey);

    /// <summary>
    /// 预览导入结果（验证但不写入数据库）
    /// </summary>
    Task<ImportPreviewResult> PreviewAsync(string entityKey, byte[] fileData, string? userName);

    /// <summary>
    /// 执行导入（事务内操作）
    /// </summary>
    Task<ImportResult> ImportAsync(string entityKey, byte[] fileData, string strategy, string? userName);

    /// <summary>
    /// 修复现有生产记录中错误的 SequenceNumber 值
    /// </summary>
    Task<int> FixSequenceNumbersAsync();
}
