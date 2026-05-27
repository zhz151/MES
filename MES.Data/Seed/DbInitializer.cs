using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MES.Data;
using MES.Data.Entities;
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
    }
}