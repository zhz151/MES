using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Scheduling;
using MES.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;
using MES.Core.Enums;

namespace MES.Data.Seed;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // ===== 数据库迁移策略：兼容既有 EnsureCreated 和 Migrate 模式 =====
        // 检测 __EFMigrationsHistory 是否存在
        bool hasHistoryTable;
        try
        {
            _ = await context.Database.GetAppliedMigrationsAsync();
            hasHistoryTable = true;
        }
        catch
        {
            hasHistoryTable = false;
        }

        if (hasHistoryTable)
        {
            // 已有迁移历史，直接应用待处理迁移
            var pending = await context.Database.GetPendingMigrationsAsync();
            if (pending.Any())
                await context.Database.MigrateAsync();
        }
        else
        {
            // 数据库由 EnsureCreated 创建（无迁移历史）
            // 先创建 __EFMigrationsHistory 表并标记已存在的迁移
            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
                BEGIN
                    CREATE TABLE [__EFMigrationsHistory] (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                    );
                    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES ('20260521022534_AddOrderListSummary', '8.0.0');
                END
            ");
            // 然后应用新增迁移（AddWorkOrderReadModels）
            await context.Database.MigrateAsync();
        }

        // ========== 1. Initialize Roles ==========
        foreach (var role in Roles.GetAllRoles())
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ========== 2. Initialize Admin Account ==========
        var adminUser = await userManager.FindByEmailAsync("admin@mes.com");
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                UserName = "admin@mes.com",
                Email = "admin@mes.com",
                FullName = "System Administrator",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Roles.Admin);
            }
        }

        // ========== 3. Initialize Production Standards ==========
        if (!context.ProductionStandards.Any())
        {
            var productionStandards = new List<ProductionStandard>
            {
                new ProductionStandard
                {
                    StandardCode = "GB/T 14976",
                    StandardName = "Fluid transport stainless steel seamless steel pipe",
                    SortOrder = 1,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "ASTM A312",
                    StandardName = "Standard Specification for Seamless, Welded, and Heavily Cold Worked Austenitic Stainless Steel Pipes",
                    SortOrder = 2,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "GB/T 13296",
                    StandardName = "Boiler, heat exchanger stainless steel seamless steel pipe",
                    SortOrder = 3,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "ASTM A269",
                    StandardName = "Standard Specification for Seamless and Welded Austenitic Stainless Steel Tubing for General Service",
                    SortOrder = 4,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "EN 10216-5",
                    StandardName = "Seamless steel tubes for pressure purposes - Technical delivery conditions - Part 5: Stainless steel tubes",
                    SortOrder = 5,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "JIS G3459",
                    StandardName = "Stainless steel pipes",
                    SortOrder = 6,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "GB/T 12771",
                    StandardName = "Fluid transport stainless steel welded steel pipe",
                    SortOrder = 7,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "ASTM A213",
                    StandardName = "Standard Specification for Seamless Ferritic and Austenitic Alloy-Steel Boiler, Superheater, and Heat-Exchanger Tubes",
                    SortOrder = 8,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "ASTM A789",
                    StandardName = "Standard Specification for Seamless and Welded Ferritic/Austenitic Stainless Steel Tubing for General Service",
                    SortOrder = 9,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "DIN EN 10217-7",
                    StandardName = "Welded steel tubes for pressure purposes - Part 7: Stainless steel tubes",
                    SortOrder = 10,
                    IsActive = true
                },
                new ProductionStandard
                {
                    StandardCode = "ISO 9330-6",
                    StandardName = "Welded steel tubes for pressure purposes - Technical delivery conditions - Part 6: Stainless steel tubes",
                    SortOrder = 11,
                    IsActive = true
                }
            };

            await context.ProductionStandards.AddRangeAsync(productionStandards);
            await context.SaveChangesAsync();
        }

        // ========== 4. Initialize Grade Mappings ==========
        if (!context.StandardGradeMappings.Any())
        {
            var gradeMappings = new List<StandardGradeMapping>
            {
                // 304 Series
                new StandardGradeMapping
                {
                    StandardGrade = "304",
                    PlantGrade = "06Cr19Ni10",
                    Density = 7.93m,
                    HeatTreatment = "Solution treatment 1010-1150℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "Austenitic stainless steel"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "304L",
                    PlantGrade = "022Cr19Ni10",
                    Density = 7.93m,
                    HeatTreatment = "Solution treatment 1010-1150℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "Low carbon austenitic stainless steel"
                },
                // 316 Series
                new StandardGradeMapping
                {
                    StandardGrade = "316",
                    PlantGrade = "06Cr17Ni12Mo2",
                    Density = 7.98m,
                    HeatTreatment = "Solution treatment 1010-1150℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "Molybdenum-containing austenitic stainless steel"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "316L",
                    PlantGrade = "022Cr17Ni12Mo2",
                    Density = 7.98m,
                    HeatTreatment = "Solution treatment 1010-1150℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "Low carbon molybdenum-containing austenitic stainless steel"
                },
                // 321 Series
                new StandardGradeMapping
                {
                    StandardGrade = "321",
                    PlantGrade = "06Cr18Ni11Ti",
                    Density = 7.93m,
                    HeatTreatment = "Solution treatment 920-1150℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "Titanium-stabilized austenitic stainless steel, resistant to intergranular corrosion"
                },
                // 310S Series
                new StandardGradeMapping
                {
                    StandardGrade = "310S",
                    PlantGrade = "06Cr25Ni20",
                    Density = 7.98m,
                    HeatTreatment = "Solution treatment 1030-1180℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "High temperature resistant austenitic stainless steel"
                },
                // 201 Series
                new StandardGradeMapping
                {
                    StandardGrade = "201",
                    PlantGrade = "12Cr17Mn6Ni5N",
                    Density = 7.93m,
                    HeatTreatment = "Solution treatment 1010-1120℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "Nickel-saving austenitic stainless steel"
                },
                // 202 Series
                new StandardGradeMapping
                {
                    StandardGrade = "202",
                    PlantGrade = "12Cr18Mn9Ni5N",
                    Density = 7.93m,
                    HeatTreatment = "Solution treatment 1010-1120℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "Nickel-saving austenitic stainless steel"
                },
                // 309S Series
                new StandardGradeMapping
                {
                    StandardGrade = "309S",
                    PlantGrade = "06Cr23Ni13",
                    Density = 7.98m,
                    HeatTreatment = "Solution treatment 1030-1150℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "High temperature resistant austenitic stainless steel"
                },
                // 347 Series
                new StandardGradeMapping
                {
                    StandardGrade = "347",
                    PlantGrade = "06Cr18Ni11Nb",
                    Density = 7.93m,
                    HeatTreatment = "Solution treatment 980-1150℃, rapid cooling",
                    SpecialMaterial = false,
                    SteelProperty = "镍基合金",
                    Remark = "Niobium-stabilized austenitic stainless steel, resistant to intergranular corrosion"
                },
                // Special Materials
                new StandardGradeMapping
                {
                    StandardGrade = "904L",
                    PlantGrade = "015Cr21Ni26Mo5Cu2",
                    Density = 8.24m,
                    HeatTreatment = "Solution treatment 1090-1170℃, rapid cooling",
                    SpecialMaterial = true,
                    SpecialNote = "Super austenitic stainless steel, pay attention to pickling process",
                    SteelProperty = "镍基合金",
                    Remark = "Super austenitic stainless steel"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "S31803",
                    PlantGrade = "022Cr22Ni5Mo3N",
                    Density = 7.80m,
                    HeatTreatment = "Solution treatment 1020-1100℃, rapid cooling",
                    SpecialMaterial = true,
                    SpecialNote = "Duplex stainless steel, strictly control heat treatment temperature",
                    SteelProperty = "镍基合金",
                    Remark = "Duplex stainless steel"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "S32750",
                    PlantGrade = "022Cr25Ni7Mo4N",
                    Density = 7.80m,
                    HeatTreatment = "Solution treatment 1050-1120℃, rapid cooling",
                    SpecialMaterial = true,
                    SpecialNote = "Super duplex stainless steel, strictly control heat treatment process",
                    SteelProperty = "镍基合金",
                    Remark = "Super duplex stainless steel"
                }
            };

            await context.StandardGradeMappings.AddRangeAsync(gradeMappings);
            await context.SaveChangesAsync();
        }

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
                    EndCustomer = null,
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
                    EndCustomer = null,
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
                    EndCustomer = null,
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
                    EndCustomer = null,
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
                    EndCustomer = null,
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

        // ========== 7. Initialize Standard Process Cycles ==========
        if (!context.StandardProcessCycles.Any())
        {
            var cycles = new List<StandardProcessCycle>
            {
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "余库料", RawSpec = "10*1.5", ProductSpec = "8*1.5", DeliveryState = "固溶酸洗", StandardCycleDays = 7 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "90*12", ProductSpec = "60.3*8.74", DeliveryState = "固溶酸洗", StandardCycleDays = 19 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "90*11", ProductSpec = "60.3*8.74", DeliveryState = "固溶酸洗", StandardCycleDays = 19 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "荒管", RawSpec = "76*9", ProductSpec = "60.3*5.6", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "31000", RawMaterialType = "余库料", RawSpec = "71*8", ProductSpec = "60.3*5.6", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "90*9", ProductSpec = "60.3*5.54", DeliveryState = "固溶酸洗", StandardCycleDays = 19 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "90*8.5", ProductSpec = "60.3*5.54", DeliveryState = "固溶酸洗", StandardCycleDays = 19 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "78*8.5", ProductSpec = "60.3*5.54", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "76*9", ProductSpec = "60.3*5.54", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "76*8.5", ProductSpec = "60.3*5.54", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "余库料", RawSpec = "90*8", ProductSpec = "60.3*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "90*7.5", ProductSpec = "60.3*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 19 },
                new StandardProcessCycle { PlantGrade = "25073", RawMaterialType = "余库料", RawSpec = "89*6", ProductSpec = "60.3*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 14 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*7.5", ProductSpec = "60.3*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "荒管", RawSpec = "76*7.5", ProductSpec = "60.3*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "76*7", ProductSpec = "60.3*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "76*6.5", ProductSpec = "60.3*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "荒管", RawSpec = "76*5.5", ProductSpec = "60.3*2.9", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*7.5", ProductSpec = "60.3*2.77", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "71*16", ProductSpec = "60.3*12.5", DeliveryState = "固溶酸洗", StandardCycleDays = 15 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "90*14", ProductSpec = "60.3*11.07", DeliveryState = "固溶酸洗", StandardCycleDays = 19 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "76*6.5", ProductSpec = "60*4", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "余库料", RawSpec = "76*5.5", ProductSpec = "60*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*5.5", ProductSpec = "60*3", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "8*1.5", ProductSpec = "6*1.5", DeliveryState = "固溶酸洗", StandardCycleDays = 7 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "76*9", ProductSpec = "57*4", DeliveryState = "固溶酸洗", StandardCycleDays = 11 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "76*7", ProductSpec = "57*4", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*5.5", ProductSpec = "57*4", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*7", ProductSpec = "57*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*6.5", ProductSpec = "57*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*6", ProductSpec = "57*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "60*5", ProductSpec = "54*5", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*5.5", ProductSpec = "51*2.5", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "76*7.5", ProductSpec = "50*4", DeliveryState = "固溶酸洗-外抛光", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*7.5", ProductSpec = "50*4", DeliveryState = "固溶酸洗-外抛光", StandardCycleDays = 20 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "76*6.5", ProductSpec = "50*4", DeliveryState = "固溶酸洗-外抛光", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*5.5", ProductSpec = "50*2.5", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "76*8.5", ProductSpec = "48.3*5.08", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*8", ProductSpec = "48.3*5.08", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "荒管", RawSpec = "76*8", ProductSpec = "48.3*5.08", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*7.5", ProductSpec = "48.3*5.08", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "67*8.5", ProductSpec = "48.3*5.08", DeliveryState = "固溶酸洗", StandardCycleDays = 19 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*8.5", ProductSpec = "48.3*5.08", DeliveryState = "固溶酸洗", StandardCycleDays = 17 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*7.5", ProductSpec = "48.3*5.08", DeliveryState = "固溶酸洗", StandardCycleDays = 17 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "余库料", RawSpec = "67*7", ProductSpec = "48.3*5.08", DeliveryState = "固溶酸洗", StandardCycleDays = 15 },
                new StandardProcessCycle { PlantGrade = "31000", RawMaterialType = "余库料", RawSpec = "71*8", ProductSpec = "48.3*5", DeliveryState = "固溶酸洗", StandardCycleDays = 12 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*8", ProductSpec = "48.3*5", DeliveryState = "固溶酸洗", StandardCycleDays = 17 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*5.5", ProductSpec = "48.3*3.68", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "48.3*3.68", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "48.3*3.68", DeliveryState = "固溶酸洗", StandardCycleDays = 17 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "48.3*3.68", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*5.2", ProductSpec = "48.3*3.68", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "48.3*3.68", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "荒管", RawSpec = "76*5.5", ProductSpec = "48.3*2.77", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "90*14", ProductSpec = "48.3*10.16", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "76*5.5", ProductSpec = "48*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "57*3.5", ProductSpec = "45*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "316H0", RawMaterialType = "余库料", RawSpec = "50.8*8", ProductSpec = "42*8", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "45*3", ProductSpec = "42*3", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "67*5", ProductSpec = "38*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 13 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "38*3", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "34*4.5", DeliveryState = "固溶酸洗", StandardCycleDays = 20 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "34*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "34*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "荒管", RawSpec = "67*7.5", ProductSpec = "33.7*4.5", DeliveryState = "固溶酸洗", StandardCycleDays = 17 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "34*3.1", ProductSpec = "33.7*2.6", DeliveryState = "固溶酸洗", StandardCycleDays = 7 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "90*12", ProductSpec = "33.4*9.09", DeliveryState = "固溶酸洗", StandardCycleDays = 30 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "78*13", ProductSpec = "33.4*9.09", DeliveryState = "固溶酸洗", StandardCycleDays = 20 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "67*8.5", ProductSpec = "33.4*6.35", DeliveryState = "固溶酸洗", StandardCycleDays = 23 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "67*7.5", ProductSpec = "33.4*4.55", DeliveryState = "固溶酸洗", StandardCycleDays = 20 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*7.5", ProductSpec = "33.4*4.55", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "67*7", ProductSpec = "33.4*4.55", DeliveryState = "固溶酸洗", StandardCycleDays = 19 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*7", ProductSpec = "33.4*4.55", DeliveryState = "固溶酸洗", StandardCycleDays = 17 },
                new StandardProcessCycle { PlantGrade = "25073", RawMaterialType = "余库料", RawSpec = "48.3*5.08", ProductSpec = "33.4*4.55", DeliveryState = "固溶酸洗", StandardCycleDays = 13 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*7", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 17 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "31600", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "余库料", RawSpec = "67*6", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 13 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 17 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "余库料", RawSpec = "67*6", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 13 },
                new StandardProcessCycle { PlantGrade = "31600", RawMaterialType = "余库料", RawSpec = "67*5.5", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 13 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "余库料", RawSpec = "67*5", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 13 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "余库料", RawSpec = "42*3.4", ProductSpec = "33.4*3.38", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "余库料", RawSpec = "35*2.5", ProductSpec = "33.4*2.77", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "35*2.5", ProductSpec = "33.4*2.5", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "33.4*5.3", ProductSpec = "32*5.5", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "32*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 16 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "67*5", ProductSpec = "32*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 14 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5.2", ProductSpec = "32*2.5", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "32*2.5", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "316H0", RawMaterialType = "余库料", RawSpec = "67*6.5", ProductSpec = "28*4", DeliveryState = "固溶酸洗", StandardCycleDays = 19 },
                new StandardProcessCycle { PlantGrade = "31600", RawMaterialType = "余库料", RawSpec = "32*3.5", ProductSpec = "27*3.5", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "27*3", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "28*2", ProductSpec = "26.9*2", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "78*11", ProductSpec = "26.7*7.82", DeliveryState = "固溶酸洗", StandardCycleDays = 23 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "76*13", ProductSpec = "26.7*7.82", DeliveryState = "固溶酸洗", StandardCycleDays = 23 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "67*7.5", ProductSpec = "26.7*5.56", DeliveryState = "固溶酸洗", StandardCycleDays = 24 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*8", ProductSpec = "26.7*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "26.7*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 24 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "26.7*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "26.7*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "余库料", RawSpec = "67*6", ProductSpec = "26.7*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "26.7*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "余库料", RawSpec = "67*5.5", ProductSpec = "26.7*3.91", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "26.7*2.87", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "26.7*2.87", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*5.2", ProductSpec = "26.7*2.87", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "余库料", RawSpec = "48*3.6", ProductSpec = "26.7*2.87", DeliveryState = "固溶酸洗", StandardCycleDays = 17 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "28*2", ProductSpec = "26.7*2.11", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "25*3", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "余库料", RawSpec = "27*3", ProductSpec = "25*3", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "余库料", RawSpec = "26.7*2.87", ProductSpec = "25*3", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5.5", ProductSpec = "25*2.5", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "荒管", RawSpec = "67*5.5", ProductSpec = "25*2.5", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*5.2", ProductSpec = "25*2", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "荒管", RawSpec = "67*5.2", ProductSpec = "25*2", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5.2", ProductSpec = "25*2", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "荒管", RawSpec = "67*5.2", ProductSpec = "25*2", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "25*2", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "余库料", RawSpec = "67*5", ProductSpec = "25*2", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "28*2", ProductSpec = "24*2", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "余库料", RawSpec = "25*3", ProductSpec = "22*3", DeliveryState = "固溶酸洗", StandardCycleDays = 7 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "90*12", ProductSpec = "21.3*7.47", DeliveryState = "固溶酸洗", StandardCycleDays = 26 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "78*11", ProductSpec = "21.3*7.47", DeliveryState = "固溶酸洗", StandardCycleDays = 23 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "76*13", ProductSpec = "21.3*7.47", DeliveryState = "固溶酸洗", StandardCycleDays = 23 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*7.5", ProductSpec = "21.3*4.78", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "67*7", ProductSpec = "21.3*4.78", DeliveryState = "固溶酸洗", StandardCycleDays = 24 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "荒管", RawSpec = "67*7", ProductSpec = "21.3*3.73", DeliveryState = "固溶酸洗", StandardCycleDays = 24 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "21.3*3.73", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*6.5", ProductSpec = "21.3*3.73", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "304L0", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "21.3*3.73", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "22051", RawMaterialType = "余库料", RawSpec = "67*5.5", ProductSpec = "21.3*3.73", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "25073", RawMaterialType = "余库料", RawSpec = "26.7*3.5", ProductSpec = "21.3*3.73", DeliveryState = "固溶酸洗", StandardCycleDays = 11 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "25*3.5", ProductSpec = "21.3*3.73", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "67*4.8", ProductSpec = "21.3*2.9", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*6", ProductSpec = "21.3*2.77", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*5.5", ProductSpec = "21.3*2.77", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "67*5", ProductSpec = "21.3*2.77", DeliveryState = "固溶酸洗", StandardCycleDays = 18 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "21.3*2.77", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "3160T", RawMaterialType = "余库料", RawSpec = "25*3", ProductSpec = "21.3*2.77", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "31600", RawMaterialType = "余库料", RawSpec = "22*3", ProductSpec = "21*3", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "20*2", DeliveryState = "固溶酸洗-外抛光", StandardCycleDays = 26 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "余库料", RawSpec = "22*2.5", ProductSpec = "19*2.5", DeliveryState = "固溶酸洗", StandardCycleDays = 7 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5.5", ProductSpec = "19*2", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*5.2", ProductSpec = "19*2", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "19*2", DeliveryState = "光亮", StandardCycleDays = 26 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "19*2", DeliveryState = "固溶酸洗", StandardCycleDays = 22 },
                new StandardProcessCycle { PlantGrade = "316L0", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "19*2", DeliveryState = "固溶酸洗-U型管", StandardCycleDays = 26 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "荒管", RawSpec = "67*5", ProductSpec = "19*2", DeliveryState = "固溶酸洗", StandardCycleDays = 21 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "余库料", RawSpec = "38*3", ProductSpec = "19*2", DeliveryState = "固溶酸洗", StandardCycleDays = 12 },
                new StandardProcessCycle { PlantGrade = "22052", RawMaterialType = "余库料", RawSpec = "38*2.7", ProductSpec = "19*2", DeliveryState = "固溶酸洗", StandardCycleDays = 15 },
                new StandardProcessCycle { PlantGrade = "22052", RawMaterialType = "余库料", RawSpec = "32*2", ProductSpec = "19*2", DeliveryState = "固溶酸洗", StandardCycleDays = 11 },
                new StandardProcessCycle { PlantGrade = "22052", RawMaterialType = "余库料", RawSpec = "25*2", ProductSpec = "19*2", DeliveryState = "固溶酸洗", StandardCycleDays = 11 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "38*2.7", ProductSpec = "18*1.5", DeliveryState = "固溶酸洗-外抛光", StandardCycleDays = 15 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "22*2", ProductSpec = "18*1.5", DeliveryState = "固溶酸洗", StandardCycleDays = 12 },
                new StandardProcessCycle { PlantGrade = "316H0", RawMaterialType = "余库料", RawSpec = "21.3*2.9", ProductSpec = "16*3", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "20*0.95", ProductSpec = "15*1", DeliveryState = "固溶酸洗", StandardCycleDays = 7 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "18*1.9", ProductSpec = "15*1", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "32100", RawMaterialType = "余库料", RawSpec = "18*2.5", ProductSpec = "14*2.5", DeliveryState = "固溶酸洗", StandardCycleDays = 7 },
                new StandardProcessCycle { PlantGrade = "31600", RawMaterialType = "余库料", RawSpec = "14*3", ProductSpec = "12*3", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "14*2", ProductSpec = "10*2", DeliveryState = "固溶酸洗", StandardCycleDays = 7 },
                new StandardProcessCycle { PlantGrade = "30400", RawMaterialType = "余库料", RawSpec = "12*2", ProductSpec = "10*2", DeliveryState = "固溶酸洗", StandardCycleDays = 10 },
            };

            await context.StandardProcessCycles.AddRangeAsync(cycles);
            await context.SaveChangesAsync();
        }

        // ========== 8. Initialize Standard Work Days ==========
        if (!context.StandardWorkDays.Any())
        {
            var workDays = new List<StandardWorkDay>
            {
                // 通用配置（PlantGradePrefix = null）
                new() { SectionName = "冷轧拔", PlantGradePrefix = null, StandardDays = 2,   Remark = "冷轧/冷拔" },
                new() { SectionName = "油管断", PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = "去油",   PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = "固溶",   PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = "矫直",   PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = "断切",   PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = "测壁厚", PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = "酸洗",   PlantGradePrefix = null, StandardDays = 2,   Remark = "非3系牌号" },
                new() { SectionName = "外抛光", PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = "内修磨", PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = "外点磨", PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = "检验",   PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = "打焊头", PlantGradePrefix = null, StandardDays = 0.5, Remark = null },
                new() { SectionName = "润滑",   PlantGradePrefix = null, StandardDays = 1,   Remark = null },
                new() { SectionName = "入库",   PlantGradePrefix = null, StandardDays = 2,   Remark = null },
                // 牌号前缀覆盖：3系牌号酸洗只需1天
                new() { SectionName = "酸洗",   PlantGradePrefix = "3",  StandardDays = 1,   Remark = "3系牌号（304/316/321等）" },
            };

            await context.StandardWorkDays.AddRangeAsync(workDays);
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
                new() { Category = "WarehouseThreshold", ParamKey = "CompleteRatio", ParamValue = 0.95m, Remark = "入库完工比率阈值" },
                new() { Category = "WarehouseThreshold", ParamKey = "CompleteDeviation", ParamValue = 100m, Remark = "入库完工绝对偏差(kg)" },
                new() { Category = "WarehouseThreshold", ParamKey = "SubcontractCompleteRatio", ParamValue = 0.95m, Remark = "委外完工比率阈值" },
                new() { Category = "WarehouseThreshold", ParamKey = "PurchaseCompleteRatio", ParamValue = 0.965m, Remark = "采购完工比率阈值" },
                new() { Category = "WarehouseThreshold", ParamKey = "PurchaseCompleteDeviation", ParamValue = 200m, Remark = "采购完工绝对偏差(kg)" },
                new() { Category = "WarehouseThreshold", ParamKey = "OutsourceRecoveryRatio", ParamValue = 0.99m, Remark = "委外回收比率阈值" },

                // ===== ProductionThreshold 生产阈值 =====
                new() { Category = "ProductionThreshold", ParamKey = "ColdRollCompleteRatio", ParamValue = 0.95m, Remark = "冷轧拔完工比率" },
                new() { Category = "ProductionThreshold", ParamKey = "ValidInputUpper", ParamValue = 1.03m, Remark = "有效投料比率上限" },
                new() { Category = "ProductionThreshold", ParamKey = "ValidInputLower", ParamValue = 0.97m, Remark = "有效投料比率下限" },

                // ===== MaterialPlanRatio 物料计划系数 =====
                new() { Category = "MaterialPlanRatio", ParamKey = "FixedFinishRatio", ParamValue = 1.02m, Remark = "定尺成品采购系数" },
                new() { Category = "MaterialPlanRatio", ParamKey = "FixedInventoryRatio", ParamValue = 1.02m, Remark = "定尺库存使用系数" },
                new() { Category = "MaterialPlanRatio", ParamKey = "NonFixedFinishRatio", ParamValue = 1.05m, Remark = "非定尺成品采购系数" },
                new() { Category = "MaterialPlanRatio", ParamKey = "NonFixedInventoryRatio", ParamValue = 1.05m, Remark = "非定尺库存使用系数" },

                // ===== DimensionTolerance 尺寸公差系数 =====
                new() { Category = "DimensionTolerance", ParamKey = "OdLower", ParamValue = 1.002m, Remark = "外径下限系数" },
                new() { Category = "DimensionTolerance", ParamKey = "OdUpper", ParamValue = 0.998m, Remark = "外径上限系数" },
                new() { Category = "DimensionTolerance", ParamKey = "WtLower", ParamValue = 1.02m, Remark = "壁厚下限系数" },
                new() { Category = "DimensionTolerance", ParamKey = "WtUpper", ParamValue = 0.98m, Remark = "壁厚上限系数" },

                // ===== ReworkRatio 改制系数 =====
                new() { Category = "ReworkRatio", ParamKey = "EmptyDrawingOdLower", ParamValue = 1.05m, Remark = "空拔外径下限" },
                new() { Category = "ReworkRatio", ParamKey = "FewerPassOdLower", ParamValue = 1.1m, Remark = "少道次外径下限" },
                new() { Category = "ReworkRatio", ParamKey = "OdUpper", ParamValue = 2.0m, Remark = "改制外径上限" },
                new() { Category = "ReworkRatio", ParamKey = "EmptyDrawingWtLower", ParamValue = 0.95m, Remark = "空拔壁厚下限" },
                new() { Category = "ReworkRatio", ParamKey = "FewerPassWtLower", ParamValue = 1.05m, Remark = "少道次壁厚下限" },
                new() { Category = "ReworkRatio", ParamKey = "EmptyDrawingWtUpper", ParamValue = 1.05m, Remark = "空拔壁厚上限" },
                new() { Category = "ReworkRatio", ParamKey = "FewerPassWtUpper", ParamValue = 2.0m, Remark = "少道次壁厚上限" },
                new() { Category = "ReworkRatio", ParamKey = "MinUnitWeightRatio", ParamValue = 1.05m, Remark = "改制最小单重系数" },

                // ===== LengthDefault 长度默认值 =====
                new() { Category = "LengthDefault", ParamKey = "PipeLength", ParamValue = 6000m, Remark = "默认管长(mm)" },
                new() { Category = "LengthDefault", ParamKey = "UnitWeightLength", ParamValue = 4500m, Remark = "默认单重计算长度(mm)" },

                // ===== MaterialPlanStatus 物料计划状态阈值 =====
                new() { Category = "MaterialPlanStatus", ParamKey = "FixedPartial", ParamValue = 102m, Remark = "定尺部分阈值(%)" },
                new() { Category = "MaterialPlanStatus", ParamKey = "FixedSatisfied", ParamValue = 110m, Remark = "定尺满足阈值(%)" },
                new() { Category = "MaterialPlanStatus", ParamKey = "NonFixedPartial", ParamValue = 105m, Remark = "非定尺部分阈值(%)" },
                new() { Category = "MaterialPlanStatus", ParamKey = "NonFixedSatisfied", ParamValue = 120m, Remark = "非定尺满足阈值(%)" },
                new() { Category = "MaterialPlanStatus", ParamKey = "SmallBatchMaxQty", ParamValue = 20m, Remark = "小批量最大支数" },
                new() { Category = "MaterialPlanStatus", ParamKey = "SmallBatchSatisfiedRate", ParamValue = 100m, Remark = "小批量满足率(%)" },
                new() { Category = "MaterialPlanStatus", ParamKey = "SupplySatisfiedRate", ParamValue = 100m, Remark = "投料满足率(%)" },

                // ===== ProcessingDiscount 加工折扣率 =====
                new() { Category = "ProcessingDiscount", ParamKey = "GroupDiscountRate", ParamValue = 0.025m, Remark = "每工序组损耗折扣率" },
                new() { Category = "ProcessingDiscount", ParamKey = "RawMaterialRatio", ParamValue = 1.1m, Remark = "原料换算系数" },

                // ===== WorkOrderDays 工单天数 =====
                new() { Category = "WorkOrderDays", ParamKey = "BufferDays", ParamValue = 3m, Remark = "缓冲天数" },
                new() { Category = "WorkOrderDays", ParamKey = "InspectionFixedDays", ParamValue = 3m, Remark = "检验固定天数" },

                // ===== UrgencyThreshold 紧急程度阈值 =====
                new() { Category = "UrgencyThreshold", ParamKey = "APlus", ParamValue = 7m, Remark = "A+急阈值(天)" },
                new() { Category = "UrgencyThreshold", ParamKey = "A", ParamValue = -3m, Remark = "A急阈值(天)" },
                new() { Category = "UrgencyThreshold", ParamKey = "B", ParamValue = -10m, Remark = "B顺阈值(天)" },
                new() { Category = "UrgencyThreshold", ParamKey = "C", ParamValue = -17m, Remark = "C缓阈值(天)" },

                // ===== DateBucket 日期桶边界 =====
                new() { Category = "DateBucket", ParamKey = "Bucket1", ParamValue = 15m, Remark = "日期桶1(天)" },
                new() { Category = "DateBucket", ParamKey = "Bucket2", ParamValue = 30m, Remark = "日期桶2(天)" },
                new() { Category = "DateBucket", ParamKey = "Bucket3", ParamValue = 45m, Remark = "日期桶3(天)" },
                new() { Category = "DateBucket", ParamKey = "Bucket4", ParamValue = 60m, Remark = "日期桶4(天)" },
                new() { Category = "DateBucket", ParamKey = "Bucket5", ParamValue = 90m, Remark = "日期桶5(天)" },

                // ===== ProductionCapacity 产能负荷 =====
                new() { Category = "ProductionCapacity", ParamKey = "Polish", ParamValue = 12m, Remark = "荒管抛光日产能(吨)" },
                new() { Category = "ProductionCapacity", ParamKey = "Mill50_60", ParamValue = 11m, Remark = "50/60轧机日产能(吨)" },
                new() { Category = "ProductionCapacity", ParamKey = "Mill20_30", ParamValue = 9m, Remark = "20/30轧机日产能(吨)" },
                new() { Category = "ProductionCapacity", ParamKey = "ThreeRoll", ParamValue = 0.5m, Remark = "三辊轧机日产能(吨)" },
                new() { Category = "ProductionCapacity", ParamKey = "DrawBench", ParamValue = 3m, Remark = "拉机日产能(吨)" },

                // ===== SequenceJump 序号跳跃 =====
                new() { Category = "SequenceJump", ParamKey = "MaxJump", ParamValue = 7m, Remark = "最大序号跳跃值" },

                // ===== ContractWeight 合同重量验证 =====
                new() { Category = "ContractWeight", ParamKey = "LowerBound", ParamValue = 0.94m, Remark = "合同重量验证下限" },
                new() { Category = "ContractWeight", ParamKey = "UpperBound", ParamValue = 1.06m, Remark = "合同重量验证上限" },

                // ===== DefaultValue 默认值 =====
                new() { Category = "DefaultValue", ParamKey = "ProcessCycle", ParamValue = 25m, Remark = "默认工序周期(天)" },
                new() { Category = "DefaultValue", ParamKey = "StandardCycle", ParamValue = 3m, Remark = "默认标准周期(天)" },
                new() { Category = "DefaultValue", ParamKey = "BatchMaxSequence", ParamValue = 9999m, Remark = "批次号最大序号" },
                new() { Category = "DefaultValue", ParamKey = "RoughTubeFinishRatio", ParamValue = 0.92m, Remark = "荒管转成品系数" },
            };

            await context.ConfigParameters.AddRangeAsync(configParams);
            await context.SaveChangesAsync();
        }

        // ========== 11. Initialize Section Flow Analysis Category Settings ==========
        if (!context.SectionFlowCategorySettings.Any())
        {
            var settings = new List<SectionFlowCategorySetting>
            {
                new() { CategoryCode = "A", CategoryName = "外抛光" },
                new() { CategoryCode = "B", CategoryName = "内修磨" },
                new() { CategoryCode = "C", CategoryName = "外点磨" },
                new() { CategoryCode = "D", CategoryName = "荒管检" },
                new() { CategoryCode = "E", CategoryName = "在制检" },
                new() { CategoryCode = "F", CategoryName = "固溶" },
                new() { CategoryCode = "G", CategoryName = "矫直" },
                new() { CategoryCode = "H", CategoryName = "切割" },
                new() { CategoryCode = "I", CategoryName = "去油" },
                new() { CategoryCode = "J", CategoryName = "酸洗" },
                new() { CategoryCode = "K", CategoryName = "大轧" },
                new() { CategoryCode = "L", CategoryName = "小轧" },
                new() { CategoryCode = "M", CategoryName = "冷拔" },
                new() { CategoryCode = "N", CategoryName = "成品待检" },
            };

            context.SectionFlowCategorySettings.AddRange(settings);
            await context.SaveChangesAsync();

            var settingMap = await context.SectionFlowCategorySettings
                .OrderBy(s => s.CategoryCode)
                .ToDictionaryAsync(s => s.CategoryCode);

            var items = new List<SectionFlowCategoryItem>();

            void AddItem(SectionFlowCategorySetting s, string pg, string sn, decimal coeff, int order)
            {
                items.Add(new SectionFlowCategoryItem
                {
                    SettingId = s.Id,
                    ProcessGroupName = pg,
                    SectionName = sn,
                    Coefficient = coeff,
                    DisplayOrder = order,
                });
            }

            // A 外抛光
            AddItem(settingMap["A"], "荒管处理", "外抛光", 1m, 1);

            // B 内修磨
            AddItem(settingMap["B"], "荒管处理", "内修磨", 1m, 1);

            // C 外点磨
            AddItem(settingMap["C"], "荒管处理", "外点磨", 1m, 1);

            // D 荒管检
            AddItem(settingMap["D"], "荒管处理", "检验", 1m, 1);

            // E 在制检：全部工序组工段=检验的汇总量，后处理减去 D+N
            AddItem(settingMap["E"], "全部", "检验", 1m, 1);

            // F 固溶
            AddItem(settingMap["F"], "20冷轧",   "固溶", 1m, 1);
            AddItem(settingMap["F"], "30冷轧",   "固溶", 1m, 2);
            AddItem(settingMap["F"], "50冷轧",   "固溶", 1m, 3);
            AddItem(settingMap["F"], "60冷轧",   "固溶", 1m, 4);
            AddItem(settingMap["F"], "冷拔",     "固溶", 1m, 5);
            AddItem(settingMap["F"], "三辊冷轧", "固溶", 1m, 6);
            AddItem(settingMap["F"], "在制修检", "固溶", 1m, 7);

            // G 矫直
            AddItem(settingMap["G"], "20冷轧",   "矫直", 1m,    1);
            AddItem(settingMap["G"], "30冷轧",   "矫直", 1m,    2);
            AddItem(settingMap["G"], "50冷轧",   "矫直", 0.5m,  3);
            AddItem(settingMap["G"], "60冷轧",   "矫直", 0.5m,  4);
            AddItem(settingMap["G"], "荒管处理", "矫直", 0.25m, 5);
            AddItem(settingMap["G"], "冷拔",     "矫直", 1m,    6);
            AddItem(settingMap["G"], "三辊冷轧", "矫直", 1m,    7);
            AddItem(settingMap["G"], "在制修检", "矫直", 1m,    8);

            // H 切割
            AddItem(settingMap["H"], "20冷轧",   "断切",   1m,    1);
            AddItem(settingMap["H"], "30冷轧",   "断切",   1m,    2);
            AddItem(settingMap["H"], "50冷轧",   "断切",   0.5m,  3);
            AddItem(settingMap["H"], "60冷轧",   "断切",   0.5m,  4);
            AddItem(settingMap["H"], "荒管处理", "断切",   0.25m, 5);
            AddItem(settingMap["H"], "冷拔",     "断切",   1m,    6);
            AddItem(settingMap["H"], "三辊冷轧", "断切",   1m,    7);
            AddItem(settingMap["H"], "在制修检", "断切",   0.25m, 8);
            AddItem(settingMap["H"], "20冷轧",   "油管断", 0.75m, 9);
            AddItem(settingMap["H"], "30冷轧",   "油管断", 0.75m, 10);
            AddItem(settingMap["H"], "50冷轧",   "油管断", 0.5m,  11);
            AddItem(settingMap["H"], "60冷轧",   "油管断", 0.5m,  12);
            AddItem(settingMap["H"], "三辊冷轧", "油管断", 0.75m, 13);

            // I 去油
            AddItem(settingMap["I"], "20冷轧",   "去油", 1m,   1);
            AddItem(settingMap["I"], "30冷轧",   "去油", 1m,   2);
            AddItem(settingMap["I"], "50冷轧",   "去油", 0.5m, 3);
            AddItem(settingMap["I"], "60冷轧",   "去油", 0.5m, 4);
            AddItem(settingMap["I"], "三辊冷轧", "去油", 1m,   5);

            // J 酸洗
            AddItem(settingMap["J"], "20冷轧",   "酸洗", 1m,    1);
            AddItem(settingMap["J"], "30冷轧",   "酸洗", 1m,    2);
            AddItem(settingMap["J"], "50冷轧",   "酸洗", 0.5m,  3);
            AddItem(settingMap["J"], "60冷轧",   "酸洗", 0.5m,  4);
            AddItem(settingMap["J"], "荒管处理", "酸洗", 0.25m, 5);
            AddItem(settingMap["J"], "冷拔",     "酸洗", 1m,    6);
            AddItem(settingMap["J"], "三辊冷轧", "酸洗", 1m,    7);
            AddItem(settingMap["J"], "在制修检", "酸洗", 0.25m, 8);

            // K 大轧
            AddItem(settingMap["K"], "50冷轧", "冷轧拔", 1m, 1);
            AddItem(settingMap["K"], "60冷轧", "冷轧拔", 1m, 2);

            // L 小轧
            AddItem(settingMap["L"], "20冷轧",   "冷轧拔", 1m, 1);
            AddItem(settingMap["L"], "30冷轧",   "冷轧拔", 1m, 2);
            AddItem(settingMap["L"], "三辊冷轧", "冷轧拔", 1m, 3);

            // M 冷拔
            AddItem(settingMap["M"], "冷拔", "冷轧拔", 1m, 1);

            // N 成品待检：所有工序组中工段=检验的属成品工序量（FinalProcessTotal）汇总
            AddItem(settingMap["N"], "全部", "检验", 1m, 1);

            await context.SectionFlowCategoryItems.AddRangeAsync(items);
            await context.SaveChangesAsync();
        }

        // ========== 12. Initialize Daily Output Estimates ==========
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
                new() { Code = "CR01", Name = "1号冷轧机",  EquipmentName = "LG60冷轧机",   SectionName = "冷轧拔", IsActive = true },
                new() { Code = "CR02", Name = "2号冷轧机",  EquipmentName = "LG30冷轧机",   SectionName = "冷轧拔", IsActive = true },
                new() { Code = "CR03", Name = "3号冷轧机",  EquipmentName = "LG20冷轧机",   SectionName = "冷轧拔", IsActive = true },
                // 外抛光工段
                new() { Code = "PL01", Name = "1号抛光机",  EquipmentName = "外抛光机",     SectionName = "外抛光", IsActive = true },
                new() { Code = "PL02", Name = "2号抛光机",  EquipmentName = "外抛光机",     SectionName = "外抛光", IsActive = true },
                // 酸洗工段
                new() { Code = "PK01", Name = "酸洗槽1",    EquipmentName = "酸洗槽",       SectionName = "酸洗",   IsActive = true },
                // 内修磨工段
                new() { Code = "IG01", Name = "内磨机1",    EquipmentName = "内修磨机",     SectionName = "内修磨", IsActive = true },
                // 固溶工段
                new() { Code = "SL01", Name = "固溶炉1",    EquipmentName = "固溶热处理炉", SectionName = "固溶",   IsActive = true },
                // 断切工段
                new() { Code = "CT01", Name = "切管机1",    EquipmentName = "自动切管机",   SectionName = "断切",   IsActive = true },
                // 矫直工段
                new() { Code = "ST01", Name = "矫直机1",    EquipmentName = "矫直机",       SectionName = "矫直",   IsActive = true },
                // 检验工段
                new() { Code = "IN01", Name = "检验台1",    EquipmentName = "检验设备",     SectionName = "检验",   IsActive = true },
                // 仓储工段
                new() { Code = "WH01", Name = "入库1",      EquipmentName = null,           SectionName = "入库",   IsActive = true },
            };

            context.Workstations.AddRange(workstations);
            await context.SaveChangesAsync();
        }
    }
}