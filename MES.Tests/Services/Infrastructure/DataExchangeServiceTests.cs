using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities;
using MES.Services.DataExchange;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;
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
        var exportService = new DataExportService(ctx, exportLoggerMock.Object);
        var importService = new DataImportService(ctx, importLoggerMock.Object);
        return new DataExchangeService(importService, exportService, fixServiceMock.Object, loggerMock.Object);
    }

    // ========== Registry 验证 ==========

    [Fact]
    public void Registry_包含所有67个实体()
    {
        DataExchangeRegistry.Registry.Should().HaveCount(67);
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

        headers.Should().Contain("有效投料疑问");
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

        def.Columns.First(c => c.Header == "有效投料疑问").IsSystem.Should().BeTrue();
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
    public async Task ImportAsync_仓库_基础导入_skip策略()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("仓库档案", new() { "仓库编码", "仓库名称", "显示顺序", "是否启用" },
            new() { new() { "WH001", "一号仓库", "1", "是" } });

        var result = await svc.ImportAsync("Warehouse", bytes, "skip", "test");

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

        var result = await svc.ImportAsync("Warehouse", bytes, "overwrite", "test");

        result.SuccessCount.Should().Be(1);
        result.HasRolledBack.Should().BeFalse();

        var saved = await ctx.Warehouses.FirstAsync(w => w.Code == "WH001");
        saved.Name.Should().Be("新名称");
        saved.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task ImportAsync_仓库_跳过重复()
    {
        var ctx = CreateDbContext();
        ctx.Warehouses.Add(new Warehouse { Code = "WH001", Name = "原始名称", SortOrder = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("仓库档案", new() { "仓库编码", "仓库名称", "显示顺序", "是否启用" },
            new() { new() { "WH001", "跳过不应更新", "2", "是" } });

        var result = await svc.ImportAsync("Warehouse", bytes, "skip", "test");

        result.SuccessCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
        result.HasRolledBack.Should().BeFalse();

        var saved = await ctx.Warehouses.FirstAsync(w => w.Code == "WH001");
        saved.Name.Should().Be("原始名称");
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

        var result = await svc.ImportAsync("Warehouse", bytes, "skip", "test");

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

        var result = await svc.ImportAsync("Warehouse", bytes, "skip", "test");

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

        var result = await svc.ImportAsync("CustomerProfile", bytes, "skip", "test");

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

        var result = await svc.ImportAsync("StandardGradeMapping", bytes, "skip", "test");

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
            new() { new() { "测试供应商", "不锈钢管", "是" } });

        var result = await svc.ImportAsync("SupplierProfile", bytes, "skip", "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.SupplierProfiles.FirstAsync(s => s.SupplierName == "测试供应商");
        saved.SupplierCode.Should().Be("SU0001");
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
            MaterialCategory = "钢管",
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("供应商档案", new() { "供应商名称", "物料分类", "是否启用" },
            new() { new() { "新供应商", "不锈钢管", "是" } });

        var result = await svc.ImportAsync("SupplierProfile", bytes, "skip", "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.SupplierProfiles.FirstAsync(s => s.SupplierName == "新供应商");
        saved.SupplierCode.Should().Be("SU0002");
    }

    [Fact]
    public async Task ImportAsync_物料_复合键_不填备注()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("物料", new() { "物料分类", "厂内钢种", "名义规格", "是否启用" },
            new() { new() { "管坯", "304", "Φ65", "是" } });

        var result = await svc.ImportAsync("Material", bytes, "skip", "test");

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.Materials.FirstAsync(m => m.MaterialCategory == "管坯");
        saved.PlantGrade.Should().Be("304");
        saved.Specification.Should().Be("Φ65");
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_设备_FK引用和复合字段()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        var bytes = CreateTestExcel("设备", new() { "设备编号", "设备名称", "型号规格", "是否需点检", "是否需保养", "生命周期", "作用类型" },
            new() { new() { "EQ001", "冷拔机", "LB-100", "是", "是", "在用", "主生产设备" } });

        var result = await svc.ImportAsync("Equipment", bytes, "skip", "test");

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

        var result = await svc.ImportAsync("Warehouse", bytes, "skip", "test");

        result.TotalRows.Should().Be(2);
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.Strategy.Should().Be("skip");
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
                EnumHelper.GetDisplayName(typeof(ManufacturingItem), ManufacturingItem.OrderFinishedProduct), // 制造物品
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

        var result = await svc.ImportAsync("ProductionBatch", bytes, "skip", "test");

        result.HasRolledBack.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(0);

        var saved = await ctx.ProductionBatches.FirstAsync(b => b.BatchNo == "B2026001");
        saved.BatchNo.Should().Be("B2026001");
        saved.Status.Should().Be(BatchStatus.InProgress);
        saved.ManufacturingItem.Should().Be(nameof(ManufacturingItem.OrderFinishedProduct));
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
        saved.WarehouseId.Should().Be(
            ctx.Warehouses.Where(w => w.Code == "WH001").Select(w => w.Id).First());
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

        var result = await svc.ImportAsync("CustomerProfile", bytes, "skip", "test");

        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().Contain(e => e.Message.Contains("无法识别值") || e.Message.Contains("未知状态"));
    }

    [Fact]
    public async Task ImportAsync_不存在的FK引用_导入失败()
    {
        var ctx = CreateDbContext();
        var svc = CreateTestableService(ctx);

        // 不建任何 CustomerProfile，引用一个不存在的客户编码
        var bytes = CreateTestExcel("销售订单", new() { "订单号", "签订日期", "客户编码", "状态" },
            new() { new() { "SO2026001", "2026-01-15", "C999", "已确认" } });

        var result = await svc.ImportAsync("SalesOrder", bytes, "skip", "test");

        // FK 解析失败 → 行级错误，SuccessCount = 0
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.HasRolledBack.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Message.Contains("外键解析失败") && e.Message.Contains("客户编码"));

        // 数据库中没有新记录
        ctx.Set<SalesOrder>().Count().Should().Be(0);
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
