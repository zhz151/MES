namespace MES.Data.Entities.Quality;

/// <summary>
/// 巡检单附件 — 按 PhotoType 区分「巡检照片」与「整改验证照片」，每类各限 9 张。
/// 文件本体存服务器文件系统（路径由 Attachment:RootPath 配置），本表只存元数据。
/// </summary>
public class InspectionPatrolAttachment : BaseEntity
{
    /// <summary>
    /// 所属巡检单ID
    /// </summary>
    public int PatrolId { get; set; }

    /// <summary>
    /// 照片类型（InspectionPatrolPhotoTypes 常量：Patrol=巡检照片 / Rectification=整改验证照片）
    /// </summary>
    public string PhotoType { get; set; } = null!;

    /// <summary>
    /// 原始文件名（仅展示用）
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// 存储文件名（GUID + 扩展名，防碰撞与目录遍历）
    /// </summary>
    public string StoredName { get; set; } = null!;

    /// <summary>
    /// 内容类型（image/jpeg、image/png 等）
    /// </summary>
    public string ContentType { get; set; } = null!;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int SortOrder { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属巡检单
    /// </summary>
    public InspectionPatrol Patrol { get; set; } = null!;
}
