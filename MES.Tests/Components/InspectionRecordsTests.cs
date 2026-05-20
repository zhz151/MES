using Bunit;
using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class InspectionRecordsTests : TestBase
{
    public InspectionRecordsTests()
    {
        RegisterServices(typeof(InspectionRecordService));
        ConfigureEmptyResponse("/api/inspection-record/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<InspectionRecords>();
        cut.Markup.Should().Contain("点检记录");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<InspectionRecords>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_HasColumns()
    {
        var cut = Ctx.RenderComponent<InspectionRecords>();
        cut.Markup.Should().ContainAll("记录号", "设备名称", "点检人");
    }
}
