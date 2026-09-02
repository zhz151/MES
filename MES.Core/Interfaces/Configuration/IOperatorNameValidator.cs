using MES.Core.Helpers;

namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 操作人实名校验：返回启用员工快照，供 6 类报工提交硬校验复用。
/// </summary>
public interface IOperatorNameValidator
{
    /// <summary>
    /// 加载全部启用员工快照（工号→姓名 + 姓名集合）。批量提交时调用一次，逐行用静态 FindUnmatched。
    /// </summary>
    Task<ActiveEmployeeSet> LoadActiveAsync();

    /// <summary>
    /// 单条校验：操作人为空/空白直接通过；未命中启用员工即抛 BusinessException。
    /// </summary>
    /// <param name="operatorText">操作人显示串（姓名(工号)、多人「、」连接）</param>
    /// <param name="rowLabel">批量场景行前缀，如「第2行」；单条传 null 不带前缀</param>
    Task EnsureValidOrThrowAsync(string? operatorText, string? rowLabel = null);
}
