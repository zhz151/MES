namespace MES.Core.Enums;

/// <summary>
/// 纠正预防措施验证结论
/// </summary>
public enum VerifyResult
{
    /// <summary>通过</summary>
    Passed,
    /// <summary>需整改</summary>
    NeedsRectification,
    /// <summary>不适用</summary>
    NotApplicable
}
