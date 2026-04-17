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
                    Remark = "Austenitic stainless steel"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "304L",
                    PlantGrade = "022Cr19Ni10",
                    Density = 7.93m,
                    HeatTreatment = "Solution treatment 1010-1150℃, rapid cooling",
                    SpecialMaterial = false,
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
                    Remark = "Molybdenum-containing austenitic stainless steel"
                },
                new StandardGradeMapping
                {
                    StandardGrade = "316L",
                    PlantGrade = "022Cr17Ni12Mo2",
                    Density = 7.98m,
                    HeatTreatment = "Solution treatment 1010-1150℃, rapid cooling",
                    SpecialMaterial = false,
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
    }
}