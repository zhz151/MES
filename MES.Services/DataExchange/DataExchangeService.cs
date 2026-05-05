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
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;
using MES.Shared.Constants;

namespace MES.Services.DataExchange;

/// <summary>
/// 数据导入导出服务（仅管理员可导入，所有认证用户可导出）
/// </summary>
public class DataExchangeService
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

        // === 第2批：依赖客户档案 ===
        ["SalesOrder"] = new EntityDef("销售订单", "销售订单", typeof(SalesOrder), 2, "OrderNumber", new List<ColumnDef>
        {
            new("订单号", "OrderNumber"),
            new("签订日期", "SignDate", typeof(DateTime)),
            new("客户编码", null!) { IsFkColumn = true, FkEntityKey = "CustomerProfile", FkLookupProperty = "CustomerCode", FkTargetProperty = "CustomerId" },
            new("状态", "Status", typeof(SalesOrderStatus), isEnum: true),
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

        // === 第7批：采购订单、委外订单（依赖供应商） ===
        ["PurchaseOrder"] = new EntityDef("采购订单", "采购订单", typeof(PurchaseOrder), 7, "OrderNo", new List<ColumnDef>
        {
            new("采购单号", "OrderNo"),
            new("供应商名称", null!) { IsFkColumn = true, FkEntityKey = "SupplierProfile", FkLookupProperty = "SupplierName", FkTargetProperty = "SupplierId" },
            new("下单日期", "OrderDate", typeof(DateTime)),
            new("状态", "Status"),
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
            new("状态", "Status"),
            new("发出物料分类", "OutMaterialCategory"),
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

        // === 第8批：仓库出入库 ===
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
            new("出库类型", "OutboundType", typeof(string)),
            new("目标单位", "TargetCompany", typeof(string), isRequired: false),
            new("出库支数", "OutboundQuantity", typeof(int)),
            new("出库重量(kg)", "OutboundWeight", typeof(decimal)),
            new("出库日期", "OutboundDate", typeof(DateTime)),
            new("操作人", "Operator"),
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

        ["PurchaseSemiPlan"] = new EntityDef("原料采购计划", "原料采购计划", typeof(PurchaseSemiPlan), 9, null, new List<ColumnDef>
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
    };

    public static readonly List<string> EntityOrder = new()
    {
        "Warehouse", "ProductionStandard", "StandardGradeMapping", "CustomerProfile", "SupplierProfile",
        "SalesOrder",
        "OrderItem", "ProductRequirement",
        "WorkOrder", "Material",
        "PurchaseOrder", "SubcontractOrder", "SubcontractReturnItem",
        "InventoryBatch", "OutboundRecord",
        "InventoryPlan", "PurchaseSemiPlan", "PurchaseFinishedPlan",
    };

    #endregion

    #region 导出

    /// <summary>
    /// 导出指定实体的全部数据为 Excel
    /// </summary>
    public async Task<byte[]> ExportAsync(string entityKey)
    {
        if (!Registry.TryGetValue(entityKey, out var def))
            throw new ArgumentException($"不支持的实体类型: {entityKey}");

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
                    sheet.Cells[row, col + 1].Value = EnumHelper.GetDisplayName(colDef.EnumType, value);
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
            throw new ArgumentException($"不支持的实体类型: {entityKey}");

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
                    throw new InvalidOperationException(
                        $"列 '{colDef.Header}' 的枚举类型 '{colDef.EnumType.FullName}' 无效: IsEnum={colDef.EnumType.IsEnum}, BaseType={colDef.EnumType.BaseType}");
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
            throw new ArgumentException($"不支持的实体类型: {entityKey}");

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
            throw new ArgumentException($"不支持的实体类型: {entityKey}");

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
                throw new InvalidOperationException(
                    $"外键约束验证失败，共 {checkErrors.Count} 个错误:\n" +
                    string.Join("\n", checkErrors.Take(10)));
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
            .Where(oi => idList.Contains(oi.Id))
            .OrderBy(oi => oi.Id)
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
                orderItemIds.Add(oi.Id);
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
                if (propertyCache.TryGetValue(sysCol.Property, out var codeProp) && codeProp.CanWrite)
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

            // 特殊处理：OrderItem 复合键
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
            throw new InvalidOperationException($"值不能为空 (期望类型: {targetType.Name})");
        }

        // 自定义值转换器
        if (colDef.ValueConverter != null)
            return colDef.ValueConverter(value);

        // 枚举类型
        if (colDef.IsEnum && colDef.EnumType != null)
        {
            return EnumHelper.Parse(value, colDef.EnumType);
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

public class ImportRowData
{
    public int RowNumber { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();
}

public class ImportPreviewResult
{
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }
    public int DuplicateCount { get; set; }
    public List<ImportRowResult> RowResults { get; set; } = new();
}

public class ImportRowResult
{
    public int RowNumber { get; set; }
    public string? Key { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool IsDuplicate { get; set; }
    public bool IsValid { get; set; }
    public ImportRowData? Data { get; set; }
}

public class ImportResult
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string Strategy { get; set; } = "skip";
    public bool HasRolledBack { get; set; }
    public string? RollbackReason { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
}

public class ImportRowError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = "";
}

#endregion
