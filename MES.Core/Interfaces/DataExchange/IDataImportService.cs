using MES.Core.Models;

namespace MES.Core.Interfaces.DataExchange;

/// <summary>
/// 数据导入服务
/// </summary>
public interface IDataImportService
{
    /// <summary>
    /// 预览导入结果（验证但不写入数据库）
    /// </summary>
    Task<ImportPreviewResult> PreviewAsync(string entityKey, byte[] fileData, string? userName);

    /// <summary>
    /// 执行导入（事务内操作）
    /// </summary>
    Task<ImportResult> ImportAsync(string entityKey, byte[] fileData, string? userName);
}
