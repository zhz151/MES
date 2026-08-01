namespace MES.Core.Models;

/// <summary>
/// 数据回填操作结果
/// </summary>
public class BackfillResultDto
{
    /// <summary>
    /// 总处理记录数
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// 成功更新记录数
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 未能匹配的记录数
    /// </summary>
    public int UnmatchedCount { get; set; }

    /// <summary>
    /// 存在多个项次组合满足 TotalQuantity 需人工确认的记录数
    /// </summary>
    public int AmbiguousCount { get; set; }

    /// <summary>
    /// 错误信息列表
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
