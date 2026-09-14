using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MES.Core.Exceptions;
using MES.Services.Infrastructure;

namespace MES.Tests.Services.Infrastructure;

/// <summary>
/// 附件文件存储测试：扩展名白名单、大小上限、目录遍历防护、读写删。
/// </summary>
public class AttachmentStorageTests : IDisposable
{
    private readonly string _root;

    public AttachmentStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"mes_att_{Guid.NewGuid():N}");
    }

    private AttachmentStorage CreateStorage(long? maxBytes = null)
    {
        var settings = new Dictionary<string, string?> { ["Attachment:RootPath"] = _root };
        if (maxBytes.HasValue)
            settings["Attachment:MaxFileSizeBytes"] = maxBytes.Value.ToString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new AttachmentStorage(config, NullLogger<AttachmentStorage>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SaveAsync_保存并可按存储名读回()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(new byte[] { 7, 8, 9 });

        var storedName = await storage.SaveAsync(content, "问题照片.JPG");

        storedName.Should().EndWith(".jpg");                 // 扩展名归一为小写
        Path.IsPathRooted(storedName).Should().BeFalse();    // 只返回文件名，不含路径
        (await storage.ReadAsync(storedName)).Should().Equal(7, 8, 9);
    }

    [Fact]
    public async Task SaveAsync_不支持扩展名_抛业务异常()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(new byte[] { 1 });

        var act = () => storage.SaveAsync(content, "evil.exe");

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task SaveAsync_超过大小上限_抛业务异常且不留残留文件()
    {
        var storage = CreateStorage(maxBytes: 10);
        using var content = new MemoryStream(new byte[50]);

        var act = () => storage.SaveAsync(content, "big.jpg");

        await act.Should().ThrowAsync<BusinessException>();
        (Directory.Exists(_root) ? Directory.GetFiles(_root) : Array.Empty<string>()).Should().BeEmpty();
    }

    [Fact]
    public void MaxFileSizeBytes_未配置时回退默认5MB()
    {
        var storage = CreateStorage();

        storage.MaxFileSizeBytes.Should().Be(5 * 1024 * 1024);
        storage.AllowedExtensions.Should().Contain(".jpg").And.Contain(".png");
    }

    [Fact]
    public async Task ReadAsync_文件不存在_返回null()
    {
        var storage = CreateStorage();

        (await storage.ReadAsync("nope.jpg")).Should().BeNull();
    }

    [Theory]
    [InlineData("../escape.jpg")]
    [InlineData("..\\escape.jpg")]
    [InlineData("sub/dir.jpg")]
    [InlineData("")]
    public async Task ReadAsync_路径穿越或空名_返回null(string storedName)
    {
        var storage = CreateStorage();

        (await storage.ReadAsync(storedName)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_删除已存在文件_不存在不抛异常()
    {
        var storage = CreateStorage();
        var storedName = await storage.SaveAsync(new MemoryStream(new byte[] { 1 }), "a.png");

        await storage.DeleteAsync(storedName);
        (await storage.ReadAsync(storedName)).Should().BeNull();

        var act = () => storage.DeleteAsync(storedName);
        await act.Should().NotThrowAsync();
    }
}
