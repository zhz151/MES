using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Infrastructure;

namespace MES.Services.Infrastructure;

/// <summary>
/// 附件文件存储实现 — 文件本体落服务器文件系统，根目录由 <c>Attachment:RootPath</c> 配置
/// （缺省为程序目录下 attachments）。
/// 存储名一律由服务端生成 GUID+扩展名，用户无法控制路径；读写前再做一次根目录归一化校验防目录遍历。
/// </summary>
public class AttachmentStorage : IAttachmentStorage
{
    /// <summary>图片扩展名白名单</summary>
    private static readonly string[] Allowed = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };

    private const long DefaultMaxBytes = 5 * 1024 * 1024;

    private readonly string _rootPath;
    private readonly ILogger<AttachmentStorage> _logger;

    public AttachmentStorage(IConfiguration configuration, ILogger<AttachmentStorage> logger)
    {
        _logger = logger;

        var configured = configuration.GetValue<string>("Attachment:RootPath");
        _rootPath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "attachments")
            : configured;

        MaxFileSizeBytes = configuration.GetValue<long?>("Attachment:MaxFileSizeBytes") ?? DefaultMaxBytes;
    }

    public long MaxFileSizeBytes { get; }

    public IReadOnlyCollection<string> AllowedExtensions => Allowed;

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName)?.ToLowerInvariant() ?? string.Empty;
        if (!Allowed.Contains(extension))
            throw new BusinessException($"不支持的附件格式，仅支持 {string.Join(" / ", Allowed)}");

        if (content.CanSeek && content.Length > MaxFileSizeBytes)
            throw new BusinessException($"附件大小超过上限（{MaxFileSizeBytes / 1024 / 1024} MB）");

        Directory.CreateDirectory(_rootPath);

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_rootPath, storedName);

        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        // 兜底：流式写入前无法预知大小时，写完后复查实际大小，超限即删除
        if (new FileInfo(fullPath).Length > MaxFileSizeBytes)
        {
            File.Delete(fullPath);
            throw new BusinessException($"附件大小超过上限（{MaxFileSizeBytes / 1024 / 1024} MB）");
        }

        return storedName;
    }

    public async Task<byte[]?> ReadAsync(string storedName, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(storedName);
        if (fullPath == null || !File.Exists(fullPath)) return null;
        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public Task DeleteAsync(string storedName, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(storedName);
        if (fullPath != null && File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                // 文件删除失败不应阻断业务（数据库记录已删，残留文件由运维清理）
                _logger.LogWarning(ex, "删除附件文件失败：{StoredName}", storedName);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 把存储名解析为根目录内的绝对路径；含路径分隔符或 .. 或越出根目录时返回 null。
    /// </summary>
    private string? ResolveSafePath(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName)) return null;
        if (storedName.Contains("..") || storedName.Contains('/') || storedName.Contains('\\')) return null;

        var root = Path.GetFullPath(_rootPath);
        var full = Path.GetFullPath(Path.Combine(root, storedName));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;

        return full;
    }
}
