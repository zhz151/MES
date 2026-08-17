using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Batches;
using MES.Blazor.Services;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.Enums;

namespace MES.Tests.Components;

public class ProcessCardPrintTests : TestBase
{
    public ProcessCardPrintTests()
    {
        RegisterServices(typeof(BatchService), typeof(StandardWorkDayService), typeof(ProcessCardColumnDefinitionService), typeof(ProcessCardStyleDefinitionService));
        ConfigureEmptyResponse("/api/batch/list");
        // 工段列从参数表加载；未配置时 fallback 为预置 26 工段，此处配置空响应触发降级路径
        ConfigureEmptyResponse("/api/standard-work-day/enabled-sections");
        // 格式设置面板从配置表加载；空响应触发 fallback 到默认列
        ConfigureEmptyResponse("/api/process-card-column-definition/all");
        // 打印版式配置从配置表加载；空响应触发 fallback 到默认字体/字号
        ConfigureEmptyResponse("/api/process-card-style-definition/all");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<ProcessCardPrint>();
        cut.Markup.Should().Contain("工艺流转卡打印");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<ProcessCardPrint>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<ProcessCardPrint>();
        cut.WaitForState(() => cut.Markup.Contains("BATCH001"));
        cut.Markup.Should().Contain("BATCH001");
    }

    [Fact]
    public void Render_FormatSettingsPanel_加载数据库配置标签()
    {
        // 数据库配置：BatchNo 显示名改「生产编号（DB配置）」
        ConfigureResponse("/api/process-card-column-definition/all", new ApiResponse<List<ProcessCardColumnDefinitionDto>>
        {
            Success = true,
            Code = 200,
            Data = new List<ProcessCardColumnDefinitionDto>
            {
                new()
                {
                    Id = 1,
                    BlockKey = "BatchInfo",
                    FieldKey = "BatchNo",
                    Label = "生产编号（DB配置）",
                    Visible = true,
                    RowIndex = 1,
                    ColumnIndex = 1,
                    ColumnWeight = 9
                }
            }
        });
        var cut = Ctx.RenderComponent<ProcessCardPrint>();

        // 展开格式设置面板
        var expandBtn = cut.FindAll("button").First(b => b.TextContent.Contains("展开"));
        expandBtn.Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("生产编号（DB配置）"));
        cut.Markup.Should().Contain("格式设置");
        cut.Markup.Should().Contain("保存格式设置");
        cut.Markup.Should().Contain("恢复默认");
    }

    [Fact]
    public void Render_FormatSettingsPanel_打印版式Tab加载数据库配置()
    {
        // 数据库版式配置：正文字体改「黑体」、主标题字号 24
        ConfigureResponse("/api/process-card-style-definition/all", new ApiResponse<List<ProcessCardStyleDefinitionDto>>
        {
            Success = true,
            Code = 200,
            Data = new List<ProcessCardStyleDefinitionDto>
            {
                new() { Id = 1, Key = "PageFontFamily", Value = "黑体", DisplayName = "正文字体", Remark = "页面默认字体族" },
                new() { Id = 2, Key = "HeaderFontSize", Value = "24", DisplayName = "主标题字号", Remark = "工艺流转卡标题" }
            }
        });
        var cut = Ctx.RenderComponent<ProcessCardPrint>();

        // 展开格式设置面板，应出现「打印版式」Tab
        var expandBtn = cut.FindAll("button").First(b => b.TextContent.Contains("展开"));
        expandBtn.Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("打印版式"));

        // 切换至「打印版式」Tab：显示数据库配置的显示名与值
        var styleTab = cut.FindAll(".mud-tab").First(t => t.TextContent.Contains("打印版式"));
        styleTab.Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("主标题字号"));
        cut.Markup.Should().Contain("24");
        cut.Markup.Should().Contain("正文字体");
        cut.Markup.Should().Contain("黑体");
        cut.Markup.Should().Contain("保存版式设置");
        cut.Markup.Should().Contain("恢复默认");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/batch/list");
        var pagedResult = new PagedResult<ProductionBatchListDto>
        {
            Items = new List<ProductionBatchListDto>
            {
                new()
                {
                    Id = 1,
                    BatchNo = "BATCH001",
                    Status = BatchStatus.None,
                    WorkOrderNo = "WO001",
                    ManufacturingItem = MaterialType.OrderFinished,
                    PlantGrade = "304",
                    Specification = "219*8",
                    TotalWeight = 2500m,
                    ProductionRatio = 1
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/batch/list", new ApiResponse<PagedResult<ProductionBatchListDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
