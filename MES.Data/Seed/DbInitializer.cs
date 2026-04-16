using Microsoft.AspNetCore.Identity;
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

        await context.Database.EnsureCreatedAsync();

        // ========== 1. 初始化角色 ==========
        foreach (var role in Roles.GetAllRoles())
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ========== 2. 初始化管理员账号 ==========
        var adminUser = await userManager.FindByEmailAsync("admin@mes.com");
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                UserName = "admin@mes.com",
                Email = "admin@mes.com",
                FullName = "系统管理员",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Roles.Admin);
            }
        }

        // ========== 3. 初始化产品标准 ==========
        if (!context.ProductionStandards.Any())
        {
            var productionStandards = new List<ProductionStandard>
            {
                new ProductionStandard
                {
                    StandardCode = "GB/T 14976",
                    StandardName = "流体输送用不锈钢无缝钢管",
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
                    StandardName = "锅炉、热交换器用不锈钢无缝钢管",
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
                    StandardName = "流体输送用不锈钢焊接钢管",
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

        // ========== 4. 初始化牌号对照 ==========
        if (!context.StandardGradeMappings.Any())
        {
            var gradeMappings = new List<StandardGradeMapping>
            {
                // 304 系列
                new StandardGradeMapping
                {
                    StandardGrade = "304",
                    PlantGrade = "06Cr19Ni10",
                    Density = 7.93m,
                    HeatTreatment = "固溶处理 1010-1150℃，快冷",
                    SpecialMaterial = false,
                    Remark = "奥氏体不锈钢"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "304L",
                    PlantGrade = "022Cr19Ni10",
                    Density = 7.93m,
                    HeatTreatment = "固溶处理 1010-1150℃，快冷",
                    SpecialMaterial = false,
                    Remark = "低碳奥氏体不锈钢"
                },
                // 316 系列
                new StandardGradeMapping
                {
                    StandardGrade = "316",
                    PlantGrade = "06Cr17Ni12Mo2",
                    Density = 7.98m,
                    HeatTreatment = "固溶处理 1010-1150℃，快冷",
                    SpecialMaterial = false,
                    Remark = "含钼奥氏体不锈钢"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "316L",
                    PlantGrade = "022Cr17Ni12Mo2",
                    Density = 7.98m,
                    HeatTreatment = "固溶处理 1010-1150℃，快冷",
                    SpecialMaterial = false,
                    Remark = "低碳含钼奥氏体不锈钢"
                },
                // 321 系列
                new StandardGradeMapping
                {
                    StandardGrade = "321",
                    PlantGrade = "06Cr18Ni11Ti",
                    Density = 7.93m,
                    HeatTreatment = "固溶处理 920-1150℃，快冷",
                    SpecialMaterial = false,
                    Remark = "含钛奥氏体不锈钢，耐晶间腐蚀"
                },
                // 310S 系列
                new StandardGradeMapping
                {
                    StandardGrade = "310S",
                    PlantGrade = "06Cr25Ni20",
                    Density = 7.98m,
                    HeatTreatment = "固溶处理 1030-1180℃，快冷",
                    SpecialMaterial = false,
                    Remark = "耐高温奥氏体不锈钢"
                },
                // 201 系列
                new StandardGradeMapping
                {
                    StandardGrade = "201",
                    PlantGrade = "12Cr17Mn6Ni5N",
                    Density = 7.93m,
                    HeatTreatment = "固溶处理 1010-1120℃，快冷",
                    SpecialMaterial = false,
                    Remark = "节镍奥氏体不锈钢"
                },
                // 202 系列
                new StandardGradeMapping
                {
                    StandardGrade = "202",
                    PlantGrade = "12Cr18Mn9Ni5N",
                    Density = 7.93m,
                    HeatTreatment = "固溶处理 1010-1120℃，快冷",
                    SpecialMaterial = false,
                    Remark = "节镍奥氏体不锈钢"
                },
                // 309S 系列
                new StandardGradeMapping
                {
                    StandardGrade = "309S",
                    PlantGrade = "06Cr23Ni13",
                    Density = 7.98m,
                    HeatTreatment = "固溶处理 1030-1150℃，快冷",
                    SpecialMaterial = false,
                    Remark = "耐高温奥氏体不锈钢"
                },
                // 347 系列
                new StandardGradeMapping
                {
                    StandardGrade = "347",
                    PlantGrade = "06Cr18Ni11Nb",
                    Density = 7.93m,
                    HeatTreatment = "固溶处理 980-1150℃，快冷",
                    SpecialMaterial = false,
                    Remark = "含铌奥氏体不锈钢，耐晶间腐蚀"
                },
                // 特殊材料
                new StandardGradeMapping
                {
                    StandardGrade = "904L",
                    PlantGrade = "015Cr21Ni26Mo5Cu2",
                    Density = 8.24m,
                    HeatTreatment = "固溶处理 1090-1170℃，快冷",
                    SpecialMaterial = true,
                    SpecialNote = "超级奥氏体不锈钢，注意酸洗工艺",
                    Remark = "超级奥氏体不锈钢"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "S31803",
                    PlantGrade = "022Cr22Ni5Mo3N",
                    Density = 7.80m,
                    HeatTreatment = "固溶处理 1020-1100℃，快冷",
                    SpecialMaterial = true,
                    SpecialNote = "双相不锈钢，严格控制热处理温度",
                    Remark = "双相不锈钢"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "S32750",
                    PlantGrade = "022Cr25Ni7Mo4N",
                    Density = 7.80m,
                    HeatTreatment = "固溶处理 1050-1120℃，快冷",
                    SpecialMaterial = true,
                    SpecialNote = "超级双相不锈钢，严格控制热处理工艺",
                    Remark = "超级双相不锈钢"
                }
            };

            await context.StandardGradeMappings.AddRangeAsync(gradeMappings);
            await context.SaveChangesAsync();
        }

        // ========== 5. 初始化测试客户 ==========
        if (!context.CustomerProfiles.Any())
        {
            var customers = new List<CustomerProfile>
            {
                new CustomerProfile
                {
                    CustomerCode = "C001",
                    Salesman = "张三",
                    CustomerUnit = "某某石化工程有限公司",
                    EndCustomer = "某某炼化厂",
                    ContactPerson = "李经理",
                    ContactPhone = "13800000001",
                    Address = "浙江省宁波市某某区石化大道88号",
                    Status = CustomerStatus.Active,
                    Remark = "长期合作客户，主要采购316L/304无缝管"
                },
                new CustomerProfile
                {
                    CustomerCode = "C002",
                    Salesman = "李四",
                    CustomerUnit = "某某锅炉制造有限公司",
                    EndCustomer = null,
                    ContactPerson = "王工",
                    ContactPhone = "13800000002",
                    Address = "江苏省无锡市某某区工业园18号",
                    Status = CustomerStatus.Active,
                    Remark = "锅炉用管客户，需提供质保书"
                },
                new CustomerProfile
                {
                    CustomerCode = "C003",
                    Salesman = "王五",
                    CustomerUnit = "某某海洋工程有限公司",
                    EndCustomer = "某某海上平台项目",
                    ContactPerson = "赵总",
                    ContactPhone = "13800000003",
                    Address = "山东省青岛市某某区海工路1号",
                    Status = CustomerStatus.Active,
                    Remark = "海洋工程用管，要求双相不锈钢"
                },
                new CustomerProfile
                {
                    CustomerCode = "C004",
                    Salesman = "赵六",
                    CustomerUnit = "某某换热器有限公司",
                    EndCustomer = null,
                    ContactPerson = "孙经理",
                    ContactPhone = "13800000004",
                    Address = "广东省佛山市某某区工业大道66号",
                    Status = CustomerStatus.Active,
                    Remark = "换热器用管，要求高精度"
                },
                new CustomerProfile
                {
                    CustomerCode = "C005",
                    Salesman = "钱七",
                    CustomerUnit = "某某食品机械有限公司",
                    EndCustomer = null,
                    ContactPerson = "周工",
                    ContactPhone = "13800000005",
                    Address = "上海市某某区食品工业园2号",
                    Status = CustomerStatus.Active,
                    Remark = "食品级不锈钢管，要求内壁抛光"
                },
                new CustomerProfile
                {
                    CustomerCode = "C006",
                    Salesman = "张三",
                    CustomerUnit = "某某化工设备有限公司",
                    EndCustomer = "某某化工厂",
                    ContactPerson = "陈工",
                    ContactPhone = "13800000006",
                    Address = "江苏省南京市某某区化工园5号",
                    Status = CustomerStatus.Active,
                    Remark = "化工设备用管"
                },
                new CustomerProfile
                {
                    CustomerCode = "C007",
                    Salesman = "李四",
                    CustomerUnit = "某某制药机械有限公司",
                    EndCustomer = null,
                    ContactPerson = "刘经理",
                    ContactPhone = "13800000007",
                    Address = "上海市某某区生物医药园10号",
                    Status = CustomerStatus.Active,
                    Remark = "制药机械用管"
                },
                new CustomerProfile
                {
                    CustomerCode = "C008",
                    Salesman = "王五",
                    CustomerUnit = "某某造船厂",
                    EndCustomer = "某某船舶项目",
                    ContactPerson = "徐总",
                    ContactPhone = "13800000008",
                    Address = "辽宁省大连市某某区造船路1号",
                    Status = CustomerStatus.Active,
                    Remark = "造船用管"
                },
                new CustomerProfile
                {
                    CustomerCode = "C009",
                    Salesman = "赵六",
                    CustomerUnit = "某某核电设备有限公司",
                    EndCustomer = "某某核电站",
                    ContactPerson = "黄工",
                    ContactPhone = "13800000009",
                    Address = "广东省深圳市某某区核电大道88号",
                    Status = CustomerStatus.Active,
                    Remark = "核电用管，要求严格"
                },
                new CustomerProfile
                {
                    CustomerCode = "C010",
                    Salesman = "钱七",
                    CustomerUnit = "某某医疗器械有限公司",
                    EndCustomer = null,
                    ContactPerson = "朱经理",
                    ContactPhone = "13800000010",
                    Address = "江苏省苏州市某某区医疗器械园20号",
                    Status = CustomerStatus.Active,
                    Remark = "医疗器械用管"
                }
            };

            await context.CustomerProfiles.AddRangeAsync(customers);
            await context.SaveChangesAsync();
        }
    }
}