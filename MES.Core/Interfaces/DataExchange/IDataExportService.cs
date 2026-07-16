namespace MES.Core.Interfaces.DataExchange;

/// <summary>
/// 数据导出服务
/// </summary>
public interface IDataExportService
{
    /// <summary>
    /// 导出指定实体的全部数据为 Excel 字节数组
    /// </summary>
    Task<byte[]> ExportAsync(string entityKey);

    /// <summary>
    /// 生成导入模板（含示例行）
    /// </summary>
    Task<byte[]> GenerateTemplateAsync(string entityKey);
}
