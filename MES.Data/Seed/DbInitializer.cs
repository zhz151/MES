using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Order;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Quality;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using MES.Shared.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MES.Core.Enums;
using MES.Core.Constants;
using MES.Core.Helpers;

namespace MES.Data.Seed;

public static class DbInitializer
{
    /// <summary>DictValueDefinition 种子字典：除专门配置表（工段/工序）外的 11 个（含责任类别/NCR责任类别/原锁备注/生产关注/岗位/岗位类别）</summary>
    private static readonly string[] DictValueSeedKeys =
    [
        DictValueDefaults.UrgencyLevelKey,
        DictValueDefaults.ProductStatus,
        DictValueDefaults.ProductionFlowKey,
        DictValueDefaults.FlowTargetKey,
        DictValueDefaults.ProductionOverviewRowKey,
        DictValueDefaults.LiabilityTypeKey,
        DictValueDefaults.NcrResponsibilityKey,
        DictValueDefaults.RawMaterialLockRemarkKey,
        DictValueDefaults.ProductionAttentionKey,
        DictValueDefaults.PositionKey,
        DictValueDefaults.PositionCategoryKey,
    ];

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // 默认管理员密码：优先读配置 Seed:AdminPassword（部署时应通过环境变量/占位符注入），缺失回退内置默认并告警
        var adminPassword = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = "Admin@123";
            Console.WriteLine("[DbInitializer] 未配置 Seed:AdminPassword，使用默认管理员密码 Admin@123，生产环境请务必通过配置注入！");
        }

        // ========== 1. Initialize Roles ==========
        foreach (var role in Roles.GetAllRoles())
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ========== 2. Initialize Admin Account ==========
        // 用户名用 Roles.Admin（"Admin"），不与邮箱混淆；邮箱 admin@mes.com 仅作为登录兜底
        // 兼容存量库：旧种子曾用 UserName="admin@mes.com"，故按邮箱二次查找避免重复创建
        var adminUser = await userManager.FindByNameAsync(Roles.Admin)
            ?? await userManager.FindByEmailAsync("admin@mes.com");
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                UserName = Roles.Admin,
                Email = "admin@mes.com",
                FullName = "System Administrator",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Roles.Admin);
            }
        }

        // ========== 3. Grade Mappings ==========
        // 牌号对照表数据由用户通过数据工具导入，此处不生成种子数据

        // ========== 5. Initialize Test Customers ==========
        if (!context.CustomerProfiles.Any())
        {
            var customers = new List<CustomerProfile>
            {
                new CustomerProfile
                {
                    CustomerCode = "C001",
                    Salesman = "Zhang San",
                    CustomerUnit = "XX Petrochemical Engineering Co., Ltd.",
                    EndCustomer = "XX Refinery",
                    ContactPerson = "Manager Li",
                    ContactPhone = "13800000001",
                    Address = "No. 88, Petrochemical Avenue, Ningbo, Zhejiang",
                    Status = CustomerStatus.Active,
                    Remark = "Long-term cooperation customer, mainly purchasing 316L/304 seamless pipes"
                },
                new CustomerProfile
                {
                    CustomerCode = "C002",
                    Salesman = "Li Si",
                    CustomerUnit = "XX Boiler Manufacturing Co., Ltd.",
                    EndCustomer = "XX Boiler Manufacturing Co., Ltd.",
                    ContactPerson = "Engineer Wang",
                    ContactPhone = "13800000002",
                    Address = "No. 18, Industrial Park, Wuxi, Jiangsu",
                    Status = CustomerStatus.Active,
                    Remark = "Boiler tube customer, need to provide quality certificate"
                },
                new CustomerProfile
                {
                    CustomerCode = "C003",
                    Salesman = "Wang Wu",
                    CustomerUnit = "XX Ocean Engineering Co., Ltd.",
                    EndCustomer = "XX Offshore Platform Project",
                    ContactPerson = "Manager Zhao",
                    ContactPhone = "13800000003",
                    Address = "No. 1, Marine Industry Road, Qingdao, Shandong",
                    Status = CustomerStatus.Active,
                    Remark = "Marine engineering tubes, requires duplex stainless steel"
                },
                new CustomerProfile
                {
                    CustomerCode = "C004",
                    Salesman = "Zhao Liu",
                    CustomerUnit = "XX Heat Exchanger Co., Ltd.",
                    EndCustomer = "XX Heat Exchanger Co., Ltd.",
                    ContactPerson = "Manager Sun",
                    ContactPhone = "13800000004",
                    Address = "No. 66, Industrial Avenue, Foshan, Guangdong",
                    Status = CustomerStatus.Active,
                    Remark = "Heat exchanger tubes, requires high precision"
                },
                new CustomerProfile
                {
                    CustomerCode = "C005",
                    Salesman = "Qian Qi",
                    CustomerUnit = "XX Food Machinery Co., Ltd.",
                    EndCustomer = "XX Food Machinery Co., Ltd.",
                    ContactPerson = "Engineer Zhou",
                    ContactPhone = "13800000005",
                    Address = "No. 2, Food Industrial Park, Shanghai",
                    Status = CustomerStatus.Active,
                    Remark = "Food grade stainless steel tubes, requires internal polishing"
                },
                new CustomerProfile
                {
                    CustomerCode = "C006",
                    Salesman = "Zhang San",
                    CustomerUnit = "XX Chemical Equipment Co., Ltd.",
                    EndCustomer = "XX Chemical Plant",
                    ContactPerson = "Engineer Chen",
                    ContactPhone = "13800000006",
                    Address = "No. 5, Chemical Park, Nanjing, Jiangsu",
                    Status = CustomerStatus.Active,
                    Remark = "Chemical equipment tubes"
                },
                new CustomerProfile
                {
                    CustomerCode = "C007",
                    Salesman = "Li Si",
                    CustomerUnit = "XX Pharmaceutical Machinery Co., Ltd.",
                    EndCustomer = "XX Pharmaceutical Machinery Co., Ltd.",
                    ContactPerson = "Manager Liu",
                    ContactPhone = "13800000007",
                    Address = "No. 10, Biomedical Park, Shanghai",
                    Status = CustomerStatus.Active,
                    Remark = "Pharmaceutical machinery tubes"
                },
                new CustomerProfile
                {
                    CustomerCode = "C008",
                    Salesman = "Wang Wu",
                    CustomerUnit = "XX Shipyard",
                    EndCustomer = "XX Vessel Project",
                    ContactPerson = "Manager Xu",
                    ContactPhone = "13800000008",
                    Address = "No. 1, Shipbuilding Road, Dalian, Liaoning",
                    Status = CustomerStatus.Active,
                    Remark = "Shipbuilding tubes"
                },
                new CustomerProfile
                {
                    CustomerCode = "C009",
                    Salesman = "Zhao Liu",
                    CustomerUnit = "XX Nuclear Equipment Co., Ltd.",
                    EndCustomer = "XX Nuclear Power Plant",
                    ContactPerson = "Engineer Huang",
                    ContactPhone = "13800000009",
                    Address = "No. 88, Nuclear Power Avenue, Shenzhen, Guangdong",
                    Status = CustomerStatus.Active,
                    Remark = "Nuclear power tubes, requires strict standards"
                },
                new CustomerProfile
                {
                    CustomerCode = "C010",
                    Salesman = "Qian Qi",
                    CustomerUnit = "XX Medical Device Co., Ltd.",
                    EndCustomer = "XX Medical Device Co., Ltd.",
                    ContactPerson = "Manager Zhu",
                    ContactPhone = "13800000010",
                    Address = "No. 20, Medical Device Park, Suzhou, Jiangsu",
                    Status = CustomerStatus.Active,
                    Remark = "Medical device tubes"
                }
            };

            await context.CustomerProfiles.AddRangeAsync(customers);
            await context.SaveChangesAsync();
        }

        // ========== 6. Initialize Warehouses ==========
        if (!context.Warehouses.Any())
        {
            var warehouses = new List<Warehouse>
            {
                new Warehouse
                {
                    Code = "RAW",
                    Name = "原料库",
                    SortOrder = 1,
                    IsActive = true,
                    Remark = "原料（荒管/圆钢）存放"
                },
                new Warehouse
                {
                    Code = "FG",
                    Name = "成品库",
                    SortOrder = 2,
                    IsActive = true,
                    Remark = "成品管存放"
                },
                new Warehouse
                {
                    Code = "DEFECT",
                    Name = "次品库",
                    SortOrder = 3,
                    IsActive = true,
                    Remark = "次品/不合格品存放"
                },
                new Warehouse
                {
                    Code = "WIP",
                    Name = "在制品库",
                    SortOrder = 4,
                    IsActive = true,
                    Remark = "在制品/半成品存放"
                }
            };

            await context.Warehouses.AddRangeAsync(warehouses);
            await context.SaveChangesAsync();
        }

        // ========== 8. Initialize Standard Work Days ==========
        if (!context.StandardWorkDays.Any())
        {
            var workDays = new List<StandardWorkDay>
            {
                // 通用配置（PlantGradePrefix = null）
                new() { SectionName = SectionDefs.ColdRollDraw,      SectionKey = nameof(SectionDefs.ColdRollDraw),      DisplayOrder = 1,  IsEnabled = true,  PlantGradePrefix = null, StandardDays = 2,   Remark = "冷轧/冷拔" },
                new() { SectionName = SectionDefs.OilPipeCut,        SectionKey = nameof(SectionDefs.OilPipeCut),        DisplayOrder = 2,  IsEnabled = true,  PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = SectionDefs.Degrease,          SectionKey = nameof(SectionDefs.Degrease),          DisplayOrder = 3,  IsEnabled = true,  PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = SectionDefs.EmulsionWash,      SectionKey = nameof(SectionDefs.EmulsionWash),      DisplayOrder = 4,  IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.UltrasonicWash,    SectionKey = nameof(SectionDefs.UltrasonicWash),    DisplayOrder = 5,  IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.ClothPolish,       SectionKey = nameof(SectionDefs.ClothPolish),       DisplayOrder = 6,  IsEnabled = true,  PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = SectionDefs.BrightAnnealing,   SectionKey = nameof(SectionDefs.BrightAnnealing),   DisplayOrder = 7,  IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.Solution,          SectionKey = nameof(SectionDefs.Solution),          DisplayOrder = 8,  IsEnabled = true,  PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = SectionDefs.Straighten,        SectionKey = nameof(SectionDefs.Straighten),        DisplayOrder = 9,  IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.Cut,               SectionKey = nameof(SectionDefs.Cut),               DisplayOrder = 10, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.ThicknessMeasure,  SectionKey = nameof(SectionDefs.ThicknessMeasure),  DisplayOrder = 11, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = SectionDefs.Pickle,            SectionKey = nameof(SectionDefs.Pickle),            DisplayOrder = 12, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 2,   Remark = "非3系牌号" },
                new() { SectionName = SectionDefs.OuterPolish,       SectionKey = nameof(SectionDefs.OuterPolish),       DisplayOrder = 13, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.InnerPolish,       SectionKey = nameof(SectionDefs.InnerPolish),       DisplayOrder = 14, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = SectionDefs.InnerGrinding,     SectionKey = nameof(SectionDefs.InnerGrinding),     DisplayOrder = 15, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.OuterSpotGrinding, SectionKey = nameof(SectionDefs.OuterSpotGrinding), DisplayOrder = 16, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.SandBlasting,      SectionKey = nameof(SectionDefs.SandBlasting),      DisplayOrder = 17, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = SectionDefs.ShotBlasting,      SectionKey = nameof(SectionDefs.ShotBlasting),      DisplayOrder = 18, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.Inspection,        SectionKey = nameof(SectionDefs.Inspection),        DisplayOrder = 19, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = SectionDefs.WeldingHead,       SectionKey = nameof(SectionDefs.WeldingHead),       DisplayOrder = 20, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.Welding,           SectionKey = nameof(SectionDefs.Welding),           DisplayOrder = 21, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.Lubrication,       SectionKey = nameof(SectionDefs.Lubrication),       DisplayOrder = 22, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = SectionDefs.Packing,           SectionKey = nameof(SectionDefs.Packing),           DisplayOrder = 23, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.Warehouse,         SectionKey = nameof(SectionDefs.Warehouse),         DisplayOrder = 24, IsEnabled = true,  PlantGradePrefix = null, StandardDays = 2,   Remark = null },
                new() { SectionName = SectionDefs.Extra1,            SectionKey = nameof(SectionDefs.Extra1),            DisplayOrder = 25, IsEnabled = false, PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = SectionDefs.Extra2,            SectionKey = nameof(SectionDefs.Extra2),            DisplayOrder = 26, IsEnabled = false, PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                // 牌号前缀覆盖：3系牌号酸洗只需1天
                new() { SectionName = SectionDefs.Pickle,            SectionKey = nameof(SectionDefs.Pickle),            DisplayOrder = 12, IsEnabled = true,  PlantGradePrefix = "3",  StandardDays = 1,   Remark = "3系牌号（304/316/321等）" },
            };

            await context.StandardWorkDays.AddRangeAsync(workDays);
            await context.SaveChangesAsync();
        }

        // ========== 8b. 回填工段字典字段（SectionKey/DisplayOrder/IsEnabled）——幂等 ==========
        // 存量数据迁移加列后 SectionKey 为 null，此处按 SectionName 反查补全；仅执行一次
        var swdMissingKey = await context.StandardWorkDays
            .Where(w => w.SectionKey == null)
            .ToListAsync();
        if (swdMissingKey.Count > 0)
        {
            foreach (var w in swdMissingKey)
            {
                var key = SectionDefs.PropertyToName
                    .FirstOrDefault(kv => string.Equals(kv.Value, w.SectionName, StringComparison.OrdinalIgnoreCase))
                    .Key;
                if (string.IsNullOrEmpty(key)) continue;

                w.SectionKey = key;
                var idx = Array.IndexOf(SectionDefs.All, w.SectionName);
                w.DisplayOrder = idx >= 0 ? idx + 1 : 999;
                w.IsEnabled = !string.Equals(w.SectionName, SectionDefs.Extra1, StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(w.SectionName, SectionDefs.Extra2, StringComparison.OrdinalIgnoreCase);
            }
            await context.SaveChangesAsync();
        }

        // ========== 8c. Initialize Process Definitions（工序组配置表）——幂等 ==========
        // 预置工序默认工段（SectionKey 列表，JSON 数组字符串），计划页新增该工序行自动填充；AdditionalFinalInspection 无默认
        var defaultSectionsByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProcessKeys.RoughTubeProcessing] = "[\"Straighten\",\"Cut\",\"Pickle\",\"OuterPolish\",\"OuterSpotGrinding\",\"Inspection\"]",
            [ProcessKeys.InProcessRepair] = "[\"Solution\",\"Straighten\",\"Cut\",\"Pickle\",\"Inspection\"]",
            [ProcessKeys.ColdRoll60] = "[\"ColdRollDraw\",\"OilPipeCut\",\"Degrease\",\"Solution\",\"Straighten\",\"Cut\",\"Pickle\",\"Inspection\"]",
            [ProcessKeys.ColdRoll50] = "[\"ColdRollDraw\",\"OilPipeCut\",\"Degrease\",\"Solution\",\"Straighten\",\"Cut\",\"Pickle\",\"Inspection\"]",
            [ProcessKeys.ColdRoll30] = "[\"ColdRollDraw\",\"OilPipeCut\",\"Degrease\",\"Solution\",\"Straighten\",\"Cut\",\"Pickle\",\"Inspection\"]",
            [ProcessKeys.ColdRoll20] = "[\"ColdRollDraw\",\"OilPipeCut\",\"Degrease\",\"Solution\",\"Straighten\",\"Cut\",\"Pickle\",\"Inspection\"]",
            [ProcessKeys.ThreeRollColdRoll] = "[\"ColdRollDraw\",\"OilPipeCut\",\"Degrease\",\"Solution\",\"Straighten\",\"Cut\",\"Pickle\",\"Inspection\"]",
            [ProcessKeys.ColdDraw] = "[\"ColdRollDraw\",\"Solution\",\"Straighten\",\"Cut\",\"Pickle\",\"Inspection\"]",
        };
        string? GetDefaultSections(string key)
            => defaultSectionsByKey.TryGetValue(key, out var ds) ? ds : null;

        if (!context.ProcessDefinitions.Any())
        {
            var processDefs = new List<ProcessDefinition>
            {
                new() { ProcessKey = ProcessKeys.RoughTubeProcessing,           ProcessName = ProcessNames.RoughTubeProcessing,           DisplayOrder = 1, IsEnabled = true, IsColdRoll = false, IsColdDraw = false, DefaultSections = GetDefaultSections(ProcessKeys.RoughTubeProcessing),           Remark = null },
                new() { ProcessKey = ProcessKeys.InProcessRepair,               ProcessName = ProcessNames.InProcessRepair,               DisplayOrder = 2, IsEnabled = true, IsColdRoll = false, IsColdDraw = false, DefaultSections = GetDefaultSections(ProcessKeys.InProcessRepair),               Remark = null },
                new() { ProcessKey = ProcessKeys.ColdRoll60,                    ProcessName = ProcessNames.ColdRoll60,                    DisplayOrder = 3, IsEnabled = true, IsColdRoll = true,  IsColdDraw = false, DefaultSections = GetDefaultSections(ProcessKeys.ColdRoll60),                    Remark = null },
                new() { ProcessKey = ProcessKeys.ColdRoll50,                    ProcessName = ProcessNames.ColdRoll50,                    DisplayOrder = 4, IsEnabled = true, IsColdRoll = true,  IsColdDraw = false, DefaultSections = GetDefaultSections(ProcessKeys.ColdRoll50),                    Remark = null },
                new() { ProcessKey = ProcessKeys.ColdRoll30,                    ProcessName = ProcessNames.ColdRoll30,                    DisplayOrder = 5, IsEnabled = true, IsColdRoll = true,  IsColdDraw = false, DefaultSections = GetDefaultSections(ProcessKeys.ColdRoll30),                    Remark = null },
                new() { ProcessKey = ProcessKeys.ColdRoll20,                    ProcessName = ProcessNames.ColdRoll20,                    DisplayOrder = 6, IsEnabled = true, IsColdRoll = true,  IsColdDraw = false, DefaultSections = GetDefaultSections(ProcessKeys.ColdRoll20),                    Remark = null },
                new() { ProcessKey = ProcessKeys.ThreeRollColdRoll,             ProcessName = ProcessNames.ThreeRollColdRoll,             DisplayOrder = 7, IsEnabled = true, IsColdRoll = true,  IsColdDraw = false, DefaultSections = GetDefaultSections(ProcessKeys.ThreeRollColdRoll),             Remark = null },
                new() { ProcessKey = ProcessKeys.ColdDraw,                      ProcessName = ProcessNames.ColdDraw,                      DisplayOrder = 8, IsEnabled = true, IsColdRoll = false, IsColdDraw = true,  DefaultSections = GetDefaultSections(ProcessKeys.ColdDraw),                      Remark = null },
                new() { ProcessKey = ProcessKeys.AdditionalFinalInspection,     ProcessName = ProcessNames.AdditionalFinalInspection,     DisplayOrder = 9, IsEnabled = true, IsColdRoll = false, IsColdDraw = false, DefaultSections = GetDefaultSections(ProcessKeys.AdditionalFinalInspection),     Remark = "成品检验附加工序" },
            };

            await context.ProcessDefinitions.AddRangeAsync(processDefs);
            await context.SaveChangesAsync();
        }
        else
        {
            // 存量库回填：仅补 DefaultSections 为空的预置工序（幂等，不动已配置值）
            var defsPending = await context.ProcessDefinitions
                .Where(w => w.DefaultSections == null)
                .ToListAsync();
            if (defsPending.Count > 0)
            {
                var changed = false;
                foreach (var def in defsPending)
                {
                    var ds = GetDefaultSections(def.ProcessKey);
                    if (ds != null)
                    {
                        def.DefaultSections = ds;
                        changed = true;
                    }
                }
                if (changed)
                    await context.SaveChangesAsync();
            }
        }

        // ========== 8d. Initialize ColdRollMachineGroupConfig（冷轧机台组配置）——幂等 ==========
        // 预置 4 组（5060 供给目标=2030 / 2030 / 三辊 / 冷拔），与 ColdRollMachineGroupKeys.Groups 规范定义源一致。
        // 引擎归组运行时读本表；新增冷轧工序可经配置归入现有组或新建组（勿改代码）。
        // 供需链：5060 组 SupplyTargetGroupKey='2030'（2026-08-29 方案 A，允许多链/多级链，组角色字段已移除）。
        if (!context.ColdRollMachineGroupConfigs.Any())
        {
            var groupConfigs = ColdRollMachineGroupKeys.Groups
                .Select((g, i) => new ColdRollMachineGroupConfig
                {
                    GroupKey = g.Key,
                    DisplayName = g.Display,
                    ProcessKeys = string.Join(",", g.Keys),
                    DisplayOrder = i + 1,
                    SupplyTargetGroupKey = g.SupplyTargetGroupKey,
                    Remark = null,
                })
                .ToList();
            await context.ColdRollMachineGroupConfigs.AddRangeAsync(groupConfigs);
            await context.SaveChangesAsync();
        }

        // ========== 8e. Initialize Enum Display Definitions（枚举显示配置，41 枚举）——幂等 ==========
        if (!context.EnumDisplayDefinitions.Any())
        {
            var enumRows = new List<EnumDisplayDefinition>();
            foreach (var kvp in EnumHelper.GetAllMappings())
            {
                var order = 0;
                foreach (var v in kvp.Value)
                {
                    enumRows.Add(new EnumDisplayDefinition
                    {
                        EnumKey = kvp.Key,
                        Value = v.Key,
                        DisplayName = v.Value,
                        DisplayOrder = ++order,
                        Remark = null
                    });
                }
            }

            await context.EnumDisplayDefinitions.AddRangeAsync(enumRows);
            await context.SaveChangesAsync();
        }

        // ========== 8f. Initialize Dict Value Definitions（字典显示配置）——幂等 ==========
        // 仅含无专门配置表的 10 个字典（紧急度/产类/流转/关注目标/汇总行/责任类别/NCR责任类别/原锁备注/生产关注/岗位）；
        // 工段/工序由各自专门配置表管理（StandardWorkDays/ProcessDefinitions），不在此重复 seed，避免双入口。
        if (!context.DictValueDefinitions.Any())
        {
            var dictRows = new List<DictValueDefinition>();
            foreach (var dictKey in DictValueSeedKeys)
            {
                if (!DictValueDefaults.All.TryGetValue(dictKey, out var map)) continue;
                var order = 0;
                foreach (var v in map)
                {
                    dictRows.Add(new DictValueDefinition
                    {
                        DictKey = dictKey,
                        Value = v.Key,
                        DisplayName = v.Value,
                        DisplayOrder = ++order,
                        IsEnabled = true,
                        Remark = null
                    });
                }
            }

            await context.DictValueDefinitions.AddRangeAsync(dictRows);
            await context.SaveChangesAsync();
        }

        // ========== 9. Initialize Standard Work Day Delivery States ==========
        if (!context.StandardWorkDayDeliveryStates.Any())
        {
            var deliveryStates = new List<StandardWorkDayDeliveryState>
            {
                // 默认：非固溶酸洗/非硬态 +4 天
                new() { DeliveryState = "",                  ExtraDays = 4, Remark = "默认附加天数（非固溶酸洗/非硬态）" },
                // 固溶酸洗及其变体 → +0 天（不额外加）
                new() { DeliveryState = "SolutionAnnealedAndPickled",                   ExtraDays = 0, Remark = "固溶酸洗" },
                new() { DeliveryState = "SolutionAnnealedAndPickledUTube",              ExtraDays = 0, Remark = "固溶酸洗-U型管" },
                new() { DeliveryState = "SolutionAnnealedAndPickledExternalPolished",   ExtraDays = 0, Remark = "固溶酸洗-外抛光" },
                new() { DeliveryState = "SolutionAnnealedAndPickledInternalPolished",   ExtraDays = 0, Remark = "固溶酸洗-内抛光" },
                new() { DeliveryState = "SolutionAnnealedAndPickledBothPolished",       ExtraDays = 0, Remark = "固溶酸洗-内外抛光" },
                new() { DeliveryState = "SolutionAnnealedAndPickledCoiled",             ExtraDays = 0, Remark = "固溶酸洗-盘管" },
                // 光亮及其变体 → +4 天
                new() { DeliveryState = "Bright",             ExtraDays = 4, Remark = "光亮" },
                new() { DeliveryState = "BrightUTube",        ExtraDays = 4, Remark = "光亮-U型管" },
                new() { DeliveryState = "BrightCoiled",       ExtraDays = 4, Remark = "光亮-盘管" },
                // 硬态 → +0 天（不额外加）
                new() { DeliveryState = "Hard",               ExtraDays = 0, Remark = "硬态" },
            };

            await context.StandardWorkDayDeliveryStates.AddRangeAsync(deliveryStates);
            await context.SaveChangesAsync();
        }

        // ========== 10. Initialize Config Parameters ==========
        if (!context.ConfigParameters.Any())
        {
            var configParams = new List<ConfigParameter>
            {
                // ===== WarehouseThreshold 仓库完工阈值 =====
                new() { Category = "WarehouseThreshold", CategoryDisplay = "工单-入库完结比率", Context = "工单", ParamKey = "CompleteRatio", ParamValue = 0.95m, Remark = "入库完工比率阈值" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "工单-入库完结偏差", Context = "工单", ParamKey = "CompleteDeviation", ParamValue = 100m, Remark = "入库完工绝对偏差(kg)" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "工单-入库超额比率", Context = "工单", ParamKey = "CompleteOverRatio", ParamValue = 1.05m, Remark = "入库超额比率阈值(重量口径入库重>需求重×此比率判定入库超额)" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "委外-回收比率", Context = "物料", ParamKey = "SubcontractCompleteRatio", ParamValue = 0.95m, Remark = "委外完工比率阈值" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "采购-完工比率", Context = "物料", ParamKey = "PurchaseCompleteRatio", ParamValue = 0.965m, Remark = "采购完工比率阈值" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "采购-完工偏差", Context = "物料", ParamKey = "PurchaseCompleteDeviation", ParamValue = 200m, Remark = "采购完工绝对偏差(kg)" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "采购-超额比率", Context = "物料", ParamKey = "PurchaseOverRatio", ParamValue = 1.05m, Remark = "采购超额比率阈值(实际采购/委外量>计划量×此比率判定超额采购/超额穿孔)" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "采购-超额偏差", Context = "物料", ParamKey = "PurchaseOverDeviation", ParamValue = 100m, Remark = "采购超额绝对偏差(kg)：到料重量>计划×超额比率且超出量>此阈值判定超量到货" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "委外-超额比率", Context = "物料", ParamKey = "SubcontractOverRatio", ParamValue = 1.05m, Remark = "委外超额回收比率阈值(回收重量>需求重量×此比率判定超量回收)" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "委外-超额偏差", Context = "物料", ParamKey = "SubcontractOverDeviation", ParamValue = 100m, Remark = "委外超额回收绝对偏差(kg)：回收重量>需求×超额比率且超出量>此阈值判定超量到货" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "仓库-完工阈值", Context = "批次", ParamKey = "OutsourceRecoveryRatio", ParamValue = 0.99m, Remark = "委外回收比率阈值" },

                // ===== ProductionThreshold 生产阈值 =====
                new() { Category = "ProductionThreshold", CategoryDisplay = "批次-生产阈值", Context = "批次", ParamKey = "ColdRollCompleteRatio", ParamValue = 0.95m, Remark = "冷轧拔完工比率" },

                // ===== MaterialPlanRatio 物料计划系数 =====
                new() { Category = "MaterialPlanRatio", CategoryDisplay = "工单-用料计划比率", Context = "工单", ParamKey = "FixedFinishRatio", ParamValue = 1.02m, Remark = "定尺成品采购系数" },
                new() { Category = "MaterialPlanRatio", CategoryDisplay = "工单-用料计划比率", Context = "工单", ParamKey = "FixedInventoryRatio", ParamValue = 1.02m, Remark = "定尺库存使用系数" },
                new() { Category = "MaterialPlanRatio", CategoryDisplay = "工单-用料计划比率", Context = "工单", ParamKey = "NonFixedFinishRatio", ParamValue = 1.05m, Remark = "非定尺成品采购系数" },
                new() { Category = "MaterialPlanRatio", CategoryDisplay = "工单-用料计划比率", Context = "工单", ParamKey = "NonFixedInventoryRatio", ParamValue = 1.05m, Remark = "非定尺库存使用系数" },

                // ===== DimensionTolerance 尺寸公差系数 =====
                new() { Category = "DimensionTolerance", CategoryDisplay = "工单-尺寸公差", Context = "工单", ParamKey = "OdLower", ParamValue = 1.002m, Remark = "外径下限系数" },
                new() { Category = "DimensionTolerance", CategoryDisplay = "工单-尺寸公差", Context = "工单", ParamKey = "OdUpper", ParamValue = 0.998m, Remark = "外径上限系数" },
                new() { Category = "DimensionTolerance", CategoryDisplay = "工单-尺寸公差", Context = "工单", ParamKey = "WtLower", ParamValue = 1.02m, Remark = "壁厚下限系数" },
                new() { Category = "DimensionTolerance", CategoryDisplay = "工单-尺寸公差", Context = "工单", ParamKey = "WtUpper", ParamValue = 0.98m, Remark = "壁厚上限系数" },

                // ===== ReworkRatio 改制系数 =====
                new() { Category = "ReworkRatio", CategoryDisplay = "工单-改制系数", Context = "工单", ParamKey = "EmptyDrawingOdLower", ParamValue = 1.05m, Remark = "空拔外径下限" },
                new() { Category = "ReworkRatio", CategoryDisplay = "工单-改制系数", Context = "工单", ParamKey = "FewerPassOdLower", ParamValue = 1.1m, Remark = "少道次外径下限" },
                new() { Category = "ReworkRatio", CategoryDisplay = "工单-改制系数", Context = "工单", ParamKey = "OdUpper", ParamValue = 2.0m, Remark = "改制外径上限" },
                new() { Category = "ReworkRatio", CategoryDisplay = "工单-改制系数", Context = "工单", ParamKey = "EmptyDrawingWtLower", ParamValue = 0.95m, Remark = "空拔壁厚下限" },
                new() { Category = "ReworkRatio", CategoryDisplay = "工单-改制系数", Context = "工单", ParamKey = "FewerPassWtLower", ParamValue = 1.05m, Remark = "少道次壁厚下限" },
                new() { Category = "ReworkRatio", CategoryDisplay = "工单-改制系数", Context = "工单", ParamKey = "EmptyDrawingWtUpper", ParamValue = 1.05m, Remark = "空拔壁厚上限" },
                new() { Category = "ReworkRatio", CategoryDisplay = "工单-改制系数", Context = "工单", ParamKey = "FewerPassWtUpper", ParamValue = 2.0m, Remark = "少道次壁厚上限" },
                new() { Category = "ReworkRatio", CategoryDisplay = "工单-改制系数", Context = "工单", ParamKey = "MinUnitWeightRatio", ParamValue = 1.05m, Remark = "改制最小单重系数" },

                // ===== LengthDefault 长度默认值 =====
                new() { Category = "LengthDefault", CategoryDisplay = "工单-长度默认值", Context = "工单", ParamKey = "PipeLength", ParamValue = 6000m, Remark = "默认管长(mm)" },
                new() { Category = "LengthDefault", CategoryDisplay = "工单-长度默认值", Context = "工单", ParamKey = "UnitWeightLength", ParamValue = 4500m, Remark = "默认单重计算长度(mm)" },

                // ===== MaterialPlanStatus 物料计划状态阈值 =====
                new() { Category = "MaterialPlanStatus", CategoryDisplay = "工单-用料计划状态阈值", Context = "工单", ParamKey = "FixedPartial", ParamValue = 102m, Remark = "定尺部分阈值(%)" },
                new() { Category = "MaterialPlanStatus", CategoryDisplay = "工单-用料计划状态阈值", Context = "工单", ParamKey = "FixedSatisfied", ParamValue = 110m, Remark = "定尺满足阈值(%)" },
                new() { Category = "MaterialPlanStatus", CategoryDisplay = "工单-用料计划状态阈值", Context = "工单", ParamKey = "NonFixedPartial", ParamValue = 105m, Remark = "非定尺部分阈值(%)" },
                new() { Category = "MaterialPlanStatus", CategoryDisplay = "工单-用料计划状态阈值", Context = "工单", ParamKey = "NonFixedSatisfied", ParamValue = 120m, Remark = "非定尺满足阈值(%)" },
                new() { Category = "MaterialPlanStatus", CategoryDisplay = "工单-用料计划状态阈值", Context = "工单", ParamKey = "SmallBatchMaxQty", ParamValue = 20m, Remark = "小批量最大支数" },
                new() { Category = "MaterialPlanStatus", CategoryDisplay = "工单-用料计划状态阈值", Context = "工单", ParamKey = "SmallBatchSatisfiedRate", ParamValue = 100m, Remark = "小批量满足率(%)" },
                new() { Category = "MaterialPlanStatus", CategoryDisplay = "工单-用料计划状态阈值", Context = "工单", ParamKey = "SupplySatisfiedRate", ParamValue = 100m, Remark = "投料满足率(%)" },
                new() { Category = "MaterialPlanStatus", CategoryDisplay = "工单-用料计划状态阈值", Context = "工单", ParamKey = "QualifiedRate", ParamValue = 98m, Remark = "定尺其它投料类型理论成品合格率(%)" },

                // ===== ProcessingDiscount 加工折扣率 =====
                new() { Category = "ProcessingDiscount", CategoryDisplay = "批次-加工损耗率", Context = "批次", ParamKey = "GroupDiscountRate", ParamValue = 0.025m, Remark = "每工序组加工损耗率" },
                new() { Category = "ProcessingDiscount", CategoryDisplay = "批次-加工损耗率", Context = "批次", ParamKey = "RawMaterialRatio", ParamValue = 1.1m, Remark = "原料换算系数" },

                // ===== WorkOrderDays 工单天数 =====
                new() { Category = "WorkOrderDays", CategoryDisplay = "工单-交期排程天数", Context = "工单", ParamKey = "BufferDays", ParamValue = 3m, Remark = "缓冲天数" },
                new() { Category = "WorkOrderDays", CategoryDisplay = "工单-交期排程天数", Context = "工单", ParamKey = "InspectionFixedDays", ParamValue = 3m, Remark = "检验固定天数" },

                // ===== UrgencyThreshold 紧急程度阈值 =====
                new() { Category = "UrgencyThreshold", CategoryDisplay = "工单-紧急度阈值", Context = "工单", ParamKey = "APlus", ParamValue = 7m, Remark = "A+急阈值(天)" },
                new() { Category = "UrgencyThreshold", CategoryDisplay = "工单-紧急度阈值", Context = "工单", ParamKey = "A", ParamValue = -3m, Remark = "A急阈值(天)" },
                new() { Category = "UrgencyThreshold", CategoryDisplay = "工单-紧急度阈值", Context = "工单", ParamKey = "B", ParamValue = -10m, Remark = "B顺阈值(天)" },
                new() { Category = "UrgencyThreshold", CategoryDisplay = "工单-紧急度阈值", Context = "工单", ParamKey = "C", ParamValue = -17m, Remark = "C缓阈值(天)" },

                // ===== DateBucket 日期桶边界（2026-08-19 改为 +7/+15/+30/+45/+60 七桶） =====
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket1", ParamValue = 7m, Remark = "日期桶1(天)" },
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket2", ParamValue = 15m, Remark = "日期桶2(天)" },
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket3", ParamValue = 30m, Remark = "日期桶3(天)" },
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket4", ParamValue = 45m, Remark = "日期桶4(天)" },
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket5", ParamValue = 60m, Remark = "日期桶5(天)" },

                // ===== SequenceJump 序号跳跃 =====
                new() { Category = "SequenceJump", CategoryDisplay = "批次-工序跳号", Context = "批次", ParamKey = "MaxJump", ParamValue = 7m, Remark = "最大序号跳跃值" },

                // ===== ContractWeight 合同重量验证 =====
                new() { Category = "ContractWeight", CategoryDisplay = "订单-合同重量校验", Context = "订单", ParamKey = "LowerBound", ParamValue = 0.94m, Remark = "合同重量验证下限" },
                new() { Category = "ContractWeight", CategoryDisplay = "订单-合同重量校验", Context = "订单", ParamKey = "UpperBound", ParamValue = 1.06m, Remark = "合同重量验证上限" },

                // ===== NcrThreshold NCR 触发阈值 =====
                new() { Category = "NcrThreshold", CategoryDisplay = "质量-NCR触发阈值", Context = "质量", ParamKey = "ReworkCount", ParamValue = 5m, Remark = "返工触发绝对支数" },
                new() { Category = "NcrThreshold", CategoryDisplay = "质量-NCR触发阈值", Context = "质量", ParamKey = "ReworkPercent", ParamValue = 0.05m, Remark = "返工触发百分比" },
                new() { Category = "NcrThreshold", CategoryDisplay = "质量-NCR触发阈值", Context = "质量", ParamKey = "WarehouseCount", ParamValue = 5m, Remark = "让步接收触发绝对支数" },
                new() { Category = "NcrThreshold", CategoryDisplay = "质量-NCR触发阈值", Context = "质量", ParamKey = "WarehousePercent", ParamValue = 0.05m, Remark = "让步接收触发百分比" },
                new() { Category = "NcrThreshold", CategoryDisplay = "质量-NCR触发阈值", Context = "质量", ParamKey = "ScrapCount", ParamValue = 3m, Remark = "报废触发绝对支数" },
                new() { Category = "NcrThreshold", CategoryDisplay = "质量-NCR触发阈值", Context = "质量", ParamKey = "ScrapPercent", ParamValue = 0.05m, Remark = "报废触发百分比" },

                // ===== DefaultValue 默认值 =====
                new() { Category = "DefaultValue", CategoryDisplay = "工单-默认工艺周期", Context = "工单", ParamKey = "DefaultProcessCycle", ParamValue = 22m, Remark = "默认工艺周期(天)，主号/库料改制无工时默认使用" },
                new() { Category = "DefaultValue", CategoryDisplay = "工单-标准周期", Context = "工单", ParamKey = "StandardCycle", ParamValue = 3m, Remark = "默认标准周期(天)" },
                new() { Category = "DefaultValue", CategoryDisplay = "批次-最大序号", Context = "批次", ParamKey = "BatchMaxSequence", ParamValue = 9999m, Remark = "批次号最大序号" },

                // ===== MaterialPlanTolerance 用料计划执行容差 =====
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "ExternalLower", ParamValue = 0.97m, Remark = "对外计划下限(97%)，圆棒穿孔/荒管采购/成品采购" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "ExternalUpper", ParamValue = 1.03m, Remark = "对外计划上限(103%)，圆棒穿孔/荒管采购/成品采购" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "WarehouseLower", ParamValue = 0.95m, Remark = "对内-仓库下限(95%)，库存使用/库料改制" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "WarehouseUpper", ParamValue = 1.50m, Remark = "对内-仓库上限(150%)，库存使用/库料改制" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "ProductionLower", ParamValue = 0.90m, Remark = "对内-生产下限(90%)，在产改制/在产主工单" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "ProductionUpper", ParamValue = 1.50m, Remark = "对内-生产上限(150%)，在产改制/在产主工单" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "InputConsistencyTolerance", ParamValue = 0.03m, Remark = "到料实投一致性容差(±3%)与档5缺口率阈值" },
            };

            await context.ConfigParameters.AddRangeAsync(configParams);
            await context.SaveChangesAsync();
        }

        // ========== 11. Initialize Section Paragraph Config ==========
        // 段落日产配置：段落由 3 类配置自动生成（冷轧拔=机台组显示名 / 普通工段=StandardWorkDays / 检验=固定），
        // 服务层 GetSettingsAsync 每次同步期望段落集。此处仅空表时种子预置带初始参数的段落（无参数段落由同步补齐）。
        if (!context.SectionParagraphConfigs.Any())
        {
            var paragraphs = new List<SectionParagraphConfig>
            {
                // 冷轧拔：ParagraphKey=机台组 GroupKey、ParagraphName=机台组显示名
                new() { CategoryType = ParagraphCategoryTypes.Cold,    ParagraphKey = ColdRollMachineGroupKeys.Roll5060,    ParagraphName = ColdRollMachineGroupKeys.Roll5060Display,    DisplayOrder = 1,  DailyFlowTarget = 13m, LowerLimitDays = 3m,  UpperLimitDays = 6m },
                new() { CategoryType = ParagraphCategoryTypes.Cold,    ParagraphKey = ColdRollMachineGroupKeys.Roll2030,    ParagraphName = ColdRollMachineGroupKeys.Roll2030Display,    DisplayOrder = 2,  DailyFlowTarget = 12m, LowerLimitDays = 3m,  UpperLimitDays = 6m },
                new() { CategoryType = ParagraphCategoryTypes.Cold,    ParagraphKey = ColdRollMachineGroupKeys.ThreeRoll,   ParagraphName = ColdRollMachineGroupKeys.ThreeRollDisplay,   DisplayOrder = 3 },
                new() { CategoryType = ParagraphCategoryTypes.Cold,    ParagraphKey = ColdRollMachineGroupKeys.Draw,        ParagraphName = ColdRollMachineGroupKeys.DrawDisplay,        DisplayOrder = 4 },
                // 普通工段：ParagraphKey=SectionKey、ParagraphName=工段中文名（断切对应旧"切割"语义）
                new() { CategoryType = ParagraphCategoryTypes.Section, ParagraphKey = nameof(SectionDefs.Solution),        ParagraphName = SectionDefs.Solution,        DisplayOrder = 5,  DailyFlowTarget = 25m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
                new() { CategoryType = ParagraphCategoryTypes.Section, ParagraphKey = nameof(SectionDefs.Straighten),      ParagraphName = SectionDefs.Straighten,      DisplayOrder = 6,  DailyFlowTarget = 35m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
                new() { CategoryType = ParagraphCategoryTypes.Section, ParagraphKey = nameof(SectionDefs.Cut),            ParagraphName = SectionDefs.Cut,            DisplayOrder = 7,  DailyFlowTarget = 50m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
                new() { CategoryType = ParagraphCategoryTypes.Section, ParagraphKey = nameof(SectionDefs.Degrease),       ParagraphName = SectionDefs.Degrease,       DisplayOrder = 8,  DailyFlowTarget = 30m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
                new() { CategoryType = ParagraphCategoryTypes.Section, ParagraphKey = nameof(SectionDefs.Pickle),         ParagraphName = SectionDefs.Pickle,         DisplayOrder = 9,  DailyFlowTarget = 40m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
            };

            context.SectionParagraphConfigs.AddRange(paragraphs);
            await context.SaveChangesAsync();
        }

        // ========== 12. Initialize Daily Production Capacities ==========
        // 荒管抛光固定首行 + 冷轧机台组遍历（2026-08-30 起产能档案键=机台组 GroupKey，
        // 完全遍历机台组含 110；新增组无默认值时落 0，需在日产能配置页补录）
        if (!context.DailyProductionCapacities.Any())
        {
            var capacities = new List<DailyProductionCapacity>
            {
                new() { ProcessName = ProductionOverviewRowKeys.Polish, DailyCapacity = ProductionOverviewDefaults.Polish, Remark = "荒管抛光日产能(吨)" },
            };
            foreach (var g in context.ColdRollMachineGroupConfigs.OrderBy(x => x.DisplayOrder))
            {
                capacities.Add(new DailyProductionCapacity
                {
                    ProcessName = g.GroupKey,
                    DailyCapacity = ProductionOverviewDefaults.ForGroup.GetValueOrDefault(g.GroupKey, 0m),
                    Remark = $"{g.DisplayName}日产能(吨)",
                });
            }
            context.DailyProductionCapacities.AddRange(capacities);
            await context.SaveChangesAsync();
        }

        // ========== 13. Initialize Daily Output Estimates ==========
        if (!context.DailyOutputEstimates.Any())
        {
            var estimates = new List<DailyOutputEstimate>
            {
                new() { MinOuterDiameter = 38, DailyOutputTons = 3.5m, Remark = "外径>=38mm" },
                new() { MinOuterDiameter = 18, DailyOutputTons = 3.0m, Remark = "外径>=18mm" },
                new() { MinOuterDiameter = 14, DailyOutputTons = 1.0m, Remark = "外径>=14mm" },
                new() { MinOuterDiameter = 12, DailyOutputTons = 0.5m, Remark = "外径>=12mm" },
                new() { MinOuterDiameter = 6,  DailyOutputTons = 0.1m, Remark = "外径>=6mm" },
            };
            context.DailyOutputEstimates.AddRange(estimates);
            await context.SaveChangesAsync();
        }

        // ========== 13. Initialize Workstations ==========
        if (!context.Workstations.Any())
        {
            var workstations = new List<Workstation>
            {
                // 冷轧拔工段
                new() { Code = "CR01", Name = "1号冷轧机",  EquipmentName = "LG60冷轧机",   SectionName = SectionKeys.ColdRollDraw, IsActive = true },
                new() { Code = "CR02", Name = "2号冷轧机",  EquipmentName = "LG30冷轧机",   SectionName = SectionKeys.ColdRollDraw, IsActive = true },
                new() { Code = "CR03", Name = "3号冷轧机",  EquipmentName = "LG20冷轧机",   SectionName = SectionKeys.ColdRollDraw, IsActive = true },
                // 外抛光工段
                new() { Code = "PL01", Name = "1号抛光机",  EquipmentName = "外抛光机",     SectionName = SectionKeys.OuterPolish, IsActive = true },
                new() { Code = "PL02", Name = "2号抛光机",  EquipmentName = "外抛光机",     SectionName = SectionKeys.OuterPolish, IsActive = true },
                // 酸洗工段
                new() { Code = "PK01", Name = "酸洗槽1",    EquipmentName = "酸洗槽",       SectionName = SectionKeys.Pickle,  IsActive = true },
                // 内修磨工段
                new() { Code = "IG01", Name = "内磨机1",    EquipmentName = "内修磨机",     SectionName = SectionKeys.InnerGrinding, IsActive = true },
                // 固溶工段
                new() { Code = "SL01", Name = "固溶炉1",    EquipmentName = "固溶热处理炉", SectionName = SectionKeys.Solution,  IsActive = true },
                // 断切工段
                new() { Code = "CT01", Name = "切管机1",    EquipmentName = "自动切管机",   SectionName = SectionKeys.Cut,  IsActive = true },
                // 矫直工段
                new() { Code = "ST01", Name = "矫直机1",    EquipmentName = "矫直机",       SectionName = SectionKeys.Straighten,  IsActive = true },
                // 检验工段
                new() { Code = "IN01", Name = "检验台1",    EquipmentName = "检验设备",     SectionName = SectionKeys.Inspection,  IsActive = true },
                // 仓储工段
                new() { Code = "WH01", Name = "入库1",      EquipmentName = null,           SectionName = SectionKeys.Warehouse,  IsActive = true },
            };

            context.Workstations.AddRange(workstations);
            await context.SaveChangesAsync();
        }
    }
}