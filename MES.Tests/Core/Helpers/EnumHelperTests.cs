using FluentAssertions;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 枚举显示辅助测试：配置覆盖（显示名 + 排序）注入、GetDisplayOptions 排序、静态注册顺序兜底。
/// 注意：EnumHelper 为进程级静态状态，每个用例 finally 中 ClearEnumOverrides 复位。
/// </summary>
public class EnumHelperTests
{
    [Fact]
    public void GetDisplayOptions_未注入排序_静态注册顺序()
    {
        var options = EnumHelper.GetDisplayOptions<BatchStatus>();

        options.Select(o => o.Value).Should().Equal(
            "None", "InProgress", "InFinalInspection", "Completed", "Suspended");
    }

    [Fact]
    public void ApplyEnumOrder_注入后_按DisplayOrder排序()
    {
        try
        {
            EnumHelper.ApplyEnumOrder("BatchStatus", new Dictionary<string, int>
            {
                ["Suspended"] = 1,
                ["None"] = 2,
                ["InProgress"] = 3,
                ["Completed"] = 4,
                ["InFinalInspection"] = 5
            });

            var options = EnumHelper.GetDisplayOptions<BatchStatus>();

            options.Select(o => o.Value).Should().Equal(
                "Suspended", "None", "InProgress", "Completed", "InFinalInspection");
        }
        finally
        {
            EnumHelper.ClearEnumOverrides();
        }
    }

    [Fact]
    public void ApplyEnumOrder_只注入部分值_未注入值按静态序排后()
    {
        try
        {
            // 只注入两个值的顺序，其余保持静态注册顺序
            EnumHelper.ApplyEnumOrder("BatchStatus", new Dictionary<string, int>
            {
                ["Suspended"] = 1,
                ["None"] = 2,
            });

            var options = EnumHelper.GetDisplayOptions<BatchStatus>();

            options.Select(o => o.Value).Should().Equal(
                "Suspended", "None", "InProgress", "InFinalInspection", "Completed");
        }
        finally
        {
            EnumHelper.ClearEnumOverrides();
        }
    }

    [Fact]
    public void ApplyEnumOverrides_显示名覆盖_反向解析可用()
    {
        try
        {
            EnumHelper.ApplyEnumOverrides("BatchStatus", new Dictionary<string, string>
            {
                ["None"] = "未投产",
                ["InProgress"] = "在产"
            });

            EnumHelper.GetDisplayName(BatchStatus.None).Should().Be("未投产");
            EnumHelper.Parse<BatchStatus>("未投产").Should().Be(BatchStatus.None);
            // 覆盖不影响未配置值
            EnumHelper.GetDisplayName(BatchStatus.Completed).Should().Be("完成");
        }
        finally
        {
            EnumHelper.ClearEnumOverrides();
        }
    }

    [Fact]
    public void ClearEnumOverrides_覆盖与排序均复位()
    {
        try
        {
            EnumHelper.ApplyEnumOverrides("BatchStatus", new Dictionary<string, string> { ["None"] = "未投产" });
            EnumHelper.ApplyEnumOrder("BatchStatus", new Dictionary<string, int> { ["Suspended"] = 1 });
        }
        finally
        {
            EnumHelper.ClearEnumOverrides();
        }

        EnumHelper.GetDisplayName(BatchStatus.None).Should().Be("未产");
        EnumHelper.GetDisplayOptions<BatchStatus>().Select(o => o.Value).Should().Equal(
            "None", "InProgress", "InFinalInspection", "Completed", "Suspended");
    }

    [Fact]
    public void GetDisplayOptions_显示名跟随覆盖()
    {
        try
        {
            EnumHelper.ApplyEnumOverrides("BatchStatus", new Dictionary<string, string>
            {
                ["InProgress"] = "生产中"
            });

            var options = EnumHelper.GetDisplayOptions<BatchStatus>();
            options.Single(o => o.Value == "InProgress").DisplayName.Should().Be("生产中");
        }
        finally
        {
            EnumHelper.ClearEnumOverrides();
        }
    }

    [Fact]
    public void GetEnumRemark_已注册说明枚举_返回定义说明()
    {
        EnumHelper.GetEnumRemark("VerifyResult").Should().Be("纠正预防措施验证结论");
        EnumHelper.GetEnumRemark("NcrStatus").Should().Be("NCR 不合格品报告状态");
    }

    [Fact]
    public void GetEnumRemark_未定义说明枚举_返回null()
    {
        EnumHelper.GetEnumRemark("DeliveryState").Should().BeNull();
        EnumHelper.GetEnumRemark("NotExistEnum").Should().BeNull();
    }

    [Fact]
    public void EnumDisplayDefinitionDto_RemarkDisplay_Remark优先于静态兜底()
    {
        var withRemark = new MES.Core.DTOs.Configuration.EnumDisplayDefinitionDto
        {
            EnumKey = "VerifyResult",
            Value = "Passed",
            DisplayName = "通过",
            Remark = "自定义说明"
        };
        withRemark.RemarkDisplay.Should().Be("自定义说明");

        var emptyRemark = new MES.Core.DTOs.Configuration.EnumDisplayDefinitionDto
        {
            EnumKey = "VerifyResult",
            Value = "Passed",
            DisplayName = "通过",
            Remark = null
        };
        emptyRemark.RemarkDisplay.Should().Be("纠正预防措施验证结论");
    }
}
