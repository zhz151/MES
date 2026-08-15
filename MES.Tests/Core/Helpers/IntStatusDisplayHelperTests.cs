using FluentAssertions;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 第三类 int 档位字段统一出口测试：
/// 1) Text 中文映射正确（含未知值兜底）；
/// 2) Options 选项与 Text 一致（下拉选项的中文永不漂移）；
/// 3) 各 DTO 的 XxxText 计算属性走 Helper（消除内嵌 switch 后的行为一致性）。
/// </summary>
public class IntStatusDisplayHelperTests
{
    // ========== Text 中文映射 ==========

    [Fact]
    public void GetInputStatusText_4档映射()
    {
        IntStatusDisplayHelper.GetInputStatusText(0).Should().Be("未投料");
        IntStatusDisplayHelper.GetInputStatusText(1).Should().Be("部分");
        IntStatusDisplayHelper.GetInputStatusText(2).Should().Be("满足");
        IntStatusDisplayHelper.GetInputStatusText(3).Should().Be("超量");
        IntStatusDisplayHelper.GetInputStatusText(-1).Should().Be("未知");
        IntStatusDisplayHelper.GetInputStatusText(99).Should().Be("未知");
    }

    [Fact]
    public void GetMainNoFlowStatusText_4档映射()
    {
        IntStatusDisplayHelper.GetMainNoFlowStatusText(0).Should().Be("未计划");
        IntStatusDisplayHelper.GetMainNoFlowStatusText(1).Should().Be("部分");
        IntStatusDisplayHelper.GetMainNoFlowStatusText(2).Should().Be("满足");
        IntStatusDisplayHelper.GetMainNoFlowStatusText(3).Should().Be("超量");
        IntStatusDisplayHelper.GetMainNoFlowStatusText(5).Should().Be("未知");
    }

    [Fact]
    public void GetMainNoPlanExecutionStatusText_4档映射()
    {
        IntStatusDisplayHelper.GetMainNoPlanExecutionStatusText(0).Should().Be("无计划");
        IntStatusDisplayHelper.GetMainNoPlanExecutionStatusText(1).Should().Be("未执行");
        IntStatusDisplayHelper.GetMainNoPlanExecutionStatusText(2).Should().Be("执行中");
        IntStatusDisplayHelper.GetMainNoPlanExecutionStatusText(3).Should().Be("计划落实");
        IntStatusDisplayHelper.GetMainNoPlanExecutionStatusText(9).Should().Be("未知");
    }

    [Fact]
    public void GetScheduleStageText_5档映射_支持null与fallback()
    {
        IntStatusDisplayHelper.GetScheduleStageText(0).Should().Be("主号暂停");
        IntStatusDisplayHelper.GetScheduleStageText(1).Should().Be("主号完成");
        IntStatusDisplayHelper.GetScheduleStageText(2).Should().Be("原料锁定");
        IntStatusDisplayHelper.GetScheduleStageText(3).Should().Be("生产执行");
        IntStatusDisplayHelper.GetScheduleStageText(4).Should().Be("成品检验");
        IntStatusDisplayHelper.GetScheduleStageText(null).Should().Be("未知");
        IntStatusDisplayHelper.GetScheduleStageText(99).Should().Be("未知");
        // fallback 参数：未排产等业务空值语义
        IntStatusDisplayHelper.GetScheduleStageText(null, "未排产").Should().Be("未排产");
        IntStatusDisplayHelper.GetScheduleStageText(3, "未排产").Should().Be("生产执行");
    }

    [Fact]
    public void GetPlanScheduleStageText_4档映射()
    {
        IntStatusDisplayHelper.GetPlanScheduleStageText(0).Should().Be("主号完成");
        IntStatusDisplayHelper.GetPlanScheduleStageText(1).Should().Be("原料锁定");
        IntStatusDisplayHelper.GetPlanScheduleStageText(2).Should().Be("生产执行");
        IntStatusDisplayHelper.GetPlanScheduleStageText(3).Should().Be("成品检验");
        IntStatusDisplayHelper.GetPlanScheduleStageText(null).Should().Be("未知");
        IntStatusDisplayHelper.GetPlanScheduleStageText(4).Should().Be("未知");
    }

    [Fact]
    public void GetWarehousingStatusText_4档映射()
    {
        IntStatusDisplayHelper.GetWarehousingStatusText(0).Should().Be("无入库");
        IntStatusDisplayHelper.GetWarehousingStatusText(1).Should().Be("入库部分");
        IntStatusDisplayHelper.GetWarehousingStatusText(2).Should().Be("入库完结");
        IntStatusDisplayHelper.GetWarehousingStatusText(3).Should().Be("入库超额");
        IntStatusDisplayHelper.GetWarehousingStatusText(4).Should().Be("未知");
    }

