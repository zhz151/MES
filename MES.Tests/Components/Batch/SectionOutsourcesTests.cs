using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Batches;
using MES.Blazor.Services;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Scheduling;
using MES.Core.Enums;

namespace MES.Tests.Components;

public class SectionOutsourcesTests : TestBase
{
    public SectionOutsourcesTests()
    {
        RegisterServices(typeof(SectionOutsourceService), typeof(ProductionRecordService), typeof(BatchPlanService));
        ConfigureEmptyResponse("/api/section-outsource/list");
        ConfigureEmptyResponse("/api/section-outsource/recoveries/filter-contexts");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = RenderPage<SectionOutsources>();
        cut.Markup.Should().Contain("工段委外");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = RenderPage<SectionOutsources>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData(SectionOutsourceStatus.PendingRecovery, "待回收")]
    [InlineData(SectionOutsourceStatus.Recovered, "已回收")]
    [InlineData(SectionOutsourceStatus.InProgress, "在轧")]
    public void StatusColumn_DisplaysCorrectText(SectionOutsourceStatus status, string expectedText)
    {
        ConfigureListResponse(status);
        var cut = RenderPage<SectionOutsources>();
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    [Fact]
    public void PendingCard_过滤厂内行_重算合计()
    {
        // 厂内单位集合：一车间
        ConfigureResponse("/api/section-outsource/internal-vendors", new ApiResponse<List<string>>
        {
            Success = true,
            Code = 200,
            Data = new List<string> { "一车间" }
        });
        // 批次计划页「实时委外在产」：含非厂内「委外厂A」+ 厂内「一车间」+ 原合计 1500
        ConfigureResponse("/api/batch-plan/outsource-pending", new ApiResponse<BatchPlanOutsourcePendingDto>
        {
            Success = true,
            Code = 200,
            Data = new BatchPlanOutsourcePendingDto
            {
                Sections = new List<string> { "冷轧拔-60冷轧", "酸洗" },
                Rows = new List<OutsourcePendingRowDto>
                {
                    new()
                    {
                        OutsourceUnit = "委外厂A",
                        Cells = new Dictionary<string, OutsourcePendingCellDto>
                        {
                            ["冷轧拔-60冷轧"] = new() { Total = 1000 }
                        },
                        TotalCell = new OutsourcePendingCellDto { Total = 1000 }
                    },
                    new()
                    {
                        OutsourceUnit = "一车间",
                        Cells = new Dictionary<string, OutsourcePendingCellDto>
                        {
                            ["酸洗"] = new() { Total = 500 }
                        },
                        TotalCell = new OutsourcePendingCellDto { Total = 500 }
                    },
                    new()
                    {
                        OutsourceUnit = "合计",
                        Cells = new Dictionary<string, OutsourcePendingCellDto>
                        {
                            ["冷轧拔-60冷轧"] = new() { Total = 1000 },
                            ["酸洗"] = new() { Total = 500 }
                        },
                        TotalCell = new OutsourcePendingCellDto { Total = 1500 }
                    }
                }
            }
        });

        var cut = RenderPage<SectionOutsources>();
        cut.FindAll("button").First(b => b.TextContent.Contains("实时委外在产")).Click();

        cut.WaitForAssertion(() =>
        {
            // 整页标记（页面其余部分无这些数据）；数值断言用「>x.y<」标签边界精确匹配单元格，
            // 避免 MudBlazor 图标 SVG path 中的小数坐标子串（如 "1.5"）误命中
            var pendingTable = cut.Markup;
            pendingTable.Should().Contain("委外厂A");
            pendingTable.Should().NotContain("一车间");        // 厂内行已过滤
            pendingTable.Should().NotContain("酸洗");          // 过滤后全空工段列已移除
            pendingTable.Should().Contain(">1.0<");            // 重算后合计 = 1000kg → 1.0t（非原 1500）
            pendingTable.Should().NotContain(">1.5<");         // 若误保留原合计 1500 → 1.5t，必须不存在
        });
    }

    [Fact]
    public void MonthlyCard_现在产列_显示未回收重量_橙色底()
    {
        // 月度委外数据：一行（委外厂A/冷轧拔-60冷轧），发 1200/回 800/退 0，现在产=未回收发出重量 1200kg → 1.2t
        ConfigureResponse("/api/section-outsource/monthly-summary", new ApiResponse<List<SectionOutsourceMonthlyRowDto>>
        {
            Success = true,
            Code = 200,
            Data = new List<SectionOutsourceMonthlyRowDto>
            {
                new()
                {
                    OutsourceVendor = "委外厂A",
                    SectionName = "冷轧拔-60冷轧",
                    Months = Enumerable.Range(0, 12).Select(i => new SectionOutsourceMonthValueDto
                    {
                        Send = i == 0 ? 1200m : 0m,
                        Recover = i == 0 ? 800m : 0m,
                        Unprocessed = 0m
                    }).ToList(),
                    TotalSend = 1200m,
                    TotalRecover = 800m,
                    TotalUnprocessed = 0m,
                    NowInProduction = 1200m
                }
            }
        });

        var cut = RenderPage<SectionOutsources>();
        cut.FindAll("button").First(b => b.TextContent.Contains("月度委外数据")).Click();

        cut.WaitForAssertion(() =>
        {
            var monthlyTable = cut.Markup;                     // 整页标记（这些断言内容仅月度卡片包含，等效限定表格）
            monthlyTable.Should().Contain("现在产(t)");          // 表头存在
            monthlyTable.Should().Contain("background:#ffe0b2"); // 表头橙色底
            monthlyTable.Should().Contain("background:#fff3e0"); // 单元格橙色底
            monthlyTable.Should().Contain(">1.2<");              // 现在产 = 未回收发出重量 1200kg → 1.2t（标签边界精确匹配）
        });
    }

    private void ConfigureListResponse(SectionOutsourceStatus status)
    {
        ConfigureEmptyResponse("/api/section-outsource/list");
        ConfigureEmptyResponse("/api/section-outsource/recoveries/filter-contexts");
        var pagedResult = new PagedResult<SectionOutsourceDto>
        {
            Items = new List<SectionOutsourceDto>
            {
                new()
                {
                    Id = 1,
                    ProductionBatchId = 1,
                    ProcessGroupId = 1,
                    BatchNo = "BATCH001",
                    ProcessName = "60冷轧",
                    SectionName = "冷轧拔",
                    SequenceNumber = 1,
                    OutsourceVendor = "测试供应商",
                    SendOutDate = DateTime.Today,
                    Status = status
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/section-outsource/list", new ApiResponse<PagedResult<SectionOutsourceDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
