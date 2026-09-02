using FluentAssertions;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 生产计件类别「自动组合名」纯函数测试（2026-09-02，§3.2）。
/// 展示名 = 工段中文 ｜ 产类中文集(空=全部产类) ｜ 工序中文集(空=全部工序) ｜ 阶段中文集(空=全部阶段)。
/// </summary>
public class CategoryDisplayNameHelperTests
{
    [Fact]
    public void Build_全选形态显示全部占位()
    {
        var name = CategoryDisplayNameHelper.Build("酸洗", null, null, null);
        name.Should().Be("酸洗｜全部产类｜全部工序｜全部阶段");
    }

    [Fact]
    public void Build_空集合等价全选()
    {
        var name = CategoryDisplayNameHelper.Build("酸洗", [], [], []);
        name.Should().Be("酸洗｜全部产类｜全部工序｜全部阶段");
    }

    [Fact]
    public void Build_多值用点连接_单值集正常()
    {
        var name = CategoryDisplayNameHelper.Build("酸洗", ["荒管"], null, ["出缸"]);
        name.Should().Be("酸洗｜荒管｜全部工序｜出缸");
    }

    [Fact]
    public void Build_产类工序都具体()
    {
        var name = CategoryDisplayNameHelper.Build("矫直", ["在制", "成品"], ["荒管处理", "冷拔"], null);
        name.Should().Be("矫直｜在制·成品｜荒管处理·冷拔｜全部阶段");
    }
}