    [Fact]
    public void GetMainNoWarehousingStatusText_4档映射()
    {
        IntStatusDisplayHelper.GetMainNoWarehousingStatusText(0).Should().Be("无入库");
        IntStatusDisplayHelper.GetMainNoWarehousingStatusText(1).Should().Be("入库部分");
        IntStatusDisplayHelper.GetMainNoWarehousingStatusText(2).Should().Be("入库完结");
        IntStatusDisplayHelper.GetMainNoWarehousingStatusText(3).Should().Be("入库超额");
        IntStatusDisplayHelper.GetMainNoWarehousingStatusText(4).Should().Be("未知");
    }

    [Fact]
    public void GetPlanExecutionStatusText_5档映射()
    {
        IntStatusDisplayHelper.GetPlanExecutionStatusText(0).Should().Be("无计划");
        IntStatusDisplayHelper.GetPlanExecutionStatusText(1).Should().Be("未执行");
        IntStatusDisplayHelper.GetPlanExecutionStatusText(2).Should().Be("部分");
        IntStatusDisplayHelper.GetPlanExecutionStatusText(3).Should().Be("已完成");
        IntStatusDisplayHelper.GetPlanExecutionStatusText(4).Should().Be("异常");
        IntStatusDisplayHelper.GetPlanExecutionStatusText(9).Should().Be("未知");
    }

    [Fact]
    public void GetPlanInputConsistencyText_7档映射()
    {
        IntStatusDisplayHelper.GetPlanInputConsistencyText(0).Should().Be("一致");
        IntStatusDisplayHelper.GetPlanInputConsistencyText(1).Should().Be("待投");
        IntStatusDisplayHelper.GetPlanInputConsistencyText(2).Should().Be("疑问-到料少投");
        IntStatusDisplayHelper.GetPlanInputConsistencyText(3).Should().Be("疑问-到料超投");
        IntStatusDisplayHelper.GetPlanInputConsistencyText(4).Should().Be("错误-无料已投");
        IntStatusDisplayHelper.GetPlanInputConsistencyText(5).Should().Be("错误-无需投料");
        IntStatusDisplayHelper.GetPlanInputConsistencyText(6).Should().Be("略");
        IntStatusDisplayHelper.GetPlanInputConsistencyText(9).Should().Be("未知");
    }

    // ========== Options 与 Text 一致性 ==========

    [Fact]
    public void GetInputStatusOptions_与Text一致()
    {
        var options = IntStatusDisplayHelper.GetInputStatusOptions();
        options.Should().HaveCount(4);
        for (int i = 0; i < options.Count; i++)
        {
            options[i].Value.Should().Be(i.ToString());
            options[i].DisplayName.Should().Be(IntStatusDisplayHelper.GetInputStatusText(i));
        }
    }

    [Fact]
    public void GetMainNoFlowStatusOptions_与Text一致()
    {
        var options = IntStatusDisplayHelper.GetMainNoFlowStatusOptions();
        options.Should().HaveCount(4);
        for (int i = 0; i < options.Count; i++)
        {
            options[i].Value.Should().Be(i.ToString());
            options[i].DisplayName.Should().Be(IntStatusDisplayHelper.GetMainNoFlowStatusText(i));
        }
    }

    [Fact]
    public void GetMainNoPlanExecutionStatusOptions_与Text一致()
    {
        var options = IntStatusDisplayHelper.GetMainNoPlanExecutionStatusOptions();
        options.Should().HaveCount(4);
        for (int i = 0; i < options.Count; i++)
        {
            options[i].Value.Should().Be(i.ToString());
            options[i].DisplayName.Should().Be(IntStatusDisplayHelper.GetMainNoPlanExecutionStatusText(i));
        }
    }

    [Fact]
    public void GetScheduleStageOptions_与Text一致()
    {
        var options = IntStatusDisplayHelper.GetScheduleStageOptions();
        options.Should().HaveCount(5);
        for (int i = 0; i < options.Count; i++)
        {
            options[i].Value.Should().Be(i.ToString());
            options[i].DisplayName.Should().Be(IntStatusDisplayHelper.GetScheduleStageText(i));
        }
    }

    [Fact]
    public void GetPlanScheduleStageOptions_与Text一致()
    {
        var options = IntStatusDisplayHelper.GetPlanScheduleStageOptions();
        options.Should().HaveCount(4);
        for (int i = 0; i < options.Count; i++)
        {
            options[i].Value.Should().Be(i.ToString());
            options[i].DisplayName.Should().Be(IntStatusDisplayHelper.GetPlanScheduleStageText(i));
        }
    }

    [Fact]
    public void GetWarehousingStatusOptions_与Text一致()
    {
        var options = IntStatusDisplayHelper.GetWarehousingStatusOptions();
        options.Should().HaveCount(4);
        for (int i = 0; i < options.Count; i++)
        {
            options[i].Value.Should().Be(i.ToString());
            options[i].DisplayName.Should().Be(IntStatusDisplayHelper.GetWarehousingStatusText(i));
        }
    }

