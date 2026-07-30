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
                new() { Category = "WarehouseThreshold", CategoryDisplay = "工单-入库完结比率", Context = "工单", ParamKey = "CompleteRatio", ParamValue = 0.95m, Remark = "入库完工比率阈值" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "工单-入库完结偏差", Context = "工单", ParamKey = "CompleteDeviation", ParamValue = 100m, Remark = "入库完工绝对偏差(kg)" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "委外-回收比率", Context = "物料", ParamKey = "SubcontractCompleteRatio", ParamValue = 0.95m, Remark = "委外完工比率阈值" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "采购-完工比率", Context = "物料", ParamKey = "PurchaseCompleteRatio", ParamValue = 0.965m, Remark = "采购完工比率阈值" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "采购-完工偏差", Context = "物料", ParamKey = "PurchaseCompleteDeviation", ParamValue = 200m, Remark = "采购完工绝对偏差(kg)" },
                new() { Category = "WarehouseThreshold", CategoryDisplay = "仓库-完工阈值", Context = "批次", ParamKey = "OutsourceRecoveryRatio", ParamValue = 0.99m, Remark = "委外回收比率阈值" },

                // ===== ProductionThreshold 生产阈值 =====
                new() { Category = "ProductionThreshold", CategoryDisplay = "批次-生产阈值", Context = "批次", ParamKey = "ColdRollCompleteRatio", ParamValue = 0.95m, Remark = "冷轧拔完工比率" },
                new() { Category = "ProductionThreshold", CategoryDisplay = "批次-生产阈值", Context = "批次", ParamKey = "ValidInputUpper", ParamValue = 1.03m, Remark = "有效投料比率上限" },
                new() { Category = "ProductionThreshold", CategoryDisplay = "批次-生产阈值", Context = "批次", ParamKey = "ValidInputLower", ParamValue = 0.97m, Remark = "有效投料比率下限" },

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

                // ===== DateBucket 日期桶边界 =====
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket1", ParamValue = 15m, Remark = "日期桶1(天)" },
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket2", ParamValue = 30m, Remark = "日期桶2(天)" },
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket3", ParamValue = 45m, Remark = "日期桶3(天)" },
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket4", ParamValue = 60m, Remark = "日期桶4(天)" },
                new() { Category = "DateBucket", CategoryDisplay = "排程-日期桶", Context = "排程", ParamKey = "Bucket5", ParamValue = 90m, Remark = "日期桶5(天)" },

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
                new() { Category = "DefaultValue", CategoryDisplay = "工单-荒管成品系数", Context = "工单", ParamKey = "RoughTubeFinishRatio", ParamValue = 0.92m, Remark = "荒管转成品系数" },

                // ===== MaterialPlanTolerance 用料计划执行容差 =====
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "ExternalLower", ParamValue = 0.97m, Remark = "对外计划下限(97%)，圆棒穿孔/荒管采购/成品采购" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "ExternalUpper", ParamValue = 1.03m, Remark = "对外计划上限(103%)，圆棒穿孔/荒管采购/成品采购" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "WarehouseLower", ParamValue = 0.95m, Remark = "对内-仓库下限(95%)，库存使用/库料改制" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "WarehouseUpper", ParamValue = 1.50m, Remark = "对内-仓库上限(150%)，库存使用/库料改制" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "ProductionLower", ParamValue = 0.90m, Remark = "对内-生产下限(90%)，在产改制/在产主工单" },
                new() { Category = "MaterialPlanTolerance", CategoryDisplay = "工单-用料计划执行容差", Context = "工单", ParamKey = "ProductionUpper", ParamValue = 1.50m, Remark = "对内-生产上限(150%)，在产改制/在产主工单" },
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
            AddItem(settingMap["F"], "20冷轧", "固溶", 1m, 1);
            AddItem(settingMap["F"], "30冷轧", "固溶", 1m, 2);
            AddItem(settingMap["F"], "50冷轧", "固溶", 1m, 3);
            AddItem(settingMap["F"], "60冷轧", "固溶", 1m, 4);
            AddItem(settingMap["F"], "冷拔", "固溶", 1m, 5);
            AddItem(settingMap["F"], "三辊冷轧", "固溶", 1m, 6);
            AddItem(settingMap["F"], "在制修检", "固溶", 1m, 7);

            // G 矫直
            AddItem(settingMap["G"], "20冷轧", "矫直", 1m, 1);
            AddItem(settingMap["G"], "30冷轧", "矫直", 1m, 2);
            AddItem(settingMap["G"], "50冷轧", "矫直", 0.5m, 3);
            AddItem(settingMap["G"], "60冷轧", "矫直", 0.5m, 4);
            AddItem(settingMap["G"], "荒管处理", "矫直", 0.25m, 5);
            AddItem(settingMap["G"], "冷拔", "矫直", 1m, 6);
            AddItem(settingMap["G"], "三辊冷轧", "矫直", 1m, 7);
            AddItem(settingMap["G"], "在制修检", "矫直", 1m, 8);

            // H 切割
            AddItem(settingMap["H"], "20冷轧", "断切", 1m, 1);
            AddItem(settingMap["H"], "30冷轧", "断切", 1m, 2);
            AddItem(settingMap["H"], "50冷轧", "断切", 0.5m, 3);
            AddItem(settingMap["H"], "60冷轧", "断切", 0.5m, 4);
            AddItem(settingMap["H"], "荒管处理", "断切", 0.25m, 5);
            AddItem(settingMap["H"], "冷拔", "断切", 1m, 6);
            AddItem(settingMap["H"], "三辊冷轧", "断切", 1m, 7);
            AddItem(settingMap["H"], "在制修检", "断切", 0.25m, 8);
            AddItem(settingMap["H"], "20冷轧", "油管断", 0.75m, 9);
            AddItem(settingMap["H"], "30冷轧", "油管断", 0.75m, 10);
            AddItem(settingMap["H"], "50冷轧", "油管断", 0.5m, 11);
            AddItem(settingMap["H"], "60冷轧", "油管断", 0.5m, 12);
            AddItem(settingMap["H"], "三辊冷轧", "油管断", 0.75m, 13);

            // I 去油
            AddItem(settingMap["I"], "20冷轧", "去油", 1m, 1);
            AddItem(settingMap["I"], "30冷轧", "去油", 1m, 2);
            AddItem(settingMap["I"], "50冷轧", "去油", 0.5m, 3);
            AddItem(settingMap["I"], "60冷轧", "去油", 0.5m, 4);
            AddItem(settingMap["I"], "三辊冷轧", "去油", 1m, 5);

            // J 酸洗
            AddItem(settingMap["J"], "20冷轧", "酸洗", 1m, 1);
            AddItem(settingMap["J"], "30冷轧", "酸洗", 1m, 2);
            AddItem(settingMap["J"], "50冷轧", "酸洗", 0.5m, 3);
            AddItem(settingMap["J"], "60冷轧", "酸洗", 0.5m, 4);
            AddItem(settingMap["J"], "荒管处理", "酸洗", 0.25m, 5);
            AddItem(settingMap["J"], "冷拔", "酸洗", 1m, 6);
            AddItem(settingMap["J"], "三辊冷轧", "酸洗", 1m, 7);
            AddItem(settingMap["J"], "在制修检", "酸洗", 0.25m, 8);

            // K 大轧
            AddItem(settingMap["K"], "50冷轧", "冷轧拔", 1m, 1);
            AddItem(settingMap["K"], "60冷轧", "冷轧拔", 1m, 2);

            // L 小轧
            AddItem(settingMap["L"], "20冷轧", "冷轧拔", 1m, 1);
            AddItem(settingMap["L"], "30冷轧", "冷轧拔", 1m, 2);
            AddItem(settingMap["L"], "三辊冷轧", "冷轧拔", 1m, 3);

            // M 冷拔
            AddItem(settingMap["M"], "冷拔", "冷轧拔", 1m, 1);

            // N 成品待检：所有工序组中工段=检验的属成品工序量（FinalProcessTotal）汇总
            AddItem(settingMap["N"], "全部", "检验", 1m, 1);

            await context.SectionFlowCategoryItems.AddRangeAsync(items);
            await context.SaveChangesAsync();
        }

        // ========== 12. Initialize Daily Production Capacities ==========
        if (!context.DailyProductionCapacities.Any())
        {
            var capacities = new List<DailyProductionCapacity>
            {
                new() { ProcessName = "荒管抛光", DailyCapacity = 15m, Remark = "荒管抛光日产能(吨)" },
                new() { ProcessName = "50,60轧机", DailyCapacity = 11m, Remark = "50,60轧机日产能(吨)" },
                new() { ProcessName = "20,30轧机", DailyCapacity = 9m, Remark = "20,30轧机日产能(吨)" },
                new() { ProcessName = "三辊轧机", DailyCapacity = 0.5m, Remark = "三辊轧机日产能(吨)" },
                new() { ProcessName = "拉机", DailyCapacity = 3m, Remark = "拉机日产能(吨)" },
            };
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