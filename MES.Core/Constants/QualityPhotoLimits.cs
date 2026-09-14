namespace MES.Core.Constants;

/// <summary>
/// 质量上下文照片张数上限（单一出口，前后端同源引用）。
/// 值经 2026-09-12 二次拍板（配合打印照片页「单列每页 2 张 / 290pt」版式）：
/// 客户端压缩后（最长边 1600px / JPEG q0.8）单张约 250~400KB，4 张/条 ≈ 1.6MB，服务器磁盘可长期承受。
/// </summary>
public static class QualityPhotoLimits
{
    /// <summary>按「记录」计数的模块上限（过程检验 / 成品检验 / 不合格反馈）</summary>
    public const int PerRecord = 4;

    /// <summary>按「照片类型」计数的模块上限（巡检单：巡检照片 / 整改验证照片 各 4 张 → 单张单共 8 张）</summary>
    public const int PerType = 4;
}
