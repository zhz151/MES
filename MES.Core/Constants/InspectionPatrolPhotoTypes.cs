namespace MES.Core.Constants;

/// <summary>
/// 巡检单照片类型（存英文 Key，显示中文）— 附件子表 InspectionPatrolAttachment.PhotoType。
/// 每类各限 <see cref="MaxPerType"/> 张（上限值单一出口见 <see cref="QualityPhotoLimits.PerType"/>）。
/// </summary>
public static class InspectionPatrolPhotoTypes
{
    /// <summary>巡检照片</summary>
    public const string Patrol = "Patrol";

    /// <summary>整改验证照片</summary>
    public const string Rectification = "Rectification";

    /// <summary>每类照片张数上限</summary>
    public const int MaxPerType = QualityPhotoLimits.PerType;

    /// <summary>全部类型（校验用）</summary>
    public static readonly string[] All = { Patrol, Rectification };

    /// <summary>是否为合法类型</summary>
    public static bool IsValid(string? key)
        => !string.IsNullOrWhiteSpace(key) && Array.IndexOf(All, key) >= 0;

    /// <summary>英文 Key → 中文显示（未知值原样返回）</summary>
    public static string ToChinese(string? key) => key switch
    {
        Patrol => "巡检照片",
        Rectification => "整改验证照片",
        _ => key ?? ""
    };
}
