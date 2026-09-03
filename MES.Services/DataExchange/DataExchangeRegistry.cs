using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Warehouse;

namespace MES.Services.DataExchange;

/// <summary>
/// 数据导入导出实体注册表
/// </summary>
public static class DataExchangeRegistry
{
    /// <summary>
    /// 系统编码字段的生成前缀映射（属性名 → 前缀）
    /// </summary>
    public static readonly Dictionary<string, string> CodePrefixMap = new()
    {
        ["SupplierCode"] = "SU",
    };

    /// <summary>
    /// 实体注册表：所有支持导入导出的实体
    /// </summary>
    public static readonly Dictionary<string, EntityDef> Registry = new()
    {
        // === 第1批：独立实体（无外部FK依赖） ===
        ["Warehouse"] = new EntityDef("仓库-仓库档案", "仓库-仓库档案", typeof(MES.Data.Entities.Warehouse.Warehouse), 1, "Code", new List<ColumnDef>
        {
            new("仓库编码", "Code"),
            new("仓库名称", "Name"),
            new("显示顺序", "SortOrder", typeof(int)),
            new("是否启用", "IsActive", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["StandardGradeMapping"] = new EntityDef("标准-牌号对照", "标准-牌号对照", typeof(MES.Data.Entities.StandardRegister.StandardGradeMapping), 1, null, new List<ColumnDef>
        {
            new("标准牌号", "StandardGrade"),
            new("标准牌号类别", "StandardGradeCategory", typeof(string), isRequired: false),
            new("工厂牌号", "PlantGrade"),
            new("密度(g/cm³)", "Density", typeof(decimal)),
            new("热处理方式", "HeatTreatment", typeof(string), isRequired: false),
            new("特殊材料", "SpecialMaterial", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("特殊说明", "SpecialNote", typeof(string), isRequired: false),
            new("钢性", "SteelProperty", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "StandardGrade", "StandardGradeCategory" }),

        ["CustomerProfile"] = new EntityDef("订单-客户档案", "订单-客户档案", typeof(Data.Entities.Order.CustomerProfile), 1, "CustomerCode", new List<ColumnDef>
        {
            new("客户编码", "CustomerCode"),
            new("客户单位", "CustomerUnit"),
            new("业务员", "Salesman"),
            new("最终用户", "EndCustomer", typeof(string), isRequired: false),
            new("联系人", "ContactPerson", typeof(string), isRequired: false),
            new("联系电话", "ContactPhone", typeof(string), isRequired: false),
            new("地址", "Address", typeof(string), isRequired: false),
            new("状态", "Status", typeof(Core.Enums.CustomerStatus), isEnum: true),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["SupplierProfile"] = new EntityDef("物料-供应商档案", "物料-供应商档案", typeof(Data.Entities.Materials.SupplierProfile), 1, "SupplierName", new List<ColumnDef>
        {
            new("供应商编码", "SupplierCode", isSystem: true),
            new("供应商名称", "SupplierName"),
            new("物料分类", "MaterialCategory", typeof(MES.Core.Enums.MaterialType), isEnum: true, isRequired: false),
            new("联系人", "ContactPerson", typeof(string), isRequired: false),
            new("联系电话", "ContactPhone", typeof(string), isRequired: false),
            new("地址", "Address", typeof(string), isRequired: false),
            new("是否启用", "IsActive", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 设备台账 ===
        ["Equipment"] = new EntityDef("设备-设备台账", "设备-设备台账", typeof(Data.Entities.Equipment.Equipment), 1, "EquipmentCode", new List<ColumnDef>
        {
            new("设备编号", "EquipmentCode"),
            new("设备名称", "EquipmentName"),
            new("型号规格", "ModelNumber", typeof(string), isRequired: false),
            new("技术参数", "TechnicalParams", typeof(string), isRequired: false),
            new("制造商", "Manufacturer", typeof(string), isRequired: false),
            new("安装日期", "InstallationDate", typeof(DateTime?), isRequired: false),
            new("所在区域", "Location", typeof(string), isRequired: false),
            new("关联工段", "RelatedSection", typeof(string), isRequired: false),
            new("是否需点检", "NeedInspection", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("点检负责人", "InspectionPerson", typeof(string), isRequired: false),
            new("点检周期(天)", "InspectionCycleDays", typeof(int), isRequired: false),
            new("本次点检日起始", "CurrentInspectionStartDate", typeof(DateTime?), isRequired: false),
            new("是否需保养", "NeedMaintenance", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("保养负责人", "MaintPerson", typeof(string), isRequired: false),
            new("保养周期(天)", "MaintCycleDays", typeof(int), isRequired: false),
            new("本次保养日起始", "CurrentMaintStartDate", typeof(DateTime?), isRequired: false),
            new("最近维修日期", "LastRepairDate", typeof(DateTime?), isRequired: false),
            new("最近点检日期", "LastInspectionDate", typeof(DateTime?), isRequired: false, isSystem: true),
            new("最近保养日期", "LastMaintDate", typeof(DateTime?), isRequired: false, isSystem: true),
            new("生命周期", "LifecycleStatus", typeof(Core.Enums.LifecycleStatus), isEnum: true),
            new("作用类型", "UsageType", typeof(Core.Enums.UsageType), isEnum: true),
            new("运行状态", "RunningStatus", typeof(Core.Enums.RunningStatus), isEnum: true, isSystem: true),
            new("点检状况", "InspectionStatus", typeof(Core.Enums.EquipmentTaskStatus), isEnum: true, isSystem: true),
            new("保养状况", "MaintStatus", typeof(Core.Enums.EquipmentTaskStatus), isEnum: true, isSystem: true),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 第2批：依赖客户档案 ===
        ["SalesOrder"] = new EntityDef("订单-销售订单", "订单-销售订单", typeof(Data.Entities.Order.SalesOrder), 2, "OrderNumber", new List<ColumnDef>
        {
            new("订单号", "OrderNumber"),
            new("签订日期", "SignDate", typeof(DateTime)),
            new("状态", "Status", typeof(Core.Enums.SalesOrderStatus), isEnum: true),
            new("客户名称", "CustomerName"),
            new("业务员", "Salesman"),
            new("最终用户", "EndCustomer", typeof(string), isRequired: false),
            new("最后项次变更时间", "LastItemChangeTime", typeof(DateTimeOffset?), isRequired: false, isSystem: true),
        }),

        // === 设备上下文（依赖设备台账） ===
        ["RepairOrder"] = new EntityDef("设备-维修工单", "设备-维修工单", typeof(Data.Entities.Equipment.RepairOrder), 2, "RepairOrderNo", new List<ColumnDef>
        {
            new("工单编号", "RepairOrderNo", isSystem: true),
            new("设备编号", null!) { IsFkColumn = true, FkEntityKey = "Equipment", FkLookupProperty = "EquipmentCode", FkTargetProperty = "EquipmentId" },
            new("故障描述", "FaultDescription"),
            new("故障类型", "FaultType", typeof(string), isRequired: false),
            new("优先级", "Priority", typeof(Core.Enums.RepairPriority), isEnum: true),
            new("维修状态", "RepairStatus", typeof(Core.Enums.RepairOrderStatus), isEnum: true),
            new("报修人", "ReportPerson"),
            new("报修时间", "ReportTime", typeof(DateTime)),
            new("维修人", "RepairPerson", typeof(string), isRequired: false),
            new("维修开始时间", "RepairStartTime", typeof(DateTime?), isRequired: false),
            new("维修结束时间", "RepairEndTime", typeof(DateTime?), isRequired: false),
            new("维修内容", "RepairContent", typeof(string), isRequired: false),
            new("备件更换", "SparePartUsed", typeof(string), isRequired: false),
            new("维修类别", "RepairCategory", typeof(string), isRequired: false),
            new("辅助维修人", "OtherRepairPersons", typeof(string), isRequired: false),
        }),

        ["MaintenanceOrder"] = new EntityDef("设备-保养工单", "设备-保养工单", typeof(Data.Entities.Equipment.MaintenanceOrder), 2, "MaintOrderNo", new List<ColumnDef>
        {
            new("工单编号", "MaintOrderNo", isSystem: true),
            new("设备编号", null!) { IsFkColumn = true, FkEntityKey = "Equipment", FkLookupProperty = "EquipmentCode", FkTargetProperty = "EquipmentId" },
            new("实际日期", "ActualDate", typeof(DateTime?), isRequired: false),
            new("执行人", "Executor", typeof(string), isRequired: false),
            new("执行简述", "ExecutionSummary", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["InspectionRecord"] = new EntityDef("设备-点检记录", "设备-点检记录", typeof(Data.Entities.Equipment.InspectionRecord), 2, "RecordNo", new List<ColumnDef>
        {
            new("记录编号", "RecordNo", isSystem: true),
            new("设备编号", null!) { IsFkColumn = true, FkEntityKey = "Equipment", FkLookupProperty = "EquipmentCode", FkTargetProperty = "EquipmentId" },
            new("实际日期", "ActualDate", typeof(DateTime?), isRequired: false),
            new("点检人", "Inspector", typeof(string), isRequired: false),
            new("执行简述", "ExecutionSummary", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 第3批：依赖销售订单、产品标准、牌号对照 ===
        ["OrderItem"] = new EntityDef("订单-订单项次", "订单-订单项次", typeof(Data.Entities.Order.OrderItem), 3, null, new List<ColumnDef>
        {
            new("订单号", "OrderNumber") { IsFkColumn = true, FkEntityKey = "SalesOrder", FkLookupProperty = "OrderNumber", FkTargetProperty = "SalesOrderId" },
            new("项次号", "Sequence", typeof(int)),
            new("交货日期", "DeliveryDate", typeof(DateTime)),
            new("延期罚款", "DelayPenalty", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("结算方式", "SettlementMethod", typeof(Core.Enums.SettlementMethod), isEnum: true),
            new("物料名称", "PipeManufacturingType", typeof(Core.Enums.PipeManufacturingType), isEnum: true),
            new("产品标准编码", "StandardNo"),
            new("交货状态", "DeliveryState", typeof(Core.Enums.DeliveryState), isEnum: true),
            new("标准牌号", "StandardGrade"),
            new("工厂牌号", "PlantGrade"),
            new("密度(g/cm³)", "Density", typeof(decimal)),
            new("外径(mm)", "OuterDiameter", typeof(decimal)),
            new("壁厚(mm)", "WallThickness", typeof(decimal)),
            new("规格", "Specification"),
            new("外径下偏差(mm)", "OuterDiameterNegative", typeof(decimal)),
            new("外径上偏差(mm)", "OuterDiameterPositive", typeof(decimal)),
            new("壁厚下偏差(mm)", "WallThicknessNegative", typeof(decimal)),
            new("壁厚上偏差(mm)", "WallThicknessPositive", typeof(decimal)),
            new("长度状态", "LengthStatus", typeof(Core.Enums.LengthStatus), isEnum: true),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("数量(支)", "Quantity", typeof(int?), isRequired: false),
            new("米数(m)", "Meters", typeof(decimal?), isRequired: false),
            new("合同重量(kg)", "ContractWeight", typeof(decimal)),
            new("理算重量(kg)", "TheoreticalWeight", typeof(decimal)),
            new("备注", "Remark", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "OrderNumber", "Sequence" }),

        // === 第4批：依赖订单项次 ===
        ["ProductRequirement"] = new EntityDef("订单-技术要求", "订单-技术要求", typeof(Data.Entities.Order.ProductRequirement), 4, null, new List<ColumnDef>
        {
            new("订单号", "OrderNo") { IsFkColumn = true, FkEntityKey = "OrderItem", FkLookupProperty = "Id", FkTargetProperty = "OrderItemId", FkRequiresJoin = true },
            new("项次号", "ItemSequence") { IsFkColumn = true, FkEntityKey = "OrderItem", FkLookupProperty = "Sequence", FkTargetProperty = "OrderItemId", FkRequiresJoin = true },
            new("技术要求类型", "RequirementType", typeof(Core.Enums.RequirementType), isEnum: true),
            new("化学分析(成品)", "ChemicalComposition", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("PMI检验", "PmiInspection", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("表检", "SurfaceInspection", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("尺寸", "Dimension", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("内窥", "Endoscopy", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("液压检验", "HydrostaticTest", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("水下气压", "UnderwaterPressure", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("涡流探伤", "EddyCurrent", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("超声波检验", "UltrasonicTest", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("端口着色", "PortColoring", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("射线探伤", "RadiographicTest", typeof(Core.Enums.InspectionRequirementStage), isEnum: true),
            new("硬度(洛氏)", "HardnessRockwell", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("硬度(布氏)", "HardnessBrinell", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("硬度(维氏)", "HardnessVickers", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("拉伸(室温)", "TensileRoomTemp", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("拉伸(高温)", "TensileHighTemp", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("焊接接头拉伸", "WeldJointTensile", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("冲击试验", "ImpactTest", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("焊接接头冲击", "WeldJointImpact", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("压扁试验", "FlatteningTest", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("卷边试验", "FlaringTest", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("扩口试验", "ExpandingTest", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("弯曲试验", "BendTest", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("焊接接头弯曲", "WeldJointBend", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("晶粒度", "GrainSize", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("晶间腐蚀", "IntergranularCorrosion", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("点腐蚀", "PittingCorrosion", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("金相检验", "FerriteContent", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("低倍组织", "Macrostructure", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("其他要求", "OtherRequirement", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "OrderNo", "ItemSequence" }),

        // === 第5批：工单（字符串引用订单，无FK约束） ===
        ["WorkOrder"] = new EntityDef("工单-工单", "工单-工单", typeof(Data.Entities.WorkOrder.WorkOrder), 5, "WorkOrderNo", new List<ColumnDef>
        {
            new("工单号", "WorkOrderNo"),
            new("订单号", "SalesOrderNo"),
            new("主号", "ProductionMainNo"),
            new("次号", "ProductionSubNo", typeof(string), isRequired: false),
            new("状态", "Status", typeof(Core.Enums.WorkOrderStatus), isEnum: true),
            new("签订日期", "SignDate", typeof(DateTime)),
            new("业务员", "Salesman"),
            new("最终用户", "EndCustomer", typeof(string), isRequired: false),
            new("交货日期", "DeliveryDate", typeof(DateTime)),
            new("延期罚款", "DelayPenalty", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("物料名称", "PipeManufacturingType", typeof(Core.Enums.PipeManufacturingType), isEnum: true),
            new("结算方式", "SettlementMethod", typeof(Core.Enums.SettlementMethod), isEnum: true),
            new("产品标准编码", "StandardCode"),
            new("交货状态", "DeliveryState", typeof(Core.Enums.DeliveryState), isEnum: true),
            new("工厂牌号", "PlantGrade"),
            new("规格", "Specification"),
            new("外径下偏差(mm)", "OuterDiameterNegative", typeof(decimal)),
            new("外径上偏差(mm)", "OuterDiameterPositive", typeof(decimal)),
            new("壁厚下偏差(mm)", "WallThicknessNegative", typeof(decimal)),
            new("壁厚上偏差(mm)", "WallThicknessPositive", typeof(decimal)),
            new("长度状态", "LengthStatus", typeof(Core.Enums.LengthStatus), isEnum: true),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("总数量(支)", "TotalQuantity", typeof(int)),
            new("总米数(m)", "TotalMeters", typeof(decimal)),
            new("总重量(kg)", "TotalWeight", typeof(decimal)),
            new("技术要求", "TechnicalRequirements", typeof(Core.Enums.RequirementType), isEnum: true),
            new("总项次数", "TotalItemCount", typeof(int), isSystem: true),
            new("明细", "ItemDetails", typeof(string), isRequired: false, isSystem: true),
            new("用料计划状态", "MaterialPlanStatus", typeof(Core.Enums.MaterialPlanStatus), isEnum: true, isSystem: true),
            new("用料计划满足率(%)", "MaterialPlanRate", typeof(decimal), isSystem: true),
            new("关联项次(订单号|项次号)", "OrderItemIds", typeof(string), isRequired: false),
        }),

        // === 第5批：工单-需求调整（依赖工单） ===
        ["OrderDemandAdjustment"] = new EntityDef("工单-需求调整", "工单-需求调整",
            typeof(MES.Data.Entities.WorkOrder.OrderDemandAdjustment), 5, null, new List<ColumnDef>
        {
            new("工单号", null!) { IsFkColumn = true, FkEntityKey = "WorkOrder", FkLookupProperty = "WorkOrderNo", FkTargetProperty = "WorkOrderId" },
            new("催单", "IsUrging", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("分批交货", "IsBatchDelivery", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("暂停", "IsPaused", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("强制完成", "IsForceCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("调整备注", "AdjustmentRemark", typeof(string), isRequired: false),
        }),

        // === 第7批：采购订单、委外订单、生产批次（依赖供应商/工单） ===
        ["ProductionBatch"] = new EntityDef("批次-生产批次", "批次-生产批次", typeof(MES.Data.Entities.Batch.ProductionBatch), 7, "BatchNo", new List<ColumnDef>
        {
            new("生产编号", "BatchNo"),
            new("状态", "Status", typeof(MES.Core.Enums.BatchStatus), isEnum: true),
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("生产类型", "ProductionType", typeof(MES.Core.Enums.ProductionType), isEnum: true, isRequired: false),
            new("制造物品", "ManufacturingItem", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("制成倍数", "ProductionRatio", typeof(int), isRequired: false),
            new("强制完成", "IsForceCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("质量备注", "QualityRemark", typeof(string), isRequired: false),
            new("固溶参数", "SolutionParams", typeof(string), isRequired: false),
            new("截止执行日", "CurrentExecDate", typeof(DateTime?), isRequired: false),
            new("当前工序", "CurrentGroupName", typeof(string), isRequired: false),
            new("当前工段", "CurrentSectionName", typeof(string), isRequired: false),
            new("当前设备", "CurrentEquipmentName", typeof(string), isRequired: false),
            new("当前委外", "CurrentOutsource", typeof(string), isRequired: false),
            new("当前规格", "CurrentSpec", typeof(string), isRequired: false),
            new("下一工段", "NextSectionName", typeof(string), isRequired: false),
            new("对应规格", "CorrespondingSpec", typeof(string), isRequired: false),
            new("下一工序", "NextProcess", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            // 工单冗余字段
            new("工单号", "WorkOrderNo"),
            new("订单号", "SalesOrderNo"),
            new("主号", "ProductionMainNo"),
            new("次号", "ProductionSubNo", typeof(string), isRequired: false),
            new("签订日期", "SignDate", typeof(DateTime)),
            new("业务员", "Salesman"),
            new("最终用户", "EndCustomer", typeof(string), isRequired: false),
            new("交货日期", "DeliveryDate", typeof(DateTime)),
            new("延期罚款", "DelayPenalty", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("物料名称", "MaterialName", typeof(MES.Core.Enums.PipeManufacturingType), isEnum: true),
            new("结算方式", "SettlementMethod", typeof(MES.Core.Enums.SettlementMethod), isEnum: true),
            new("标准编码", "StandardCode"),
            new("交货状态", "DeliveryState", typeof(MES.Core.Enums.DeliveryState), isEnum: true),
            new("制造状态", "ManufacturingStatus", typeof(MES.Core.Enums.DeliveryState), isEnum: true, isRequired: false),
            new("工厂牌号", "PlantGrade"),
            new("规格", "Specification"),
            new("外径下偏差(mm)", "OuterDiameterNegative", typeof(decimal)),
            new("外径上偏差(mm)", "OuterDiameterPositive", typeof(decimal)),
            new("壁厚下偏差(mm)", "WallThicknessNegative", typeof(decimal)),
            new("壁厚上偏差(mm)", "WallThicknessPositive", typeof(decimal)),
            new("长度状态", "LengthStatus", typeof(MES.Core.Enums.LengthStatus), isEnum: true),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("总数量(支)", "TotalQuantity", typeof(int)),
            new("总米数(m)", "TotalMeters", typeof(decimal)),
            new("总重量(kg)", "TotalWeight", typeof(decimal)),
            new("总项次数", "TotalItemCount", typeof(int)),
            new("明细", "ItemDetails", typeof(string), isRequired: false),
            new("技术要求", "TechnicalRequirements", typeof(MES.Core.Enums.RequirementType), isEnum: true),
            new("关联项次", "OrderItemIds", typeof(string), isRequired: false),
            // 仓库冗余字段
            new("来源库存批次号", "SourceBatchNo", typeof(string), isRequired: false),
            new("原料类型", "SourceMaterialType", typeof(MES.Core.Enums.MaterialType), isEnum: true, isRequired: false),
            new("来料单位", "SourceName", typeof(string), isRequired: false),
            new("炉号", "SourceHeatNo", typeof(string), isRequired: false),
            new("来源工厂牌号", "SourcePlantGrade", typeof(string), isRequired: false),
            new("来源名义规格", "SourceSpecification", typeof(string), isRequired: false),
            new("来源长度状态", "SourceLengthStatus", typeof(MES.Core.Enums.LengthStatus), isEnum: true, isRequired: false),
            new("单支重(kg)", "SourceUnitWeight", typeof(decimal?), isRequired: false),
            new("领料支数", "InputQuantity", typeof(int?), isRequired: false),
            new("领料重量(kg)", "InputWeight", typeof(decimal?), isRequired: false),
            new("投料类型", "InputType", typeof(Core.Enums.BatchInputType), isEnum: true, isRequired: false),
            new("源生产编号", "SourceProductionNo", typeof(string), isRequired: false),
            new("投料备注", "SourceRemark", typeof(string), isRequired: false),
            new("现有效支数", "CurrentValidQty", typeof(int?), isRequired: false),
            new("现有效重量(kg)", "CurrentValidWeight", typeof(int?), isRequired: false),
            // 系统跟踪字段
            new("投料变更", "HasInputChange", typeof(bool?), isRequired: false, isSystem: true),
            new("当前工段完工", "CurrentSectionCompleted", typeof(bool?), isRequired: false, isSystem: true),
            new("剩余工量(天)", "RemainingWorkDays", typeof(int), isSystem: true),
            new("全工量(天)", "TotalWorkDays", typeof(int), isSystem: true),
            // 理论成品（系统派生，仅导出不导入）
            new("理论成品支", "TheoreticalOutputQty", typeof(int?), isRequired: false, isSystem: true),
            new("理论成品重", "TheoreticalOutputWeight", typeof(int?), isRequired: false, isSystem: true),
            new("理论单支重", "TheoreticalUnitWeight", typeof(decimal?), isRequired: false, isSystem: true),
            new("产品单支量(kg/支)", "ProductUnitWeight", typeof(decimal?), isRequired: false, isSystem: true),
            // 成切跟踪（系统派生，仅导出不导入）
            new("成切需求", "CutRequirement", typeof(bool), isRequired: false, isSystem: true),
            new("成切执行", "CutExecution", typeof(bool?), isRequired: false, isSystem: true),
            new("成切支数", "CutQuantity", typeof(int?), isRequired: false, isSystem: true),
            new("成切存疑", "CutDoubt", typeof(CutDoubtType?), isEnum: true, isRequired: false, isSystem: true),
            new("成检附加", "InspectionStage", typeof(MES.Core.Enums.InspectionType), isEnum: true, isRequired: false, isSystem: true),
            new("过程检合格支", "ProcessInspectionQualifiedQty", typeof(int?), isRequired: false, isSystem: true),
            new("过程检合格量(kg)", "ProcessInspectionQualifiedWeight", typeof(decimal?), isRequired: false, isSystem: true),
            new("过程检理论成品支", "ProcessInspectionTheoreticalQty", typeof(int?), isRequired: false, isSystem: true),
            new("过程检需调整", "ProcessInspectionNeedAdjust", typeof(bool?), isRequired: false, isSystem: true),
            new("过程检返整量(kg)", "ProcessInspectionReworkWeight", typeof(int?), isRequired: false, isSystem: true),
            new("过程检次品量(kg)", "ProcessInspectionScrapWeight", typeof(int?), isRequired: false, isSystem: true),
        }),

        ["PurchaseOrder"] = new EntityDef("物料-采购订单", "物料-采购订单", typeof(MES.Data.Entities.Materials.PurchaseOrder), 7, "OrderNo", new List<ColumnDef>
        {
            new("采购单号", "OrderNo"),
            new("供应商名称", null!) { IsFkColumn = true, FkEntityKey = "SupplierProfile", FkLookupProperty = "SupplierName", FkTargetProperty = "SupplierId" },
            new("下单日期", "OrderDate", typeof(DateTime)),
            new("状态", "Status", typeof(MES.Core.Enums.PurchaseOrderStatus), isEnum: true),
            new("强制完成", "IsForceCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("物料分类", "MaterialCategory", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("厂内钢种", "PlantGrade"),
            new("名义规格", "Specification"),
            new("单支重量(kg)", "UnitWeight", typeof(decimal?), isRequired: false),
            new("采购支数", "Quantity", typeof(int?), isRequired: false),
            new("采购重量(kg)", "Weight", typeof(decimal)),
            new("投料制成倍", "InputMultiple", typeof(int?), isRequired: false),
            new("要求到货日期", "RequiredDate", typeof(DateTime)),
            new("单价", "UnitPrice", typeof(decimal?), isRequired: false),
            new("总金额", "TotalAmount", typeof(decimal?), isRequired: false),
            new("来源工单号", "SourceWorkOrderNo", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            // Service 维护字段
            new("最后到货日期", "LastArrivalDate", typeof(DateTime?), isRequired: false, isSystem: true),
            new("已到货支数", "ReceivedQuantity", typeof(int), isSystem: true),
            new("已到货重量(kg)", "ReceivedWeight", typeof(decimal), isSystem: true),
        }),

        ["SubcontractOrder"] = new EntityDef("物料-委外订单", "物料-委外订单", typeof(MES.Data.Entities.Materials.SubcontractOrder), 7, "OrderNo", new List<ColumnDef>
        {
            new("委外单号", "OrderNo"),
            new("供应商名称", null!) { IsFkColumn = true, FkEntityKey = "SupplierProfile", FkLookupProperty = "SupplierName", FkTargetProperty = "SupplierId" },
            new("下单日期", "OrderDate", typeof(DateTime)),
            new("加工类型", "ProcessType"),
            new("状态", "Status", typeof(MES.Core.Enums.SubcontractOrderStatus), isEnum: true),
            new("强制完成", "IsForceCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("发出物料分类", "OutMaterialCategory", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("炉号", "FurnaceNumber", typeof(string), isRequired: false),
            new("发出钢种", "OutPlantGrade"),
            new("发出规格", "OutSpecification"),
            new("发出支数", "OutQuantity", typeof(int)),
            new("发出重量(kg)", "OutWeight", typeof(decimal)),
            new("收回截止日期", "ReturnDeadline", typeof(DateTime?), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            // Service 维护字段
            new("已收回支数", "InQuantity", typeof(int?), isRequired: false, isSystem: true),
            new("已收回重量(kg)", "InWeight", typeof(decimal?), isRequired: false, isSystem: true),
        }),

        ["SubcontractReturnItem"] = new EntityDef("物料-委外子项", "物料-委外子项", typeof(MES.Data.Entities.Materials.SubcontractReturnItem), 7, null, new List<ColumnDef>
        {
            new("委外单号", "OrderNo") { IsFkColumn = true, FkEntityKey = "SubcontractOrder", FkLookupProperty = "OrderNo", FkTargetProperty = "SubcontractOrderId" },
            new("行号", "Sequence", typeof(int)),
            new("物料分类", "MaterialCategory", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("加工规格", "ProcessSpecification"),
            new("单重(kg)", "UnitWeight", typeof(decimal?), isRequired: false),
            new("需求支数", "RequiredQuantity", typeof(int?), isRequired: false),
            new("需求重量(kg)", "RequiredWeight", typeof(decimal?), isRequired: false),
            new("投料制成倍", "InputMultiple", typeof(int?), isRequired: false),
            new("状态备注", "ProcessStatusRemark", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("加工单价", "ProcessUnitPrice", typeof(decimal?), isRequired: false),
            new("加工总价", "ProcessTotalAmount", typeof(decimal?), isRequired: false),
            new("来源工单号", "SourceWorkOrderNo", typeof(string), isRequired: false),
            // 回收执行数据（系统回写）
            new("回收支数", "ReturnedQuantity", typeof(int), isSystem: true),
            new("回收重量(kg)", "ReturnedWeight", typeof(decimal), isSystem: true),
            new("加工状态", "ProcessStatus", typeof(MES.Core.Enums.SubcontractOrderStatus), isEnum: true, isSystem: true),
            new("强制完成", "IsForceCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
        }, compositeKeyColumns: new[] { "OrderNo", "Sequence" }),

        // === 第8批：工序组（依赖生产批次）、仓库出入库 ===
        ["ProcessGroup"] = new EntityDef("批次-工序组", "批次-工序组", typeof(MES.Data.Entities.Batch.ProcessGroup), 8, null, new List<ColumnDef>
        {
            new("所属批次号", "BatchNo") { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("组内序号", "SequenceNumber", typeof(int)),
            new("工序名称", "ProcessName"),
            new("制造规格", "ManufacturingSpec", typeof(string), isRequired: false),
            new("外径公差", "OuterDiameterTolerance", typeof(string), isRequired: false),
            new("壁厚公差", "WallThicknessTolerance", typeof(string), isRequired: false),
            new("制造长度", "ManufacturingLength", typeof(string), isRequired: false),
            new("断切处理", "CuttingTreatment", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new(SectionDefs.ColdRollDraw, "ColdRollDraw", typeof(int?), isRequired: false),
            new(SectionDefs.OilPipeCut, "OilPipeCut", typeof(int?), isRequired: false),
            new(SectionDefs.Degrease, "Degrease", typeof(int?), isRequired: false),
            new(SectionDefs.EmulsionWash, "EmulsionWash", typeof(int?), isRequired: false),
            new(SectionDefs.UltrasonicWash, "UltrasonicWash", typeof(int?), isRequired: false),
            new(SectionDefs.ClothPolish, "ClothPolish", typeof(int?), isRequired: false),
            new(SectionDefs.BrightAnnealing, "BrightAnnealing", typeof(int?), isRequired: false),
            new(SectionDefs.Solution, "Solution", typeof(int?), isRequired: false),
            new(SectionDefs.Straighten, "Straighten", typeof(int?), isRequired: false),
            new(SectionDefs.Cut, "Cut", typeof(int?), isRequired: false),
            new(SectionDefs.ThicknessMeasure, "ThicknessMeasure", typeof(int?), isRequired: false),
            new(SectionDefs.Pickle, "Pickle", typeof(int?), isRequired: false),
            new(SectionDefs.OuterPolish, "OuterPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerPolish, "InnerPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerGrinding, "InnerGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.OuterSpotGrinding, "OuterSpotGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.SandBlasting, "SandBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.ShotBlasting, "ShotBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.Inspection, "Inspection", typeof(int?), isRequired: false),
            new(SectionDefs.WeldingHead, "WeldingHead", typeof(int?), isRequired: false),
            new(SectionDefs.Welding, "Welding", typeof(int?), isRequired: false),
            new(SectionDefs.Lubrication, "Lubrication", typeof(int?), isRequired: false),
            new(SectionDefs.Packing, "Packing", typeof(int?), isRequired: false),
            new(SectionDefs.Warehouse, "Warehouse", typeof(int?), isRequired: false),
            new(SectionDefs.Extra1, "Extra1", typeof(int?), isRequired: false),
            new(SectionDefs.Extra2, "Extra2", typeof(int?), isRequired: false),
        }, compositeKeyColumns: new[] { "BatchNo", "SequenceNumber" }),

        ["ProductionRecord"] = new EntityDef("批次-生产记录", "批次-生产记录", typeof(MES.Data.Entities.Batch.ProductionRecord), 8, null, new List<ColumnDef>
        {
            new("ID", "Id", typeof(int), isRequired: false, isSystem: true),
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("组内序号", null!) { IsFkColumn = true, FkEntityKey = "ProcessGroup", FkLookupProperty = "SequenceNumber", FkTargetProperty = "ProcessGroupId", FkRequiresJoin = true },
            new("工序名称", "ProcessName"),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("制造规格", "ManufacturingSpec"),
            new("工段名称", "SectionName"),
            new("执行序号", "SequenceNumber", typeof(int), isRequired: false),
            new("执行日期", "ExecDate", typeof(DateTime)),
            new("设备名称", "EquipmentName", typeof(string), isRequired: false),
            new("操作人", "Operator", typeof(string), isRequired: false),
            new("班次", "Shift", typeof(MES.Core.Enums.ShiftType), isEnum: true, isRequired: false),
            new("加工支数", "Quantity", typeof(int?), isRequired: false),
            new("加工重量(kg)", "Weight", typeof(decimal?), isRequired: false),
            new("固溶温度(℃)", "SolutionTemperature", typeof(decimal?), isRequired: false),
            new("保温时间(min)", "SoakTime", typeof(int?), isRequired: false),
            new("产类", "ProductStatus", typeof(string), isRequired: false),
            new("长度状态", "LengthStatus", typeof(MES.Core.Enums.LengthStatus), isEnum: true, isRequired: false),
            new("断切倍数", "CuttingMultiple", typeof(decimal?), isRequired: false),
            new("成品断切长度(mm)", "FinishedCutLength", typeof(decimal?), isRequired: false),
            new("切后支数", "PostCutQuantity", typeof(int?), isRequired: false),
            new("平头数", "FaceCutCount", typeof(int?), isRequired: false),
            new("预成切", "IsPreCut", typeof(bool?), isRequired: false, valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("定尺切割匹配", "CutLengthMatchType", typeof(MES.Core.Enums.CutLengthMatchType), isEnum: true, isRequired: false, isSystem: true),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("数据来源", "DataSource", typeof(string), isRequired: false, isSystem: true),
        }),

        ["SectionOutsource"] = new EntityDef("批次-工段委外", "批次-工段委外", typeof(MES.Data.Entities.Batch.SectionOutsource), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("组内序号", null!) { IsFkColumn = true, FkEntityKey = "ProcessGroup", FkLookupProperty = "SequenceNumber", FkTargetProperty = "ProcessGroupId", FkRequiresJoin = true },
            new("工序名称", "ProcessName"),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("委外规格", "OutsourceSpec", typeof(string), isRequired: false),
            new("制造规格", "ManufacturingSpec"),
            new("工段名称", "SectionName"),
            new("执行序号", "SequenceNumber", typeof(int), isRequired: false),
            new("委外单位", "OutsourceVendor"),
            new("发出日期", "SendOutDate", typeof(DateTime)),
            new("发出支数", "SendQuantity", typeof(int?), isRequired: false),
            new("发出重量(kg)", "SendWeight", typeof(decimal?)),
            new("要求收回日期", "ExpectedReturnDate", typeof(DateTime?), isRequired: false),
            new("是否紧急", "IsUrgent", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("是否厂内", "IsInternal", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("产类", "ProductStatus", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("状态", "Status", typeof(MES.Core.Enums.SectionOutsourceStatus), isEnum: true),
            new("数据来源", "DataSource", typeof(string), isRequired: false, isSystem: true),
        }),

        ["OutsourceRecovery"] = new EntityDef("批次-委外回收", "批次-委外回收", typeof(MES.Data.Entities.Batch.OutsourceRecovery), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "SectionOutsource", FkLookupProperty = "BatchNo", FkTargetProperty = "SectionOutsourceId", FkRequiresJoin = true },
            new("工段名称", null!) { IsFkColumn = true, FkEntityKey = "SectionOutsource", FkLookupProperty = "SectionName", FkTargetProperty = "SectionOutsourceId", FkRequiresJoin = true },
            new("委外单位", null!) { IsFkColumn = true, FkEntityKey = "SectionOutsource", FkLookupProperty = "OutsourceVendor", FkTargetProperty = "SectionOutsourceId", FkRequiresJoin = true },
            new("回收日期", "RecoveryDate", typeof(DateTime)),
            new("回收支数", "RecoveryQuantity", typeof(int?), isRequired: false),
            new("回收重量(kg)", "RecoveryWeight", typeof(decimal?)),
            new("未加工支数", "UnprocessedQuantity", typeof(int?), isRequired: false),
            new("未加工重量(kg)", "UnprocessedWeight", typeof(decimal?), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("数据来源", "DataSource", typeof(string), isRequired: false, isSystem: true),
        }),

        ["MaterialReceiveCheck"] = new EntityDef("质量-成检到料", "质量-成检到料", typeof(MES.Data.Entities.Quality.MaterialReceiveCheck), 8, null,
            new List<ColumnDef>
        {
            new("批次号", "BatchNo") { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("到料日期", "ReceiveDate", typeof(DateTime)),
            new("工序序号", null!) { IsFkColumn = true, FkEntityKey = "ProcessGroup", FkLookupProperty = "SequenceNumber", FkTargetProperty = "ProcessGroupId", FkRequiresJoin = true },
            new("工序名称", "ProcessName"),
            new("执行序号", "SequenceNumber", typeof(int), isRequired: false),
            new("成检类型", "InspectionType", typeof(MES.Core.Enums.InspectionType), isEnum: true, isRequired: false),
            new("强制完成", "IsForceCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("班次", "Shift", typeof(MES.Core.Enums.ShiftType), isEnum: true, isRequired: false),
            new("确认人", "Checker", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("数据来源", "DataSource", typeof(string), isRequired: false, isSystem: true),
        }, compositeKeyColumns: new[] { "BatchNo" }),

        ["ProcessInspection"] = new EntityDef("质量-过程检验", "质量-过程检验", typeof(MES.Data.Entities.Quality.ProcessInspection), 8, null,
            new List<ColumnDef>
        {
            new("批次号", "BatchNo") { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("组内序号", null!) { IsFkColumn = true, FkEntityKey = "ProcessGroup", FkLookupProperty = "SequenceNumber", FkTargetProperty = "ProcessGroupId", FkRequiresJoin = true },
            new("工序名称", "ProcessName"),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("制造规格", "ManufacturingSpec"),
            new("工段名称", "SectionName"),
            new("执行序号", "SequenceNumber", typeof(int), isRequired: false),
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("设备名称", "EquipmentName", typeof(string), isRequired: false),
            new("检验员", "Inspector", typeof(string), isRequired: false),
            new("班次", "Shift", typeof(MES.Core.Enums.ShiftType), isEnum: true, isRequired: false),
            new("产类", "ProductStatus", typeof(string), isRequired: false),
            new("检验支数", "Quantity", typeof(int?), isRequired: false),
            new("检验重量(kg)", "Weight", typeof(decimal?), isRequired: false),
            new("检验项目", "InspectionItem", typeof(MES.Core.Enums.InspectionItem), isEnum: true, isRequired: false),
            new("合格支数", "QualifiedQuantity", typeof(int?), isRequired: false),
            new("合格重量(kg)", "QualifiedWeight", typeof(decimal?), isRequired: false),
            new("合格中让步放行支", "QualifiedConcessionQuantity", typeof(int?), isRequired: false),
            new("让步说明", "ConcessionRemark", typeof(string), isRequired: false),
            new("不合格返整支数", "DefectReworkQuantity", typeof(int?), isRequired: false),
            new("不合格入库支数", "DefectWarehouseQuantity", typeof(int?), isRequired: false),
            new("不合格报废支数", "DefectScrapQuantity", typeof(int?), isRequired: false),
            new("不合格描述", "DefectDescription", typeof(string), isRequired: false),
            new("理论返整重(kg)", "TheoreticalReworkWeight", typeof(int?), isRequired: false, isSystem: true),
            new("理论入库重(kg)", "TheoreticalWarehouseWeight", typeof(int?), isRequired: false, isSystem: true),
            new("理论报废重(kg)", "TheoreticalScrapWeight", typeof(int?), isRequired: false, isSystem: true),
            new("来料单位", "SourceUnit", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("数据来源", "DataSource", typeof(string), isRequired: false, isSystem: true),
        }, compositeKeyColumns: new[] { "BatchNo", "ProcessName", "ManufacturingSpec", "SectionName", "InspectionDate" }),

        ["FinalInspection"] = new EntityDef("质量-成品检验", "质量-成品检验", typeof(MES.Data.Entities.Quality.FinalInspection), 8, null, new List<ColumnDef>
        {
            new("生产编号", "BatchNo") { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("定尺长度", "FixedLength", typeof(string), isRequired: false),
            new("非定尺长度范围", "NonFixedLengthRange", typeof(string), isRequired: false),
            new("定尺切割匹配", "CutLengthMatchType", typeof(MES.Core.Enums.CutLengthMatchType), isEnum: true, isRequired: false, isSystem: true),
            new("成检类型", "InspectionType", typeof(MES.Core.Enums.InspectionType), isEnum: true, isRequired: false),
            new("检验项目", "InspectionItem", typeof(MES.Core.Enums.InspectionItem), isEnum: true),
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("设备名称", "EquipmentName", typeof(string), isRequired: false),
            new("班次", "Shift", typeof(MES.Core.Enums.ShiftType), isEnum: true, isRequired: false),
            new("操作员", "Operator", typeof(string), isRequired: false),
            new("检验支数", "Quantity", typeof(int?), isRequired: false),
            new("检验重量(kg)", "Weight", typeof(int?), isRequired: false),
            new("合格支数", "QualifiedQuantity", typeof(int?), isRequired: false),
            new("合格重量(kg)", "QualifiedWeight", typeof(int?), isRequired: false),
            new("合格中让步放行支", "QualifiedConcessionQuantity", typeof(int?), isRequired: false),
            new("让步说明", "ConcessionRemark", typeof(string), isRequired: false),
            new("不合格返整支数", "DefectReworkQuantity", typeof(int?), isRequired: false),
            new("不合格入库支数", "DefectWarehouseQuantity", typeof(int?), isRequired: false),
            new("不合格报废支数", "DefectScrapQuantity", typeof(int?), isRequired: false),
            new("次品返整重量(kg)", "DefectReworkWeight", typeof(int?), isRequired: false),
            new("次品入库重量(kg)", "DefectWarehouseWeight", typeof(int?), isRequired: false),
            new("次品报废重量(kg)", "DefectScrapWeight", typeof(int?), isRequired: false),
            new("不合格情况描述", "DefectDescription", typeof(string), isRequired: false),
            new("外径范围", "OuterDiameterRange", typeof(string), isRequired: false),
            new("壁厚范围", "WallThicknessRange", typeof(string), isRequired: false),
            new("长度余量范围", "LengthAllowanceRange", typeof(string), isRequired: false),
            new("压力Mpa", "Pressure", typeof(decimal?), isRequired: false),
            new("保压时间s", "HoldTime", typeof(int?), isRequired: false),
            // 涡流/超声波探伤专用字段（仅 InspectionItem=EddyCurrent/Ultrasonic 时有效）
            new("资格等级", "QualificationLevel", typeof(string), isRequired: false),
            new("检验标准", "InspectionStandard", typeof(string), isRequired: false),
            new("检验等级", "InspectionGrade", typeof(string), isRequired: false),
            new("检验仪器型号", "InstrumentModel", typeof(string), isRequired: false),
            new("检验方式", "NdtMethod", typeof(string), isRequired: false),
            new("标样尺寸", "StandardSampleSize", typeof(string), isRequired: false),
            new("标样人工缺陷", "StandardSampleDefect", typeof(string), isRequired: false),
            new("探头类型", "ProbeType", typeof(string), isRequired: false),
            new("耦合剂", "Couplant", typeof(string), isRequired: false),
            new("检测设备校准频率", "CalibrationFrequency", typeof(string), isRequired: false),
            new("检测频率", "DetectionFrequency", typeof(string), isRequired: false),
            new("检测灵敏度", "DetectionSensitivity", typeof(string), isRequired: false),
            new("检测相位", "DetectionPhase", typeof(string), isRequired: false),
            new("检测速度", "DetectionSpeed", typeof(string), isRequired: false),
            new("检验备注", "Remark", typeof(string), isRequired: false),
            new("数据来源", "DataSource", typeof(string), isRequired: false, isSystem: true),
        }),

        ["HardnessTest"] = new EntityDef("质量-硬度检验", "质量-硬度检验", typeof(MES.Data.Entities.Quality.HardnessTest), 8, null, new List<ColumnDef>
        {
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("检验员", "Inspector"),
            new("炉批号", "FurnaceNo"),
            new("牌号", "Grade"),
            new("规格", "Specification"),
            new("试样编号", "SampleNo", typeof(int?), isRequired: false),
            new("试样尺寸", "SampleSize", typeof(string), isRequired: false),
            new("检验标准", "InspectionStandard", typeof(string), isRequired: false),
            new("硬度模式", "HardnessMode", typeof(string), isRequired: false),
            new("硬度测定值", "HardnessValue", typeof(string), isRequired: false),
            new("判定", "Judgment", typeof(string), isRequired: false),
        }),

        ["GrainSizeTest"] = new EntityDef("质量-晶粒度检验", "质量-晶粒度检验", typeof(MES.Data.Entities.Quality.GrainSizeTest), 8, null, new List<ColumnDef>
        {
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("检验员", "Inspector"),
            new("炉批号", "FurnaceNo"),
            new("牌号", "Grade"),
            new("规格", "Specification"),
            new("试样编号", "SampleNo", typeof(int?), isRequired: false),
            new("试样尺寸", "SampleSize", typeof(string), isRequired: false),
            new("检验标准", "InspectionStandard", typeof(string), isRequired: false),
            new("晶粒度级别", "GrainSizeGrade", typeof(string), isRequired: false),
            new("晶粒度测定方法", "GrainSizeMethod", typeof(string), isRequired: false),
            new("观察倍数", "Magnification", typeof(string), isRequired: false),
            new("判定", "Judgment", typeof(string), isRequired: false),
        }),

        ["PittingCorrosionTest"] = new EntityDef("质量-点腐蚀检验", "质量-点腐蚀检验", typeof(MES.Data.Entities.Quality.PittingCorrosionTest), 8, null, new List<ColumnDef>
        {
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("检验员", "Inspector"),
            new("生产编号", "FurnaceNo"),
            new("牌号", "Grade"),
            new("规格", "Specification"),
            new("试样编号", "SampleNo", typeof(int?), isRequired: false),
            new("试样尺寸(mm)", "SampleSize", typeof(string), isRequired: false),
            new("检验标准", "InspectionStandard", typeof(string), isRequired: false),
            new("试样研磨粒度", "PolishingGrade", typeof(string), isRequired: false),
            new("试样原始重量mg", "RawWeight", typeof(decimal?), isRequired: false),
            new("浸蚀溶液", "CorrosionSolution", typeof(string), isRequired: false),
            new("浸蚀温度", "CorrosionTemperature", typeof(string), isRequired: false),
            new("浸蚀时间", "CorrosionTime", typeof(string), isRequired: false),
            new("浸蚀后试样重量mg", "FinalWeight", typeof(decimal?), isRequired: false),
            new("腐蚀率g/(m2.h)", "CorrosionRate", typeof(decimal?), isRequired: false),
            new("腐蚀最大孔深mm", "MaxPitDepth", typeof(decimal?), isRequired: false),
            new("判定", "Judgment", typeof(string), isRequired: false),
        }),

        ["IntergranularCorrosionTest"] = new EntityDef("质量-晶间腐蚀检验", "质量-晶间腐蚀检验", typeof(MES.Data.Entities.Quality.IntergranularCorrosionTest), 8, null, new List<ColumnDef>
        {
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("检验员", "Inspector"),
            new("生产编号", "FurnaceNo"),
            new("牌号", "Grade"),
            new("规格", "Specification"),
            new("试样编号", "SampleNo", typeof(int?), isRequired: false),
            new("试样尺寸", "SampleSize", typeof(string), isRequired: false),
            new("检验标准", "InspectionStandard", typeof(string), isRequired: false),
            new("试样敏化温度", "SensitizationTemperature", typeof(string), isRequired: false),
            new("敏化持续时间", "SensitizationDuration", typeof(string), isRequired: false),
            new("浸蚀溶液", "CorrosionSolution", typeof(string), isRequired: false),
            new("浸蚀时间", "CorrosionTime", typeof(string), isRequired: false),
            new("试样弯曲度数", "BendDegree", typeof(string), isRequired: false),
            new("观察放大倍数", "Magnification", typeof(string), isRequired: false),
            new("观察结果", "ObservationResult", typeof(string), isRequired: false),
            new("判定", "Judgment", typeof(string), isRequired: false),
        }),

        ["TensileTest"] = new EntityDef("质量-室温拉伸检验", "质量-室温拉伸检验", typeof(MES.Data.Entities.Quality.TensileTest), 8, null, new List<ColumnDef>
        {
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("检验员", "Inspector"),
            new("生产编号", "FurnaceNo"),
            new("牌号", "Grade"),
            new("规格", "Specification"),
            new("试样编号", "SampleNo", typeof(int?), isRequired: false),
            new("试样尺寸(mm)", "SampleSize", typeof(string), isRequired: false),
            new("检验标准", "InspectionStandard", typeof(string), isRequired: false),
            new("原始标距(mm)", "OriginalGaugeLength", typeof(decimal?), isRequired: false),
            new("断后标距(mm)", "FinalGaugeLength", typeof(decimal?), isRequired: false),
            new("抗拉强度(MPa)", "TensileStrength", typeof(decimal?), isRequired: false),
            new("屈服强度Rp0.2", "YieldStrengthRp02", typeof(decimal?), isRequired: false),
            new("屈服强度Rp1", "YieldStrengthRp1", typeof(decimal?), isRequired: false),
            new("延伸率(%)", "Elongation", typeof(decimal?), isRequired: false),
            new("判定", "Judgment", typeof(string), isRequired: false),
        }),

        ["MetallographicTest"] = new EntityDef("质量-金相检验", "质量-金相检验", typeof(MES.Data.Entities.Quality.MetallographicTest), 8, null, new List<ColumnDef>
        {
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("检验员", "Inspector"),
            new("生产编号", "FurnaceNo"),
            new("牌号", "Grade"),
            new("规格", "Specification"),
            new("试样编号", "SampleNo", typeof(int?), isRequired: false),
            new("试样尺寸(mm)", "SampleSize", typeof(string), isRequired: false),
            new("检验标准", "InspectionStandard", typeof(string), isRequired: false),
            new("浸蚀方式", "EtchingMethod", typeof(string), isRequired: false),
            new("电解电压", "ElectrolyticVoltage", typeof(string), isRequired: false),
            new("电解时间", "ElectrolyticTime", typeof(string), isRequired: false),
            new("检测观察倍数", "Magnification", typeof(string), isRequired: false),
            new("对照测定铁素体含量(%)", "FerriteContent", typeof(decimal?), isRequired: false),
            new("判定", "Judgment", typeof(string), isRequired: false),
        }),

        ["FlatteningTest"] = new EntityDef("质量-压扁检验", "质量-压扁检验", typeof(MES.Data.Entities.Quality.FlatteningTest), 8, null, new List<ColumnDef>
        {
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("检验员", "Inspector"),
            new("生产编号", "FurnaceNo"),
            new("牌号", "Grade"),
            new("规格", "Specification"),
            new("试样编号", "SampleNo", typeof(int?), isRequired: false),
            new("试样尺寸(mm)", "SampleSize", typeof(string), isRequired: false),
            new("检验标准", "InspectionStandard", typeof(string), isRequired: false),
            new("压后平板间距(mm)", "FlatteningGap", typeof(decimal?), isRequired: false),
            new("观察", "Observation", typeof(string), isRequired: false),
            new("判定", "Judgment", typeof(string), isRequired: false),
        }),

        ["FlaringTest"] = new EntityDef("质量-扩口检验", "质量-扩口检验", typeof(MES.Data.Entities.Quality.FlaringTest), 8, null, new List<ColumnDef>
        {
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("检验员", "Inspector"),
            new("生产编号", "FurnaceNo"),
            new("牌号", "Grade"),
            new("规格", "Specification"),
            new("试样编号", "SampleNo", typeof(int?), isRequired: false),
            new("试样尺寸(mm)", "SampleSize", typeof(string), isRequired: false),
            new("检验标准", "InspectionStandard", typeof(string), isRequired: false),
            new("顶心锥度", "MandrelTaper", typeof(string), isRequired: false),
            new("扩后外径(mm)", "FlaredDiameter", typeof(decimal?), isRequired: false),
            new("扩口率(%)", "FlaringRate", typeof(decimal?), isRequired: false),
            new("观察", "Observation", typeof(string), isRequired: false),
            new("判定", "Judgment", typeof(string), isRequired: false),
        }),

        ["ChemicalAnalysis"] = new EntityDef("质量-化学分析", "质量-化学分析", typeof(MES.Data.Entities.Quality.ChemicalAnalysis), 8, null, new List<ColumnDef>
        {
            new("分析日期", "AnalysisDate", typeof(DateTime)),
            new("分析员", "Analyst"),
            new("炉号", "FurnaceNo"),
            new("牌号", "Grade"),
            new("分析次数", "AnalysisCount", typeof(int?), isRequired: false),
            new("分析标准", "AnalysisStandard", typeof(string), isRequired: false),
            new("C%", "C", typeof(decimal?), isRequired: false),
            new("Si%", "Si", typeof(decimal?), isRequired: false),
            new("Mn%", "Mn", typeof(decimal?), isRequired: false),
            new("P%", "P", typeof(decimal?), isRequired: false),
            new("S%", "S", typeof(decimal?), isRequired: false),
            new("Ni%", "Ni", typeof(decimal?), isRequired: false),
            new("Cr%", "Cr", typeof(decimal?), isRequired: false),
            new("Mo%", "Mo", typeof(decimal?), isRequired: false),
            new("Cu%", "Cu", typeof(decimal?), isRequired: false),
            new("N%", "N", typeof(decimal?), isRequired: false),
            new("Nb%", "Nb", typeof(decimal?), isRequired: false),
            new("Ti%", "Ti", typeof(decimal?), isRequired: false),
            new("Fe%", "Fe", typeof(decimal?), isRequired: false),
            new("Al%", "Al", typeof(decimal?), isRequired: false),
            new("W%", "W", typeof(decimal?), isRequired: false),
        }),

        // === 去油/酸洗入缸记录（依赖 ProductionBatch + ProcessGroup） ===
        ["PicklingInRecord"] = new EntityDef("批次-去油酸洗入缸记录", "批次-去油酸洗入缸记录", typeof(MES.Data.Entities.Batch.PicklingInRecord), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("工序名称", "ProcessName"),
            new("制造规格", "ManufacturingSpec", typeof(string), isRequired: false),
            new("工段名称", "SectionName"),
            new("组内序号", null!) { IsFkColumn = true, FkEntityKey = "ProcessGroup", FkLookupProperty = "SequenceNumber", FkTargetProperty = "ProcessGroupId", FkRequiresJoin = true },
            new("执行序号", "SequenceNumber", typeof(int), isRequired: false),
            new("入缸日期", "InDate", typeof(DateTime)),
            new("状态", "Status", typeof(MES.Core.Enums.PicklingStatus), isEnum: true),
            new("设备名称", "EquipmentName", typeof(string), isRequired: false),
            new("操作人", "Operator", typeof(string), isRequired: false),
            new("班次", "Shift", typeof(MES.Core.Enums.ShiftType), isEnum: true, isRequired: false),
            new("加工支数", "Quantity", typeof(int?), isRequired: false),
            new("加工重量(kg)", "Weight", typeof(decimal?), isRequired: false),
            new("产类", "ProductStatus", typeof(string), isRequired: false),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("数据来源", "DataSource", typeof(string), isRequired: false, isSystem: true),
        }),

        // === 去油/酸洗完工记录（依赖 PicklingInRecord） ===
        ["PicklingOutRecord"] = new EntityDef("批次-去油酸洗完工记录", "批次-去油酸洗完工记录", typeof(MES.Data.Entities.Batch.PicklingOutRecord), 8, null, new List<ColumnDef>
        {
            new("入缸批次号", null!) { IsFkColumn = true, FkEntityKey = "PicklingInRecord", FkLookupProperty = "BatchNo", FkTargetProperty = "PicklingInRecordId", FkRequiresJoin = true },
            new("入缸工段", null!) { IsFkColumn = true, FkEntityKey = "PicklingInRecord", FkLookupProperty = "SectionName", FkTargetProperty = "PicklingInRecordId", FkRequiresJoin = true },
            // 系统回写快照（由入缸记录复制），仅导出不导入，防手工写入与 FK 解析结果不一致
            new("生产批次ID", "ProductionBatchId", typeof(int), isSystem: true, isRequired: false),
            new("批次号", "BatchNo", typeof(string), isRequired: false),
            new("制造规格", "ManufacturingSpec", typeof(string), isRequired: false),
            new("工段名称", "SectionName"),
            new("设备名称", "EquipmentName", typeof(string), isRequired: false),
            new("操作人", "Operator", typeof(string), isRequired: false),
            // 班次与入缸记录一致：string 属性存 ShiftType 枚举名，导出/导入走 EnumHelper 中文转换
            new("班次", "Shift", typeof(MES.Core.Enums.ShiftType), isEnum: true, isRequired: false),
            new("加工支数", "Quantity", typeof(int?), isRequired: false),
            new("加工重量(kg)", "Weight", typeof(decimal?), isRequired: false),
            new("产类", "ProductStatus", typeof(string), isRequired: false),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("工序名称", "ProcessName", typeof(string), isRequired: false),
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("完工日期", "CompleteDate", typeof(DateTime)),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("数据来源", "DataSource", typeof(string), isRequired: false, isSystem: true),
        }),

        ["OperationLog"] = new EntityDef("系统-操作日志", "系统-操作日志", typeof(MES.Data.Entities.Infrastructure.OperationLog), 8, null, new List<ColumnDef>
        {
            new("模块", "Module"),
            new("业务主键", "EntityId", typeof(int)),
            new("操作类型", "OperationType"),
            new("操作详情", "Detail", typeof(string), isRequired: false),
        }),

        ["InventoryBatch"] = new EntityDef("仓库-库存批次", "仓库-库存批次", typeof(MES.Data.Entities.Warehouse.InventoryBatch), 8, "BatchNo", new List<ColumnDef>
        {
            new("批次号", "BatchNo"),
            new("仓库编码", null!) { IsFkColumn = true, FkEntityKey = "Warehouse", FkLookupProperty = "Code", FkTargetProperty = "WarehouseId" },
            new("物料类型", "MaterialType", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("厂内钢种", "PlantGrade"),
            new("名义规格", "Specification"),
            new("入库来源", "InboundSource", typeof(MES.Core.Enums.InboundSource), isEnum: true),
            new("来料单位", "SourceName"),
            new("入库日期", "InboundDate", typeof(DateTime)),
            new("炉号", "HeatNo", typeof(string), isRequired: false),
            new("生产批号", "ProductionBatchNo", typeof(string), isRequired: false),
            new("长度状态", "LengthStatus", typeof(MES.Core.Enums.LengthStatus), isEnum: true, isRequired: false),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("定尺切割匹配", "CutLengthMatchType", typeof(MES.Core.Enums.CutLengthMatchType), isEnum: true, isRequired: false, isSystem: true),
            new("入库支数", "InitialQuantity", typeof(int)),
            new("入库重量(kg)", "InitialWeight", typeof(decimal)),
            new("理论单支重(kg)", "UnitWeight", typeof(decimal?), isRequired: false),
            new("米数(m)", "Meters", typeof(decimal?), isRequired: false),
            new("当前剩余支数", "RemainingQuantity", typeof(int)),
            new("当前剩余重量(kg)", "RemainingWeight", typeof(decimal)),
            new("当前剩余米数(m)", "RemainingMeters", typeof(decimal?), isRequired: false),
            new("实际规格", "ActualSpecification", typeof(string), isRequired: false),
            new("制造状态", "ManufacturingStatus", typeof(MES.Core.Enums.DeliveryState), isEnum: true, isRequired: false),
            new("放置区域", "LocationArea", typeof(string), isRequired: false),
            new("放置架号", "LocationRack", typeof(string), isRequired: false),
            new("次品原因", "DefectReason", typeof(string), isRequired: false),
            new("责任类型", "LiabilityType", typeof(string), isRequired: false),
            new("原始来料单位", "OriginalSupplier", typeof(string), isRequired: false),
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("次品备注", "DefectRemark", typeof(string), isRequired: false),
            new("工单号", "WorkOrderNo", typeof(string), isRequired: false),
            new("订单号", "SalesOrderNo", typeof(string), isRequired: false),
            new("主号", "ProductionMainNo", typeof(string), isRequired: false),
            new("是否关联工单", "IsLinkedToWorkOrder", typeof(bool)),
            new("项次ID", "OrderItemIds", typeof(string), isRequired: false),
            new("来源单号", "SourceOrderNo", typeof(string), isRequired: false),
            new("来源序号", "SourceOrderSequence", typeof(int?), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["OutboundRecord"] = new EntityDef("仓库-出库记录", "仓库-出库记录", typeof(MES.Data.Entities.Warehouse.OutboundRecord), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "InventoryBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "InventoryBatchId" },
            new("出库类型", "OutboundType", typeof(MES.Core.Enums.OutboundType), isEnum: true),
            new("仓库批次号", "BatchNo", typeof(string), isRequired: false),
            new("出库工单号", "WorkOrderNo", typeof(string), isRequired: false),
            new("退货-原仓库批", "ReturnSourceBatchNo", typeof(string), isRequired: false),
            new("委外-穿孔号", "SourceOrderNo", typeof(string), isRequired: false),
            new("目标单位", "TargetCompany", typeof(string), isRequired: false),
            new("出库支数", "OutboundQuantity", typeof(int)),
            new("出库重量(kg)", "OutboundWeight", typeof(decimal)),
            new("出库米数(m)", "OutboundMeters", typeof(decimal?), isRequired: false),
            new("出库日期", "OutboundDate", typeof(DateTime)),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 第9批：各类计划 ===
        ["InventoryPlan"] = new EntityDef("工单-库存计划", "工单-库存计划", typeof(InventoryPlan), 9, null, new List<ColumnDef>
        {
            new("工单号", null!) { IsFkColumn = true, FkEntityKey = "WorkOrder", FkLookupProperty = "WorkOrderNo", FkTargetProperty = "WorkOrderId" },
            new("计划日期", "PlanDate", typeof(DateTime)),
            new("库存批次号", "InventoryBatchNo"),
            new("批次号", "BatchNo"),
            new("物料名称", "MaterialType", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("工厂牌号", "PlantGrade"),
            new("规格", "Specification"),
            new("放置区域", "LocationArea", typeof(string), isRequired: false),
            new("放置架号", "LocationRack", typeof(string), isRequired: false),
            new("投料制成倍", "InputMultiple", typeof(int)),
            new("使用模式", "UsageMode"),
            new("出库支数", "UsedQuantity", typeof(int?), isRequired: false),
            new("出库重量(kg)", "UsedWeight", typeof(decimal)),
            new("要求到位日期", "RequiredDate", typeof(DateTime?), isRequired: false),
            new("计划状态", "PlanStatus", typeof(MES.Core.Enums.InventoryPlanStatus), isEnum: true),
            new("改制类型", "ReworkType", typeof(MES.Core.Enums.ReworkType?), isEnum: true, isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("工艺周期", "StandardCycle", typeof(int), isRequired: false),
        }),

        ["PurchaseSemiPlan"] = new EntityDef("工单-荒管采购计划", "工单-荒管采购计划", typeof(PurchaseSemiPlan), 9, null, new List<ColumnDef>
        {
            new("工单号", null!) { IsFkColumn = true, FkEntityKey = "WorkOrder", FkLookupProperty = "WorkOrderNo", FkTargetProperty = "WorkOrderId" },
            new("计划日期", "PlanDate", typeof(DateTime)),
            new("调整成品壁厚(mm)", "AdjustedWallThickness", typeof(decimal)),
            new("成材率(%)", "YieldRate", typeof(decimal)),
            new("投料制成倍", "InputMultiple", typeof(int)),
            new("正品率(%)", "QualifiedRate", typeof(decimal)),
            new("密度(g/cm³)", "Density", typeof(decimal?), isRequired: false, isSystem: true),
            new("成品单重(kg/支)", "UnitWeight", typeof(decimal?), isRequired: false, isSystem: true),
            new("原料单重(kg/支)", "RawUnitWeight", typeof(decimal?), isRequired: false, isSystem: true),
            new("原料类型", "RawMaterialType", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("工厂牌号", "PlantGrade"),
            new("原料规格", "RawMaterialSpec"),
            new("需求单重(kg/支)", "RequiredUnitWeight", typeof(decimal?), isRequired: false),
            new("需求支数", "RequiredPieces", typeof(int?), isRequired: false),
            new("需求重量(kg)", "RequiredWeight", typeof(decimal)),
            new("要求到货日期", "RequiredDate", typeof(DateTime), isRequired: true),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("工艺周期", "StandardCycle", typeof(int), isRequired: false),
        }),

        ["PurchaseFinishedPlan"] = new EntityDef("工单-成品采购计划", "工单-成品采购计划", typeof(PurchaseFinishedPlan), 9, null, new List<ColumnDef>
        {
            new("工单号", null!) { IsFkColumn = true, FkEntityKey = "WorkOrder", FkLookupProperty = "WorkOrderNo", FkTargetProperty = "WorkOrderId" },
            new("计划日期", "PlanDate", typeof(DateTime)),
            new("成品类型", "ProductType", typeof(MES.Core.Enums.FinishedProductType), isEnum: true),
            new("工厂牌号", "PlantGrade"),
            new("规格", "Specification"),
            new("外径负公差(mm)", "OuterDiameterNegative", typeof(decimal)),
            new("外径正公差(mm)", "OuterDiameterPositive", typeof(decimal)),
            new("壁厚负公差(mm)", "WallThicknessNegative", typeof(decimal)),
            new("壁厚正公差(mm)", "WallThicknessPositive", typeof(decimal)),
            new("长度状态", "LengthStatus", typeof(MES.Core.Enums.LengthStatus), isEnum: true),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("交货状态", "DeliveryState", typeof(MES.Core.Enums.DeliveryState), isEnum: true),
            new("采购支数", "RequiredPiece", typeof(int?), isRequired: false),
            new("采购重量(kg)", "RequiredWeight", typeof(decimal)),
            new("投料制成倍", "InputMultiple", typeof(int?), isRequired: false),
            new("要求到货日期", "RequiredDate", typeof(DateTime?), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("工艺周期", "StandardCycle", typeof(int), isRequired: false),
        }),

        // === 独立实体：工厂牌号化学成分 ===
        ["ChemicalComposition"] = new EntityDef("标准-工厂牌号化学成分", "标准-工厂牌号化学成分", typeof(MES.Data.Entities.StandardRegister.ChemicalComposition), 1, "PlantGrade", new List<ColumnDef>
        {
            new("工厂牌号", "PlantGrade"),
            new("C", "Carbon", typeof(string), isRequired: false),
            new("Si", "Silicon", typeof(string), isRequired: false),
            new("Mn", "Manganese", typeof(string), isRequired: false),
            new("P", "Phosphorus", typeof(string), isRequired: false),
            new("S", "Sulfur", typeof(string), isRequired: false),
            new("Ni", "Nickel", typeof(string), isRequired: false),
            new("Cr", "Chromium", typeof(string), isRequired: false),
            new("Mo", "Molybdenum", typeof(string), isRequired: false),
            new("Cu", "Copper", typeof(string), isRequired: false),
            new("N", "Nitrogen", typeof(string), isRequired: false),
            new("Nb", "Niobium", typeof(string), isRequired: false),
            new("Ti", "Titanium", typeof(string), isRequired: false),
            new("Fe", "Iron", typeof(string), isRequired: false),
            new("Al", "Aluminum", typeof(string), isRequired: false),
            new("W", "Tungsten", typeof(string), isRequired: false),
            new("PREN腐蚀当量", "PREN", typeof(string), isRequired: false),
        }),

        // === 独立实体：牌号验证规则 ===
        ["ChemicalValidationRule"] = new EntityDef("标准-工厂牌号化分验证规则", "标准-工厂牌号化分验证规则", typeof(MES.Data.Entities.StandardRegister.ChemicalValidationRule), 1, "PlantGrade", new List<ColumnDef>
        {
            new("工厂牌号", "PlantGrade"),
            new("C-", "CMin", typeof(string), isRequired: false),
            new("C+", "CMax", typeof(string), isRequired: false),
            new("Si-", "SiMin", typeof(string), isRequired: false),
            new("Si+", "SiMax", typeof(string), isRequired: false),
            new("Mn-", "MnMin", typeof(string), isRequired: false),
            new("Mn+", "MnMax", typeof(string), isRequired: false),
            new("P-", "PMin", typeof(string), isRequired: false),
            new("P+", "PMax", typeof(string), isRequired: false),
            new("S-", "SMin", typeof(string), isRequired: false),
            new("S+", "SMax", typeof(string), isRequired: false),
            new("Ni-", "NiMin", typeof(string), isRequired: false),
            new("Ni+", "NiMax", typeof(string), isRequired: false),
            new("Cr-", "CrMin", typeof(string), isRequired: false),
            new("Cr+", "CrMax", typeof(string), isRequired: false),
            new("Mo-", "MoMin", typeof(string), isRequired: false),
            new("Mo+", "MoMax", typeof(string), isRequired: false),
            new("Cu-", "CuMin", typeof(string), isRequired: false),
            new("Cu+", "CuMax", typeof(string), isRequired: false),
            new("N-", "NMin", typeof(string), isRequired: false),
            new("N+", "NMax", typeof(string), isRequired: false),
            new("Nb-", "NbMin", typeof(string), isRequired: false),
            new("Nb+", "NbMax", typeof(string), isRequired: false),
            new("Ti-", "TiMin", typeof(string), isRequired: false),
            new("Ti+", "TiMax", typeof(string), isRequired: false),
            new("Fe-", "FeMin", typeof(string), isRequired: false),
            new("Fe+", "FeMax", typeof(string), isRequired: false),
            new("Al-", "AlMin", typeof(string), isRequired: false),
            new("Al+", "AlMax", typeof(string), isRequired: false),
            new("W-", "WMin", typeof(string), isRequired: false),
            new("W+", "WMax", typeof(string), isRequired: false),
            new("PREN腐蚀当量-", "PRENMin", typeof(string), isRequired: false),
        }),

        // === 独立实体：来料炉号登记 ===
        ["FurnaceRegistration"] = new EntityDef("质量-来料炉号登记", "质量-来料炉号登记", typeof(MES.Data.Entities.Quality.FurnaceRegistration), 1, null, new List<ColumnDef>
        {
            new("来料日期", "IncomingDate", typeof(DateTime)),
            new("原料单位", "RawMaterialUnit"),
            new("原料类型", "RawMaterialType", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("登记牌号", "RegisteredGrade"),
            new("关联工厂牌号", "RelatedPlantGrade", typeof(string), isRequired: false),
            new("炉号", "FurnaceNumber"),
            new("规格", "Specification", typeof(string), isRequired: false),
            new("支数", "Quantity", typeof(int?), isRequired: false),
            new("重量", "Weight", typeof(decimal?), isRequired: false),
            new("C", "Carbon", typeof(decimal?), isRequired: false),
            new("Si", "Silicon", typeof(decimal?), isRequired: false),
            new("Mn", "Manganese", typeof(decimal?), isRequired: false),
            new("P", "Phosphorus", typeof(decimal?), isRequired: false),
            new("S", "Sulfur", typeof(decimal?), isRequired: false),
            new("Ni", "Nickel", typeof(decimal?), isRequired: false),
            new("Cr", "Chromium", typeof(decimal?), isRequired: false),
            new("Mo", "Molybdenum", typeof(decimal?), isRequired: false),
            new("Cu", "Copper", typeof(decimal?), isRequired: false),
            new("N", "Nitrogen", typeof(decimal?), isRequired: false),
            new("Nb", "Niobium", typeof(decimal?), isRequired: false),
            new("Ti", "Titanium", typeof(decimal?), isRequired: false),
            new("Fe", "Iron", typeof(decimal?), isRequired: false),
            new("Al", "Aluminum", typeof(decimal?), isRequired: false),
            new("W", "Tungsten", typeof(decimal?), isRequired: false),
            new("PREN腐蚀当量", "PREN", typeof(decimal?), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === NCR 不合格品报告（独立实体，无外部FK依赖）===
        ["Ncr"] = new EntityDef("质量-不合格报告", "质量-不合格报告", typeof(MES.Data.Entities.Quality.Ncr), 1, null, new List<ColumnDef>
        {
            // G1: 问题反馈
            new("反馈日期", "ReportDate", typeof(DateTime)),
            new("反馈部门", "ReportDepartment", typeof(string), isRequired: false),
            new("反馈人", "Reporter", typeof(string), isRequired: false),
            new("钢管类别", "PipeCategory", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("生产编号", "BatchNo"),
            new("工单号", "WorkOrderNo", typeof(string), isRequired: false),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("规格", "Specification", typeof(string), isRequired: false),
            new("次品支数", "DefectiveQuantity", typeof(int?), isRequired: false),
            new("次品重量", "DefectiveWeight", typeof(int?), isRequired: false),
            new("问题描述", "ProblemDescription", typeof(string), isRequired: false),
            new("来源检验项目", "SourceInspectionItem", typeof(string), isRequired: false),
            // G2: 不合格品处置
            new("处置方式", "DisposalMethod", typeof(MES.Core.Enums.DisposalMethod?), isEnum: true, isRequired: false),
            new("处置备注", "DisposalRemark", typeof(string), isRequired: false),
            new("处置是否完结", "DisposalIsCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("处置完结日期", "DisposalCompleteDate", typeof(DateTime?), isRequired: false),
            // G3: 原因分析
            new("原因分析", "RootCauseAnalysis", typeof(string), isRequired: false),
            new("严重程度", "Severity", typeof(MES.Core.Enums.SeverityLevel?), isEnum: true, isRequired: false),
            new("分析确认人", "AnalysisConfirmer", typeof(string), isRequired: false),
            new("分析确认日期", "AnalysisConfirmDate", typeof(DateTime?), isRequired: false),
            // G4: 责任人及处理
            new("责任类别", "ResponsibilityCategory", typeof(string), isRequired: false),
            new("责任部门", "ResponsibleDept", typeof(string), isRequired: false),
            new("生产操作日期", "OperationDate", typeof(DateTime?), isRequired: false),
            new("生产责任人", "ResponsiblePerson", typeof(string), isRequired: false),
            new("责任人处理", "PersonDisposition", typeof(string), isRequired: false),
            new("责任人处理完结", "PersonIsCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("责任人处理完结日期", "PersonCompleteDate", typeof(DateTime?), isRequired: false),
            // G5: 纠正预防措施及结果验证
            new("纠正预防措施", "CorrectiveAction", typeof(string), isRequired: false),
            new("计划人", "ActionPlanner", typeof(string), isRequired: false),
            new("计划日期", "ActionPlanDate", typeof(DateTime?), isRequired: false),
            new("验证人", "ActionVerifier", typeof(string), isRequired: false),
            new("验证日期", "ActionVerifyDate", typeof(DateTime?), isRequired: false),
            new("结果判定", "ActionResult", typeof(string), isRequired: false),
            new("验证结论", "VerifyResult", typeof(MES.Core.Enums.VerifyResult?), isEnum: true, isRequired: false),
            // 状态
            new("状态", "Status", typeof(MES.Core.Enums.NcrStatus), isEnum: true),
        }),

        ["RoundBarPiercingPlan"] = new EntityDef("工单-圆棒穿孔计划", "工单-圆棒穿孔计划", typeof(MES.Data.Entities.WorkOrder.RoundBarPiercingPlan), 9, null, new List<ColumnDef>
        {
            new("工单号", null!) { IsFkColumn = true, FkEntityKey = "WorkOrder", FkLookupProperty = "WorkOrderNo", FkTargetProperty = "WorkOrderId" },
            new("计划日期", "PlanDate", typeof(DateTime)),
            new("调整成品壁厚(mm)", "AdjustedWallThickness", typeof(decimal)),
            new("成材率(%)", "YieldRate", typeof(decimal)),
            new("投料制成倍", "InputMultiple", typeof(int)),
            new("正品率(%)", "QualifiedRate", typeof(decimal)),
            new("密度(g/cm³)", "Density", typeof(decimal?), isRequired: false, isSystem: true),
            new("成品单重(kg/支)", "UnitWeight", typeof(decimal?), isRequired: false, isSystem: true),
            new("原料单重(kg/支)", "RawUnitWeight", typeof(decimal?), isRequired: false, isSystem: true),
            new("原料类型", "RawMaterialType", typeof(MES.Core.Enums.MaterialType), isEnum: true),
            new("工厂牌号", "PlantGrade"),
            new("圆棒规格", "RoundBarSpec"),
            new("穿孔规格", "PiercingSpec"),
            new("需求单重(kg/支)", "RequiredUnitWeight", typeof(decimal?), isRequired: false),
            new("需求支数", "RequiredPieces", typeof(int?), isRequired: false),
            new("需求重量(kg)", "RequiredWeight", typeof(decimal)),
            new("要求到货日期", "RequiredDate", typeof(DateTime), isRequired: true),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("工艺周期", "StandardCycle", typeof(int), isRequired: false),
        }),

        // === 用料计划工序组子实体（依赖各自父计划）===
        ["SemiPlanProcessGroup"] = new EntityDef("工单-荒管采购工序组", "工单-荒管采购工序组", typeof(MES.Data.Entities.WorkOrder.SemiPlanProcessGroup), 9, null, new List<ColumnDef>
        {
            new("所属荒管计划ID", "PurchaseSemiPlanId", typeof(int)),
            new("组内序号", "SequenceNumber", typeof(int)),
            new("工序名称", "ProcessName"),
            new("制造规格", "ManufacturingSpec", typeof(string), isRequired: false),
            new("外径公差", "OuterDiameterTolerance", typeof(string), isRequired: false),
            new("壁厚公差", "WallThicknessTolerance", typeof(string), isRequired: false),
            new("制造长度", "ManufacturingLength", typeof(string), isRequired: false),
            new("断切处理", "CuttingTreatment", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new(SectionDefs.ColdRollDraw, "ColdRollDraw", typeof(int?), isRequired: false),
            new(SectionDefs.OilPipeCut, "OilPipeCut", typeof(int?), isRequired: false),
            new(SectionDefs.Degrease, "Degrease", typeof(int?), isRequired: false),
            new(SectionDefs.EmulsionWash, "EmulsionWash", typeof(int?), isRequired: false),
            new(SectionDefs.UltrasonicWash, "UltrasonicWash", typeof(int?), isRequired: false),
            new(SectionDefs.ClothPolish, "ClothPolish", typeof(int?), isRequired: false),
            new(SectionDefs.BrightAnnealing, "BrightAnnealing", typeof(int?), isRequired: false),
            new(SectionDefs.Solution, "Solution", typeof(int?), isRequired: false),
            new(SectionDefs.Straighten, "Straighten", typeof(int?), isRequired: false),
            new(SectionDefs.Cut, "Cut", typeof(int?), isRequired: false),
            new(SectionDefs.ThicknessMeasure, "ThicknessMeasure", typeof(int?), isRequired: false),
            new(SectionDefs.Pickle, "Pickle", typeof(int?), isRequired: false),
            new(SectionDefs.OuterPolish, "OuterPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerPolish, "InnerPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerGrinding, "InnerGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.OuterSpotGrinding, "OuterSpotGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.SandBlasting, "SandBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.ShotBlasting, "ShotBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.Inspection, "Inspection", typeof(int?), isRequired: false),
            new(SectionDefs.WeldingHead, "WeldingHead", typeof(int?), isRequired: false),
            new(SectionDefs.Welding, "Welding", typeof(int?), isRequired: false),
            new(SectionDefs.Lubrication, "Lubrication", typeof(int?), isRequired: false),
            new(SectionDefs.Packing, "Packing", typeof(int?), isRequired: false),
            new(SectionDefs.Warehouse, "Warehouse", typeof(int?), isRequired: false),
            new(SectionDefs.Extra1, "Extra1", typeof(int?), isRequired: false),
            new(SectionDefs.Extra2, "Extra2", typeof(int?), isRequired: false),
        }, compositeKeyColumns: new[] { "PurchaseSemiPlanId", "SequenceNumber" }),

        ["InventoryPlanProcessGroup"] = new EntityDef("工单-库存计划工序组", "工单-库存计划工序组", typeof(InventoryPlanProcessGroup), 9, null, new List<ColumnDef>
        {
            new("所属库存计划ID", "InventoryPlanId", typeof(int)),
            new("组内序号", "SequenceNumber", typeof(int)),
            new("工序名称", "ProcessName"),
            new("制造规格", "ManufacturingSpec", typeof(string), isRequired: false),
            new("外径公差", "OuterDiameterTolerance", typeof(string), isRequired: false),
            new("壁厚公差", "WallThicknessTolerance", typeof(string), isRequired: false),
            new("制造长度", "ManufacturingLength", typeof(string), isRequired: false),
            new("断切处理", "CuttingTreatment", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new(SectionDefs.ColdRollDraw, "ColdRollDraw", typeof(int?), isRequired: false),
            new(SectionDefs.OilPipeCut, "OilPipeCut", typeof(int?), isRequired: false),
            new(SectionDefs.Degrease, "Degrease", typeof(int?), isRequired: false),
            new(SectionDefs.EmulsionWash, "EmulsionWash", typeof(int?), isRequired: false),
            new(SectionDefs.UltrasonicWash, "UltrasonicWash", typeof(int?), isRequired: false),
            new(SectionDefs.ClothPolish, "ClothPolish", typeof(int?), isRequired: false),
            new(SectionDefs.BrightAnnealing, "BrightAnnealing", typeof(int?), isRequired: false),
            new(SectionDefs.Solution, "Solution", typeof(int?), isRequired: false),
            new(SectionDefs.Straighten, "Straighten", typeof(int?), isRequired: false),
            new(SectionDefs.Cut, "Cut", typeof(int?), isRequired: false),
            new(SectionDefs.ThicknessMeasure, "ThicknessMeasure", typeof(int?), isRequired: false),
            new(SectionDefs.Pickle, "Pickle", typeof(int?), isRequired: false),
            new(SectionDefs.OuterPolish, "OuterPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerPolish, "InnerPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerGrinding, "InnerGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.OuterSpotGrinding, "OuterSpotGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.SandBlasting, "SandBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.ShotBlasting, "ShotBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.Inspection, "Inspection", typeof(int?), isRequired: false),
            new(SectionDefs.WeldingHead, "WeldingHead", typeof(int?), isRequired: false),
            new(SectionDefs.Welding, "Welding", typeof(int?), isRequired: false),
            new(SectionDefs.Lubrication, "Lubrication", typeof(int?), isRequired: false),
            new(SectionDefs.Packing, "Packing", typeof(int?), isRequired: false),
            new(SectionDefs.Warehouse, "Warehouse", typeof(int?), isRequired: false),
            new(SectionDefs.Extra1, "Extra1", typeof(int?), isRequired: false),
            new(SectionDefs.Extra2, "Extra2", typeof(int?), isRequired: false),
        }, compositeKeyColumns: new[] { "InventoryPlanId", "SequenceNumber" }),

        ["PiercingPlanProcessGroup"] = new EntityDef("工单-圆棒穿孔工序组", "工单-圆棒穿孔工序组", typeof(MES.Data.Entities.WorkOrder.PiercingPlanProcessGroup), 9, null, new List<ColumnDef>
        {
            new("所属穿孔计划ID", "RoundBarPiercingPlanId", typeof(int)),
            new("组内序号", "SequenceNumber", typeof(int)),
            new("工序名称", "ProcessName"),
            new("制造规格", "ManufacturingSpec", typeof(string), isRequired: false),
            new("外径公差", "OuterDiameterTolerance", typeof(string), isRequired: false),
            new("壁厚公差", "WallThicknessTolerance", typeof(string), isRequired: false),
            new("制造长度", "ManufacturingLength", typeof(string), isRequired: false),
            new("断切处理", "CuttingTreatment", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new(SectionDefs.ColdRollDraw, "ColdRollDraw", typeof(int?), isRequired: false),
            new(SectionDefs.OilPipeCut, "OilPipeCut", typeof(int?), isRequired: false),
            new(SectionDefs.Degrease, "Degrease", typeof(int?), isRequired: false),
            new(SectionDefs.EmulsionWash, "EmulsionWash", typeof(int?), isRequired: false),
            new(SectionDefs.UltrasonicWash, "UltrasonicWash", typeof(int?), isRequired: false),
            new(SectionDefs.ClothPolish, "ClothPolish", typeof(int?), isRequired: false),
            new(SectionDefs.BrightAnnealing, "BrightAnnealing", typeof(int?), isRequired: false),
            new(SectionDefs.Solution, "Solution", typeof(int?), isRequired: false),
            new(SectionDefs.Straighten, "Straighten", typeof(int?), isRequired: false),
            new(SectionDefs.Cut, "Cut", typeof(int?), isRequired: false),
            new(SectionDefs.ThicknessMeasure, "ThicknessMeasure", typeof(int?), isRequired: false),
            new(SectionDefs.Pickle, "Pickle", typeof(int?), isRequired: false),
            new(SectionDefs.OuterPolish, "OuterPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerPolish, "InnerPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerGrinding, "InnerGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.OuterSpotGrinding, "OuterSpotGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.SandBlasting, "SandBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.ShotBlasting, "ShotBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.Inspection, "Inspection", typeof(int?), isRequired: false),
            new(SectionDefs.WeldingHead, "WeldingHead", typeof(int?), isRequired: false),
            new(SectionDefs.Welding, "Welding", typeof(int?), isRequired: false),
            new(SectionDefs.Lubrication, "Lubrication", typeof(int?), isRequired: false),
            new(SectionDefs.Packing, "Packing", typeof(int?), isRequired: false),
            new(SectionDefs.Warehouse, "Warehouse", typeof(int?), isRequired: false),
            new(SectionDefs.Extra1, "Extra1", typeof(int?), isRequired: false),
            new(SectionDefs.Extra2, "Extra2", typeof(int?), isRequired: false),
        }, compositeKeyColumns: new[] { "RoundBarPiercingPlanId", "SequenceNumber" }),

        ["InProcessReworkPlan"] = new EntityDef("工单-在产改制计划", "工单-在产改制计划", typeof(MES.Data.Entities.WorkOrder.InProcessReworkPlan), 9, null, new List<ColumnDef>
        {
            new("所属工单ID", "WorkOrderId", typeof(int)),
            new("计划日期", "PlanDate", typeof(DateTime)),
            new("生产批次ID", "ProductionBatchId", typeof(int)),
            new("批次号", "BatchNo"),
            new("挂牌号", "BatchTagNo", typeof(string), isRequired: false),
            new("工厂牌号", "PlantGrade"),
            new("规格", "Specification"),
            new("长度状态", "LengthStatus", typeof(MES.Core.Enums.LengthStatus), isEnum: true),
            new("投料制成倍", "InputMultiple", typeof(int)),
            new("使用支数", "UsedQuantity", typeof(int?), isRequired: false),
            new("使用重量", "UsedWeight", typeof(decimal)),
            new("要求到货日", "RequiredDate", typeof(DateTime?), isRequired: false),
            new("计划状态", "PlanStatus", typeof(MES.Core.Enums.InventoryPlanStatus), isEnum: true),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("工艺周期(天)", "StandardCycle", typeof(int)),
        }),

        ["InProcessReworkPlanProcessGroup"] = new EntityDef("工单-在产改制工序组", "工单-在产改制工序组", typeof(MES.Data.Entities.WorkOrder.InProcessReworkPlanProcessGroup), 9, null, new List<ColumnDef>
        {
            new("所属在产改制计划ID", "InProcessReworkPlanId", typeof(int)),
            new("组内序号", "SequenceNumber", typeof(int)),
            new("工序名称", "ProcessName"),
            new("制造规格", "ManufacturingSpec", typeof(string), isRequired: false),
            new("外径公差", "OuterDiameterTolerance", typeof(string), isRequired: false),
            new("壁厚公差", "WallThicknessTolerance", typeof(string), isRequired: false),
            new("制造长度", "ManufacturingLength", typeof(string), isRequired: false),
            new("断切处理", "CuttingTreatment", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
            new(SectionDefs.ColdRollDraw, "ColdRollDraw", typeof(int?), isRequired: false),
            new(SectionDefs.OilPipeCut, "OilPipeCut", typeof(int?), isRequired: false),
            new(SectionDefs.Degrease, "Degrease", typeof(int?), isRequired: false),
            new(SectionDefs.EmulsionWash, "EmulsionWash", typeof(int?), isRequired: false),
            new(SectionDefs.UltrasonicWash, "UltrasonicWash", typeof(int?), isRequired: false),
            new(SectionDefs.ClothPolish, "ClothPolish", typeof(int?), isRequired: false),
            new(SectionDefs.BrightAnnealing, "BrightAnnealing", typeof(int?), isRequired: false),
            new(SectionDefs.Solution, "Solution", typeof(int?), isRequired: false),
            new(SectionDefs.Straighten, "Straighten", typeof(int?), isRequired: false),
            new(SectionDefs.Cut, "Cut", typeof(int?), isRequired: false),
            new(SectionDefs.ThicknessMeasure, "ThicknessMeasure", typeof(int?), isRequired: false),
            new(SectionDefs.Pickle, "Pickle", typeof(int?), isRequired: false),
            new(SectionDefs.OuterPolish, "OuterPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerPolish, "InnerPolish", typeof(int?), isRequired: false),
            new(SectionDefs.InnerGrinding, "InnerGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.OuterSpotGrinding, "OuterSpotGrinding", typeof(int?), isRequired: false),
            new(SectionDefs.SandBlasting, "SandBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.ShotBlasting, "ShotBlasting", typeof(int?), isRequired: false),
            new(SectionDefs.Inspection, "Inspection", typeof(int?), isRequired: false),
            new(SectionDefs.WeldingHead, "WeldingHead", typeof(int?), isRequired: false),
            new(SectionDefs.Welding, "Welding", typeof(int?), isRequired: false),
            new(SectionDefs.Lubrication, "Lubrication", typeof(int?), isRequired: false),
            new(SectionDefs.Packing, "Packing", typeof(int?), isRequired: false),
            new(SectionDefs.Warehouse, "Warehouse", typeof(int?), isRequired: false),
            new(SectionDefs.Extra1, "Extra1", typeof(int?), isRequired: false),
            new(SectionDefs.Extra2, "Extra2", typeof(int?), isRequired: false),
        }, compositeKeyColumns: new[] { "InProcessReworkPlanId", "SequenceNumber" }),

        ["Workstation"] = new EntityDef("扫码-工位管理", "扫码-工位管理", typeof(MES.Data.Entities.Configuration.Workstation), 1, "Code", new List<ColumnDef>
        {
            new("工位编码", "Code"),
            new("工位名称", "Name", typeof(string), isRequired: false),
            new("设备名称", "EquipmentName", typeof(string), isRequired: false),
            new("工段", "SectionName", typeof(string), isRequired: true),
            new("报工模板类型", "ReportType", typeof(Core.Enums.ReportTemplateType), isEnum: true, isRequired: true),
            new("成品检验项目", "InspectionItem", typeof(Core.Enums.InspectionItem), isEnum: true, isRequired: false),
            new("是否启用", "IsActive", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
        }),

        ["Employee"] = new EntityDef("扫码-员工管理", "扫码-员工管理", typeof(MES.Data.Entities.Configuration.Employee), 1, "Code", new List<ColumnDef>
        {
            new("工号", "Code"),
            new("姓名", "Name"),
            new("岗位类别", "Department", typeof(string), isRequired: false),
            new("岗位", "Position", typeof(string), isRequired: false),
            new("岗位备注", "PositionRemark", typeof(string), isRequired: false),
            new("工资结算模式", "SalaryMode", typeof(Core.Enums.SalaryMode), isEnum: true, isRequired: false),
            new("工资结算备注", "SalaryRemark", typeof(string), isRequired: false),
            new("靠工岗位", "AttendancePositions", typeof(string), isRequired: false),
            new("靠工系数", "AttendanceCoefficient", typeof(decimal), isRequired: false),
            new("小时工资", "HourlyWage", typeof(decimal), isRequired: false),
            new("日工资", "DailyWage", typeof(decimal), isRequired: false),
            new("月工资", "MonthlyWage", typeof(decimal), isRequired: false),
            new("工序组", "GroupName", typeof(string), isRequired: false),
            new("工段", "SectionName", typeof(string), isRequired: false),
            new("成检项目资质", "InspectionItems", typeof(string), isRequired: false),
            new("成检到料确认人", "MaterialReceiveCheckItems", typeof(bool?), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("是否启用", "IsActive", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
        }),

        // === 工资结算上下文 ===
        ["AttendanceRecord"] = new EntityDef("工资-考勤表", "工资-考勤表", typeof(MES.Data.Entities.Payroll.AttendanceRecord), 1, null, new List<ColumnDef>
        {
            new("员工工号", null!) { IsFkColumn = true, FkEntityKey = "Employee", FkLookupProperty = "Code", FkTargetProperty = "EmployeeId" },
            new("日期", "AttendDate", typeof(DateTime), isRequired: true),
            new("出勤小时", "WorkHours", typeof(decimal), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "EmployeeId", "AttendDate" }),

        ["PayrollDailyWageRecord"] = new EntityDef("工资-每日工资表", "工资-每日工资表", typeof(MES.Data.Entities.Payroll.PayrollDailyWageRecord), 1, null, new List<ColumnDef>
        {
            new("员工工号", null!) { IsFkColumn = true, FkEntityKey = "Employee", FkLookupProperty = "Code", FkTargetProperty = "EmployeeId" },
            new("日期", "WageDate", typeof(DateTime), isRequired: true),
            new("每日工资", "Amount", typeof(decimal), isRequired: false),
            new("计薪模式快照", "SalaryMode", typeof(Core.Enums.SalaryMode), isEnum: true, isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "EmployeeId", "WageDate" }),

        ["PayrollCollectiveScore"] = new EntityDef("工资-月度评分", "工资-月度评分", typeof(MES.Data.Entities.Payroll.PayrollCollectiveScore), 1, null, new List<ColumnDef>
        {
            new("员工工号", null!) { IsFkColumn = true, FkEntityKey = "Employee", FkLookupProperty = "Code", FkTargetProperty = "EmployeeId" },
            new("结算年", "Year", typeof(int)),
            new("结算月", "Month", typeof(int)),
            new("月度分值", "Score", typeof(decimal)),
        }, compositeKeyColumns: new[] { "EmployeeId", "Year", "Month" }),

        ["PayrollCollectiveWageRecord"] = new EntityDef("工资-集体计件月结", "工资-集体计件月结", typeof(MES.Data.Entities.Payroll.PayrollCollectiveWageRecord), 1, null, new List<ColumnDef>
        {
            new("员工工号", null!) { IsFkColumn = true, FkEntityKey = "Employee", FkLookupProperty = "Code", FkTargetProperty = "EmployeeId" },
            new("结算年", "WageYear", typeof(int)),
            new("结算月", "WageMonth", typeof(int)),
            new("岗位", "Position", typeof(string), isRequired: false),
            new("月度分值", "Score", typeof(decimal), isRequired: false),
            new("出勤小时", "AttendanceHours", typeof(decimal), isRequired: false),
            new("实得金额", "Amount", typeof(decimal), isRequired: false),
        }, compositeKeyColumns: new[] { "EmployeeId", "WageYear", "WageMonth" }),

        ["PayrollAttendanceWageRecord"] = new EntityDef("工资-靠工计件月结", "工资-靠工计件月结", typeof(MES.Data.Entities.Payroll.PayrollAttendanceWageRecord), 1, null, new List<ColumnDef>
        {
            new("员工工号", null!) { IsFkColumn = true, FkEntityKey = "Employee", FkLookupProperty = "Code", FkTargetProperty = "EmployeeId" },
            new("结算年", "WageYear", typeof(int)),
            new("结算月", "WageMonth", typeof(int)),
            new("靠工岗位", "AttendancePositions", typeof(string), isRequired: false),
            new("出勤小时", "AttendanceHours", typeof(decimal), isRequired: false),
            new("靠工系数", "AttendanceCoefficient", typeof(decimal), isRequired: false),
            new("实得金额", "Amount", typeof(decimal), isRequired: false),
        }, compositeKeyColumns: new[] { "EmployeeId", "WageYear", "WageMonth" }),

        ["PayrollMiscWorkRecord"] = new EntityDef("工资-杂辅工记录", "工资-杂辅工记录", typeof(MES.Data.Entities.Payroll.PayrollMiscWorkRecord), 1, null, new List<ColumnDef>
        {
            new("员工工号", null!) { IsFkColumn = true, FkEntityKey = "Employee", FkLookupProperty = "Code", FkTargetProperty = "EmployeeId" },
            new("日期", "WorkDate", typeof(DateTime), isRequired: true),
            new("内容", "Content", typeof(string), isRequired: true),
            new("小时数", "Hours", typeof(decimal), isRequired: false),
            new("杂辅工资", "Amount", typeof(decimal), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["PayrollAllowanceRecord"] = new EntityDef("工资-津贴与处罚", "工资-津贴与处罚", typeof(MES.Data.Entities.Payroll.PayrollAllowanceRecord), 1, null, new List<ColumnDef>
        {
            new("员工工号", null!) { IsFkColumn = true, FkEntityKey = "Employee", FkLookupProperty = "Code", FkTargetProperty = "EmployeeId" },
            new("结算年", "Year", typeof(int)),
            new("结算月", "Month", typeof(int)),
            new("满勤奖", "FullAttendanceBonus", typeof(decimal), isRequired: false),
            new("工龄奖", "SeniorityBonus", typeof(decimal), isRequired: false),
            new("夜班津贴", "NightShiftAllowance", typeof(decimal), isRequired: false),
            new("岗位补贴", "PositionAllowance", typeof(decimal), isRequired: false),
            new("高温费", "HighTempAllowance", typeof(decimal), isRequired: false),
            new("工伤补贴", "InjurySubsidy", typeof(decimal), isRequired: false),
            new("带班费", "LeadBonus", typeof(decimal), isRequired: false),
            new("处罚", "Penalty", typeof(decimal), isRequired: false),
            new("代缴社保", "SocialSecurity", typeof(decimal), isRequired: false),
        }, compositeKeyColumns: new[] { "EmployeeId", "Year", "Month" }),

        ["PayrollMonthlySummaryRecord"] = new EntityDef("工资-月工资汇总", "工资-月工资汇总", typeof(MES.Data.Entities.Payroll.PayrollMonthlySummaryRecord), 1, null, new List<ColumnDef>
        {
            new("员工工号", null!) { IsFkColumn = true, FkEntityKey = "Employee", FkLookupProperty = "Code", FkTargetProperty = "EmployeeId" },
            new("结算年", "Year", typeof(int)),
            new("结算月", "Month", typeof(int)),
            new("出勤天数", "AttendanceDays", typeof(int)),
            new("本月基础工资", "BaseWage", typeof(decimal)),
            new("本月杂辅工资", "MiscWorkAmount", typeof(decimal)),
            new("岗位补贴", "PositionAllowance", typeof(decimal)),
            new("工龄奖", "SeniorityBonus", typeof(decimal)),
            new("满勤奖", "FullAttendanceBonus", typeof(decimal)),
            new("带班费", "LeadBonus", typeof(decimal)),
            new("夜班津贴", "NightShiftAllowance", typeof(decimal)),
            new("高温费", "HighTempAllowance", typeof(decimal)),
            new("工伤补贴", "InjurySubsidy", typeof(decimal)),
            new("处罚", "Penalty", typeof(decimal)),
            new("代缴社保", "SocialSecurity", typeof(decimal)),
            new("应发工资及津贴", "TotalPayable", typeof(decimal)),
            new("实发工资及津贴", "TotalPaid", typeof(decimal)),
        }, compositeKeyColumns: new[] { "EmployeeId", "Year", "Month" }),


        // === 系统参数(全局参数)（独立配置表） ===
        ["ConfigParameter"] = new EntityDef("系统-系统参数(全局参数)", "系统-系统参数(全局参数)", typeof(MES.Data.Entities.Configuration.ConfigParameter), 1, null, new List<ColumnDef>
        {
            new("英文分类代码", "Category"),
            new("分类及用途", "CategoryDisplay", typeof(string), isRequired: false),
            new("所属上下文", "Context", typeof(string), isRequired: false),
            new("参数键", "ParamKey"),
            new("参数值", "ParamValue", typeof(decimal)),
            new("用途说明", "Remark", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "Category", "ParamKey" }),

        // === 标准号（生产标准上下文） ===
        ["StandardRegister"] = new EntityDef("标准-标准号", "标准-标准号", typeof(MES.Data.Entities.StandardRegister.StandardRegister), 1, "StandardNo", new List<ColumnDef>
        {
            new("标准号", "StandardNo"),
            new("标准名称", "StandardName"),
            new("引用规范", "RefSpecification", typeof(string), isRequired: false),
            new("标准级别", "StandardLevel", typeof(string), isRequired: false),
            new("制造方式", "ManufactureMethod", typeof(string), isRequired: false),
            new("钢类", "SteelType", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["StandardRegisterItem"] = new EntityDef("标准-标准号子项目", "标准-标准号子项目", typeof(MES.Data.Entities.StandardRegister.StandardRegisterItem), 2, null, new List<ColumnDef>
        {
            new("标准号", null!) { IsFkColumn = true, FkEntityKey = "StandardRegister", FkLookupProperty = "StandardNo", FkTargetProperty = "StandardRegisterId" },
            new("序号", "SeqNo", typeof(int)),
            new("检验项目类别", "InspectionCategory", typeof(string), isRequired: false),
            new("检验项目", "InspectionItem"),
            new("强制性", "IsMandatory", typeof(string), isRequired: false),
            new("取样要求", "SamplingRequirement", typeof(string), isRequired: false),
            new("适用范围", "ApplicableRange", typeof(string), isRequired: false),
            new("引用标准", "RefStandard", typeof(string), isRequired: false),
            new("详细要求", "DetailRequirement", typeof(string), isRequired: false),
        }),

        ["GradeChemicalComposition"] = new EntityDef("标准-牌号化学成分", "标准-牌号化学成分", typeof(MES.Data.Entities.StandardRegister.GradeChemicalComposition), 1, null, new List<ColumnDef>
        {
            new("标准牌号", "StandardGrade"),
            new("标准牌号类别", "StandardGradeCategory", typeof(string), isRequired: false),
            new("碳(C)", "Carbon", typeof(string), isRequired: false),
            new("硅(Si)", "Silicon", typeof(string), isRequired: false),
            new("锰(Mn)", "Manganese", typeof(string), isRequired: false),
            new("磷(P)", "Phosphorus", typeof(string), isRequired: false),
            new("硫(S)", "Sulfur", typeof(string), isRequired: false),
            new("镍(Ni)", "Nickel", typeof(string), isRequired: false),
            new("铬(Cr)", "Chromium", typeof(string), isRequired: false),
            new("钼(Mo)", "Molybdenum", typeof(string), isRequired: false),
            new("铜(Cu)", "Copper", typeof(string), isRequired: false),
            new("氮(N)", "Nitrogen", typeof(string), isRequired: false),
            new("铌(Nb)", "Niobium", typeof(string), isRequired: false),
            new("钛(Ti)", "Titanium", typeof(string), isRequired: false),
            new("铁(Fe)", "Iron", typeof(string), isRequired: false),
            new("铝(Al)", "Aluminum", typeof(string), isRequired: false),
            new("钨(W)", "Tungsten", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "StandardGrade", "StandardGradeCategory" }),

        ["GradePhysicalProperty"] = new EntityDef("标准-牌号物理性能", "标准-牌号物理性能", typeof(MES.Data.Entities.StandardRegister.GradePhysicalProperty), 1, null, new List<ColumnDef>
        {
            new("标准牌号", "StandardGrade"),
            new("标准牌号类别", "StandardGradeCategory", typeof(string), isRequired: false),
            new("密度(g/cm³)", "Density", typeof(decimal)),
            new("热处理温度", "HeatTreatmentTemp", typeof(string), isRequired: false),
            new("硬度洛氏", "HardnessRockwell", typeof(string), isRequired: false),
            new("硬度维氏", "HardnessVickers", typeof(string), isRequired: false),
            new("硬度布氏", "HardnessBrinell", typeof(string), isRequired: false),
            new("抗拉强度(MPa)", "TensileStrength", typeof(string), isRequired: false),
            new("屈服强度0.2(MPa)", "YieldStrength02", typeof(string), isRequired: false),
            new("屈服强度1.0(MPa)", "YieldStrength10", typeof(string), isRequired: false),
            new("延伸率(%)", "Elongation", typeof(string), isRequired: false),
            new("晶粒度", "GrainSize", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "StandardGrade", "StandardGradeCategory" }),

        ["SubStandardQuickView"] = new EntityDef("标准-子标准速览", "标准-子标准速览", typeof(MES.Data.Entities.StandardRegister.SubStandardQuickView), 1, "StandardNo", new List<ColumnDef>
        {
            new("标准号", "StandardNo"),
            new("化学分析(成品)", "ChemicalComposition", typeof(string), isRequired: false),
            new("液压检验", "HydrostaticTest", typeof(string), isRequired: false),
            new("涡流探伤", "EddyCurrent", typeof(string), isRequired: false),
            new("超声波检验", "UltrasonicTest", typeof(string), isRequired: false),
            new("射线探伤", "RadiographicTest", typeof(string), isRequired: false),
            new("硬度(洛氏)", "HardnessRockwell", typeof(string), isRequired: false),
            new("硬度(布氏)", "HardnessBrinell", typeof(string), isRequired: false),
            new("硬度(维氏)", "HardnessVickers", typeof(string), isRequired: false),
            new("拉伸(室温)", "TensileRoomTemp", typeof(string), isRequired: false),
            new("拉伸(高温)", "TensileHighTemp", typeof(string), isRequired: false),
            new("焊接接头拉伸", "WeldJointTensile", typeof(string), isRequired: false),
            new("冲击试验", "ImpactTest", typeof(string), isRequired: false),
            new("焊接接头冲击", "WeldJointImpact", typeof(string), isRequired: false),
            new("压扁试验", "FlatteningTest", typeof(string), isRequired: false),
            new("卷边试验", "FlaringTest", typeof(string), isRequired: false),
            new("扩口试验", "ExpandingTest", typeof(string), isRequired: false),
            new("弯曲试验", "BendTest", typeof(string), isRequired: false),
            new("焊接接头弯曲", "WeldJointBend", typeof(string), isRequired: false),
            new("晶粒度", "GrainSize", typeof(string), isRequired: false),
            new("晶间腐蚀", "IntergranularCorrosion", typeof(string), isRequired: false),
            new("点腐蚀", "PittingCorrosion", typeof(string), isRequired: false),
            new("金相检验", "FerriteContent", typeof(string), isRequired: false),
            new("低倍组织", "Macrostructure", typeof(string), isRequired: false),
        }),

        ["StandardInspectionRequirement"] = new EntityDef("标准-标准号检验项要求", "标准-标准号检验项要求", typeof(MES.Data.Entities.StandardRegister.StandardInspectionRequirement), 1, "StandardNo", new List<ColumnDef>
        {
            new("标准号", "StandardNo"),
            new("化学分析(成品)", "ChemicalComposition", typeof(string), isRequired: false),
            new("液压检验", "HydrostaticTest", typeof(string), isRequired: false),
            new("涡流探伤", "EddyCurrent", typeof(string), isRequired: false),
            new("超声波检验", "UltrasonicTest", typeof(string), isRequired: false),
            new("射线探伤", "RadiographicTest", typeof(string), isRequired: false),
            new("硬度(洛氏)", "HardnessRockwell", typeof(string), isRequired: false),
            new("硬度(布氏)", "HardnessBrinell", typeof(string), isRequired: false),
            new("硬度(维氏)", "HardnessVickers", typeof(string), isRequired: false),
            new("拉伸(室温)", "TensileRoomTemp", typeof(string), isRequired: false),
            new("拉伸(高温)", "TensileHighTemp", typeof(string), isRequired: false),
            new("焊接接头拉伸", "WeldJointTensile", typeof(string), isRequired: false),
            new("冲击试验", "ImpactTest", typeof(string), isRequired: false),
            new("焊接接头冲击", "WeldJointImpact", typeof(string), isRequired: false),
            new("压扁试验", "FlatteningTest", typeof(string), isRequired: false),
            new("卷边试验", "FlaringTest", typeof(string), isRequired: false),
            new("扩口试验", "ExpandingTest", typeof(string), isRequired: false),
            new("弯曲试验", "BendTest", typeof(string), isRequired: false),
            new("焊接接头弯曲", "WeldJointBend", typeof(string), isRequired: false),
            new("晶粒度", "GrainSize", typeof(string), isRequired: false),
            new("晶间腐蚀", "IntergranularCorrosion", typeof(string), isRequired: false),
            new("点腐蚀", "PittingCorrosion", typeof(string), isRequired: false),
            new("金相检验", "FerriteContent", typeof(string), isRequired: false),
            new("低倍组织", "Macrostructure", typeof(string), isRequired: false),
        }),

        ["FactoryInspectionRequirement"] = new EntityDef("标准-工厂检验项要求", "标准-工厂检验项要求", typeof(MES.Data.Entities.StandardRegister.FactoryInspectionRequirement), 1, "StandardNo", new List<ColumnDef>
        {
            new("标准号", "StandardNo"),
            new("化学分析(成品)", "ChemicalComposition", typeof(string), isRequired: false),
            new("PMI检验", "PmiInspection", typeof(string), isRequired: false),
            new("表检", "SurfaceInspection", typeof(string), isRequired: false),
            new("尺寸", "Dimension", typeof(string), isRequired: false),
            new("内窥", "Endoscopy", typeof(string), isRequired: false),
            new("液压检验", "HydrostaticTest", typeof(string), isRequired: false),
            new("水下气压", "UnderwaterPressure", typeof(string), isRequired: false),
            new("涡流探伤", "EddyCurrent", typeof(string), isRequired: false),
            new("超声波检验", "UltrasonicTest", typeof(string), isRequired: false),
            new("端口着色", "PortColoring", typeof(string), isRequired: false),
            new("射线探伤", "RadiographicTest", typeof(string), isRequired: false),
            new("硬度(洛氏)", "HardnessRockwell", typeof(string), isRequired: false),
            new("硬度(布氏)", "HardnessBrinell", typeof(string), isRequired: false),
            new("硬度(维氏)", "HardnessVickers", typeof(string), isRequired: false),
            new("拉伸(室温)", "TensileRoomTemp", typeof(string), isRequired: false),
            new("拉伸(高温)", "TensileHighTemp", typeof(string), isRequired: false),
            new("焊接接头拉伸", "WeldJointTensile", typeof(string), isRequired: false),
            new("冲击试验", "ImpactTest", typeof(string), isRequired: false),
            new("焊接接头冲击", "WeldJointImpact", typeof(string), isRequired: false),
            new("压扁试验", "FlatteningTest", typeof(string), isRequired: false),
            new("卷边试验", "FlaringTest", typeof(string), isRequired: false),
            new("扩口试验", "ExpandingTest", typeof(string), isRequired: false),
            new("弯曲试验", "BendTest", typeof(string), isRequired: false),
            new("焊接接头弯曲", "WeldJointBend", typeof(string), isRequired: false),
            new("晶粒度", "GrainSize", typeof(string), isRequired: false),
            new("晶间腐蚀", "IntergranularCorrosion", typeof(string), isRequired: false),
            new("点腐蚀", "PittingCorrosion", typeof(string), isRequired: false),
            new("金相检验", "FerriteContent", typeof(string), isRequired: false),
            new("低倍组织", "Macrostructure", typeof(string), isRequired: false),
        }),

        // === 工段工量天数(排程/用料)（独立配置表） ===
        ["StandardWorkDay"] = new EntityDef("配置-工段工量天数(排程/用料)", "配置-工段工量天数(排程/用料)", typeof(MES.Data.Entities.Configuration.StandardWorkDay), 1, null, new List<ColumnDef>
        {
            new("工段名称", "SectionName"),
            new("稳定Key", "SectionKey", typeof(string), isRequired: false),
            new("显示顺序", "DisplayOrder", typeof(int), isRequired: false),
            new("是否启用", "IsEnabled", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("牌号前缀", "PlantGradePrefix", typeof(string), isRequired: false),
            new("标准天数", "StandardDays", typeof(double)),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 交货状态附加天数(排程/用料)（独立配置表） ===
        ["StandardWorkDayDeliveryState"] = new EntityDef("配置-交货状态附加天数(排程/用料)", "配置-交货状态附加天数(排程/用料)", typeof(MES.Data.Entities.Configuration.StandardWorkDayDeliveryState), 1, null, new List<ColumnDef>
        {
            new("交货状态", "DeliveryState", typeof(MES.Core.Enums.DeliveryState), isEnum: true),
            new("附加天数", "ExtraDays", typeof(double)),
            new("牌号前缀", "PlantGradePrefix", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 规格日产预估(工单执行)（独立配置表） ===
        ["DailyOutputEstimate"] = new EntityDef("配置-规格日产预估(工单执行)", "配置-规格日产预估(工单执行)", typeof(MES.Data.Entities.Configuration.DailyOutputEstimate), 1, null, new List<ColumnDef>
        {
            new("最小外径(mm)", "MinOuterDiameter", typeof(decimal)),
            new("日产能力(吨)", "DailyOutputTons", typeof(decimal)),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 重点工段日产(生产总览)（独立配置表） ===
        ["DailyProductionCapacity"] = new EntityDef("配置-重点工段日产(生产总览)", "配置-重点工段日产(生产总览)", typeof(MES.Data.Entities.Configuration.DailyProductionCapacity), 1, null, new List<ColumnDef>
        {
            new("工序名称", "ProcessName"),
            new("日产能力(吨/天)", "DailyCapacity", typeof(decimal)),
            new("说明", "Remark", typeof(string), isRequired: false),
        }),

        // === 段落日产配置(段落流转)（段落由 3 类配置自动生成，仅参数可编辑；标识列随导出保留供同步识别，参数批量编辑仍可用） ===
        ["SectionParagraphConfig"] = new EntityDef("配置-段落日产配置(段落流转)", "配置-段落日产配置(段落流转)", typeof(MES.Data.Entities.Configuration.SectionParagraphConfig), 1, "ParagraphName", new List<ColumnDef>
        {
            new("类型", "CategoryType", typeof(string), isRequired: false),
            new("段落Key", "ParagraphKey", typeof(string), isRequired: false),
            new("段落类别", "ParagraphName"),
            new("显示顺序", "DisplayOrder", typeof(int), isRequired: false),
            new("日流转设定", "DailyFlowTarget", typeof(decimal), isRequired: false),
            new("偏少天数值", "LowerLimitDays", typeof(decimal), isRequired: false),
            new("过多天数值", "UpperLimitDays", typeof(decimal), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 工序组定义(批次/工艺)（工序组字典，ProcessKey 稳定键 + 可改名中文名） ===
        ["ProcessDefinition"] = new EntityDef("配置-工序组定义(批次/工艺)", "配置-工序组定义(批次/工艺)", typeof(MES.Data.Entities.Configuration.ProcessDefinition), 1, "ProcessKey", new List<ColumnDef>
        {
            new("稳定Key(ProcessKeys)", "ProcessKey"),
            new("工序组中文名", "ProcessName"),
            new("显示顺序", "DisplayOrder", typeof(int), isRequired: false),
            new("是否启用", "IsEnabled", typeof(bool), isRequired: false, valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("是否冷轧", "IsColdRoll", typeof(bool), isRequired: false, valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("是否冷拔", "IsColdDraw", typeof(bool), isRequired: false, valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("默认工段(SectionKeys JSON数组)", "DefaultSections", typeof(string), isRequired: false),
            new("说明", "Remark", typeof(string), isRequired: false),
        }),

        // === 冷轧产能档案(冷轧排程)（机台×规格 单机单日量，排程保存自动反哺；复合键 ProcessType+BilletSpec+RollingSpec+IsFinished） ===
        ["ColdRollCapacity"] = new EntityDef("配置-冷轧产能档案(冷轧排程)", "配置-冷轧产能档案(冷轧排程)", typeof(MES.Data.Entities.Scheduling.ColdRollCapacity), 1, null, new List<ColumnDef>
        {
            new("冷轧类型(ProcessKeys)", "ProcessType"),
            new("轧坯规格", "BilletSpec"),
            new("轧制规格", "RollingSpec"),
            new("是否成品", "IsFinished", typeof(bool), isRequired: false, valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("常用机台(分号分隔)", "MachineNo", typeof(string), isRequired: false),
            new("单机单日量(kg)", "DailyOutput", typeof(decimal), isRequired: false),
            new("样本次数", "SampleCount", typeof(int), isRequired: false),
        }, compositeKeyColumns: new[] { "ProcessType", "BilletSpec", "RollingSpec", "IsFinished" }),

        // === 冷轧机台数配置(冷轧排程)（按机型机台数参数，排程引擎产能平衡输入） ===
        ["ColdRollMachineConfig"] = new EntityDef("配置-冷轧机台数配置(冷轧排程)", "配置-冷轧机台数配置(冷轧排程)", typeof(MES.Data.Entities.Scheduling.ColdRollMachineConfig), 1, "ProcessType", new List<ColumnDef>
        {
            new("机型(ProcessKeys)", "ProcessType"),
            new("本厂机台数", "OwnedCount", typeof(int)),
            new("最小机台数", "MinMachines", typeof(int)),
            new("最大机台数", "MaxMachines", typeof(int)),
            new("估算单机日产(kg)", "EstimatedDailyOutput", typeof(decimal), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 冷轧机台组配置(冷轧排程)（工序归组 + 供需链，GroupKey 稳定键） ===
        ["ColdRollMachineGroupConfig"] = new EntityDef("配置-冷轧机台组配置(冷轧排程)", "配置-冷轧机台组配置(冷轧排程)", typeof(MES.Data.Entities.Scheduling.ColdRollMachineGroupConfig), 1, "GroupKey", new List<ColumnDef>
        {
            new("组稳定Key", "GroupKey"),
            new("组显示名", "DisplayName"),
            new("组内工序(逗号分隔ProcessKeys)", "ProcessKeys", typeof(string), isRequired: false),
            new("显示顺序", "DisplayOrder", typeof(int), isRequired: false),
            new("供给目标组Key", "SupplyTargetGroupKey", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 枚举显示配置(全局显示)（EnumKey+Value 复合键，管理 C# 枚举中文显示） ===
        ["EnumDisplayDefinition"] = new EntityDef("配置-枚举显示配置(全局显示)", "配置-枚举显示配置(全局显示)", typeof(MES.Data.Entities.Configuration.EnumDisplayDefinition), 1, null, new List<ColumnDef>
        {
            new("枚举标识(类型名)", "EnumKey"),
            new("枚举值名(英文)", "Value"),
            new("中文显示名", "DisplayName"),
            new("显示顺序", "DisplayOrder", typeof(int), isRequired: false),
            new("说明", "Remark", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "EnumKey", "Value" }),

        // === 字典显示配置(全局显示)（DictKey+Value 复合键，管理 string 字典字段中文显示/启停/加值） ===
        ["DictValueDefinition"] = new EntityDef("配置-字典显示配置(全局显示)", "配置-字典显示配置(全局显示)", typeof(MES.Data.Entities.Configuration.DictValueDefinition), 1, null, new List<ColumnDef>
        {
            new("字典标识(DictKey)", "DictKey"),
            new("英文稳定Key", "Value"),
            new("中文显示名", "DisplayName"),
            new("显示顺序", "DisplayOrder", typeof(int), isRequired: false),
            new("是否启用", "IsEnabled", typeof(bool), isRequired: false, valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("说明", "Remark", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "DictKey", "Value" }),
    };

    public static readonly List<string> EntityOrder = new()
    {
        "Warehouse", "StandardGradeMapping", "CustomerProfile", "SupplierProfile",
        "FurnaceRegistration", "ChemicalComposition", "ChemicalValidationRule", "Ncr",
        "SalesOrder",
        "OrderItem", "ProductRequirement",
        "WorkOrder", "OrderDemandAdjustment",
        "PurchaseOrder", "SubcontractOrder", "SubcontractReturnItem", "ProductionBatch",
        "ProcessGroup", "ProductionRecord", "SectionOutsource", "OutsourceRecovery", "MaterialReceiveCheck", "ProcessInspection", "FinalInspection", "ChemicalAnalysis", "HardnessTest", "GrainSizeTest", "PittingCorrosionTest", "IntergranularCorrosionTest", "TensileTest", "MetallographicTest", "FlatteningTest", "FlaringTest", "PicklingInRecord", "PicklingOutRecord", "OperationLog", "InventoryBatch", "OutboundRecord",
        "Equipment", "RepairOrder", "MaintenanceOrder", "InspectionRecord",
        "InventoryPlan", "PurchaseSemiPlan", "PurchaseFinishedPlan", "RoundBarPiercingPlan", "InProcessReworkPlan",
        "SemiPlanProcessGroup", "InventoryPlanProcessGroup", "PiercingPlanProcessGroup", "InProcessReworkPlanProcessGroup",
        "Workstation", "Employee",
        "AttendanceRecord", "PayrollDailyWageRecord", "PayrollCollectiveScore", "PayrollCollectiveWageRecord", "PayrollAttendanceWageRecord", "PayrollMiscWorkRecord", "PayrollAllowanceRecord", "PayrollMonthlySummaryRecord",
        "ConfigParameter",
        "StandardRegister", "StandardRegisterItem",
        "GradeChemicalComposition", "GradePhysicalProperty", "StandardInspectionRequirement", "FactoryInspectionRequirement", "SubStandardQuickView",
        "StandardWorkDay", "StandardWorkDayDeliveryState", "DailyOutputEstimate", "DailyProductionCapacity", "SectionParagraphConfig",
        "ProcessDefinition", "ColdRollCapacity", "ColdRollMachineConfig", "ColdRollMachineGroupConfig", "EnumDisplayDefinition", "DictValueDefinition",
    };

    /// <summary>
    /// 上下文归类顺序（数据工具「选择数据类型」下拉按此排序显示）：
    /// 订单 → 工单 → 批次 → 质量 → 物料 → 仓库 → 设备 → 标准 → 配置 → 扫码 → 工资 → 系统
    /// </summary>
    public static readonly List<string> ContextOrder = new()
    {
        "订单", "工单", "批次", "质量", "物料", "仓库", "设备", "标准", "配置", "扫码", "工资", "系统"
    };

    /// <summary>
    /// 从 DisplayName 前缀解析上下文归类（命名规则「上下文-实体名」），前缀不在 ContextOrder 时归为「其他」排最后
    /// </summary>
    public static string GetContext(string displayName)
    {
        var dashIdx = displayName.IndexOf('-');
        var ctx = dashIdx > 0 ? displayName[..dashIdx] : displayName;
        return ContextOrder.Contains(ctx) ? ctx : "其他";
    }

    public static List<MES.Core.Interfaces.DataExchange.EntityInfo> GetEntities()
    {
        var result = EntityOrder
            .Where(k => Registry.ContainsKey(k))
            .Select(k => new MES.Core.Interfaces.DataExchange.EntityInfo
            {
                Key = k,
                Name = Registry[k].DisplayName,
                Context = GetContext(Registry[k].DisplayName),
            })
            .OrderBy(x =>
            {
                var idx = ContextOrder.IndexOf(x.Context);
                return idx < 0 ? int.MaxValue : idx;
            })
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
        return result;
    }

    public static string GetEntityDisplayName(string entityKey)
    {
        if (!Registry.TryGetValue(entityKey, out var def))
            throw new MES.Core.Exceptions.BusinessException($"不支持的实体类型: {entityKey}");
        return def.DisplayName;
    }
}
