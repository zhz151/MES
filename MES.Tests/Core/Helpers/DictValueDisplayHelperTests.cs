using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 字典显示辅助测试：OverrideMap 配置优先 → Keys 常量兜底 → 原样返回。
/// OverrideMap 为进程级静态状态，每个用例 finally 复位。
/// </summary>
public class DictValueDisplayHelperTests
{
    [Fact]
    public void GetText_无覆盖_Keys兜底中文()
    {
        DictValueDisplayHelper.OverrideMap = null;
        DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, "RoughTube").Should().Be("荒管");
        DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, "Urgent").Should().Be(UrgencyLevelKeys.ToChinese("Urgent"));
        DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, null).Should().BeNull();
        DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, "").Should().BeNull();
    }

    [Fact]
    public void GetText_无覆盖_未知值原样返回()
    {
        DictValueDisplayHelper.OverrideMap = null;
        DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, "NewKey").Should().Be("NewKey");
    }

    [Fact]
    public void GetText_覆盖优先_未覆盖兜底()
    {
        try
        {
            DictValueDisplayHelper.OverrideMap = new Dictionary<string, Dictionary<string, string>>
            {
                [DictValueDefaults.ProductStatus] = new()
                {
                    ["RoughTube"] = "荒管（新）"
                }
            };

            DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, "RoughTube").Should().Be("荒管（新）");
            DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, "InProgress").Should().Be("在制");
        }
        finally
        {
            DictValueDisplayHelper.OverrideMap = null;
        }
    }

    [Fact]
    public void GetText_覆盖含新加值()
    {
        try
        {
            DictValueDisplayHelper.OverrideMap = new Dictionary<string, Dictionary<string, string>>
            {
                [DictValueDefaults.ProductStatus] = new()
                {
                    ["NewValue"] = "新产类"
                }
            };

            DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, "NewValue").Should().Be("新产类");
        }
        finally
        {
            DictValueDisplayHelper.OverrideMap = null;
        }
    }
}
