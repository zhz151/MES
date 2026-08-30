namespace MES.Core.Constants;

/// <summary>
/// 内存缓存 Key 集中定义（各 Service IMemoryCache.GetOrCreateAsync 统一引用，防拼错/集中管理）
/// </summary>
public static class CacheKeys
{
    // ===== 列表/汇总缓存 =====
    /// <summary>定尺工单列表</summary>
    public const string FixedLengthWorkOrderList = "FixedLengthWorkOrderService:List";

    /// <summary>待发货项 DTO 第1层缓存（InventoryService 出/入库时主动失效）</summary>
    public const string PendingDeliveryLoadDtos = "PendingDeliveryQueryService:LoadDtos";

    /// <summary>工段名映射</summary>
    public const string SectionNameDisplayMap = "SectionNameDisplay:Map";

    // ===== 筛选上下文（FilterContexts）缓存 =====
    public const string WorkOrderFilterContexts = "WorkOrderService:FilterContexts";
    public const string WorkOrderExecutionFilterContexts = "WorkOrderExecutionService:FilterContexts";
    public const string OrderDemandAdjustmentFilterContexts = "OrderDemandAdjustmentService:FilterContexts";
    public const string OrderFilterContexts = "OrderService:FilterContexts";
    public const string CustomerFilterContexts = "CustomerService:FilterContexts";
    public const string SupplierFilterContexts = "SupplierService:FilterContexts";
    public const string SubcontractOrderFilterContexts = "SubcontractOrderService:FilterContexts";
    public const string SubcontractOrderReturnItemFilterContexts = "SubcontractOrderService:ReturnItemFilterContexts";
    public const string PurchaseOrderFilterContexts = "PurchaseOrderService:FilterContexts";
    public const string InventoryOutboundFilterContexts = "InventoryService:OutboundFilterContexts";
    public const string InventoryFilterContexts = "InventoryService:InventoryFilterContexts";

    // ===== 质检筛选上下文 =====
    public const string TensileTestFilterContexts = "TensileTestService:FilterContexts";
    public const string QualityProcessTrackingFilterContexts = "QualityProcessTrackingService:FilterContexts";
    public const string ProcessInspectionFilterContexts = "ProcessInspectionService:FilterContexts";
    public const string PittingCorrosionTestFilterContexts = "PittingCorrosionTestService:FilterContexts";
    public const string NcrFilterContexts = "NcrService:FilterContexts";
    public const string MetallographicTestFilterContexts = "MetallographicTestService:FilterContexts";
    public const string MaterialReceiveCheckFilterContexts = "MaterialReceiveCheckService:FilterContexts";
    public const string FlaringTestFilterContexts = "FlaringTestService:FilterContexts";
    public const string IntergranularCorrosionTestFilterContexts = "IntergranularCorrosionTestService:FilterContexts";
    public const string FinalInspectionFilterContexts = "FinalInspectionService:FilterContexts";
    public const string HardnessTestFilterContexts = "HardnessTestService:FilterContexts";
    public const string ChemicalAnalysisFilterContexts = "ChemicalAnalysisService:FilterContexts";
    public const string GrainSizeTestFilterContexts = "GrainSizeTestService:FilterContexts";
    public const string FurnaceRegistrationFilterContexts = "FurnaceRegistrationService:FilterContexts";
    public const string FlatteningTestFilterContexts = "FlatteningTestService:FilterContexts";

    // ===== 批次/委外/酸洗筛选上下文 =====
    public const string SectionOutsourceRecoveryFilterContexts = "SectionOutsourceService:RecoveryFilterContexts";
    public const string SectionOutsourceFilterContexts = "SectionOutsourceService:FilterContexts";
    public const string ProductionRecordFilterContexts = "ProductionRecordService:FilterContexts";
    public const string PicklingFilterContexts = "PicklingService:FilterContexts";
    public const string PicklingOutRecordFilterContexts = "PicklingService:OutRecordFilterContexts";
    public const string BatchFilterContexts = "BatchService:FilterContexts";
}
