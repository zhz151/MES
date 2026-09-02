using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Constants;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities;
using MES.Services.DataExchange;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Order;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Warehouse;
using MES.Core.Interfaces.DataExchange;
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
        var exportLoggerMock = new Mock<ILogger<DataExportService>>();
        var importLoggerMock = new Mock<ILogger<DataImportService>>();
        var fixServiceMock = new Mock<IDataFixService>();
        var sectionNameDisplayMock = new Mock<ISectionNameDisplayService>();
        sectionNameDisplayMock.Setup(x => x.ToDisplayAsync(It.IsAny<string?>()))
            .ReturnsAsync((string? v) => SectionKeys.ToChinese(v));
        sectionNameDisplayMock.Setup(x => x.GetSectionNameMapAsync())
            .ReturnsAsync(SectionKeys.KeyToChinese);
        var exportService = new DataExportService(ctx, exportLoggerMock.Object, sectionNameDisplayMock.Object, CreateProcessDefinitionServiceMock());
        var importService = new DataImportService(ctx, importLoggerMock.Object);
        return new DataExchangeService(importService, exportService, fixServiceMock.Object, loggerMock.Object);
    }

    // ========== Registry 验证 ==========

    [Fact]
    public void Registry_包含所有73个实体()
    {
        DataExchangeRegistry.Registry.Should().HaveCount(73);
    }

    [Fact]
    public void Registry_每个实体都有列定义()
    {
        foreach (var (key, def) in DataExchangeRegistry.Registry)
        {
            def.Columns.Should().NotBeEmpty($"实体 {key} 缺少列定义");
        }
    }

    [Fact]
    public void Registry_每个实体列定义_Header不重复()
    {
        foreach (var (key, def) in DataExchangeRegistry.Registry)
        {
            var headers = def.Columns.Select(c => c.Header).ToList();
            headers.Should().OnlyHaveUniqueItems($"实体 {key} 存在重复表头");
        }
    }

    [Fact]
    public void Registry_实体顺序与EntityOrder一致()
    {
        var registryKeys = DataExchangeRegistry.Registry.Keys.ToList();
        var orderKeys = DataExchangeRegistry.EntityOrder;

        orderKeys.Should().HaveCount(registryKeys.Count);
        foreach (var key in orderKeys)
        {
            registryKeys.Should().Contain(key);
        }
    }

    [Fact]
    public void Registry_所有实体DisplayName_按命名规则带上下文前缀()
    {
        var ctxPrefixes = string.Join("|", DataExchangeRegistry.ContextOrder.Select(c => System.Text.RegularExpressions.Regex.Escape(c)));
        foreach (var (key, def) in DataExchangeRegistry.Registry)
        {
            def.DisplayName.Should().MatchRegex($"^({ctxPrefixes})-",
                $"实体 {key} 的 DisplayName「{def.DisplayName}」未按命名规则「上下文-实体名」加前缀");
            DataExchangeRegistry.GetContext(def.DisplayName).Should().NotBe("其他",
                $"实体 {key} 的上下文前缀不在 ContextOrder 中");
        }
    }

    [Fact]
    public void GetEntities_按上下文顺序排序()
    {
        var entities = DataExchangeRegistry.GetEntities();
        entities.Should().HaveCount(73);

        // 上下文分组出现顺序须与 ContextOrder 完全一致（组内按名称升序）
        var actual = entities.Select(e => e.Context).ToList();
        var expected = DataExchangeRegistry.ContextOrder
            .SelectMany(c => entities.Where(e => e.Context == c))
            .Select(e => e.Context)
            .ToList();
        actual.Should().Equal(expected, "上下文分组应按 ContextOrder 顺序排列");

        // 每个实体都解析出合法上下文，且组内名称升序
        entities.Select(e => e.Context).Should().NotContain("其他");
        foreach (var grp in entities.GroupBy(e => e.Context))
        {
            grp.Select(e => e.Name).ToList().Should().BeInAscendingOrder();
        }
    }

    [Fact]
    public void Registry_InventoryBatch_包含缺陷字段()
    {
        var def = DataExchangeRegistry.Registry["InventoryBatch"];
        var headers = def.Columns.Select(c => c.Header).ToList();

        headers.Should().Contain("次品原因");
        headers.Should().Contain("责任类型");
        headers.Should().Contain("原始来料单位");
        headers.Should().Contain("挂牌号");
        headers.Should().Contain("次品备注");
        headers.Should().Contain("来源单号");
    }

    [Fact]
    public void Registry_ProductionBatch_包含系统跟踪字段()
    {
        var def = DataExchangeRegistry.Registry["ProductionBatch"];
        var headers = def.Columns.Select(c => c.Header).ToList();

        headers.Should().Contain("投料变更");
        headers.Should().Contain("当前工段完工");
        headers.Should().Contain("剩余工量(天)");
    }

    [Fact]
    public void Registry_SubcontractReturnItem_包含回收执行字段()
    {
        var def = DataExchangeRegistry.Registry["SubcontractReturnItem"];
        var headers = def.Columns.Select(c => c.Header).ToList();

        headers.Should().Contain("回收支数");
        headers.Should().Contain("回收重量(kg)");
        headers.Should().Contain("加工状态");
        headers.Should().Contain("强制完成");
    }

    [Fact]
    public void Registry_ProductionBatch_系统跟踪字段标记为IsSystem()
    {
        var def = DataExchangeRegistry.Registry["ProductionBatch"];

        def.Columns.First(c => c.Header == "投料变更").IsSystem.Should().BeTrue();
        def.Columns.First(c => c.Header == "当前工段完工").IsSystem.Should().BeTrue();
        def.Columns.First(c => c.Header == "剩余工量(天)").IsSystem.Should().BeTrue();
    }

    [Fact]
    public void Registry_SubcontractReturnItem_IsForceCompleted非系统字段()
    {
        var def = DataExchangeRegistry.Registry["SubcontractReturnItem"];

        def.Columns.First(c => c.Header == "回收支数").IsSystem.Should().BeTrue();
        def.Columns.First(c => c.Header == "回收重量(kg)").IsSystem.Should().BeTrue();
        def.Columns.First(c => c.Header == "加工状态").IsSystem.Should().BeTrue();
        def.Columns.First(c => c.Header == "强制完成").IsSystem.Should().BeFalse("强制完成是用户可设置的字段，应允许导入");
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
        // 模板含 ID 系统列（第1列，与"下载数据"列完全一致）
        sheet.Cells[1, 1].Value.Should().Be("ID");
        sheet.Cells[1, 2].Value.Should().Be("仓库编码");
        sheet.Cells[1, 3].Value.Should().Be("仓库名称");
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
        // 模板含 ID 系统列（第1列，与"下载数据"列完全一致）
        sheet.Cells[1, 1].Value.Should().Be("ID");
        sheet.Cells[1, 2].Value.Should().Be("客户编码");
        sheet.Cells[1, 3].Value.Should().Be("客户单位");
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
        // 第2行应有示例数据：ID 列（第1列）留空，业务列（第2列起）有示例值
        sheet.Cells[2, 1].Value.Should().BeNull();
        sheet.Cells[2, 2].Value.Should().NotBeNull();
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
        // 导出含 ID 系统列（第1列），业务列从第2列开始
        sheet.Cells[2, 1].Value.ToString().Should().Be("1");
        sheet.Cells[2, 2].Value.Should().Be("WH001");
        sheet.Cells[2, 3].Value.Should().Be("测试仓库");
        sheet.Cells[3, 1].Value.ToString().Should().Be("2");
        sheet.Cells[3, 2].Value.Should().Be("WH002");
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
            StandardGrade = "Q345B",
            PlantGrade = "Q345B",
            Density = 7.85m,
            SpecialMaterial = true
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
            CustomerCode = "C001",
            CustomerUnit = "测试客户",
            Salesman = "张三",
            Status = Core.Enums.CustomerStatus.Active
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
        DataExchangeRegistry.Registry.Keys.Select(k => new object[] { k });

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

    // ========== 导入测试（使用 TestableDataExchangeService 跳过原生 SQL 约束管理） ==========

    /// <summary>
    /// 创建测试 Excel 文件的 byte[]
    /// </summary>
    private static byte[] CreateTestExcel(string sheetName, List<string> headers, List<List<string?>> rows)
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add(sheetName);
        for (int c = 0; c < headers.Count; c++)
            sheet.Cells[1, c + 1].Value = headers[c];
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < headers.Count && c < rows[r].Count; c++)
            {
                if (rows[r][c] != null)
                    sheet.Cells[r + 2, c + 1].Value = rows[r][c];
            }
        }
        return package.GetAsByteArray();
    }

    private TestableDataImportService CreateTestableService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<DataImportService>>();
        return new TestableDataImportService(ctx, loggerMock.Object);
    }

    [Fact]
    public async Task ImportAsync_仓库_基础导入()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("仓库档案", new() { "仓库编码", "仓库名称", "显示顺序", "是否启用" },
            new() { new() { "WH001", "一号仓库", "1", "是" } });

        var result = await svc.ImportAsync("Warehouse", bytes, "test");

        result.SuccessCount.Should().Be(1);
        result.HasRolledBack.Should().BeFalse();
        var saved = await ctx.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH001");
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("一号仓库");
        saved.SortOrder.Should().Be(1);
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_仓库_覆盖已有记录()
    {
        var ctx = CreateDbContext();
        ctx.Warehouses.Add(new Warehouse { Code = "WH001", Name = "旧名称", SortOrder = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("仓库档案", new() { "仓库编码", "仓库名称", "显示顺序", "是否启用" },
            new() { new() { "WH001", "新名称", "2", "是" } });

        var result = await svc.ImportAsync("Warehouse", bytes, "test");

        result.SuccessCount.Should().Be(1);
        result.HasRolledBack.Should().BeFalse();

        var saved = await ctx.Warehouses.FirstAsync(w => w.Code == "WH001");
        saved.Name.Should().Be("新名称");
        saved.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task ImportAsync_仓库_带ID但ID不存在_报错()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        // 模板含 ID 列：填写库中不存在的 ID → 报错（防静默变新增导致重复）
        var bytes = CreateTestExcel("仓库档案", new() { "ID", "仓库编码", "仓库名称", "显示顺序", "是否启用" },
            new() { new() { "9999", "WH001", "不存在ID", "1", "是" } });

        var result = await svc.ImportAsync("Warehouse", bytes, "test");

        result.SuccessCount.Should().Be(0);
        result.Errors.Should().Contain(e => e.Message.Contains("ID 为 9999"));
        result.HasRolledBack.Should().BeFalse();
    }

    [Fact]
    public async Task ImportAsync_仓库_多行批量导入()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("仓库档案", new() { "仓库编码", "仓库名称", "显示顺序", "是否启用" },
            new() {
                new() { "WH001", "一号仓库", "1", "是" },
                new() { "WH002", "二号仓库", "2", "是" },
                new() { "WH003", "三号仓库", "3", "否" },
            });

        var result = await svc.ImportAsync("Warehouse", bytes, "test");

        result.SuccessCount.Should().Be(3);
        var count = await ctx.Warehouses.CountAsync();
        count.Should().Be(3);
    }

    [Fact]
    public async Task ImportAsync_仓库_可空字段不填()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        // 备注是可空字段，不填应正常导入
        var bytes = CreateTestExcel("仓库档案", new() { "仓库编码", "仓库名称", "显示顺序", "是否启用" },
            new() { new() { "WH001", "无备注仓库", "1", "是" } });

        var result = await svc.ImportAsync("Warehouse", bytes, "test");

        result.SuccessCount.Should().Be(1);
        result.HasRolledBack.Should().BeFalse();
    }

    [Fact]
    public async Task ImportAsync_客户档案_含枚举状态()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("客户档案", new() { "客户编码", "客户单位", "业务员", "状态", "备注" },
            new() { new() { "C001", "测试客户", "张三", "启用", "" } });

        var result = await svc.ImportAsync("CustomerProfile", bytes, "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.CustomerProfiles.FirstAsync(c => c.CustomerCode == "C001");
        saved.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact]
    public async Task ImportAsync_牌号对照_含枚举和decimal()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("牌号对照", new() { "标准牌号", "工厂牌号", "密度(g/cm³)", "特殊材料" },
            new() { new() { "Q345B", "Q345B", "7.85", "是" } });

        var result = await svc.ImportAsync("StandardGradeMapping", bytes, "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.StandardGradeMappings.FirstAsync(s => s.StandardGrade == "Q345B");
        saved.PlantGrade.Should().Be("Q345B");
        saved.Density.Should().Be(7.85m);
        saved.SpecialMaterial.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_供应商_自动生成编码()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        // 供应商编码是系统字段（SupplierCode → SU），不应在Excel中填
        var bytes = CreateTestExcel("供应商档案", new() { "供应商名称", "物料分类", "是否启用" },
            new() { new() { "测试供应商", "备料成品", "是" } });

        var result = await svc.ImportAsync("SupplierProfile", bytes, "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.SupplierProfiles.FirstAsync(s => s.SupplierName == "测试供应商");
        saved.SupplierCode.Should().Be("SU0001");
        saved.MaterialCategory.Should().Be("Finished", "物料分类中文应落库为 MaterialType 枚举名英文");
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_供应商_自动编码递增()
    {
        var ctx = CreateDbContext();
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = "SU0001",
            SupplierName = "已有供应商",
            MaterialCategory = "Finished",
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("供应商档案", new() { "供应商名称", "物料分类", "是否启用" },
            new() { new() { "新供应商", "备料成品", "是" } });

        var result = await svc.ImportAsync("SupplierProfile", bytes, "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.SupplierProfiles.FirstAsync(s => s.SupplierName == "新供应商");
        saved.SupplierCode.Should().Be("SU0002");
    }

    [Fact]
    public async Task ImportAsync_设备_FK引用和复合字段()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("设备", new() { "设备编号", "设备名称", "型号规格", "是否需点检", "是否需保养", "生命周期", "作用类型" },
            new() { new() { "EQ001", "冷拔机", "LB-100", "是", "是", "在用", "主生产设备" } });

        var result = await svc.ImportAsync("Equipment", bytes, "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.Equipment.FirstAsync(e => e.EquipmentCode == "EQ001");
        saved.EquipmentName.Should().Be("冷拔机");
        saved.NeedInspection.Should().BeTrue();
        saved.NeedMaintenance.Should().BeTrue();
        saved.LifecycleStatus.Should().Be("Active");
        saved.UsageType.Should().Be("Primary");
    }

    [Fact]
    public async Task ImportAsync_返回统计正确()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("仓库档案", new() { "仓库编码", "仓库名称", "显示顺序", "是否启用" },
            new() {
                new() { "WH001", "一号", "1", "是" },
                new() { "WH002", "二号", "2", "是" },
            });

        var result = await svc.ImportAsync("Warehouse", bytes, "test");

        result.TotalRows.Should().Be(2);
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    // ========== 进口增强测试 ==========

    [Fact]
    public async Task ImportAsync_生产批次_含多FK和枚举()
    {
        var ctx = CreateDbContext();
        // 先建仓库（FK 依赖）
        ctx.Warehouses.Add(new Warehouse { Code = "WH001", Name = "原料仓", SortOrder = 1, IsActive = true });
        await ctx.SaveChangesAsync();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("生产批次", new()
        {
            "生产编号", "状态", "制造物品", "强制完成",
            "工单号", "订单号", "主号",
            "签订日期", "业务员", "交货日期",
            "物料名称", "结算方式", "标准编码", "交货状态", "工厂牌号", "规格",
            "长度状态", "总数量(支)", "总米数(m)", "总重量(kg)", "总项次数", "技术要求",
            "关联项次", "仓库编码", "延期罚款",
        }, new()
        {
            new()
            {
                "B2026001",
                EnumHelper.GetDisplayName(typeof(BatchStatus), BatchStatus.InProgress),       // 状态
                EnumHelper.GetDisplayName(typeof(MaterialType), MaterialType.OrderFinished), // 制造物品
                "是",                                                                         // 强制完成
                "WO001", "SO2026001", "D01",
                "2026-01-15", "张三", "2026-06-01",
                EnumHelper.GetDisplayName(typeof(PipeManufacturingType), PipeManufacturingType.SeamlessPipe),     // 物料名称
                EnumHelper.GetDisplayName(typeof(SettlementMethod), SettlementMethod.Weighing), // 结算方式
                "GB/T 14976",
                EnumHelper.GetDisplayName(typeof(DeliveryState), DeliveryState.SolutionAnnealedAndPickled),
                "304", "48*4",
                EnumHelper.GetDisplayName(typeof(LengthStatus), LengthStatus.Fixed),           // 长度状态
                "100", "600", "5000", "2",
                EnumHelper.GetDisplayName(typeof(RequirementType), RequirementType.Special),   // 技术要求
                "1",                                                                          // 关联项次
                "WH001",                                                                       // FK→Warehouse
                "是",                                                                          // 延期罚款
            },
        });

        var result = await svc.ImportAsync("ProductionBatch", bytes, "test");

        result.HasRolledBack.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(0);

        var saved = await ctx.ProductionBatches.FirstAsync(b => b.BatchNo == "B2026001");
        saved.BatchNo.Should().Be("B2026001");
        saved.Status.Should().Be(BatchStatus.InProgress);
        saved.ManufacturingItem.Should().Be(nameof(MaterialType.OrderFinished));
        saved.IsForceCompleted.Should().BeTrue();
        saved.WorkOrderNo.Should().Be("WO001");
        saved.SalesOrderNo.Should().Be("SO2026001");
        saved.ProductionMainNo.Should().Be("D01");
        saved.MaterialName.Should().Be(nameof(PipeManufacturingType.SeamlessPipe));
        saved.SettlementMethod.Should().Be(nameof(SettlementMethod.Weighing));
        saved.StandardCode.Should().Be("GB/T 14976");
        saved.DeliveryState.Should().Be(nameof(DeliveryState.SolutionAnnealedAndPickled));
        saved.PlantGrade.Should().Be("304");
        saved.Specification.Should().Be("48*4");
        saved.LengthStatus.Should().Be(nameof(LengthStatus.Fixed));
        saved.TotalQuantity.Should().Be(100);
        saved.TotalMeters.Should().Be(600m);
        saved.TotalWeight.Should().Be(5000m);
        saved.TotalItemCount.Should().Be(2);
        saved.TechnicalRequirements.Should().Be(nameof(RequirementType.Special));
        saved.DelayPenalty.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_非法枚举值_失败()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        // 客户档案的"状态"列传入不存在的枚举值
        var bytes = CreateTestExcel("客户档案", new() { "客户编码", "客户单位", "业务员", "状态" },
            new() { new() { "C001", "测试客户", "张三", "未知状态" } });

        var result = await svc.ImportAsync("CustomerProfile", bytes, "test");

        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().Contain(e => e.Message.Contains("无法识别值") || e.Message.Contains("未知状态"));
    }

    [Fact]
    public async Task ImportAsync_不存在的FK引用_导入失败()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        // 引用一个不存在的批次号（MaterialReceiveCheck 批次号 FK → ProductionBatch）
        var bytes = CreateTestExcel("成检到料", new() { "批次号", "到料日期", "工序名称", "强制完成" },
            new() { new() { "B999", "2026-08-26", "检验", "否" } });

        var result = await svc.ImportAsync("MaterialReceiveCheck", bytes, "test");

        // FK 解析失败 → 行级错误，SuccessCount = 0
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.HasRolledBack.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Message.Contains("外键解析失败") && e.Message.Contains("批次号"));

        // 数据库中没有新记录
        ctx.Set<MES.Data.Entities.Quality.MaterialReceiveCheck>().Count().Should().Be(0);
    }

    // ========== 工段 SectionName 中文↔Key 往返 ==========

    [Fact]
    public async Task ExportAsync_工位_工段存储Key导出为中文()
    {
        var ctx = CreateDbContext();
        ctx.Workstations.Add(new Workstation
        {
            Code = "W001",
            Name = "1号抛光",
            EquipmentName = "抛光机",
            SectionName = SectionKeys.OuterPolish, // 存储为英文 Key
            ReportType = "ProductionRecord",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("Workstation");

        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        // 表头：ID/工位编码/工位名称/设备名称/工段/报工模板类型/是否启用 → 工段第 5 列（ID 为系统列前置）
        sheet.Cells[1, 5].Value.Should().Be("工段");
        sheet.Cells[2, 5].Value.Should().Be("外抛光", "SectionName 存储 Key，导出应转显示中文");
        sheet.Cells[2, 2].Value.Should().Be("W001");
    }

    [Fact]
    public async Task ImportAsync_工位_工段中文落库为Key()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("配置-工位管理", new() { "工位编码", "工位名称", "设备名称", "工段", "报工模板类型", "是否启用" },
            new() { new() { "W001", "1号抛光", "抛光机", "外抛光", "ProductionRecord", "是" } });

        var result = await svc.ImportAsync("Workstation", bytes, "test");

        result.SuccessCount.Should().Be(1);
        result.Errors.Should().BeEmpty();
        var saved = await ctx.Workstations.FirstAsync(w => w.Code == "W001");
        saved.SectionName.Should().Be(SectionKeys.OuterPolish, "Excel 中文工段导入应落库为英文 Key");
        saved.Name.Should().Be("1号抛光");
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_工位_工段填Key值也兼容()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        // Excel 中直接填英文 Key（SectionKeys.ToKey 幂等，原样通过）
        var bytes = CreateTestExcel("配置-工位管理", new() { "工位编码", "工段", "报工模板类型", "是否启用" },
            new() { new() { "W002", SectionKeys.Cut, "ProductionRecord", "是" } });

        var result = await svc.ImportAsync("Workstation", bytes, "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.Workstations.FirstAsync(w => w.Code == "W002");
        saved.SectionName.Should().Be(SectionKeys.Cut);
    }

    [Fact]
    public async Task ImportAsync_工位_工段别名也归一()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        // Excel 中填别名"切管"，应归一为 OilPipeCut
        var bytes = CreateTestExcel("配置-工位管理", new() { "工位编码", "工段", "报工模板类型", "是否启用" },
            new() { new() { "W003", "切管", "ProductionRecord", "是" } });

        var result = await svc.ImportAsync("Workstation", bytes, "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.Workstations.FirstAsync(w => w.Code == "W003");
        saved.SectionName.Should().Be(SectionKeys.OilPipeCut);
    }

    // ========== 枚举/字典列英文修复（V2.17） ==========

    [Fact]
    public void Registry_字典枚举列_已标isEnum并映射正确()
    {
        // 第一类：存英文枚举名的 string 属性列 → 标 isEnum，EnumHelper 已注册中文
        (string entity, string prop, Type enumType)[] cases =
        {
            ("SupplierProfile", "MaterialCategory", typeof(MaterialType)),
            ("PurchaseOrder", "MaterialCategory", typeof(MaterialType)),
            ("SubcontractReturnItem", "MaterialCategory", typeof(MaterialType)),
            ("Workstation", "ReportType", typeof(ReportTemplateType)),
            ("ProductionBatch", "CutDoubt", typeof(CutDoubtType)),
        };
        foreach (var (entity, prop, enumType) in cases)
        {
            var col = DataExchangeRegistry.Registry[entity].Columns.First(c => c.Property == prop);
            col.IsEnum.Should().BeTrue($"{entity}.{prop} 应标 isEnum");
            col.EnumType.Should().Be(enumType, $"{entity}.{prop} 枚举类型应为 {enumType.Name}");
        }
    }

    [Fact]
    public void DataExchangeValueHelper_字典字段_双向转换()
    {
        // 第二类：string 属性但存英文 Key 的字段，集中双向映射
        DataExchangeValueHelper.ToDisplay("DataSource", "SCAN").Should().Be("扫码");
        DataExchangeValueHelper.ToDisplay("DataSource", "MANUAL").Should().Be("手动");
        DataExchangeValueHelper.ToKey("DataSource", "扫码").Should().Be("SCAN");
        DataExchangeValueHelper.ToKey("DataSource", "手动").Should().Be("MANUAL");

        DataExchangeValueHelper.ToDisplay("UsageMode", "All").Should().Be("全部");
        DataExchangeValueHelper.ToDisplay("UsageMode", "Partial").Should().Be("部分");
        DataExchangeValueHelper.ToKey("UsageMode", "全部").Should().Be("All");
        DataExchangeValueHelper.ToKey("UsageMode", "部分").Should().Be("Partial");

        DataExchangeValueHelper.ToDisplay("ProcessType", "Piercing").Should().Be("穿孔");
        DataExchangeValueHelper.ToKey("ProcessType", "穿孔").Should().Be("Piercing");

        DataExchangeValueHelper.ToDisplay("Module", "Order").Should().Be("订单");
        DataExchangeValueHelper.ToDisplay("Module", "Batch").Should().Be("批次");
        DataExchangeValueHelper.ToDisplay("Module", "WorkOrder").Should().Be("工单");
        DataExchangeValueHelper.ToKey("Module", "订单").Should().Be("Order");

        DataExchangeValueHelper.ToDisplay("InspectionItems", "Ultrasonic,EddyCurrent").Should().Be("超声波,涡流");
        DataExchangeValueHelper.ToKey("InspectionItems", "超声波,涡流").Should().Be("Ultrasonic,EddyCurrent");

        // 未识别属性/值 → null（原样兜底）
        DataExchangeValueHelper.ToDisplay("UnknownProp", "Any").Should().BeNull();
        DataExchangeValueHelper.ToKey("UnknownProp", "Any").Should().BeNull();
    }

    [Fact]
    public async Task ExportAsync_工位_报工模板类型导出中文()
    {
        var ctx = CreateDbContext();
        ctx.Workstations.Add(new Workstation
        {
            Code = "W001",
            Name = "1号抛光",
            SectionName = SectionKeys.OuterPolish,
            ReportType = "ProductionRecord", // 存 ReportTemplateType 枚举名英文
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("Workstation");

        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        var colIdx = Enumerable.Range(1, sheet.Dimension.Columns).First(c => sheet.Cells[1, c].Value?.ToString() == "报工模板类型");
        sheet.Cells[2, colIdx].Value.Should().Be("普通报工", "ReportType 存英文枚举名，导出应转中文");
    }

    [Fact]
    public async Task ExportAsync_供应商_物料分类导出中文()
    {
        var ctx = CreateDbContext();
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = "SUP001",
            SupplierName = "某供应商",
            MaterialCategory = "Finished", // 存 MaterialType 枚举名英文
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("SupplierProfile");

        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        var colIdx = Enumerable.Range(1, sheet.Dimension.Columns).First(c => sheet.Cells[1, c].Value?.ToString() == "物料分类");
        sheet.Cells[2, colIdx].Value.Should().Be("备料成品", "MaterialCategory 存英文枚举名，导出应转中文");
    }

    [Fact]
    public async Task ExportAsync_员工_成检项目资质导出中文()
    {
        var ctx = CreateDbContext();
        ctx.Employees.Add(new Employee
        {
            Code = "E001",
            Name = "张三",
            InspectionItems = "Ultrasonic,EddyCurrent", // 逗号分隔 InspectionItem 枚举名
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("Employee");

        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        var colIdx = Enumerable.Range(1, sheet.Dimension.Columns).First(c => sheet.Cells[1, c].Value?.ToString() == "成检项目资质");
        sheet.Cells[2, colIdx].Value.Should().Be("超声波,涡流", "InspectionItems 逗号枚举串导出应转中文");
    }

    // ========== 综合复查回归测试（2026-08-26） ==========

    [Fact]
    public async Task ImportAsync_覆盖行转换失败_不污染已跟踪实体()
    {
        var ctx = CreateDbContext();
        await SeedProductionBatchAsync(ctx, "B-POLL");
        var svc = CreateTestableService(ctx);

        // 覆盖行：工厂牌号（registry 顺序在长度状态之前）可转换、长度状态非法枚举 → 收集-再应用整体失败，不污染已跟踪实体
        var bytes = CreateTestExcel("生产批次", new() { "生产编号", "工厂牌号", "长度状态" },
            new() { new() { "B-POLL", "改", "未知长度" } });

        var result = await svc.ImportAsync("ProductionBatch", bytes, "test");

        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.HasRolledBack.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("无法识别值") && e.Message.Contains("未知长度"));

        var reloaded = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.BatchNo == "B-POLL");
        reloaded.PlantGrade.Should().Be("304", "转换失败时不得把已收集的工厂牌号写入已跟踪实体");
    }

    [Fact]
    public async Task ImportAsync_复合键FK解析失败_报错而非静默跳过()
    {
        var ctx = CreateDbContext();
        await SeedProductionBatchAsync(ctx, "B-CFK");
        var svc = CreateTestableService(ctx);

        // ProductionRecord「组内序号」= ProcessGroup 复合键 FK：工序组不存在 → 应行级报错，不得静默留空
        var bytes = CreateTestExcel("批次-生产记录", new()
        {
            "批次号", "组内序号", "工序名称", "制造规格", "工段名称", "执行日期"
        }, new()
        {
            new() { "B-CFK", "999", "冷轧", "48*4", "冷轧拔", "2026-08-26" }
        });

        var result = await svc.ImportAsync("ProductionRecord", bytes, "test");

        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.HasRolledBack.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("外键解析失败") && e.Message.Contains("组内序号"));
        ctx.Set<ProductionRecord>().Count().Should().Be(0);
    }

    [Fact]
    public async Task ExportAsync_生产记录_定尺切割匹配导出中文()
    {
        var ctx = CreateDbContext();
        var batch = await SeedProductionBatchAsync(ctx, "B-CUT");
        var pg = new ProcessGroup { ProductionBatchId = batch.Id, SequenceNumber = 1, ProcessName = "60冷轧", ManufacturingSpec = "48*4" };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();
        ctx.Set<ProductionRecord>().Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            SectionName = "冷轧拔",
            SequenceNumber = 1,
            ExecDate = DateTime.Today,
            CutLengthMatchType = "FullMatch", // 存 CutLengthMatchType 枚举名英文
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("ProductionRecord");

        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        var colIdx = Enumerable.Range(1, sheet.Dimension.Columns).First(c => sheet.Cells[1, c].Value?.ToString() == "定尺切割匹配");
        sheet.Cells[2, colIdx].Value.Should().Be("完全匹配", "CutLengthMatchType 存英文枚举名，导出应转中文");
    }

    [Fact]
    public async Task ExportAsync_去油酸洗完工_班次导出中文()
    {
        var ctx = CreateDbContext();
        var batch = await SeedProductionBatchAsync(ctx, "B-PKL");
        var pg = new ProcessGroup { ProductionBatchId = batch.Id, SequenceNumber = 1, ProcessName = "Pickling", ManufacturingSpec = "48*4" };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();
        var pin = new PicklingInRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "Pickling",
            SectionName = SectionKeys.Pickle,
            SequenceNumber = 1,
            InDate = DateTime.Today,
        };
        ctx.Set<PicklingInRecord>().Add(pin);
        await ctx.SaveChangesAsync();
        ctx.Set<PicklingOutRecord>().Add(new PicklingOutRecord
        {
            PicklingInRecordId = pin.Id,
            CompleteDate = DateTime.Today,
            SectionName = SectionKeys.Pickle,
            BatchNo = batch.BatchNo,
            ProcessName = "Pickling",
            ProductionBatchId = batch.Id,
            Shift = "DayShift", // 存 ShiftType 枚举名英文
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var bytes = await svc.ExportAsync("PicklingOutRecord");

        using var package = new ExcelPackage(new MemoryStream(bytes));
        var sheet = package.Workbook.Worksheets[0];
        var colIdx = Enumerable.Range(1, sheet.Dimension.Columns).First(c => sheet.Cells[1, c].Value?.ToString() == "班次");
        sheet.Cells[2, colIdx].Value.Should().Be("白班", "PicklingOutRecord.Shift 存 ShiftType 枚举名，导出应转中文");
    }

    private async Task<ProductionBatch> SeedProductionBatchAsync(AppDbContext ctx, string batchNo = "B-001")
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            MaterialName = "无缝管",
            PlantGrade = "304",
            Specification = "48*4",
            Status = BatchStatus.InProgress,
            ProductionType = "Internal",
            ManufacturingItem = "OrderFinished",
            WorkOrderNo = "WO-001",
            SalesOrderNo = "SO-001",
            ProductionMainNo = "M-001",
            OrderItemIds = "1",
            Salesman = "测试",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Fixed",
            TechnicalRequirements = "按标准",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            TotalQuantity = 100,
            TotalMeters = 600m,
            TotalWeight = 5000m,
            TotalItemCount = 2
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }
}

/// <summary>
/// 可测试的 DataImportService：跳过 SQL Server 原生约束管理（InMemory 不支持原生 SQL）
/// </summary>
public class TestableDataImportService : DataImportService
{
    public TestableDataImportService(AppDbContext context, ILogger<DataImportService> logger)
        : base(context, logger) { }

    protected override Task DisableAllConstraintsAsync(DbConnection connection, DbTransaction transaction)
    {
        return Task.CompletedTask;
    }

    protected override Task<List<string>> EnableAndCheckConstraintsAsync(DbConnection connection, DbTransaction transaction)
    {
        return Task.FromResult(new List<string>());
    }

    protected override Task<(IDbContextTransaction transaction, DbTransaction? dbTransaction)> BeginImportTransactionAsync()
    {
        var mock = new Mock<IDbContextTransaction>();
        return Task.FromResult<(IDbContextTransaction transaction, DbTransaction? dbTransaction)>((mock.Object, null));
    }
}
