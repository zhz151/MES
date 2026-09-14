using System.Runtime.CompilerServices;
using FluentAssertions;

namespace MES.Tests.Components;

/// <summary>
/// 防回归：.razor 文件行首的 Razor 指令（<c>@page</c> / <c>@using</c> / <c>@inject</c> ...）若丢了开头的
/// <c>@</c>，Razor 会把这一行当成**普通 HTML 文本**渲染 —— **编译不报错**（语法合法），但 <c>@page</c>
/// 不再注册路由，页面在运行时全部落到 <c>App.razor</c> 的 <c>&lt;NotFound&gt;</c>。
///
/// 2026-09-14 线上事故即此：用行级脚本给各列表页 <c>&lt;MudTable&gt;</c> 批量补 `Breakpoint="Breakpoint.None"`
/// 时吞掉了 **95 个 .razor 的首字符**，95 个列表页路由静默消失，登录后点任何菜单都显示「抱歉，此页面不存在」。
/// 编译器与既有测试都抓不到（丢 '@' 后语法完全合法），只能靠本测试兜住。
///
/// 改动 .razor 的**任何批量脚本**跑完，务必执行本测试。
/// </summary>
public class RazorDirectiveIntegrityTests
{
    /// <summary>Razor 结构指令关键字；这些词出现在行首却**不带** '@' 即为损坏。</summary>
    private static readonly HashSet<string> Directives = new(StringComparer.Ordinal)
    {
        "page", "inject", "using", "inherits", "layout", "namespace", "attribute",
        "implements", "typeparam", "preservewhitespace", "rendermode", "model",
        "addTagHelper", "tagHelperPrefix", "pageTitle", "section", "functions", "code",
    };

    /// <summary>仓库根目录：由本测试源文件位置（MES.Tests/Components/）上溯两级得到，与运行目录无关。</summary>
    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Fact]
    public void Razor文件不得出现丢失At符号的裸指令()
    {
        var razorDir = Path.Combine(RepoRoot(), "MES.Blazor");
        Directory.Exists(razorDir).Should().BeTrue($"找不到 MES.Blazor 目录：{razorDir}");

        var files = Directory.EnumerateFiles(razorDir, "*.razor", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        files.Should().NotBeEmpty();

        var offenses = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var sep = line.IndexOfAny([' ', '\t']);
                if (sep <= 0) continue;
                if (Directives.Contains(line[..sep]))
                {
                    offenses.Add($"{Path.GetRelativePath(razorDir, file)}:{i + 1}: {line.Trim()}");
                }
            }
        }

        offenses.Should().BeEmpty(
            "以下 .razor 行首的 Razor 指令丢了 '@'：会被当纯文本渲染，@page 不再注册路由，"
            + "编译不报错但该页面在运行时落到 NotFound（显示「抱歉，此页面不存在」）：\n"
            + string.Join("\n", offenses));
    }
}
