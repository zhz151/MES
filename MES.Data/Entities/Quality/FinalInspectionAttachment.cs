namespace MES.Data.Entities.Quality;

/// <summary>
/// 成品检验记录附件 — 检验现场照片（扫码填报时可直接拍，手动录入后在列表「照片」弹窗补拍）。
/// 文件本体存服务器文件系统（路径由 Attachment:RootPath 配置），本表只存元数据。
/// </summary>
public class FinalInspectionAttachment : BaseEntity
{
    /// <summary>
    /// 所属成品检验记录ID
    /// </summary>
    public int FinalInspectionId { get; set; }

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
    /// 所属成品检验记录
    /// </summary>
    public FinalInspection FinalInspection { get; set; } = null!;
}
