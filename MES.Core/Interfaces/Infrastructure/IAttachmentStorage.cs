namespace MES.Core.Interfaces.Infrastructure;

/// <summary>
/// 附件文件存储 — 文件本体落服务器文件系统（根目录由 Attachment:RootPath 配置），
/// 数据库只存元数据（原始名/存储名/内容类型/大小）。
/// </summary>
public interface IAttachmentStorage
{
    /// <summary>单文件大小上限（字节）</summary>
    long MaxFileSizeBytes { get; }

    /// <summary>允许的扩展名白名单（小写，含点，如 ".jpg"）</summary>
    IReadOnlyCollection<string> AllowedExtensions { get; }

    /// <summary>
    /// 保存文件，返回存储文件名（GUID + 扩展名）。
    /// 校验扩展名白名单与大小上限，非法时抛 <c>BusinessException</c>。
    /// </summary>
    /// <param name="content">文件流</param>
    /// <param name="originalFileName">原始文件名（仅用于取扩展名与校验）</param>
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken cancellationToken = default);

    /// <summary>读取文件内容；文件不存在返回 null。</summary>
    Task<byte[]?> ReadAsync(string storedName, CancellationToken cancellationToken = default);

    /// <summary>删除文件；文件不存在则忽略。</summary>
    Task DeleteAsync(string storedName, CancellationToken cancellationToken = default);
}
