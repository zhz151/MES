namespace MES.Blazor.Shared;

/// <summary>
/// 扫码报工照片项（巡检照片 / 整改验证照片 / 问题照片 共用）。
/// <see cref="AttachmentId"/> 为空表示尚未上传（提交后随主记录一并上传）；
/// 非空表示已落库的既有照片（第二次扫码呈现第一次结果时加载）。
/// </summary>
public class ScanPhotoItem
{
    /// <summary>已保存附件的 Id（为空表示待上传）</summary>
    public int? AttachmentId { get; set; }

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "image/jpeg";

    /// <summary>base64（不含 data: 前缀）</summary>
    public string Base64 { get; set; } = "";

    /// <summary>Blob URL 预览地址</summary>
    public string PreviewUrl { get; set; } = "";
}