    [Fact]
    public void GetMainNoWarehousingStatusOptions_与Text一致()
    {
        var options = IntStatusDisplayHelper.GetMainNoWarehousingStatusOptions();
        options.Should().HaveCount(4);
        for (int i = 0; i < options.Count; i++)
        {
            options[i].Value.Should().Be(i.ToString());
            options[i].DisplayName.Should().Be(IntStatusDisplayHelper.GetMainNoWarehousingStatusText(i));
        }
    }

    [Fact]
    public void GetMaterialPlanStatusOptions_与EnumHelper中文一致()
    {
        var options = IntStatusDisplayHelper.GetMaterialPlanStatusOptions();
        options.Should().HaveCount(5);
        foreach (var opt in options)
        {
            var value = int.Parse(opt.Value);
            opt.DisplayName.Should().Be(EnumHelper.GetDisplayName((MaterialPlanStatus)value));
        }
    }

    [Fact]
    public void GetPlanExecutionStatusOptions_与Text一致()
    {
        var options = IntStatusDisplayHelper.GetPlanExecutionStatusOptions();
        options.Should().HaveCount(5);
        for (int i = 0; i < options.Count; i++)
        {
            options[i].Value.Should().Be(i.ToString());
            options[i].DisplayName.Should().Be(IntStatusDisplayHelper.GetPlanExecutionStatusText(i));
        }
    }

    [Fact]
    public void GetPlanInputConsistencyOptions_与Text一致()
    {
        var options = IntStatusDisplayHelper.GetPlanInputConsistencyOptions();
        options.Should().HaveCount(7);
        for (int i = 0; i < options.Count; i++)
        {
            options[i].Value.Should().Be(i.ToString());
            options[i].DisplayName.Should().Be(IntStatusDisplayHelper.GetPlanInputConsistencyText(i));
        }
    }

    // ========== DTO XxxText 计算属性 = Helper 出口 ==========

    [Fact]
    public void WorkOrderExecutionSummaryDto_XxxText_走Helper()
    {
        var dto = new WorkOrderExecutionSummaryDto
        {
            InputStatus = 2,
            MainNoInputStatus = 1,
            FlowStatus = 0,
            MainNoFlowStatus = 2,
            ReworkMainNoStatus = 1,
            WoWarehousingStatus = 2,
            MainNoWarehousingStatus = 3,
            OrderWarehousingStatus = 1,
            ScheduleStage = 3,
        };

        dto.InputStatusText.Should().Be(IntStatusDisplayHelper.GetInputStatusText(2));
        dto.MainNoInputStatusText.Should().Be(IntStatusDisplayHelper.GetInputStatusText(1));
        dto.FlowStatusText.Should().Be(IntStatusDisplayHelper.GetInputStatusText(0));
        dto.MainNoFlowStatusText.Should().Be(IntStatusDisplayHelper.GetMainNoFlowStatusText(2));
        dto.ReworkMainNoStatusText.Should().Be(IntStatusDisplayHelper.GetInputStatusText(1));
        dto.WoWarehousingStatusText.Should().Be(IntStatusDisplayHelper.GetWarehousingStatusText(2));
        dto.MainNoWarehousingStatusText.Should().Be(IntStatusDisplayHelper.GetMainNoWarehousingStatusText(3));
        dto.OrderWarehousingStatusText.Should().Be(IntStatusDisplayHelper.GetWarehousingStatusText(1));
        dto.ScheduleStageText.Should().Be(IntStatusDisplayHelper.GetScheduleStageText(3));
    }

    [Fact]
    public void SalesOrderListDto_ScheduleStageText_走Helper()
    {
        var dto = new SalesOrderListDto { ScheduleStage = 2 };
        dto.ScheduleStageText.Should().Be(IntStatusDisplayHelper.GetScheduleStageText(2));
    }

    [Fact]
    public void WorkOrderScheduleDto_ScheduleStageText_走Helper()
    {
        var dto = new WorkOrderScheduleDto { ScheduleStage = 4 };
        dto.ScheduleStageText.Should().Be(IntStatusDisplayHelper.GetScheduleStageText(4));
    }

    [Fact]
    public void RawMaterialLockPlanAndExecutionDto_ScheduleStageText_走Helper()
    {
        var dto = new RawMaterialLockPlanAndExecutionDto { ScheduleStage = 1 };
        dto.ScheduleStageText.Should().Be(IntStatusDisplayHelper.GetScheduleStageText(1));
    }

    [Fact]
    public void OrderDemandAdjustmentDto_ScheduleStageText_走Helper()
    {
        var dto = new OrderDemandAdjustmentDto { ScheduleStage = 0 };
        dto.ScheduleStageText.Should().Be(IntStatusDisplayHelper.GetScheduleStageText(0));
    }

    [Fact]
    public void FixedLengthWorkOrderListDto_ScheduleStageText_走Helper()
    {
        var dto = new FixedLengthWorkOrderListDto { ScheduleStage = 3 };
        dto.ScheduleStageText.Should().Be(IntStatusDisplayHelper.GetScheduleStageText(3));
    }
}
