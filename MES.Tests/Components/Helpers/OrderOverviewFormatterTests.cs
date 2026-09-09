using FluentAssertions;
using MES.Blazor.Helpers;

namespace MES.Tests.Components;

/// <summary>
/// 往来统计格（客户/供应商/委外 列表页 ② 列 + 报表「往来数据」卡）共用格式化纯函数测试：
/// z单/x吨/y万 三色取整、0 成分省略、全 0 显「—」、withCount=false 忽略单数成分。
/// ⚠️ 语义与 CustomerService/SupplierService/OutsourceVendorService 统计回填字段口径互锁。
/// </summary>
public class OrderOverviewFormatterTests
{
    // ========== 纯文本（RenderTradeText：tooltip/合计/打印同源口径） ==========

    [Fact]
    public void RenderTradeText_全成分_中文组合取整()
    {
        // count=2、weightKg=1500(1.5吨→2)、amountYuan=25000(2.5万→3)，均 AwayFromZero 取整
        OrderOverviewFormatter.RenderTradeText(2, 1500m, 25000m).Should().Be("2单/2吨/3万");
    }

    [Fact]
    public void RenderTradeText_整吨整万_直接呈现()
    {
        OrderOverviewFormatter.RenderTradeText(1, 1000m, 10000m).Should().Be("1单/1吨/1万");
    }

    [Fact]
    public void RenderTradeText_仅单数_省略吨万()
    {
        OrderOverviewFormatter.RenderTradeText(7, 0m, 0m).Should().Be("7单");
    }

    [Fact]
    public void RenderTradeText_仅重量_省略单万()
    {
        // withCount=false（无单数成分列）且金额为 0：仅保留吨
        OrderOverviewFormatter.RenderTradeText(0, 1234m, 0m, false).Should().Be("1吨");
    }

    [Fact]
    public void RenderTradeText_仅金额_省略单吨()
    {
        // withCount=false 且重量 0：仅保留万
        OrderOverviewFormatter.RenderTradeText(0, 0m, 30000m, false).Should().Be("3万");
    }

    [Fact]
    public void RenderTradeText_全零_显示破折号()
    {
        OrderOverviewFormatter.RenderTradeText(0, 0m, 0m).Should().Be("—");
        OrderOverviewFormatter.RenderTradeText(0, 0m, 0m, false).Should().Be("—");
    }

    // ========== 富文本（RenderTradeMarkup：三色内联，供屏幕 + DOM 打印） ==========

    [Fact]
    public void RenderTradeMarkup_三色取整_含分隔与色码()
    {
        var m = OrderOverviewFormatter.RenderTradeMarkup(2, 1500m, 25000m).Value;
        m.Should().Contain("2单").And.Contain("2吨").And.Contain("3万");
        m.Should().Contain("#1565C0").And.Contain("#2E7D32").And.Contain("#E65100");
        m.Should().Contain("<span style=\"color:#9e9e9e;\">/</span>");   // 灰 "/" 分隔符
    }

    [Fact]
    public void RenderTradeMarkup_全零_纯破折号()
    {
        OrderOverviewFormatter.RenderTradeMarkup(0, 0m, 0m).Value.Should().Be("—");
    }

    [Fact]
    public void RenderTradeMarkup_仅重量_不出单万()
    {
        // 委外本年退回列：仅吨成分
        var m = OrderOverviewFormatter.RenderTradeMarkup(0, 5000m, 0m, false).Value;
        m.Should().Contain("5吨");
        m.Should().NotContain("单").And.NotContain("万");
    }

    [Fact]
    public void RenderTradeMarkup_仅金额_不出单吨()
    {
        // 供应商本年到货/待收货、委外回收/未回收：x吨/y万
        var m = OrderOverviewFormatter.RenderTradeMarkup(0, 0m, 30000m, false).Value;
        m.Should().Contain("3万");
        m.Should().NotContain("单").And.NotContain("吨");
    }
}
