using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;
using MES.Shared.Constants;

namespace MES.Services.DataExchange;

/// <summary>
/// 数据导入导出服务（仅管理员可导入，所有认证用户可导出）
/// </summary>
public class DataExchangeService : IDataExchangeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DataExchangeService> _logger;

    public DataExchangeService(AppDbContext context, ILogger<DataExchangeService> logger)
    {
        _context = context;
        _logger = logger;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// 系统编码字段的生成前缀映射（属性名 → 前缀）
    /// </summary>
    private static readonly Dictionary<string, string> CodePrefixMap = new()
    {
        ["SupplierCode"] = "SU",
        ["MaterialCode"] = "MA",
    };

    #region 实体定义

    /// <summary>
    /// 实体注册表：所有支持导入导出的实体
    /// </summary>
    public static readonly Dictionary<string, EntityDef> Registry = new()
    {
        // === 第1批：独立实体（无外部FK依赖） ===
        ["Warehouse"] = new EntityDef("仓库", "仓库档案", typeof(Warehouse), 1, "Code", new List<ColumnDef>
        {
            new("仓库编码", "Code"),
            new("仓库名称", "Name"),
            new("显示顺序", "SortOrder", typeof(int)),
            new("是否启用", "IsActive", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["ProductionStandard"] = new EntityDef("产品标准", "产品标准", typeof(ProductionStandard), 1, "StandardCode", new List<ColumnDef>
        {
            new("标准编码", "StandardCode"),
            new("标准名称", "StandardName"),
            new("排序", "SortOrder", typeof(int)),
            new("是否启用", "IsActive", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["StandardGradeMapping"] = new EntityDef("牌号对照", "牌号对照", typeof(StandardGradeMapping), 1, "StandardGrade", new List<ColumnDef>
        {
            new("标准牌号", "StandardGrade"),
            new("工厂牌号", "PlantGrade"),
            new("密度(g/cm³)", "Density", typeof(decimal)),
            new("热处理方式", "HeatTreatment", typeof(string), isRequired: false),
            new("特殊材料", "SpecialMaterial", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("特殊说明", "SpecialNote", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["CustomerProfile"] = new EntityDef("客户档案", "客户档案", typeof(CustomerProfile), 1, "CustomerUnit", new List<ColumnDef>
        {
            new("客户编码", "CustomerCode"),
            new("客户单位", "CustomerUnit"),
            new("业务员", "Salesman"),
            new("最终用户", "EndCustomer", typeof(string), isRequired: false),
            new("联系人", "ContactPerson", typeof(string), isRequired: false),
            new("联系电话", "ContactPhone", typeof(string), isRequired: false),
            new("地址", "Address", typeof(string), isRequired: false),
            new("状态", "Status", typeof(CustomerStatus), isEnum: true),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["SupplierProfile"] = new EntityDef("供应商档案", "供应商档案", typeof(SupplierProfile), 1, "SupplierName", new List<ColumnDef>
        {
            new("供应商编码", "SupplierCode", isSystem: true),
            new("供应商名称", "SupplierName"),
            new("物料分类", "MaterialCategory", typeof(string), isRequired: false),
            new("联系人", "ContactPerson", typeof(string), isRequired: false),
            new("联系电话", "ContactPhone", typeof(string), isRequired: false),
            new("地址", "Address", typeof(string), isRequired: false),
            new("是否启用", "IsActive", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 设备台账 ===
        ["Equipment"] = new EntityDef("设备", "设备台账", typeof(Equipment), 1, "EquipmentCode", new List<ColumnDef>
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
            new("生命周期", "LifecycleStatus", typeof(LifecycleStatus), isEnum: true),
            new("作用类型", "UsageType", typeof(UsageType), isEnum: true),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 第2批：依赖客户档案 ===
        ["SalesOrder"] = new EntityDef("销售订单", "销售订单", typeof(SalesOrder), 2, "OrderNumber", new List<ColumnDef>
        {
            new("订单号", "OrderNumber"),
            new("签订日期", "SignDate", typeof(DateTime)),
            new("客户编码", null!) { IsFkColumn = true, FkEntityKey = "CustomerProfile", FkLookupProperty = "CustomerCode", FkTargetProperty = "CustomerId" },
            new("状态", "Status", typeof(SalesOrderStatus), isEnum: true),
        }),

        // === 设备上下文（依赖设备台账） ===
        ["RepairOrder"] = new EntityDef("维修工单", "维修工单", typeof(RepairOrder), 2, "RepairOrderNo", new List<ColumnDef>
        {
            new("工单编号", "RepairOrderNo", isSystem: true),
            new("设备编号", null!) { IsFkColumn = true, FkEntityKey = "Equipment", FkLookupProperty = "EquipmentCode", FkTargetProperty = "EquipmentId" },
            new("故障描述", "FaultDescription"),
            new("故障类型", "FaultType", typeof(string), isRequired: false),
            new("优先级", "Priority", typeof(RepairPriority), isEnum: true),
            new("维修状态", "RepairStatus", typeof(RepairOrderStatus), isEnum: true),
            new("报修人", "ReportPerson"),
            new("报修时间", "ReportTime", typeof(DateTime)),
            new("维修人", "RepairPerson", typeof(string), isRequired: false),
            new("维修开始时间", "RepairStartTime", typeof(DateTime?), isRequired: false),
            new("维修结束时间", "RepairEndTime", typeof(DateTime?), isRequired: false),
            new("维修内容", "RepairContent", typeof(string), isRequired: false),
            new("备件更换", "SparePartUsed", typeof(string), isRequired: false),
        }),

        ["MaintenanceOrder"] = new EntityDef("保养工单", "保养工单", typeof(MaintenanceOrder), 2, "MaintOrderNo", new List<ColumnDef>
        {
            new("工单编号", "MaintOrderNo", isSystem: true),
            new("设备编号", null!) { IsFkColumn = true, FkEntityKey = "Equipment", FkLookupProperty = "EquipmentCode", FkTargetProperty = "EquipmentId" },
            new("实际日期", "ActualDate", typeof(DateTime?), isRequired: false),
            new("执行人", "Executor", typeof(string), isRequired: false),
            new("执行简述", "ExecutionSummary", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["InspectionRecord"] = new EntityDef("点检记录", "点检记录", typeof(InspectionRecord), 2, "RecordNo", new List<ColumnDef>
        {
            new("记录编号", "RecordNo", isSystem: true),
            new("设备编号", null!) { IsFkColumn = true, FkEntityKey = "Equipment", FkLookupProperty = "EquipmentCode", FkTargetProperty = "EquipmentId" },
            new("实际日期", "ActualDate", typeof(DateTime?), isRequired: false),
            new("点检人", "Inspector", typeof(string), isRequired: false),
            new("执行简述", "ExecutionSummary", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 第3批：依赖销售订单、产品标准、牌号对照 ===
        ["OrderItem"] = new EntityDef("订单项次", "订单项次", typeof(OrderItem), 3, null, new List<ColumnDef>
        {
            new("订单号", null!) { IsFkColumn = true, FkEntityKey = "SalesOrder", FkLookupProperty = "OrderNumber", FkTargetProperty = "SalesOrderId" },
            new("项次号", "Sequence", typeof(int)),
            new("交货日期", "DeliveryDate", typeof(DateTime)),
            new("延期罚款", "DelayPenalty", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("结算方式", "SettlementMethod", typeof(SettlementMethod), isEnum: true),
            new("物料名称", "MaterialName", typeof(MaterialName), isEnum: true),
            new("产品标准编码", null!) { IsFkColumn = true, FkEntityKey = "ProductionStandard", FkLookupProperty = "StandardCode", FkTargetProperty = "ProductionStandardId" },
            new("交货状态", "DeliveryState", typeof(DeliveryState), isEnum: true),
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
            new("长度状态", "LengthStatus", typeof(LengthStatus), isEnum: true),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("数量(支)", "Quantity", typeof(int?), isRequired: false),
            new("米数(m)", "Meters", typeof(decimal?), isRequired: false),
            new("合同重量(kg)", "ContractWeight", typeof(decimal)),
            new("理算重量(kg)", "TheoreticalWeight", typeof(decimal)),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 第4批：依赖订单项次 ===
        ["ProductRequirement"] = new EntityDef("技术要求", "技术要求", typeof(ProductRequirement), 4, null, new List<ColumnDef>
        {
            new("订单号", null!) { IsFkColumn = true, FkEntityKey = "OrderItem", FkLookupProperty = "Id", FkTargetProperty = "OrderItemId", FkRequiresJoin = true },
            new("项次号", null!) { IsFkColumn = true, FkEntityKey = "OrderItem", FkLookupProperty = "Sequence", FkTargetProperty = "OrderItemId", FkRequiresJoin = true },
            new("技术要求类型", "RequirementType", typeof(RequirementType), isEnum: true),
            new("化学成分要求", "ChemicalComposition", typeof(string), isRequired: false),
            new("力学性能要求", "MechanicalProperty", typeof(string), isRequired: false),
            new("公差要求", "ToleranceRequirement", typeof(string), isRequired: false),
            new("表面质量要求", "SurfaceQuality", typeof(string), isRequired: false),
            new("无损检测要求", "NdtRequirement", typeof(string), isRequired: false),
            new("其他要求", "OtherRequirement", typeof(string), isRequired: false),
        }),

        // === 第5批：工单（字符串引用订单，无FK约束） ===
        ["WorkOrder"] = new EntityDef("工单", "工单", typeof(WorkOrder), 5, "WorkOrderNo", new List<ColumnDef>
        {
            new("工单号", "WorkOrderNo"),
            new("订单号", "SalesOrderNo"),
            new("主号", "ProductionMainNo"),
            new("次号", "ProductionSubNo", typeof(string), isRequired: false),
            new("状态", "Status", typeof(WorkOrderStatus), isEnum: true),
            new("签订日期", "SignDate", typeof(DateTime)),
            new("业务员", "Salesman"),
            new("最终用户", "EndCustomer", typeof(string), isRequired: false),
            new("交货日期", "DeliveryDate", typeof(DateTime)),
            new("延期罚款", "DelayPenalty", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("物料名称", "MaterialName", typeof(MaterialName), isEnum: true),
            new("结算方式", "SettlementMethod", typeof(SettlementMethod), isEnum: true),
            new("产品标准编码", "StandardCode"),
            new("交货状态", "DeliveryState", typeof(DeliveryState), isEnum: true),
            new("工厂牌号", "PlantGrade"),
            new("规格", "Specification"),
            new("外径下偏差(mm)", "OuterDiameterNegative", typeof(decimal)),
            new("外径上偏差(mm)", "OuterDiameterPositive", typeof(decimal)),
            new("壁厚下偏差(mm)", "WallThicknessNegative", typeof(decimal)),
            new("壁厚上偏差(mm)", "WallThicknessPositive", typeof(decimal)),
            new("长度状态", "LengthStatus", typeof(LengthStatus), isEnum: true),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("总数量(支)", "TotalQuantity", typeof(int)),
            new("总米数(m)", "TotalMeters", typeof(decimal)),
            new("总重量(kg)", "TotalWeight", typeof(decimal)),
            new("技术要求", "TechnicalRequirements", typeof(RequirementType), isEnum: true),
            new("关联项次(订单号|项次号)", "OrderItemIds", typeof(string), isRequired: false),
        }),

        // === 第6批：物料 ===
        ["Material"] = new EntityDef("物料", "物料", typeof(Material), 6, null, new List<ColumnDef>
        {
            new("物料编码", "MaterialCode", isSystem: true),
            new("物料分类", "MaterialCategory"),
            new("厂内钢种", "PlantGrade"),
            new("名义规格", "Specification"),
            new("是否启用", "IsActive", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("备注", "Remark", typeof(string), isRequired: false),
        }, compositeKeyColumns: new[] { "MaterialCategory", "PlantGrade", "Specification" }),

        // === 第7批：采购订单、委外订单、生产批次（依赖供应商/工单） ===
        ["ProductionBatch"] = new EntityDef("生产批次", "生产批次", typeof(ProductionBatch), 7, "BatchNo", new List<ColumnDef>
        {
            new("生产编号", "BatchNo"),
            new("状态", "Status", typeof(BatchStatus), isEnum: true),
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("生产类型", "ProductionType", typeof(string), isRequired: false),
            new("制造物品", "ManufacturingItem"),
            new("制几率", "ProductionRatio", typeof(int), isRequired: false),
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
            new("物料名称", "MaterialName"),
            new("结算方式", "SettlementMethod"),
            new("标准编码", "StandardCode"),
            new("交货状态", "DeliveryState"),
            new("工厂牌号", "PlantGrade"),
            new("规格", "Specification"),
            new("外径下偏差(mm)", "OuterDiameterNegative", typeof(decimal)),
            new("外径上偏差(mm)", "OuterDiameterPositive", typeof(decimal)),
            new("壁厚下偏差(mm)", "WallThicknessNegative", typeof(decimal)),
            new("壁厚上偏差(mm)", "WallThicknessPositive", typeof(decimal)),
            new("长度状态", "LengthStatus"),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("总数量(支)", "TotalQuantity", typeof(int)),
            new("总米数(m)", "TotalMeters", typeof(decimal)),
            new("总重量(kg)", "TotalWeight", typeof(decimal)),
            new("总项次数", "TotalItemCount", typeof(int)),
            new("明细", "ItemDetails", typeof(string), isRequired: false),
            new("技术要求", "TechnicalRequirements"),
            new("关联项次", "OrderItemIds", typeof(string), isRequired: false),
            // 仓库冗余字段
            new("来源库存批次号", "SourceBatchNo", typeof(string), isRequired: false),
            new("仓库编码", null!) { IsFkColumn = true, FkEntityKey = "Warehouse", FkLookupProperty = "Code", FkTargetProperty = "WarehouseId" },
            new("原料类型", "SourceMaterialType", typeof(string), isRequired: false),
            new("入库来源", "InboundSource", typeof(string), isRequired: false),
            new("来料单位", "SourceName", typeof(string), isRequired: false),
            new("入库日期", "InboundDate", typeof(DateTime?), isRequired: false),
            new("炉号", "SourceHeatNo", typeof(string), isRequired: false),
            new("来源工厂牌号", "SourcePlantGrade", typeof(string), isRequired: false),
            new("来源名义规格", "SourceSpecification", typeof(string), isRequired: false),
            new("来源长度状态", "SourceLengthStatus", typeof(string), isRequired: false),
            new("单支重(kg)", "SourceUnitWeight", typeof(decimal?), isRequired: false),
            new("领料支数", "InputQuantity", typeof(int?), isRequired: false),
            new("领料重量(kg)", "InputWeight", typeof(decimal?), isRequired: false),
            new("现有效支数", "CurrentValidQty", typeof(int?), isRequired: false),
            new("现有效重量(kg)", "CurrentValidWeight", typeof(decimal?), isRequired: false),
        }),

        ["PurchaseOrder"] = new EntityDef("采购订单", "采购订单", typeof(PurchaseOrder), 7, "OrderNo", new List<ColumnDef>
        {
            new("采购单号", "OrderNo"),
            new("供应商名称", null!) { IsFkColumn = true, FkEntityKey = "SupplierProfile", FkLookupProperty = "SupplierName", FkTargetProperty = "SupplierId" },
            new("下单日期", "OrderDate", typeof(DateTime)),
            new("状态", "Status", typeof(PurchaseOrderStatus), isEnum: true),
            new("强制完成", "IsForceCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("物料分类", "MaterialCategory"),
            new("厂内钢种", "PlantGrade"),
            new("名义规格", "Specification"),
            new("单支重量(kg)", "UnitWeight", typeof(decimal?), isRequired: false),
            new("采购支数", "Quantity", typeof(int?), isRequired: false),
            new("采购重量(kg)", "Weight", typeof(decimal)),
            new("要求到货日期", "RequiredDate", typeof(DateTime)),
            new("单价", "UnitPrice", typeof(decimal?), isRequired: false),
            new("总金额", "TotalAmount", typeof(decimal?), isRequired: false),
            new("来源工单号", "SourceWorkOrderNo", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["SubcontractOrder"] = new EntityDef("委外订单", "委外订单", typeof(SubcontractOrder), 7, "OrderNo", new List<ColumnDef>
        {
            new("委外单号", "OrderNo"),
            new("供应商名称", null!) { IsFkColumn = true, FkEntityKey = "SupplierProfile", FkLookupProperty = "SupplierName", FkTargetProperty = "SupplierId" },
            new("下单日期", "OrderDate", typeof(DateTime)),
            new("加工类型", "ProcessType"),
            new("状态", "Status", typeof(SubcontractOrderStatus), isEnum: true),
            new("强制完成", "IsForceCompleted", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("发出物料分类", "OutMaterialCategory"),
            new("炉号", "FurnaceNumber", typeof(string), isRequired: false),
            new("发出钢种", "OutPlantGrade"),
            new("发出规格", "OutSpecification"),
            new("发出支数", "OutQuantity", typeof(int)),
            new("发出重量(kg)", "OutWeight", typeof(decimal)),
            new("收回截止日期", "ReturnDeadline", typeof(DateTime?), isRequired: false),
            new("来源工单号", "SourceWorkOrderNo", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["SubcontractReturnItem"] = new EntityDef("委外退货项", "委外退货项", typeof(SubcontractReturnItem), 7, null, new List<ColumnDef>
        {
            new("委外单号", null!) { IsFkColumn = true, FkEntityKey = "SubcontractOrder", FkLookupProperty = "OrderNo", FkTargetProperty = "SubcontractOrderId" },
            new("行号", "Sequence", typeof(int)),
            new("物料分类", "MaterialCategory"),
            new("加工规格", "ProcessSpecification"),
            new("状态备注", "ProcessStatusRemark", typeof(string), isRequired: false),
            new("加工单价", "ProcessUnitPrice", typeof(decimal?), isRequired: false),
            new("加工总价", "ProcessTotalAmount", typeof(decimal?), isRequired: false),
            new("来源工单号", "SourceWorkOrderNo", typeof(string), isRequired: false),
        }),

        // === 第8批：工序组（依赖生产批次）、仓库出入库 ===
        ["ProcessGroup"] = new EntityDef("工序组", "工序组", typeof(ProcessGroup), 8, null, new List<ColumnDef>
        {
            new("所属批次号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("组内序号", "SequenceNumber", typeof(int)),
            new("工序名称", "ProcessName"),
            new("制造规格", "ManufacturingSpec", typeof(string), isRequired: false),
            new("外径公差", "OuterDiameterTolerance", typeof(string), isRequired: false),
            new("壁厚公差", "WallThicknessTolerance", typeof(string), isRequired: false),
            new("制造长度", "ManufacturingLength", typeof(string), isRequired: false),
            new("断切处理", "CuttingTreatment", typeof(string), isRequired: false),
            new("制成倍数", "ManufacturingMultiple", typeof(int)),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("冷轧拔", "ColdRollDraw", typeof(int?), isRequired: false),
            new("油管断", "OilPipeCut", typeof(int?), isRequired: false),
            new("去油", "Degrease", typeof(int?), isRequired: false),
            new("固溶", "Solution", typeof(int?), isRequired: false),
            new("矫直", "Straighten", typeof(int?), isRequired: false),
            new("断切", "Cut", typeof(int?), isRequired: false),
            new("测壁厚", "ThicknessMeasure", typeof(int?), isRequired: false),
            new("酸洗", "Pickle", typeof(int?), isRequired: false),
            new("外抛光", "OuterPolish", typeof(int?), isRequired: false),
            new("内修磨", "InnerGrinding", typeof(int?), isRequired: false),
            new("外点磨", "OuterSpotGrinding", typeof(int?), isRequired: false),
            new("检验", "Inspection", typeof(int?), isRequired: false),
            new("打焊头", "WeldingHead", typeof(int?), isRequired: false),
            new("润滑", "Lubrication", typeof(int?), isRequired: false),
            new("入库", "Warehouse", typeof(int?), isRequired: false),
        }),

        ["ProductionRecord"] = new EntityDef("生产记录", "生产记录", typeof(ProductionRecord), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("工序序号", null!) { IsFkColumn = true, FkEntityKey = "ProcessGroup", FkLookupProperty = "SequenceNumber", FkTargetProperty = "ProcessGroupId", FkRequiresJoin = true },
            new("工序名称", "ProcessName"),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("制造规格", "ManufacturingSpec"),
            new("工段名称", "SectionName"),
            new("执行日期", "ExecDate", typeof(DateTime)),
            new("设备名称", "EquipmentName", typeof(string), isRequired: false),
            new("操作人", "Operator", typeof(string), isRequired: false),
            new("班次", "Shift", typeof(string), isRequired: false),
            new("加工支数", "Quantity", typeof(int?), isRequired: false),
            new("加工重量(kg)", "Weight", typeof(decimal?), isRequired: false),
            new("是否成品", "IsFinished", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("断切倍数", "CuttingMultiple", typeof(decimal?), isRequired: false),
            new("成品断切长度(mm)", "FinishedCutLength", typeof(decimal?), isRequired: false),
            new("切后支数", "PostCutQuantity", typeof(int?), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["SectionOutsource"] = new EntityDef("工段委外", "工段委外", typeof(SectionOutsource), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("工序序号", null!) { IsFkColumn = true, FkEntityKey = "ProcessGroup", FkLookupProperty = "SequenceNumber", FkTargetProperty = "ProcessGroupId", FkRequiresJoin = true },
            new("工序名称", "ProcessName"),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("委外规格", "OutsourceSpec", typeof(string), isRequired: false),
            new("制造规格", "ManufacturingSpec"),
            new("工段名称", "SectionName"),
            new("委外单位", "OutsourceVendor"),
            new("发出日期", "SendOutDate", typeof(DateTime)),
            new("发出支数", "SendQuantity", typeof(int?), isRequired: false),
            new("发出重量(kg)", "SendWeight", typeof(decimal?)),
            new("要求收回日期", "ExpectedReturnDate", typeof(DateTime?), isRequired: false),
            new("是否紧急", "IsUrgent", typeof(bool), valueConverter: v => v == "是" || v == "true" || v == "True"),
            new("备注", "Remark", typeof(string), isRequired: false),
            new("状态", "Status", typeof(SectionOutsourceStatus), isEnum: true),
        }),

        ["OutsourceRecovery"] = new EntityDef("委外回收", "委外回收", typeof(OutsourceRecovery), 8, null, new List<ColumnDef>
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
        }),

        ["MaterialReceiveCheck"] = new EntityDef("检验到料", "检验到料", typeof(MaterialReceiveCheck), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("到料日期", "ReceiveDate", typeof(DateTime)),
            new("到料支数", "ReceivedQuantity", typeof(int?), isRequired: false),
            new("到料重量(kg)", "ReceivedWeight", typeof(decimal?), isRequired: false),
            new("班次", "Shift", typeof(string), isRequired: false),
            new("确认人", "Checker", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["ProcessInspection"] = new EntityDef("过程检验", "过程检验", typeof(ProcessInspection), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("工序序号", null!) { IsFkColumn = true, FkEntityKey = "ProcessGroup", FkLookupProperty = "SequenceNumber", FkTargetProperty = "ProcessGroupId", FkRequiresJoin = true },
            new("工序名称", "ProcessName"),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("制造规格", "ManufacturingSpec"),
            new("工段名称", "SectionName"),
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("设备名称", "EquipmentName", typeof(string), isRequired: false),
            new("检验员", "Inspector", typeof(string), isRequired: false),
            new("班次", "Shift", typeof(string), isRequired: false),
            new("检验支数", "Quantity", typeof(int?), isRequired: false),
            new("检验重量(kg)", "Weight", typeof(decimal?), isRequired: false),
            new("检验项目", "InspectionItem", typeof(string), isRequired: false),
            new("合格支数", "QualifiedQuantity", typeof(int?), isRequired: false),
            new("合格重量(kg)", "QualifiedWeight", typeof(decimal?), isRequired: false),
            new("不合格返整支数", "DefectReworkQuantity", typeof(int?), isRequired: false),
            new("不合格入库支数", "DefectWarehouseQuantity", typeof(int?), isRequired: false),
            new("不合格报废支数", "DefectScrapQuantity", typeof(int?), isRequired: false),
            new("不合格描述", "DefectDescription", typeof(string), isRequired: false),
            new("来料单位", "SourceUnit", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["FinalInspection"] = new EntityDef("成品检验", "成品检验", typeof(FinalInspection), 8, null, new List<ColumnDef>
        {
            new("生产编号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("物料名称", "MaterialName", typeof(string), isRequired: false),
            new("关联工单号", "WorkOrderNo", typeof(string), isRequired: false),
            new("关联订单号", "SalesOrderNo", typeof(string), isRequired: false),
            new("来料单位", "SourceUnit", typeof(string), isRequired: false),
            new("炉号", "FurnaceNo", typeof(string), isRequired: false),
            new("工厂牌号", "PlantGrade", typeof(string), isRequired: false),
            new("规格", "Specification", typeof(string), isRequired: false),
            new("定尺长度", "FixedLength", typeof(string), isRequired: false),
            new("检验项目", "InspectionItem", typeof(InspectionItem), isEnum: true),
            new("检验日期", "InspectionDate", typeof(DateTime)),
            new("设备名称", "EquipmentName", typeof(string), isRequired: false),
            new("班次", "Shift", typeof(string), isRequired: false),
            new("操作员", "Operator", typeof(string), isRequired: false),
            new("检验支数", "Quantity", typeof(int?), isRequired: false),
            new("检验重量(kg)", "Weight", typeof(decimal?), isRequired: false),
            new("合格支数", "QualifiedQuantity", typeof(int?), isRequired: false),
            new("合格重量(kg)", "QualifiedWeight", typeof(decimal?), isRequired: false),
            new("不合格返整支数", "DefectReworkQuantity", typeof(int?), isRequired: false),
            new("不合格入库支数", "DefectWarehouseQuantity", typeof(int?), isRequired: false),
            new("不合格报废支数", "DefectScrapQuantity", typeof(int?), isRequired: false),
            new("不合格情况描述", "DefectDescription", typeof(string), isRequired: false),
            new("外径范围", "OuterDiameterRange", typeof(string), isRequired: false),
            new("壁厚范围", "WallThicknessRange", typeof(string), isRequired: false),
            new("长度余量范围", "LengthAllowanceRange", typeof(string), isRequired: false),
            new("压力Mpa", "Pressure", typeof(decimal?), isRequired: false),
            new("保压时间s", "HoldTime", typeof(int?), isRequired: false),
            new("检验备注", "Remark", typeof(string), isRequired: false),
        }),

        ["BatchOperationLog"] = new EntityDef("批次操作日志", "批次操作日志", typeof(BatchOperationLog), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "ProductionBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "ProductionBatchId" },
            new("操作类型", "OperationType"),
            new("操作详情", "Detail", typeof(string), isRequired: false),
        }),

        ["InventoryBatch"] = new EntityDef("库存批次", "库存批次", typeof(InventoryBatch), 8, "BatchNo", new List<ColumnDef>
        {
            new("批次号", "BatchNo"),
            new("仓库编码", null!) { IsFkColumn = true, FkEntityKey = "Warehouse", FkLookupProperty = "Code", FkTargetProperty = "WarehouseId" },
            new("物料类型", "MaterialType"),
            new("厂内钢种", "PlantGrade"),
            new("名义规格", "Specification"),
            new("入库来源", "InboundSource"),
            new("来料单位", "SourceName"),
            new("入库日期", "InboundDate", typeof(DateTime)),
            new("炉号", "HeatNo", typeof(string), isRequired: false),
            new("生产批号", "ProductionBatchNo", typeof(string), isRequired: false),
            new("长度状态", "LengthStatus", typeof(string), isRequired: false),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("入库支数", "InitialQuantity", typeof(int)),
            new("入库重量(kg)", "InitialWeight", typeof(decimal)),
            new("理论单支重(kg)", "UnitWeight", typeof(decimal?), isRequired: false),
            new("米数(m)", "Meters", typeof(decimal?), isRequired: false),
            new("当前剩余支数", "RemainingQuantity", typeof(int)),
            new("当前剩余重量(kg)", "RemainingWeight", typeof(decimal)),
            new("实际规格", "ActualSpecification", typeof(string), isRequired: false),
            new("实际外径(mm)", "ActualOuterDiameter", typeof(decimal?), isRequired: false),
            new("实际壁厚(mm)", "ActualWallThickness", typeof(decimal?), isRequired: false),
            new("表面状态", "SurfaceCondition", typeof(string), isRequired: false),
            new("放置区域", "LocationArea", typeof(string), isRequired: false),
            new("放置架号", "LocationRack", typeof(string), isRequired: false),
            new("次品原因", "DefectReason", typeof(string), isRequired: false),
            new("责任类型", "LiabilityType", typeof(string), isRequired: false),
            new("原始来料单位", "OriginalSupplier", typeof(string), isRequired: false),
            new("挂牌号", "TagNo", typeof(string), isRequired: false),
            new("次品备注", "DefectRemark", typeof(string), isRequired: false),
            new("工单号", "WorkOrderNo", typeof(string), isRequired: false),
            new("订单号", "SalesOrderNo", typeof(string), isRequired: false),
            new("是否关联工单", "IsLinkedToWorkOrder", typeof(bool)),
            new("项次ID", "OrderItemIds", typeof(string), isRequired: false),
            new("来源单号", "SourceOrderNo", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["OutboundRecord"] = new EntityDef("出库记录", "出库记录", typeof(OutboundRecord), 8, null, new List<ColumnDef>
        {
            new("批次号", null!) { IsFkColumn = true, FkEntityKey = "InventoryBatch", FkLookupProperty = "BatchNo", FkTargetProperty = "InventoryBatchId" },
            new("出库类型", "OutboundType", typeof(OutboundType)),
            new("物料单号", "SourceOrderNo", typeof(string), isRequired: false),
            new("目标单位", "TargetCompany", typeof(string), isRequired: false),
            new("出库支数", "OutboundQuantity", typeof(int)),
            new("出库重量(kg)", "OutboundWeight", typeof(decimal)),
            new("出库日期", "OutboundDate", typeof(DateTime)),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 第9批：各类计划 ===
        ["InventoryPlan"] = new EntityDef("库存计划", "库存计划", typeof(InventoryPlan), 9, null, new List<ColumnDef>
        {
            new("工单号", null!) { IsFkColumn = true, FkEntityKey = "WorkOrder", FkLookupProperty = "WorkOrderNo", FkTargetProperty = "WorkOrderId" },
            new("计划日期", "PlanDate", typeof(DateTime)),
            new("库存批次号", "InventoryBatchNo"),
            new("批次号", "BatchNo"),
            new("物料名称", "MaterialType"),
            new("工厂牌号", "PlantGrade"),
            new("规格", "Specification"),
            new("放置区域", "LocationArea", typeof(string), isRequired: false),
            new("放置架号", "LocationRack", typeof(string), isRequired: false),
            new("投料倍率", "InputMultiple", typeof(int)),
            new("使用模式", "UsageMode"),
            new("出库支数", "UsedQuantity", typeof(int?), isRequired: false),
            new("出库重量(kg)", "UsedWeight", typeof(decimal)),
            new("要求到位日期", "RequiredDate", typeof(DateTime?), isRequired: false),
            new("计划状态", "PlanStatus", typeof(InventoryPlanStatus), isEnum: true),
            new("改制类型", "ReworkType", typeof(ReworkType?), isEnum: true, isRequired: false),
            new("简化工艺路线", "ProcessPlan", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["PurchaseSemiPlan"] = new EntityDef("荒管采购计划", "荒管采购计划", typeof(PurchaseSemiPlan), 9, null, new List<ColumnDef>
        {
            new("工单号", null!) { IsFkColumn = true, FkEntityKey = "WorkOrder", FkLookupProperty = "WorkOrderNo", FkTargetProperty = "WorkOrderId" },
            new("计划日期", "PlanDate", typeof(DateTime)),
            new("调整成品壁厚(mm)", "AdjustedWallThickness", typeof(decimal)),
            new("成材率(%)", "YieldRate", typeof(decimal)),
            new("投料倍率", "InputMultiple", typeof(int)),
            new("正品率(%)", "QualifiedRate", typeof(decimal)),
            new("原料类型", "RawMaterialType", typeof(RawMaterialType), isEnum: true),
            new("工厂牌号", "PlantGrade"),
            new("原料规格", "RawMaterialSpec"),
            new("需求单重(kg/支)", "RequiredUnitWeight", typeof(decimal?), isRequired: false),
            new("需求支数", "RequiredPieces", typeof(int?), isRequired: false),
            new("需求重量(kg)", "RequiredWeight", typeof(decimal)),
            new("要求到货日期", "RequiredDate", typeof(DateTime), isRequired: true),
            new("工艺路线", "ProcessPlan", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        ["PurchaseFinishedPlan"] = new EntityDef("成品采购计划", "成品采购计划", typeof(PurchaseFinishedPlan), 9, null, new List<ColumnDef>
        {
            new("工单号", null!) { IsFkColumn = true, FkEntityKey = "WorkOrder", FkLookupProperty = "WorkOrderNo", FkTargetProperty = "WorkOrderId" },
            new("计划日期", "PlanDate", typeof(DateTime)),
            new("成品类型", "ProductType", typeof(FinishedProductType), isEnum: true),
            new("工厂牌号", "PlantGrade"),
            new("规格", "Specification"),
            new("外径负公差(mm)", "OuterDiameterNegative", typeof(decimal)),
            new("外径正公差(mm)", "OuterDiameterPositive", typeof(decimal)),
            new("壁厚负公差(mm)", "WallThicknessNegative", typeof(decimal)),
            new("壁厚正公差(mm)", "WallThicknessPositive", typeof(decimal)),
            new("长度状态", "LengthStatus", typeof(LengthStatus), isEnum: true),
            new("最小长度(mm)", "MinLength", typeof(decimal?), isRequired: false),
            new("最大长度(mm)", "MaxLength", typeof(decimal?), isRequired: false),
            new("交货状态", "DeliveryState", typeof(DeliveryState), isEnum: true),
            new("采购支数", "RequiredPiece", typeof(int?), isRequired: false),
            new("采购重量(kg)", "RequiredWeight", typeof(decimal)),
            new("要求到货日期", "RequiredDate", typeof(DateTime?), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),

        // === 独立实体：牌号化学成分 ===
        ["ChemicalComposition"] = new EntityDef("牌号化学成分", "牌号化学成分", typeof(ChemicalComposition), 1, "PlantGrade", new List<ColumnDef>
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
        ["ChemicalValidationRule"] = new EntityDef("牌号验证规则", "牌号验证规则", typeof(ChemicalValidationRule), 1, "PlantGrade", new List<ColumnDef>
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
        ["FurnaceRegistration"] = new EntityDef("来料炉号登记", "来料炉号登记", typeof(FurnaceRegistration), 1, null, new List<ColumnDef>
        {
            new("来料日期", "IncomingDate", typeof(DateTime)),
            new("原料单位", "RawMaterialUnit"),
            new("原料类型", "RawMaterialType"),
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

        ["RoundBarPiercingPlan"] = new EntityDef("圆棒穿孔计划", "圆棒穿孔计划", typeof(RoundBarPiercingPlan), 9, null, new List<ColumnDef>
        {
            new("工单号", null!) { IsFkColumn = true, FkEntityKey = "WorkOrder", FkLookupProperty = "WorkOrderNo", FkTargetProperty = "WorkOrderId" },
            new("计划日期", "PlanDate", typeof(DateTime)),
            new("调整成品壁厚(mm)", "AdjustedWallThickness", typeof(decimal)),
            new("成材率(%)", "YieldRate", typeof(decimal)),
            new("投料倍率", "InputMultiple", typeof(int)),
            new("正品率(%)", "QualifiedRate", typeof(decimal)),
            new("原料类型", "RawMaterialType", typeof(RawMaterialType), isEnum: true),
            new("工厂牌号", "PlantGrade"),
            new("圆棒规格", "RoundBarSpec"),
            new("穿孔规格", "PiercingSpec"),
            new("需求单重(kg/支)", "RequiredUnitWeight", typeof(decimal?), isRequired: false),
            new("需求支数", "RequiredPieces", typeof(int?), isRequired: false),
            new("需求重量(kg)", "RequiredWeight", typeof(decimal)),
            new("要求到货日期", "RequiredDate", typeof(DateTime), isRequired: true),
            new("工艺路线", "ProcessPlan", typeof(string), isRequired: false),
            new("备注", "Remark", typeof(string), isRequired: false),
        }),
    };

    public static readonly List<string> EntityOrder = new()
    {
        "Warehouse", "ProductionStandard", "StandardGradeMapping", "CustomerProfile", "SupplierProfile",
        "FurnaceRegistration", "ChemicalComposition", "ChemicalValidationRule",
        "SalesOrder",
        "OrderItem", "ProductRequirement",
        "WorkOrder", "Material",
        "PurchaseOrder", "SubcontractOrder", "SubcontractReturnItem", "ProductionBatch",
        "ProcessGroup", "ProductionRecord", "SectionOutsource", "OutsourceRecovery", "MaterialReceiveCheck", "ProcessInspection", "FinalInspection", "BatchOperationLog", "InventoryBatch", "OutboundRecord",
        "Equipment", "RepairOrder", "MaintenanceOrder", "InspectionRecord",
        "InventoryPlan", "PurchaseSemiPlan", "PurchaseFinishedPlan", "RoundBarPiercingPlan",
    };

    #endregion

    // ========== 实体元数据 ==========

    public Task<List<EntityInfo>> GetEntitiesAsync()
    {
        var result = Registry.Select(kvp => new EntityInfo
        {
            Key = kvp.Key,
            Name = kvp.Value.DisplayName,
        }).ToList();
        return Task.FromResult(result);
    }

    public string GetEntityDisplayName(string entityKey)
    {
        if (!Registry.TryGetValue(entityKey, out var def))
            throw new BusinessException($"不支持的实体类型: {entityKey}");
        return def.DisplayName;
    }

    #region 导出

    /// <summary>
    /// 导出指定实体的全部数据为 Excel
    /// </summary>
    public async Task<byte[]> ExportAsync(string entityKey)
    {
        if (!Registry.TryGetValue(entityKey, out var def))
            throw new BusinessException($"不支持的实体类型: {entityKey}");

        var data = await QueryAllAsync(def.Type);
        var propertyCache = BuildPropertyCache(def);

        // 构建FK反向缓存（用于导出时解析外键列的显示值）
        var fkReverseCache = await BuildFkReverseCacheForExportAsync(def);
        // 特殊缓存：OrderItem 复合键（用于 ProductRequirement 导出）
        var orderItemExportCache = await BuildOrderItemExportCacheAsync(def);

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add(def.DisplayName);

        // 表头
        for (int i = 0; i < def.Columns.Count; i++)
            sheet.Cells[1, i + 1].Value = def.Columns[i].Header;
        sheet.Cells[1, 1, 1, def.Columns.Count].Style.Font.Bold = true;

        // 数据行
        var row = 2;
        foreach (var item in data)
        {
            for (int col = 0; col < def.Columns.Count; col++)
            {
                var colDef = def.Columns[col];

                // FK列：解析引用实体的业务主键值
                if (colDef.IsFkColumn)
                {
                    var fkValue = ResolveFkExportValue(colDef, item, propertyCache, fkReverseCache, orderItemExportCache);
                    if (fkValue != null)
                        sheet.Cells[row, col + 1].Value = fkValue;
                    continue;
                }

                if (colDef.Property == null || !propertyCache.TryGetValue(colDef.Property, out var prop))
                    continue;

                var value = prop.GetValue(item);
                if (value == null)
                    continue;

                // 特殊处理：WorkOrder.OrderItemIds → 解析为"订单号|项次号"格式
                if (colDef.Property == "OrderItemIds" && value is string idsStr)
                {
                    sheet.Cells[row, col + 1].Value = await ResolveOrderItemIdsForExportAsync(idsStr);
                    continue;
                }

                if (colDef.IsEnum && colDef.EnumType != null)
                {
                    // value 可能是 string（字符串存储的枚举）或实际枚举值
                    if (value is string strValue)
                    {
                        try
                        {
                            var parsedEnum = EnumHelper.Parse(strValue, colDef.EnumType);
                            sheet.Cells[row, col + 1].Value = EnumHelper.GetDisplayName(colDef.EnumType, parsedEnum);
                        }
                        catch
                        {
                            sheet.Cells[row, col + 1].Value = strValue;
                        }
                    }
                    else
                    {
                        sheet.Cells[row, col + 1].Value = EnumHelper.GetDisplayName(colDef.EnumType, value);
                    }
                }
                else if (value is DateTime dt)
                {
                    sheet.Cells[row, col + 1].Value = dt.ToString("yyyy-MM-dd");
                }
                else if (value is DateTimeOffset dto)
                {
                    sheet.Cells[row, col + 1].Value = dto.ToString("yyyy-MM-dd HH:mm");
                }
                else if (value is bool b)
                {
                    sheet.Cells[row, col + 1].Value = b ? "是" : "否";
                }
                else if (value is decimal dec)
                {
                    sheet.Cells[row, col + 1].Value = dec.ToString("G29");
                }
                else
                {
                    sheet.Cells[row, col + 1].Value = value.ToString();
                }
            }
            row++;
        }

        sheet.Cells[1, 1, row - 1, def.Columns.Count].AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }

    #endregion

    #region 模板

    /// <summary>
    /// 生成导入模板（含1行示例数据）
    /// </summary>
    public async Task<byte[]> GenerateTemplateAsync(string entityKey)
    {
        if (!Registry.TryGetValue(entityKey, out var def))
            throw new BusinessException($"不支持的实体类型: {entityKey}");

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add(def.DisplayName);

        // 表头
        for (int i = 0; i < def.Columns.Count; i++)
            sheet.Cells[1, i + 1].Value = def.Columns[i].Header;
        sheet.Cells[1, 1, 1, def.Columns.Count].Style.Font.Bold = true;

        // 系统字段标记为灰色底色
        for (int i = 0; i < def.Columns.Count; i++)
        {
            if (def.Columns[i].IsSystem)
                sheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        }

        // 示例数据行（尽量提供示例值）
        var sampleRow = 2;
        var fkReverseCache = await BuildFkReverseCacheForExportAsync(def);
        var orderItemExportCache = await BuildOrderItemExportCacheAsync(def);
        foreach (var colDef in def.Columns)
        {
            if (colDef.IsSystem) continue;
            if (colDef.EnumType != null)
            {
                if (!colDef.EnumType.IsEnum)
                    throw new BusinessException($"列 '{colDef.Header}' 的枚举类型 '{colDef.EnumType.FullName}' 无效");
                var values = Enum.GetValues(colDef.EnumType);
                if (values.Length > 0)
                    sheet.Cells[sampleRow, def.Columns.IndexOf(colDef) + 1].Value = EnumHelper.GetDisplayName(colDef.EnumType, values.GetValue(0)!);
            }
            else if (colDef.IsFkColumn)
            {
                // FK列：从缓存中取第一个示例值
                var fkSample = GetFkSampleValue(colDef, fkReverseCache);
                if (fkSample != null)
                    sheet.Cells[sampleRow, def.Columns.IndexOf(colDef) + 1].Value = fkSample;
            }
            else if (colDef.PropertyType == typeof(DateTime) || colDef.PropertyType == typeof(DateTime?))
            {
                sheet.Cells[sampleRow, def.Columns.IndexOf(colDef) + 1].Value = DateTime.Today.ToString("yyyy-MM-dd");
            }
            else if (colDef.PropertyType == typeof(bool) || colDef.PropertyType == typeof(bool?))
            {
                sheet.Cells[sampleRow, def.Columns.IndexOf(colDef) + 1].Value = "是";
            }
            else if (colDef.PropertyType == typeof(int) || colDef.PropertyType == typeof(int?))
            {
                sheet.Cells[sampleRow, def.Columns.IndexOf(colDef) + 1].Value = 1;
            }
            else if (colDef.PropertyType == typeof(decimal) || colDef.PropertyType == typeof(decimal?))
            {
                sheet.Cells[sampleRow, def.Columns.IndexOf(colDef) + 1].Value = "0.00";
            }
            else if (!colDef.IsFkColumn)
            {
                sheet.Cells[sampleRow, def.Columns.IndexOf(colDef) + 1].Value = colDef.Header;
            }
        }

        sheet.Cells[1, 1, 2, def.Columns.Count].AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }

    #endregion

    #region 预览

    /// <summary>
    /// 预览导入结果（验证但不写入数据库）
    /// </summary>
    public async Task<ImportPreviewResult> PreviewAsync(string entityKey, byte[] fileData, string? userName)
    {
        if (!Registry.TryGetValue(entityKey, out var def))
            throw new BusinessException($"不支持的实体类型: {entityKey}");

        var rows = ParseExcel(fileData, def);
        var result = new ImportPreviewResult { TotalRows = rows.Count };

        // 构建FK查询缓存
        var fkCache = await BuildFkCacheAsync(def);

        // 查询已存在的记录（用于重复检测）
        var existingKeys = await LoadExistingKeysAsync(def);

        foreach (var row in rows)
        {
            var errors = new List<string>();

            // 验证必填字段
            foreach (var colDef in def.Columns.Where(c => c.IsRequired && !c.IsFkColumn && !c.IsSystem))
            {
                if (!row.Values.TryGetValue(colDef.Header, out var val) || string.IsNullOrWhiteSpace(val))
                    errors.Add($"{colDef.Header} 不能为空");
            }

            // 检查重复
            var rowKey = GetRowKey(def, row);
            var isDuplicate = rowKey != null && existingKeys.Contains(rowKey);

            result.RowResults.Add(new ImportRowResult
            {
                RowNumber = row.RowNumber,
                Key = rowKey,
                Errors = errors,
                IsDuplicate = isDuplicate,
                IsValid = errors.Count == 0,
                Data = row,
            });
        }

        result.ValidCount = result.RowResults.Count(r => r.IsValid);
        result.ErrorCount = result.RowResults.Count(r => !r.IsValid);
        result.DuplicateCount = result.RowResults.Count(r => r.IsDuplicate);

        return result;
    }

    #endregion

    #region 导入

    /// <summary>
    /// 执行导入（EF Core事务：禁约束 → 累积写入 → 批量保存 → 校验约束 → 提交/回滚）
    /// </summary>
    public async Task<ImportResult> ImportAsync(string entityKey, byte[] fileData, string strategy, string? userName)
    {
        if (!Registry.TryGetValue(entityKey, out var def))
            throw new BusinessException($"不支持的实体类型: {entityKey}");

        var rows = ParseExcel(fileData, def);
        var result = new ImportResult { TotalRows = rows.Count, Strategy = strategy };
        var overwrite = strategy == "overwrite";

        // 构建FK查询缓存
        var fkCache = await BuildFkCacheAsync(def);

        // 预加载已存在记录缓存（用于重复检测）
        var existingCache = await LoadExistingEntitiesAsync(def);

        // 使用EF Core管理事务（避免MARS/savepoint冲突）
        using var transaction = await _context.Database.BeginTransactionAsync();
        var dbTransaction = transaction.GetDbTransaction();

        try
        {
            // 1. 禁用所有外键约束
            await DisableAllConstraintsAsync(dbTransaction.Connection!, dbTransaction);

            // 2. 逐行累积到DbContext（不逐行保存）
            // 跟踪批次内已分配的系统编码，避免重复
            var pendingCodes = def.Columns
                .Where(c => c.IsSystem && c.Property != null && CodePrefixMap.ContainsKey(c.Property))
                .ToDictionary(c => c.Property!, _ => new HashSet<string>());

            // ProcessGroup 特殊处理：有子记录引用的工序组原地更新（保留ID），无引用的安全删除
            // 避免 FK 约束冲突（FK_ProductionRecord_ProcessGroup_ProcessGroupId 等）
            if (entityKey == "ProcessGroup")
            {
                var batchNoCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "ProductionBatch");
                if (batchNoCol != null && fkCache.TryGetValue("ProductionBatch", out var batchLookup))
                {
                    var batchNos = rows
                        .Select(r => r.Values.GetValueOrDefault(batchNoCol.Header, ""))
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var batchIds = batchNos
                        .Select(bn => batchLookup.GetValueOrDefault(bn))
                        .Where(id => id > 0)
                        .ToList();
                    var existing = await _context.Set<ProcessGroup>()
                        .Where(pg => batchIds.Contains(pg.ProductionBatchId))
                        .ToListAsync();

                    if (existing.Count > 0)
                    {
                        // 检查哪些工序组有子记录引用
                        var existingIds = existing.Select(e => e.Id).ToList();
                        var referencedIds = new HashSet<int>();

                        var prodRefs = await _context.Set<ProductionRecord>()
                            .Where(r => existingIds.Contains(r.ProcessGroupId))
                            .Select(r => r.ProcessGroupId)
                            .Distinct()
                            .ToListAsync();
                        foreach (var id in prodRefs) referencedIds.Add(id);

                        var soRefs = await _context.Set<SectionOutsource>()
                            .Where(s => existingIds.Contains(s.ProcessGroupId))
                            .Select(s => s.ProcessGroupId)
                            .Distinct()
                            .ToListAsync();
                        foreach (var id in soRefs) referencedIds.Add(id);

                        var piRefs = await _context.Set<ProcessInspection>()
                            .Where(p => existingIds.Contains(p.ProcessGroupId))
                            .Select(p => p.ProcessGroupId)
                            .Distinct()
                            .ToListAsync();
                        foreach (var id in piRefs) referencedIds.Add(id);

                        // 有引用的工序组：保留ID，按 (ProductionBatchId, SequenceNumber) 索引
                        var referencedPgs = existing.Where(e => referencedIds.Contains(e.Id)).ToList();
                        var pgByKey = referencedPgs.ToDictionary(
                            pg => (pg.ProductionBatchId, pg.SequenceNumber));

                        // 无引用的工序组：安全删除
                        var unreferencedPgs = existing.Where(e => !referencedIds.Contains(e.Id)).ToList();
                        if (unreferencedPgs.Count > 0)
                        {
                            _context.Set<ProcessGroup>().RemoveRange(unreferencedPgs);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("已清理 {Count} 个无引用的旧工序组记录", unreferencedPgs.Count);
                        }

                        // 对有引用的工序组，从导入行中匹配并原地更新属性
                        if (referencedPgs.Count > 0)
                        {
                            var seqCol = def.Columns.FirstOrDefault(c => c.Property == "SequenceNumber");
                            var propertyCache = BuildPropertyCache(def);
                            var now = DateTimeOffset.Now;
                            var rowsToSkip = new List<ImportRowData>();

                            foreach (var row in rows)
                            {
                                var batchNo = row.Values.GetValueOrDefault(batchNoCol.Header, "");
                                var batchId = batchLookup.GetValueOrDefault(batchNo);
                                if (batchId <= 0) continue;

                                var seqStr = seqCol != null ? row.Values.GetValueOrDefault(seqCol.Header, "") : "";
                                if (!int.TryParse(seqStr, out var seq)) continue;

                                if (pgByKey.TryGetValue((batchId, seq), out var existingPg))
                                {
                                    foreach (var colDef in def.Columns)
                                    {
                                        // 跳过系统列、FK列、SequenceNumber（匹配键不更新）
                                        if (colDef.IsSystem || colDef.IsFkColumn) continue;
                                        if (colDef.Property == "SequenceNumber") continue;
                                        if (colDef.Property == null || !propertyCache.TryGetValue(colDef.Property, out var prop)) continue;
                                        if (!row.Values.TryGetValue(colDef.Header, out var cellValue)) continue;

                                        if (string.IsNullOrWhiteSpace(cellValue))
                                        {
                                            if (prop.PropertyType.IsGenericType &&
                                                prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                                                prop.SetValue(existingPg, null);
                                            continue;
                                        }

                                        var value = ConvertValue(cellValue, prop.PropertyType, colDef);
                                        prop.SetValue(existingPg, value);
                                    }

                                    // 更新审计字段
                                    if (existingPg is BaseEntity be)
                                    {
                                        be.UpdatedTime = now;
                                        be.UpdatedBy = userName ?? "system";
                                    }

                                    rowsToSkip.Add(row);
                                }
                            }

                            // 从导入行中移除已原地更新的行，避免重复创建
                            foreach (var row in rowsToSkip)
                                rows.Remove(row);

                            _logger.LogInformation("已原地更新 {Count} 个有引用的工序组记录", referencedPgs.Count);
                        }
                    }
                }
            }

            // OrderItem 特殊处理：导入前清理关联订单下已有的项次（避免唯一键冲突 UK_OrderItem_Sequence_Active）
            if (entityKey == "OrderItem")
            {
                var orderNoCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "SalesOrder");
                if (orderNoCol != null && fkCache.TryGetValue("SalesOrder", out var salesOrderLookup))
                {
                    var orderNos = rows
                        .Select(r => r.Values.GetValueOrDefault(orderNoCol.Header, ""))
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var salesOrderIds = orderNos
                        .Select(no => salesOrderLookup.GetValueOrDefault(no))
                        .Where(id => id > 0)
                        .ToList();
                    var existing = await _context.Set<OrderItem>()
                        .Where(oi => salesOrderIds.Contains(oi.SalesOrderId))
                        .ToListAsync();
                    if (existing.Count > 0)
                    {
                        _context.Set<OrderItem>().RemoveRange(existing);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("已清理 {Count} 个旧的订单项次记录", existing.Count);
                    }
                }
            }

            // ProductRequirement 特殊处理：导入前清理关联项次下已有的技术要求（避免唯一键冲突 UK_ProductRequirement_OrderItemId）
            if (entityKey == "ProductRequirement")
            {
                var orderNoCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "OrderItem" && c.FkLookupProperty == "Id");
                var seqCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "OrderItem" && c.FkLookupProperty == "Sequence");
                if (orderNoCol != null && seqCol != null && fkCache.TryGetValue("OrderItem", out var oiCache))
                {
                    var compositeKeys = rows
                        .Select(r =>
                        {
                            var orderNo = r.Values.GetValueOrDefault(orderNoCol.Header, "");
                            var seq = r.Values.GetValueOrDefault(seqCol.Header, "");
                            return string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(seq) ? null : $"{orderNo}|{seq}";
                        })
                        .Where(k => k != null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()!;
                    var orderItemIds = compositeKeys
                        .Select(k => oiCache.GetValueOrDefault(k!))
                        .Where(id => id > 0)
                        .ToList();
                    var existing = await _context.Set<ProductRequirement>()
                        .Where(pr => orderItemIds.Contains(pr.OrderItemId))
                        .ToListAsync();
                    if (existing.Count > 0)
                    {
                        _context.Set<ProductRequirement>().RemoveRange(existing);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("已清理 {Count} 个旧的技术要求记录", existing.Count);
                    }
                }
            }

            // SubcontractReturnItem 特殊处理：导入前清理关联委外单下已有的退货项（避免唯一键冲突 UK_ReturnItem_Seq）
            if (entityKey == "SubcontractReturnItem")
            {
                var orderNoCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "SubcontractOrder");
                if (orderNoCol != null && fkCache.TryGetValue("SubcontractOrder", out var subOrderLookup))
                {
                    var orderNos = rows
                        .Select(r => r.Values.GetValueOrDefault(orderNoCol.Header, ""))
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var subOrderIds = orderNos
                        .Select(no => subOrderLookup.GetValueOrDefault(no))
                        .Where(id => id > 0)
                        .ToList();
                    var existing = await _context.Set<SubcontractReturnItem>()
                        .Where(sri => subOrderIds.Contains(sri.SubcontractOrderId))
                        .ToListAsync();
                    if (existing.Count > 0)
                    {
                        _context.Set<SubcontractReturnItem>().RemoveRange(existing);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("已清理 {Count} 个旧的委外退货项记录", existing.Count);
                    }
                }
            }

            foreach (var row in rows)
            {
                try
                {
                    await ImportRowAsync(def, row, fkCache, overwrite, userName, existingCache, pendingCodes);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add(new ImportRowError
                    {
                        RowNumber = row.RowNumber,
                        Message = ex.Message,
                    });
                }
            }

            // 3. 批量保存所有累积的变更
            await _context.SaveChangesAsync();

            // 4. 启用并验证所有外键约束
            var checkErrors = await EnableAndCheckConstraintsAsync(dbTransaction.Connection!, dbTransaction);
            if (checkErrors.Count > 0)
            {
                throw new BusinessException("外键约束验证失败，共 " + checkErrors.Count + " 个错误");
            }

            // 5. 提交事务
            await transaction.CommitAsync();
            _logger.LogInformation(
                "导入 {Entity} 完成: 共 {Total} 行, 成功 {Success}, 失败 {Failed}, 策略 {Strategy}",
                entityKey, result.TotalRows, result.SuccessCount, result.FailedCount, strategy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入 {Entity} 失败，已回滚: {Message}", entityKey, ex.Message);
            await transaction.RollbackAsync();
            result.SuccessCount = 0;
            result.FailedCount = result.TotalRows;
            result.HasRolledBack = true;
            result.RollbackReason = GetRollbackReason(ex);
        }

        return result;
    }

    #endregion

    #region 私有方法

    private async Task<List<object>> QueryAllAsync(Type entityType)
    {
        var dbSet = _context.GetType().GetMethod("Set", Type.EmptyTypes)!
            .MakeGenericMethod(entityType)
            .Invoke(_context, null)!;

        var query = (IQueryable)dbSet;
        var result = await Task.Run(() =>
        {
            var list = new List<object>();
            foreach (var item in (IEnumerable)query)
                list.Add(item);
            return list;
        });

        return result;
    }

    private Dictionary<string, PropertyInfo> BuildPropertyCache(EntityDef def)
    {
        var cache = new Dictionary<string, PropertyInfo>();
        foreach (var prop in def.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanRead && prop.CanWrite)
                cache[prop.Name] = prop;
        }
        return cache;
    }

    private List<ImportRowData> ParseExcel(byte[] fileData, EntityDef def)
    {
        var rows = new List<ImportRowData>();

        using var stream = new MemoryStream(fileData);
        using var package = new ExcelPackage(stream);
        var sheet = package.Workbook.Worksheets[0];
        if (sheet == null || sheet.Dimension == null)
            return rows;

        // 读取表头（第一行）
        var headerCount = sheet.Dimension.Columns;
        var headers = new List<string>();
        for (int c = 1; c <= headerCount; c++)
        {
            var header = sheet.Cells[1, c].Text?.Trim();
            headers.Add(header ?? "");
        }

        // 映射表头到列定义
        var columnMapping = new List<(int colIndex, ColumnDef? colDef)>();
        foreach (var header in headers)
        {
            var colDef = def.Columns.FirstOrDefault(c => c.Header == header);
            columnMapping.Add((headers.IndexOf(header), colDef));
        }

        // 读取数据行（从第2行开始）
        for (int r = 2; r <= sheet.Dimension.Rows; r++)
        {
            var hasData = false;
            var data = new Dictionary<string, string>();
            var rowNumber = r;

            foreach (var (colIndex, colDef) in columnMapping)
            {
                if (colDef == null) continue;
                var cellValue = sheet.Cells[r, colIndex + 1]?.Text?.Trim();
                data[colDef.Header] = cellValue ?? "";
                if (!string.IsNullOrEmpty(cellValue))
                    hasData = true;
            }

            if (hasData)
                rows.Add(new ImportRowData { RowNumber = rowNumber, Values = data });
        }

        return rows;
    }

    private async Task<Dictionary<string, Dictionary<string, int>>> BuildFkCacheAsync(EntityDef def)
    {
        var cache = new Dictionary<string, Dictionary<string, int>>();

        foreach (var colDef in def.Columns.Where(c => c.IsFkColumn && !c.FkRequiresJoin))
        {
            if (colDef.FkEntityKey == null || !Registry.TryGetValue(colDef.FkEntityKey, out var fkDef))
                continue;

            var key = colDef.FkEntityKey;
            if (cache.ContainsKey(key)) continue;

            var fkData = await QueryAllAsync(fkDef.Type);
            var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var idProp = fkDef.Type.GetProperty("Id");
            var lookupProp = fkDef.Type.GetProperty(colDef.FkLookupProperty!);

            if (idProp != null && lookupProp != null)
            {
                foreach (var item in fkData)
                {
                    var id = (int)idProp.GetValue(item)!;
                    var val = lookupProp.GetValue(item)?.ToString();
                    if (val != null && !lookup.ContainsKey(val))
                        lookup[val] = id;
                    else if (val != null)
                        _logger.LogWarning("FK缓存发现重复键: 实体 {Entity}, 字段 {Field}, 值 {Value}",
                            colDef.FkEntityKey, colDef.FkLookupProperty, val);
                }
            }

            cache[key] = lookup;
        }

        // 特殊处理：OrderItem FK 解析（需要 SalesOrderNo + Sequence）
        if (def.Columns.Any(c => c.FkRequiresJoin))
        {
            var orderItems = await _context.Set<OrderItem>()
                .Include(oi => oi.SalesOrder)
                .ToListAsync();

            var orderItemLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var oi in orderItems)
            {
                var key = $"{oi.SalesOrder.OrderNumber}|{oi.Sequence}";
                if (!orderItemLookup.ContainsKey(key))
                    orderItemLookup[key] = oi.Id;
            }

            cache["OrderItem"] = orderItemLookup;
        }

        // 特殊处理：ProcessGroup FK 解析（需要 BatchNo + SequenceNumber）
        if (def.Columns.Any(c => c.FkEntityKey == "ProcessGroup"))
        {
            var processGroups = await _context.Set<ProcessGroup>()
                .Include(pg => pg.ProductionBatch)
                .ToListAsync();

            var pgLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pg in processGroups)
            {
                var key = $"{pg.ProductionBatch.BatchNo}|{pg.SequenceNumber}";
                if (!pgLookup.ContainsKey(key))
                    pgLookup[key] = pg.Id;
            }

            cache["ProcessGroup"] = pgLookup;

            // 按工段名称查找工序组的缓存（用于 ProductionRecord/SectionOutsource 按"批次号+工段名称"匹配）
            var pgIdBySectionLk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var pgSeqBySectionLk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            void AddSectionLk(Dictionary<string, int> idLk, Dictionary<string, int> seqLk,
                string batchNo, string sectionName, int? orderVal, int pgId)
            {
                if (!orderVal.HasValue) return;
                var key = $"{batchNo}|{sectionName}";
                if (!idLk.ContainsKey(key))
                {
                    idLk[key] = pgId;
                    seqLk[key] = orderVal.Value;
                }
            }
            foreach (var pg in processGroups)
            {
                var bn = pg.ProductionBatch.BatchNo;
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "冷轧拔", pg.ColdRollDraw, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "油管断", pg.OilPipeCut, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "去油", pg.Degrease, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "固溶", pg.Solution, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "矫直", pg.Straighten, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "断切", pg.Cut, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "测壁厚", pg.ThicknessMeasure, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "酸洗", pg.Pickle, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "外抛光", pg.OuterPolish, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "内修磨", pg.InnerGrinding, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "外点磨", pg.OuterSpotGrinding, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "检验", pg.Inspection, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "打焊头", pg.WeldingHead, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "润滑", pg.Lubrication, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, "入库", pg.Warehouse, pg.Id);
            }
            cache["ProcessGroupIdBySection"] = pgIdBySectionLk;
            cache["ProcessGroupSeqBySection"] = pgSeqBySectionLk;
        }

        // 特殊处理：SectionOutsource FK 解析（需要 BatchNo + SectionName + OutsourceVendor）
        if (def.Columns.Any(c => c.FkEntityKey == "SectionOutsource"))
        {
            var sectionOutsources = await _context.Set<SectionOutsource>()
                .Include(so => so.ProductionBatch)
                .ToListAsync();

            var soLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var so in sectionOutsources)
            {
                var key = $"{so.ProductionBatch.BatchNo}|{so.SectionName}|{so.OutsourceVendor}";
                if (!soLookup.ContainsKey(key))
                    soLookup[key] = so.Id;
            }

            cache["SectionOutsource"] = soLookup;
        }

        return cache;
    }

    /// <summary>
    /// 构建FK反向缓存（用于导出时解析外键列的显示值）
    /// 映射：FkEntityKey → { fkId → 业务主键值 }
    /// </summary>
    private async Task<Dictionary<string, Dictionary<int, string>>> BuildFkReverseCacheForExportAsync(EntityDef def)
    {
        var cache = new Dictionary<string, Dictionary<int, string>>();

        foreach (var colDef in def.Columns.Where(c => c.IsFkColumn && !c.FkRequiresJoin))
        {
            if (colDef.FkEntityKey == null || !Registry.TryGetValue(colDef.FkEntityKey, out var fkDef))
                continue;
            if (cache.ContainsKey(colDef.FkEntityKey)) continue;

            var fkData = await QueryAllAsync(fkDef.Type);
            var lookup = new Dictionary<int, string>();
            var idProp = fkDef.Type.GetProperty("Id");
            var targetProp = fkDef.Type.GetProperty(colDef.FkLookupProperty!);

            if (idProp != null && targetProp != null)
            {
                foreach (var item in fkData)
                {
                    var id = (int)idProp.GetValue(item)!;
                    var val = targetProp.GetValue(item)?.ToString();
                    if (val != null && !lookup.ContainsKey(id))
                        lookup[id] = val;
                }
            }

            cache[colDef.FkEntityKey] = lookup;
        }

        // 特殊处理：ProcessGroup 反向缓存（用于导出时解析 ProcessGroupId → SequenceNumber）
        if (def.Columns.Any(c => c.FkEntityKey == "ProcessGroup"))
        {
            var processGroups = await _context.Set<ProcessGroup>()
                .Include(pg => pg.ProductionBatch)
                .ToListAsync();

            var pgReverseLookup = new Dictionary<int, string>();
            foreach (var pg in processGroups)
            {
                var key = $"{pg.ProductionBatch.BatchNo}|{pg.SequenceNumber}";
                if (!pgReverseLookup.ContainsKey(pg.Id))
                    pgReverseLookup[pg.Id] = key;
            }

            cache["ProcessGroup"] = pgReverseLookup;
        }

        // 特殊处理：SectionOutsource 反向缓存（用于导出时解析 SectionOutsourceId → BatchNo,SectionName,Vendor）
        if (def.Columns.Any(c => c.FkEntityKey == "SectionOutsource"))
        {
            var sectionOutsources = await _context.Set<SectionOutsource>()
                .Include(so => so.ProductionBatch)
                .ToListAsync();

            var soReverseLookup = new Dictionary<int, string>();
            foreach (var so in sectionOutsources)
            {
                var key = $"{so.ProductionBatch.BatchNo}|{so.SectionName}|{so.OutsourceVendor}";
                if (!soReverseLookup.ContainsKey(so.Id))
                    soReverseLookup[so.Id] = key;
            }

            cache["SectionOutsource"] = soReverseLookup;
        }

        return cache;
    }

    /// <summary>
    /// 构建 OrderItem 复合键缓存（用于 ProductRequirement 导出时的 FK 解析）
    /// 映射：OrderItem.Id → { OrderNumber, Sequence }
    /// </summary>
    private async Task<Dictionary<int, (string orderNo, int sequence)>> BuildOrderItemExportCacheAsync(EntityDef def)
    {
        var cache = new Dictionary<int, (string, int)>();

        if (def.Columns.Any(c => c.FkRequiresJoin && c.FkEntityKey == "OrderItem"))
        {
            var orderItems = await _context.Set<OrderItem>()
                .Include(oi => oi.SalesOrder)
                .ToListAsync();

            foreach (var oi in orderItems)
            {
                var orderNo = oi.SalesOrder?.OrderNumber ?? "";
                cache[oi.Id] = (orderNo, oi.Sequence);
            }
        }

        return cache;
    }

    /// <summary>
    /// 解析导出时 FK 列的显示值
    /// </summary>
    private string? ResolveFkExportValue(ColumnDef colDef, object entity,
        Dictionary<string, PropertyInfo> propertyCache,
        Dictionary<string, Dictionary<int, string>> fkReverseCache,
        Dictionary<int, (string orderNo, int sequence)> orderItemExportCache)
    {
        // 特殊处理：ProductRequirement → OrderItem 复合键
        if (colDef.FkRequiresJoin && colDef.FkEntityKey == "OrderItem")
        {
            if (colDef.FkTargetProperty != null &&
                propertyCache.TryGetValue(colDef.FkTargetProperty, out var oiIdProp))
            {
                var oiIdVal = oiIdProp.GetValue(entity);
                if (oiIdVal is int oiId && orderItemExportCache.TryGetValue(oiId, out var oiInfo))
                {
                    // "订单号"列 → 返回 SalesOrder.OrderNumber
                    if (colDef.FkLookupProperty == "Id")
                        return oiInfo.orderNo;
                    // "项次号"列 → 返回 Sequence
                    if (colDef.FkLookupProperty == "Sequence")
                        return oiInfo.sequence.ToString();
                }
            }
            return null;
        }

        // 特殊处理：ProcessGroup 复合键（BatchNo|SequenceNumber → ProcessGroupId）
        if (colDef.FkRequiresJoin && colDef.FkEntityKey == "ProcessGroup")
        {
            if (colDef.FkTargetProperty != null &&
                propertyCache.TryGetValue(colDef.FkTargetProperty, out var pgIdProp))
            {
                var pgIdVal = pgIdProp.GetValue(entity);
                if (pgIdVal is int pgId &&
                    fkReverseCache.TryGetValue("ProcessGroup", out var pgCache) &&
                    pgCache.TryGetValue(pgId, out var pgCompositeKey))
                {
                    var parts = pgCompositeKey.Split('|', 2);
                    if (colDef.FkLookupProperty == "SequenceNumber" && parts.Length > 1)
                        return parts[1]; // 返回 SequenceNumber
                }
            }
            return null;
        }

        // 特殊处理：SectionOutsource 复合键（BatchNo|SectionName|Vendor → SectionOutsourceId）
        if (colDef.FkRequiresJoin && colDef.FkEntityKey == "SectionOutsource")
        {
            if (colDef.FkTargetProperty != null &&
                propertyCache.TryGetValue(colDef.FkTargetProperty, out var soIdProp))
            {
                var soIdVal = soIdProp.GetValue(entity);
                if (soIdVal is int soId &&
                    fkReverseCache.TryGetValue("SectionOutsource", out var soCache) &&
                    soCache.TryGetValue(soId, out var soCompositeKey))
                {
                    var parts = soCompositeKey.Split('|', 3);
                    if (colDef.FkLookupProperty == "BatchNo" && parts.Length > 0)
                        return parts[0];
                    if (colDef.FkLookupProperty == "SectionName" && parts.Length > 1)
                        return parts[1];
                    if (colDef.FkLookupProperty == "OutsourceVendor" && parts.Length > 2)
                        return parts[2];
                }
            }
            return null;
        }

        // 常规FK解析
        if (colDef.FkEntityKey == null || colDef.FkTargetProperty == null)
            return null;

        if (!propertyCache.TryGetValue(colDef.FkTargetProperty, out var fkIdProp))
            return null;

        var fkIdValue = fkIdProp.GetValue(entity);
        if (fkIdValue == null)
            return null;

        if (fkReverseCache.TryGetValue(colDef.FkEntityKey, out var fkLookup) &&
            fkLookup.TryGetValue((int)fkIdValue, out var displayVal))
        {
            return displayVal;
        }

        return null;
    }

    /// <summary>
    /// 获取 FK 列的示例值（模板用）
    /// </summary>
    private string? GetFkSampleValue(ColumnDef colDef,
        Dictionary<string, Dictionary<int, string>> fkReverseCache)
    {
        if (colDef.FkEntityKey == null) return null;

        if (fkReverseCache.TryGetValue(colDef.FkEntityKey, out var lookup) && lookup.Count > 0)
        {
            return lookup.First().Value;
        }

        return null;
    }

    private async Task<HashSet<string>> LoadExistingKeysAsync(EntityDef def)
    {
        if (def.KeyColumn == null) return new HashSet<string>();

        var keyProp = def.Type.GetProperty(def.KeyColumn);
        if (keyProp == null) return new HashSet<string>();

        var data = await QueryAllAsync(def.Type);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data)
        {
            var val = keyProp.GetValue(item)?.ToString();
            if (val != null)
                keys.Add(val);
        }
        return keys;
    }

    private async Task<Dictionary<string, object>> LoadExistingEntitiesAsync(EntityDef def)
    {
        var cache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var keyProps = GetKeyProperties(def);
        if (keyProps.Count == 0) return cache;

        var data = await QueryAllAsync(def.Type);
        foreach (var item in data)
        {
            var key = BuildEntityKey(item, keyProps);
            if (key != null && !cache.ContainsKey(key))
                cache[key] = item;
        }
        return cache;
    }

    private List<System.Reflection.PropertyInfo> GetKeyProperties(EntityDef def)
    {
        var props = new List<System.Reflection.PropertyInfo>();
        if (def.KeyColumn != null)
        {
            var prop = def.Type.GetProperty(def.KeyColumn);
            if (prop != null) props.Add(prop);
        }
        else if (def.CompositeKeyColumns != null)
        {
            foreach (var col in def.CompositeKeyColumns)
            {
                var prop = def.Type.GetProperty(col);
                if (prop != null) props.Add(prop);
            }
        }
        return props;
    }

    /// <summary>
    /// 从Excel行数据中提取业务键值（将属性名映射到Excel表头名）
    /// </summary>
    private static string? GetRowKey(EntityDef def, ImportRowData row)
    {
        if (def.KeyColumn != null)
        {
            var header = def.Columns.FirstOrDefault(c => c.Property == def.KeyColumn)?.Header;
            if (header != null && row.Values.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
            return null;
        }
        if (def.CompositeKeyColumns != null)
        {
            var parts = def.CompositeKeyColumns
                .Select(propName =>
                {
                    var header = def.Columns.FirstOrDefault(c => c.Property == propName)?.Header;
                    return header != null ? row.Values.GetValueOrDefault(header, "")?.Trim() ?? "" : "";
                })
                .ToArray();
            return parts.All(p => p.Length > 0) ? string.Join("|", parts) : null;
        }
        return null;
    }

    private static string? BuildEntityKey(object entity, List<System.Reflection.PropertyInfo> keyProps)
    {
        if (keyProps.Count == 1)
            return keyProps[0].GetValue(entity)?.ToString();
        var parts = keyProps.Select(p => p.GetValue(entity)?.ToString() ?? "");
        return string.Join("|", parts);
    }

    /// <summary>
    /// 导出时解析 WorkOrder.OrderItemIds（"1,2,3" → "D26Z2117001|1;D26Z2117001|2"）
    /// </summary>
    private async Task<string?> ResolveOrderItemIdsForExportAsync(string orderItemIds)
    {
        if (string.IsNullOrWhiteSpace(orderItemIds))
            return null;

        var ids = orderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length == 0)
            return null;

        var idList = new List<int>();
        foreach (var id in ids)
        {
            if (int.TryParse(id.Trim(), out var parsed))
                idList.Add(parsed);
        }

        if (idList.Count == 0)
            return null;

        var orderItems = await _context.Set<OrderItem>()
            .Include(oi => oi.SalesOrder)
            .Where(oi => idList.Contains(oi.Sequence))
            .OrderBy(oi => oi.Sequence)
            .ToListAsync();

        var result = new List<string>();
        foreach (var oi in orderItems)
        {
            var orderNo = oi.SalesOrder?.OrderNumber ?? "?";
            result.Add($"{orderNo}|{oi.Sequence}");
        }

        return string.Join(";", result);
    }

    /// <summary>
    /// 导入时解析 WorkOrder.OrderItemIds（"D26Z2117001|1;D26Z2117001|2" → "1,2,3"）
    /// </summary>
    private async Task<string> ResolveOrderItemIdsForImportAsync(string compositeKeys)
    {
        if (string.IsNullOrWhiteSpace(compositeKeys))
            return "";

        var pairs = compositeKeys.Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (pairs.Length == 0)
            return "";

        var orderItemIds = new List<int>();

        foreach (var pair in pairs)
        {
            var parts = pair.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;

            var orderNo = parts[0].Trim();
            if (!int.TryParse(parts[1].Trim(), out var sequence))
                continue;

            var oi = await _context.Set<OrderItem>()
                .FirstOrDefaultAsync(o => o.SalesOrder.OrderNumber == orderNo && o.Sequence == sequence);

            if (oi != null)
                orderItemIds.Add(oi.Sequence);
        }

        return string.Join(",", orderItemIds);
    }

    private async Task ImportRowAsync(EntityDef def, ImportRowData row,
        Dictionary<string, Dictionary<string, int>> fkCache, bool overwrite, string? userName,
        Dictionary<string, object> existingCache,
        Dictionary<string, HashSet<string>> pendingCodes)
    {
        var entityType = def.Type;
        var dbSet = _context.GetType().GetMethod("Set", Type.EmptyTypes)!
            .MakeGenericMethod(entityType)
            .Invoke(_context, null)!;
        var propertyCache = BuildPropertyCache(def);

        // 查找已存在的记录（从预加载缓存中查找，支持单键和复合键）
        object? existingEntity = null;
        var rowKey = GetRowKey(def, row);
        if (rowKey != null)
        {
            existingCache.TryGetValue(rowKey, out existingEntity);
        }

        object entity;
        if (existingEntity != null && overwrite)
        {
            entity = existingEntity;
        }
        else if (existingEntity != null)
        {
            return; // 跳过
        }
        else
        {
            entity = Activator.CreateInstance(entityType)!;

            // 自动生成系统编码（如 SupplierCode → SU0001）
            foreach (var sysCol in def.Columns.Where(c => c.IsSystem && c.Property != null && CodePrefixMap.ContainsKey(c.Property)))
            {
                if (sysCol.Property != null && propertyCache.TryGetValue(sysCol.Property, out var codeProp) && codeProp.CanWrite)
                {
                    var prefix = CodePrefixMap[sysCol.Property];

                    // 查询数据库中所有已有编码
                    var dbCodes = await ((IQueryable)dbSet).Cast<BaseEntity>()
                        .Select(e => EF.Property<string>(e, sysCol.Property))
                        .ToListAsync();

                    // 合并批次内已分配的编码
                    var allCodes = dbCodes.Concat(pendingCodes[sysCol.Property]).ToList();

                    // 计算下一个可用编码
                    var matchingCodes = allCodes.Where(c => c.StartsWith(prefix) && c.Length == 6)
                        .OrderByDescending(c => c)
                        .ToList();
                    var maxCode = matchingCodes.FirstOrDefault();
                    var newCode = maxCode == null
                        ? $"{prefix}0001"
                        : $"{prefix}{int.Parse(maxCode[2..]) + 1:D4}";

                    pendingCodes[sysCol.Property].Add(newCode);
                    codeProp.SetValue(entity, newCode);
                }
            }
        }

        // 设置审计字段
        var now = DateTimeOffset.Now;
        if (entity is BaseEntity be)
        {
            if (existingEntity == null)
            {
                be.CreatedTime = now;
                be.CreatedBy = userName ?? "system";
            }
            be.UpdatedTime = now;
            be.UpdatedBy = userName ?? "system";

        }

        // 设置属性值
        foreach (var colDef in def.Columns)
        {
            if (colDef.IsSystem || colDef.IsFkColumn) continue;

            if (!row.Values.TryGetValue(colDef.Header, out var cellValue))
                continue;

            if (string.IsNullOrWhiteSpace(cellValue))
            {
                if (colDef.Property != null && propertyCache.TryGetValue(colDef.Property, out var nullProp))
                {
                    if (nullProp.PropertyType.IsGenericType &&
                        nullProp.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        nullProp.SetValue(entity, null);
                    }
                }
                continue;
            }

            if (colDef.Property == null || !propertyCache.TryGetValue(colDef.Property, out var prop))
                continue;

            var value = ConvertValue(cellValue, prop.PropertyType, colDef);

            // 特殊处理：WorkOrder.OrderItemIds → 将"订单号|项次号"解析回内部ID
            if (colDef.Property == "OrderItemIds")
            {
                value = await ResolveOrderItemIdsForImportAsync(cellValue);
            }

            prop.SetValue(entity, value);
        }

        // 解析FK列
        ResolveForeignKeys(def, row, fkCache, entity, propertyCache);

        // 添加新实体到DbContext
        if (existingEntity == null)
        {
            var addMethod = dbSet.GetType().GetMethod("Add");
            addMethod?.Invoke(dbSet, new[] { entity });
        }
        // 注意：不在此处SaveChanges，由ImportAsync批量保存
    }

    private void ResolveForeignKeys(EntityDef def, ImportRowData row,
        Dictionary<string, Dictionary<string, int>> fkCache, object entity,
        Dictionary<string, PropertyInfo> propertyCache)
    {
        foreach (var colDef in def.Columns.Where(c => c.IsFkColumn))
        {
            if (!row.Values.TryGetValue(colDef.Header, out var cellValue) || string.IsNullOrWhiteSpace(cellValue))
                continue;

            if (colDef.FkEntityKey == null) continue;

            // 特殊处理：OrderItem 复合键（订单号|项次号）
            if (colDef.FkRequiresJoin && colDef.FkEntityKey == "OrderItem")
            {
                var orderNo = row.Values.GetValueOrDefault("订单号", "");
                var seq = row.Values.GetValueOrDefault("项次号", "");
                var compositeKey = $"{orderNo}|{seq}";

                if (fkCache.TryGetValue("OrderItem", out var oiCache) && oiCache.TryGetValue(compositeKey, out var oiId))
                {
                    if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var oiProp))
                        oiProp.SetValue(entity, oiId);
                }
                continue;
            }

            // 特殊处理：ProcessGroup 复合键
            // 实体有 SectionName 属性（ProductionRecord/SectionOutsource）→ 按"批次号+工段名称"匹配
            if (colDef.FkRequiresJoin && colDef.FkEntityKey == "ProcessGroup")
            {
                if (propertyCache.ContainsKey("SectionName"))
                {
                    var batchNo = row.Values.GetValueOrDefault("批次号", "");
                    var sectionName = row.Values.GetValueOrDefault("工段名称", "");
                    if (!string.IsNullOrWhiteSpace(batchNo) && !string.IsNullOrWhiteSpace(sectionName))
                    {
                        var compositeKey = $"{batchNo}|{sectionName}";
                        if (fkCache.TryGetValue("ProcessGroupIdBySection", out var idCache) &&
                            idCache.TryGetValue(compositeKey, out var pgId) &&
                            fkCache.TryGetValue("ProcessGroupSeqBySection", out var seqCache) &&
                            seqCache.TryGetValue(compositeKey, out var seqNum))
                        {
                            if (propertyCache.TryGetValue("ProcessGroupId", out var pgProp))
                                pgProp.SetValue(entity, pgId);
                            if (propertyCache.TryGetValue("SequenceNumber", out var seqProp))
                                seqProp.SetValue(entity, seqNum);
                        }
                    }
                }
                else
                {
                    // 无 SectionName 属性：按 BatchNo|SequenceNumber 复合键查找
                    var batchNo = row.Values.GetValueOrDefault("批次号", "");
                    var seq = row.Values.GetValueOrDefault("工序序号", "");
                    var compositeKey = $"{batchNo}|{seq}";
                    if (fkCache.TryGetValue("ProcessGroup", out var pgCache) && pgCache.TryGetValue(compositeKey, out var pgId))
                    {
                        if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var pgProp))
                            pgProp.SetValue(entity, pgId);
                        if (int.TryParse(seq, out var seqNum) && propertyCache.TryGetValue("SequenceNumber", out var seqProp))
                            seqProp.SetValue(entity, seqNum);
                    }
                }
                continue;
            }

            // 特殊处理：SectionOutsource 复合键（批次号|工段名称|委外单位）
            if (colDef.FkRequiresJoin && colDef.FkEntityKey == "SectionOutsource")
            {
                var batchNo = row.Values.GetValueOrDefault("批次号", "");
                var sectionName = row.Values.GetValueOrDefault("工段名称", "");
                var vendor = row.Values.GetValueOrDefault("委外单位", "");
                var compositeKey = $"{batchNo}|{sectionName}|{vendor}";

                if (fkCache.TryGetValue("SectionOutsource", out var soCache) && soCache.TryGetValue(compositeKey, out var soId))
                {
                    if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var soProp))
                        soProp.SetValue(entity, soId);
                }
                continue;
            }

            // 常规FK解析
            if (fkCache.TryGetValue(colDef.FkEntityKey, out var lookup) && lookup.TryGetValue(cellValue, out var fkId))
            {
                if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var fkProp))
                    fkProp.SetValue(entity, fkId);
            }
        }
    }

    private object ConvertValue(string value, Type targetType, ColumnDef colDef)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                return null!;
            if (targetType == typeof(string))
                return value;
            throw new BusinessException($"值不能为空 (期望类型: {targetType.Name})");
        }

        // 自定义值转换器
        if (colDef.ValueConverter != null)
            return colDef.ValueConverter(value);

        // 枚举类型
        if (colDef.IsEnum && colDef.EnumType != null)
        {
            var enumValue = EnumHelper.Parse(value, colDef.EnumType);
            // 实体属性为 string（字符串存储的枚举，如 LifecycleStatus/UsageType/Priority），返回枚举名称
            if (targetType == typeof(string))
                return enumValue.ToString()!;
            return enumValue;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string)) return value;
        if (underlyingType == typeof(int)) return int.Parse(value);
        if (underlyingType == typeof(decimal)) return decimal.Parse(value);
        if (underlyingType == typeof(double)) return double.Parse(value);
        if (underlyingType == typeof(DateTime)) return DateTime.Parse(value);
        if (underlyingType == typeof(bool))
        {
            if (value == "是" || value.ToLower() == "true" || value == "1") return true;
            if (value == "否" || value.ToLower() == "false" || value == "0") return false;
            return bool.Parse(value);
        }

        return value;
    }

    private async Task DisableAllConstraintsAsync(DbConnection connection, DbTransaction transaction)
    {
        var sql = @"
DECLARE @sql NVARCHAR(MAX) = ''
SELECT @sql = @sql + 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + '].[' + OBJECT_NAME(parent_object_id) + '] NOCHECK CONSTRAINT [' + name + '];' + CHAR(13)
FROM sys.foreign_keys
EXEC sp_executesql @sql";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction;
        await cmd.ExecuteNonQueryAsync();

        _logger.LogInformation("已禁用所有外键约束");
    }

    private async Task<List<string>> EnableAndCheckConstraintsAsync(DbConnection connection, DbTransaction transaction)
    {
        var errors = new List<string>();

        // 启用所有约束
        var enableSql = @"
DECLARE @sql NVARCHAR(MAX) = ''
SELECT @sql = @sql + 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + '].[' + OBJECT_NAME(parent_object_id) + '] WITH CHECK CHECK CONSTRAINT [' + name + '];' + CHAR(13)
FROM sys.foreign_keys
EXEC sp_executesql @sql";

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = enableSql;
            cmd.Transaction = transaction;
            await cmd.ExecuteNonQueryAsync();
        }

        // 检查是否有违反约束的记录
        var checkSql = @"
SELECT
    OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) AS TableName,
    fk.name AS ConstraintName
FROM sys.foreign_keys fk
WHERE fk.is_not_trusted = 1 OR fk.is_disabled = 1";

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = checkSql;
            cmd.Transaction = transaction;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                errors.Add($"表 {reader.GetString(0)}, 约束 {reader.GetString(1)}");
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("外键约束验证失败，发现 {Count} 个问题约束", errors.Count);
        }
        else
        {
            _logger.LogInformation("所有外键约束验证通过");
        }

        return errors;
    }

    private static string GetRollbackReason(Exception ex)
    {
        var inner = ex;
        while (inner.InnerException != null)
            inner = inner.InnerException;
        return inner.Message;
    }

    #endregion
}

#region 数据模型

public class EntityDef
{
    public string Key { get; }
    public string DisplayName { get; }
    public Type Type { get; }
    public int ImportOrder { get; }
    public string? KeyColumn { get; }
    public string[]? CompositeKeyColumns { get; }
    public List<ColumnDef> Columns { get; }

    public EntityDef(string key, string displayName, Type type, int importOrder, string? keyColumn, List<ColumnDef> columns, string[]? compositeKeyColumns = null)
    {
        Key = key;
        DisplayName = displayName;
        Type = type;
        ImportOrder = importOrder;
        KeyColumn = keyColumn;
        CompositeKeyColumns = compositeKeyColumns;
        Columns = columns;
    }
}

public class ColumnDef
{
    public string Header { get; }
    public string? Property { get; set; }
    public Type PropertyType { get; }
    public bool IsEnum { get; }
    public Type? EnumType { get; }
    public bool IsSystem { get; }
    public bool IsRequired { get; }
    public Func<string, object>? ValueConverter { get; }

    // FK解析相关
    public bool IsFkColumn { get; set; }
    public string? FkEntityKey { get; set; }
    public string? FkLookupProperty { get; set; }
    public string? FkTargetProperty { get; set; }
    public bool FkRequiresJoin { get; set; }

    public ColumnDef(string header, string? property, Type? propertyType = null,
                     bool isEnum = false, bool isSystem = false, bool isRequired = true,
                     Func<string, object>? valueConverter = null)
    {
        Header = header;
        Property = property;
        PropertyType = propertyType ?? typeof(string);
        IsEnum = isEnum;
        // 处理 Nullable<Enum> 类型，提取底层枚举类型
        EnumType = isEnum && propertyType != null
            ? (Nullable.GetUnderlyingType(propertyType) ?? propertyType)
            : null;
        IsSystem = isSystem;
        IsRequired = isRequired;
        ValueConverter = valueConverter;
    }
}

#endregion
