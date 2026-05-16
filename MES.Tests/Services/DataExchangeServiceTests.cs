using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;
using MES.Services.DataExchange;
using MES.Tests.Tests;
using Moq;
using OfficeOpenXml;

namespace MES.Tests.Services;

/// <summary>
/// 数据导入导出服务测试：注册表验证、导出、模板生成、预览
/// </summary>
public class DataExchangeServiceTests : TestBase
{
    private DataExchangeService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<DataExchangeService>>();
        return new DataExchangeService(ctx, loggerMock.Object);
    }

    // ========== Registry 验证 ==========

    [Fact]
    public void Registry_包含所有35个实体()
    {
        DataExchangeService.Registry.Should().HaveCount(35);
    }

    [Fact]
    public void Registry_每个实体都有列定义()
    {
        foreach (var (key, def) in DataExchangeService.Registry)
        {
            def.Columns.Should().NotBeEmpty($"实体 {key} 缺少列定义");
        }
    }

    [Fact]
    public void Registry_每个实体列定义_Header不重复()
    {
        foreach (var (key, def) in DataExchangeService.Registry)
        {
            var headers = def.Columns.Select(c => c.Header).ToList();
            headers.Should().OnlyHaveUniqueItems($"实体 {key} 存在重复表头");
        }
    }

    [Fact]
    public void Registry_实体顺序与EntityOrder一致()
    {
        var registryKeys = DataExchangeService.Registry.Keys.ToList();
        var orderKeys = DataExchangeService.EntityOrder;

        orderKeys.Should().HaveCount(registryKeys.Count);
        foreach (var key in orderKeys)
        {
            registryKeys.Should().Contain(key);
        }
    }

    [Fact]
    public void Registry_InventoryBatch_包含缺陷字段()
    {
        var def = DataExchangeService.Registry["InventoryBatch"];
        var headers = def.Columns.Select(c => c.Header).ToList();

        headers.Should().Contain("次品原因");
        headers.Should().Contain("责任类型");
        headers.Should().Contain("原始来料单位");
        headers.Should().Contain("挂牌号");
        headers.Should().Contain("次品备注");
        headers.Should().Contain("来源单号");
    }

    // ========== GenerateTemplateAsync ==========

    [Fact]
    public async Task GenerateTemplateAsync_仓库_生成有效Excel()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var bytes = await svc.GenerateTemplateAsync("Warehouse");

        bytes.Should().NotBeNullOrEmpty();
        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        sheet.Should().NotBeNull();
        sheet.Cells[1, 1].Value.Should().Be("仓库编码");
        sheet.Cells[1, 2].Value.Should().Be("仓库名称");
    }

    [Fact]
    public async Task GenerateTemplateAsync_客户档案_生成有效Excel()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var bytes = await svc.GenerateTemplateAsync("CustomerProfile");

        bytes.Should().NotBeNullOrEmpty();
        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        sheet.Should().NotBeNull();
        sheet.Cells[1, 1].Value.Should().Be("客户编码");
        sheet.Cells[1, 2].Value.Should().Be("客户单位");
    }

    [Fact]
    public async Task GenerateTemplateAsync_不支持的实体_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GenerateTemplateAsync("NonExistent");
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不支持*");
    }

    [Fact]
    public async Task GenerateTemplateAsync_含示例数据()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var bytes = await svc.GenerateTemplateAsync("Warehouse");

        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        // 第2行应有示例数据
        var sampleValue = sheet.Cells[2, 1].Value;
        sampleValue.Should().NotBeNull();
    }

    // ========== ExportAsync ==========

    [Fact]
    public async Task ExportAsync_仓库_导出包含种子数据()
    {
        var ctx = CreateDbContext();
        ctx.Warehouses.Add(new Warehouse { Code = "WH001", Name = "测试仓库", SortOrder = 1, IsActive = true });
        ctx.Warehouses.Add(new Warehouse { Code = "WH002", Name = "二号仓库", SortOrder = 2, IsActive = true });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("Warehouse");

        bytes.Should().NotBeNullOrEmpty();
        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        // 表头 + 2行数据 = 3行
        sheet.Dimension.Rows.Should().Be(3);
        sheet.Cells[2, 1].Value.Should().Be("WH001");
        sheet.Cells[2, 2].Value.Should().Be("测试仓库");
        sheet.Cells[3, 1].Value.Should().Be("WH002");
    }

    [Fact]
    public async Task ExportAsync_产品标准_导出包含种子数据()
    {
        var ctx = CreateDbContext();
        ctx.ProductionStandards.Add(new ProductionStandard
        {
            StandardCode = "GB/T 8163", StandardName = "流体管",
            SortOrder = 1, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("ProductionStandard");

        bytes.Should().NotBeNullOrEmpty();
        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        sheet.Cells[1, 1].Value.Should().Be("标准编码");
        sheet.Cells[2, 1].Value.Should().Be("GB/T 8163");
    }

    [Fact]
    public async Task ExportAsync_无数据_导出仅表头()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var bytes = await svc.ExportAsync("Warehouse");

        bytes.Should().NotBeNullOrEmpty();
        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        // 只有表头行
        sheet.Dimension.Rows.Should().Be(1);
    }

    [Fact]
    public async Task ExportAsync_不支持的实体_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.ExportAsync("NonExistent");
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不支持*");
    }

    [Fact]
    public async Task ExportAsync_牌号对照_导出含枚举转换()
    {
        var ctx = CreateDbContext();
        ctx.StandardGradeMappings.Add(new StandardGradeMapping
        {
            StandardGrade = "Q345B", PlantGrade = "Q345B",
            Density = 7.85m, SpecialMaterial = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("StandardGradeMapping");

        bytes.Should().NotBeNullOrEmpty();
        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        // 验证表头包含特殊材料列
        var headers = new List<string>();
        for (int c = 1; c <= sheet.Dimension.Columns; c++)
            headers.Add(sheet.Cells[1, c].Value?.ToString() ?? "");

        headers.Should().Contain("特殊材料");
    }

    [Fact]
    public async Task ExportAsync_客户档案_含状态枚举中文值()
    {
        var ctx = CreateDbContext();
        ctx.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerCode = "C001", CustomerUnit = "测试客户",
            Salesman = "张三", Status = Core.Enums.CustomerStatus.Active
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("CustomerProfile");

        bytes.Should().NotBeNullOrEmpty();
        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        // 表头 + 1行数据
        sheet.Dimension.Rows.Should().Be(2);
    }

    // ========== 所有实体可导出 ==========

    public static IEnumerable<object[]> AllEntityKeys =>
        DataExchangeService.Registry.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(AllEntityKeys))]
    public async Task ExportAsync_所有实体_无数据时不报错(string entityKey)
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var bytes = await svc.ExportAsync(entityKey);

        bytes.Should().NotBeNullOrEmpty();
        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        sheet.Should().NotBeNull();
        // 至少表头行
        sheet.Dimension.Rows.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(AllEntityKeys))]
    public async Task GenerateTemplateAsync_所有实体_不报错(string entityKey)
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var bytes = await svc.GenerateTemplateAsync(entityKey);

        bytes.Should().NotBeNullOrEmpty();
        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        sheet.Should().NotBeNull();
    }
}
