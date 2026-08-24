using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.Enums;

namespace MES.Tests.Services;

/// <summary>
/// 工位管理服务测试：分页查询、按编码查询、新增/更新、删除
/// </summary>
public class WorkstationServiceTests : TestBase
{
    private WorkstationService CreateService(AppDbContext ctx) => new(ctx);

    private async Task<Workstation> SeedWorkstationAsync(AppDbContext ctx,
        string code = "WS001", string sectionName = SectionKeys.Pickle, bool isActive = true, string? groupNames = null)
    {
        var ws = new Workstation
        {
            Code = code,
            Name = "测试工位",
            SectionName = sectionName,
            ReportType = ReportTemplateType.PicklingInRecord.ToString(),
            GroupNames = groupNames,
            IsActive = isActive
        };
        ctx.Workstations.Add(ws);
        await ctx.SaveChangesAsync();
        return ws;
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_返回分页数据()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, "WS001");
        await SeedWorkstationAsync(ctx, "WS002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_按关键字搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, "WS001", SectionKeys.Pickle);
        await SeedWorkstationAsync(ctx, "WS002", SectionKeys.Degrease);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = SectionKeys.Pickle });

        result.Items.Should().HaveCount(1);
        result.Items[0].SectionName.Should().Be(SectionKeys.Pickle);
    }

    [Fact]
    public async Task GetPagedAsync_默认排序为Code()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, "WS002");
        await SeedWorkstationAsync(ctx, "WS001");
        var svc = CreateService(ctx);

        // IsDescending 默认为 true，Code 降序
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items[0].Code.Should().Be("WS002");
        result.Items[1].Code.Should().Be("WS001");
    }

    // ========== GetByCodeAsync ==========

    [Fact]
    public async Task GetByCodeAsync_存在_返回工位()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("WS001");

        result.Should().NotBeNull();
        result!.Code.Should().Be("WS001");
    }

    [Fact]
    public async Task GetByCodeAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("NONEXISTENT");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCodeAsync_未启用_返回Null()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("WS001");

        result.Should().BeNull();
    }

    // ========== SaveAsync ==========

    [Fact]
    public async Task SaveAsync_新增_成功创建()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS001",
            Name = "酸洗工位",
            SectionName = SectionKeys.Pickle,
            ReportType = ReportTemplateType.PicklingInRecord
        });

        result.Should().BeTrue();

        var saved = await ctx.Workstations.FirstAsync();
        saved.Code.Should().Be("WS001");
    }

    [Fact]
    public async Task SaveAsync_更新_成功修改()
    {
        var ctx = CreateDbContext();
        var ws = await SeedWorkstationAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Id = ws.Id,
            Code = "WS001",
            Name = "更新名称",
            SectionName = SectionKeys.Degrease,
            ReportType = ReportTemplateType.PicklingInRecord,
            IsActive = true
        });

        result.Should().BeTrue();

        var updated = await ctx.Workstations.FindAsync(ws.Id);
        updated!.Name.Should().Be("更新名称");
        updated.SectionName.Should().Be(SectionKeys.Degrease);
    }

    [Fact]
    public async Task SaveAsync_更新不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(new WorkstationDto
        {
            Id = 999,
            Code = "WS001",
            SectionName = SectionKeys.Pickle,
            ReportType = ReportTemplateType.PicklingInRecord
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var ws = await SeedWorkstationAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.DeleteAsync(ws.Id);

        result.Should().BeTrue();
        var deleted = await ctx.Workstations.FindAsync(ws.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== InspectionItem（成品检验工位直击检验项目） ==========

    [Fact]
    public async Task SaveAsync_成品检验_绑定检验项目_保存并投影解析()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-FINAL",
            Name = "成品检验工位",
            SectionName = SectionKeys.Pickle,
            ReportType = ReportTemplateType.FinalInspection,
            InspectionItem = InspectionItem.Dimension
        });

        result.Should().BeTrue();

        // 存储为枚举字符串
        var saved = await ctx.Workstations.FirstAsync();
        saved.InspectionItem.Should().Be(InspectionItem.Dimension.ToString());

        // 投影解析回枚举
        var page = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });
        page.Items.Should().HaveCount(1);
        page.Items[0].InspectionItem.Should().Be(InspectionItem.Dimension);
        page.Items[0].InspectionItemDisplay.Should().Be("尺寸");
    }

    [Fact]
    public async Task SaveAsync_成品检验_未绑定检验项目_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-FINAL",
            Name = "成品检验工位",
            SectionName = SectionKeys.Pickle,
            ReportType = ReportTemplateType.FinalInspection,
            InspectionItem = null
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*检验项目*");
    }

    [Fact]
    public async Task SaveAsync_成品检验_非法检验项目_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-FINAL",
            Name = "成品检验工位",
            SectionName = SectionKeys.Pickle,
            ReportType = ReportTemplateType.FinalInspection,
            InspectionItem = (InspectionItem)999
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*检验项目*");
    }

    [Fact]
    public async Task SaveAsync_非成品检验_无需检验项目()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-PICKLE",
            Name = "酸洗工位",
            SectionName = SectionKeys.Pickle,
            ReportType = ReportTemplateType.PicklingInRecord,
            InspectionItem = null
        });

        result.Should().BeTrue();
        var saved = await ctx.Workstations.FirstAsync();
        saved.InspectionItem.Should().BeNull();
    }

    // ========== 工段选填（成检到料/成品检验豁免，其余必填） ==========

    [Fact]
    public async Task SaveAsync_成品检验_无工段_可保存()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-FINAL-NOSEC",
            Name = "成品检验工位",
            SectionName = null,
            ReportType = ReportTemplateType.FinalInspection,
            InspectionItem = InspectionItem.Dimension
        });

        result.Should().BeTrue();
        var saved = await ctx.Workstations.FirstAsync();
        saved.SectionName.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_成检到料_无工段_可保存()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-MRC-NOSEC",
            Name = "成检到料工位",
            SectionName = null,
            ReportType = ReportTemplateType.MaterialReceiveCheck,
            InspectionItem = null
        });

        result.Should().BeTrue();
        var saved = await ctx.Workstations.FirstAsync();
        saved.SectionName.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_成品检验_非标准工段_可保存()
    {
        // 成品检验工位不消费工段，选填不校验工段合法性
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-FINAL-ARB",
            Name = "成品检验工位",
            SectionName = "任意段",
            ReportType = ReportTemplateType.FinalInspection,
            InspectionItem = InspectionItem.Ultrasonic
        });

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_普通生产_无工段_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-PROD-NOSEC",
            Name = "普通生产工位",
            SectionName = null,
            ReportType = ReportTemplateType.ProductionRecord,
            InspectionItem = null
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*工段*");
    }

    [Fact]
    public async Task SaveAsync_过程检验_无工段_抛出BusinessException()
    {
        // 过程检验扫码需按工位工段过滤工序组 + 写记录定位工序，工段保持必填
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-PROC-NOSEC",
            Name = "过程检验工位",
            SectionName = null,
            ReportType = ReportTemplateType.ProcessInspection,
            InspectionItem = null
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*工段*");
    }

    [Fact]
    public async Task SaveAsync_入缸_无工段_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-IN-NOSEC",
            Name = "入缸工位",
            SectionName = null,
            ReportType = ReportTemplateType.PicklingInRecord,
            InspectionItem = null
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*工段*");
    }

    // ========== 过程检验/成检到料（布尔开关匹配，无需绑定检验项目） ==========

    [Fact]
    public async Task SaveAsync_过程检验_无需绑定检验项目_可保存()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-PROC",
            Name = "过程检验工位",
            SectionName = SectionKeys.Inspection,
            ReportType = ReportTemplateType.ProcessInspection,
            InspectionItem = null
        });

        result.Should().BeTrue();

        var saved = await ctx.Workstations.FirstAsync();
        saved.InspectionItem.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_过程检验_非检验工段_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-PROC-BADSEC",
            Name = "过程检验工位",
            SectionName = SectionKeys.Pickle,
            ReportType = ReportTemplateType.ProcessInspection,
            InspectionItem = null
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*「检验」*");
    }

    [Fact]
    public async Task SaveAsync_成检到料_无需绑定检验项目_可保存()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS-MRC",
            Name = "成检到料工位",
            SectionName = SectionKeys.Inspection,
            ReportType = ReportTemplateType.MaterialReceiveCheck,
            InspectionItem = null
        });

        result.Should().BeTrue();

        var saved = await ctx.Workstations.FirstAsync();
        saved.InspectionItem.Should().BeNull();
    }

    // ========== GroupNames（组类选项集合，扫码先选组再选人） ==========

    [Fact]
    public async Task GetPagedAsync_返回组类选项字段()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, groupNames: "甲班,乙班");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items[0].GroupNames.Should().Be("甲班,乙班");
    }

    [Fact]
    public async Task GetByCodeAsync_返回组类选项字段()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, groupNames: "甲班");
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("WS001");

        result!.GroupNames.Should().Be("甲班");
    }

    [Fact]
    public async Task SaveAsync_新增_保存组类选项()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS001",
            Name = "冷轧拔工位",
            SectionName = SectionKeys.ColdRollDraw,
            ReportType = ReportTemplateType.ProductionRecord,
            GroupNames = "甲班,乙班"
        });

        result.Should().BeTrue();

        var saved = await ctx.Workstations.FirstAsync();
        saved.GroupNames.Should().Be("甲班,乙班");
    }

    [Fact]
    public async Task SaveAsync_更新_修改组类选项()
    {
        var ctx = CreateDbContext();
        var ws = await SeedWorkstationAsync(ctx, groupNames: "甲班");
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Id = ws.Id,
            Code = "WS001",
            Name = "测试工位",
            SectionName = SectionKeys.ColdRollDraw,
            ReportType = ReportTemplateType.ProductionRecord,
            GroupNames = "丙班",
            IsActive = true
        });

        result.Should().BeTrue();

        var updated = await ctx.Workstations.FindAsync(ws.Id);
        updated!.GroupNames.Should().Be("丙班");
    }

    // ========== GetFilterContextsAsync（列头筛选上下文） ==========

    [Fact]
    public async Task GetFilterContextsAsync_枚举与布尔列_返回完整选项()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        // 报工模板类型 = 枚举全部
        result["ReportType"].Should().HaveCount(Enum.GetValues<ReportTemplateType>().Length);
        result["ReportType"].Should().Contain(ReportTemplateType.ProductionRecord.ToString());
        // 成检项目 = 枚举全部
        result["InspectionItem"].Should().HaveCount(Enum.GetValues<InspectionItem>().Length);
        result["InspectionItem"].Should().Contain(InspectionItem.Dimension.ToString());
        // 启用 = 是/否
        result["IsActive"].Should().Equal("True", "False");
    }

    [Fact]
    public async Task GetFilterContextsAsync_自由文本列_取存量去重值()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, "WS001");
        await SeedWorkstationAsync(ctx, "WS001"); // 重复 Code 不应出现在选项中两次
        await SeedWorkstationAsync(ctx, "WS002");
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result["Code"].Should().Contain("WS001");
        result["Code"].Should().HaveCount(2); // WS001/WS002 去重
    }

    [Fact]
    public async Task GetFilterContextsAsync_工段列_含标准工段与存量非标准值()
    {
        // 成检到料/成品检验工位工段选填可任意值，非标准值需补充进选项供筛选
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, sectionName: SectionKeys.Pickle);
        await SeedWorkstationAsync(ctx, "WS-ARB", sectionName: "任意段");
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result["SectionName"].Should().Contain(SectionKeys.Pickle); // 标准工段
        result["SectionName"].Should().Contain("任意段"); // 存量非标准值补充
        result["SectionName"].Should().HaveCountGreaterThan(SectionKeys.All.Length);
    }

    [Fact]
    public async Task GetFilterContextsAsync_组类列_取存量整串去重值()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, groupNames: "甲班,乙班");
        await SeedWorkstationAsync(ctx, "WS002", groupNames: "甲班,乙班");
        await SeedWorkstationAsync(ctx, "WS003", groupNames: "丙班");
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result["GroupNames"].Should().Contain("甲班,乙班");
        result["GroupNames"].Should().Contain("丙班");
        result["GroupNames"].Should().HaveCount(2); // 整串去重
    }
}
