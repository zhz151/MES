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
    public void GetText_Ncr责任类别_无覆盖_Keys兜底中文()
    {
        DictValueDisplayHelper.OverrideMap = null;
        DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, "ProductionInternal").Should().Be("生产-厂内");
        DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, "MaterialSurplus").Should().Be("原料-余库料");
        DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, "NcrRC_1").Should().Be("NcrRC_1"); // 新加值原样
        DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, null).Should().BeNull();
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
    public void GetText_原锁备注_无覆盖_Keys兜底中文()
    {
        DictValueDisplayHelper.OverrideMap = null;
        DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, "QualityReplenish").Should().Be("A质量补料");
        DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, "ImprovePlan").Should().Be("D完善计划");
        DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, null).Should().BeNull();
    }

    [Fact]
    public void GetText_生产关注_无覆盖_Keys兜底中文()
    {
        DictValueDisplayHelper.OverrideMap = null;
        DictValueDisplayHelper.GetText(DictValueDefaults.ProductionAttentionKey, "ProductionFinish").Should().Be("生产收尾");
        DictValueDisplayHelper.GetText(DictValueDefaults.ProductionAttentionKey, "UnknownKey").Should().Be("UnknownKey");
    }

    [Fact]
    public void DictKeys_不含工段工序_防配置页双入口()
    {
        // 工段/工序由专门配置表管理，不得在配置页下拉暴露（否则恢复默认会注入死行）
        DictValueDefaults.DictKeys.Should().NotContain(DictValueDefaults.SectionKey);
        DictValueDefaults.DictKeys.Should().NotContain(DictValueDefaults.ProcessKey);
        // 新增 2 字典必须可配置
        DictValueDefaults.DictKeys.Should().Contain(DictValueDefaults.RawMaterialLockRemarkKey);
        DictValueDefaults.DictKeys.Should().Contain(DictValueDefaults.ProductionAttentionKey);
        DictValueDefaults.DictKeys.Should().Contain(DictValueDefaults.NcrResponsibilityKey);
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
