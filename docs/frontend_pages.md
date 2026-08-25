# MES 前端页面结构参考

> 生成日期：2026-08-19（V25）
> 最后更新：2026-08-23（V27）
> 用途：Quick Reference - 快速了解项目前端页面组织结构和上下文归属

---

## 1. 上下文定义

本项目中"上下文"按导航菜单分组定义，共 **11 大业务上下文** + 工具/管理/首页：

| 上下文 | 导航标签 | RBAC 角色 | 页面数 | 列表页数 |
|-------|---------|----------|-------|---------|
| 首页 | 首页 | 所有 | 1 | 0 |
| 订单 | 订单管理 | OrderViewer/Editor/Full + Admin | 7 | 3 |
| 工单 | 工单管理 | WorkOrderViewer/Editor/Full + Admin | 17 | 5 |
| 计划排程 | 计划排程 | SchedulingViewer/Editor/Full + Admin | 6 | 5 |
| 批次 | 批次管理 | BatchViewer/Editor/Full + Admin | 14 | 6 |
| 质量 | 质量管理 | QualityViewer/Editor/Full + Admin | 32 | 16 |
| 物料 | 物料管理 | MaterialViewer/Editor/Full + Admin | 9 | 4 |
| 仓库 | 仓库管理 | WarehouseViewer/Editor/Full + Admin | 7 | 4 |
| 设备 | 设备管理 | EquipmentViewer/Editor/Full + Admin | 8 | 4 |
| 生产标准 | 生产标准 | StandardViewer/Editor/Full + Admin | 18 | 9 |
| 报表系统 | (已并入仓库管理) | ReportViewer/Editor/Full + Admin | 0 | 0 |
| 数据工具 | (独立按钮) | DataToolViewer/Editor/Full + Admin | 2 | 0 |
| 扫码报工 | (独立按钮) | 所有（仅登录） | 1 | 0 |
| 设备扫码 | (独立按钮) | 所有（仅登录） | 1 | 0 |
| 配置 | 参数表 | ConfigurationViewer/Editor/Full + Admin | 13 | 13 |
| 用户管理 | (Admin按钮) | UserViewer/Editor/Full + Admin | 1 | 0 |

---

## 2. 各上下文详细页面清单

### 2.1 订单上下文

```
路由前缀: /orders, /customers
菜单: 订单管理 → [订单列表, 客户管理]

┌─ 订单管理 ───────────────────────────────────────────────┐
│                                                           │
│  Orders.razor          /orders              [列表页+内联编辑]│
│  OrderCreate.razor     /orders/create       [创建页]       │
│  OrderDetail.razor     /orders/{Id:int}     [详情页]       │
│  ProductRequirement.razor /orders/{orderId:int}/requirements [子页] │
│                                                           │
│  Customers.razor       /customers           [列表页]       │
│  CustomerCreate.razor  /customers/create    [创建页]       │
│                                                           │
│  列表页: Orders, Customers                                │
│                                                           │
│  2026-06-21 变更：产品标准(Standards)已删除；牌号对照(GradeMappings)移至生产标准菜单│
└───────────────────────────────────────────────────────────┘
```

### 2.2 工单上下文

```
路由前缀: /workorders, /material-plan-overview, /workorder-overview, /workorder-execution, /order-demand-adjustment, /fixed-length-work-order-view
菜单: 工单管理 → [工单首页, 用料计划总览, 工单总览, 工单需求调整, 工单执行状况, 定尺工单定尺数据]

┌─ 工单管理 ───────────────────────────────────────────────┐
│                                                           │
│  WorkOrders.razor          /workorders         [列表页+内联编辑]│
│  WorkOrderGenerate.razor   /workorders/generate [功能页]   │
│  WorkOrderRelation.razor   /workorders/relation [功能页]   │
│  WorkOrderDetail.razor     /workorders/{Id:int} [详情页]   │
│                                                           │
│  子页（嵌入在WorkOrderDetail中导航）:                        │
│  WorkOrderMaterialPlan.razor     /workorders/{id}/material-plan │
│  WorkOrderMaterialPlanCreate.razor /workorders/{id}/material-plan/create │
│  WorkOrderPiercingPlanCreate.razor /workorders/{id}/piercing-plan/create │
│  WorkOrderPiercingPlanEdit.razor   /workorders/{id}/piercing-plan/edit/{Id:int} │
│  WorkOrderInventoryPlanCreate.razor /workorders/{id}/inventory-plan/create │
│  WorkOrderFinishPlanCreate.razor   /workorders/{id}/finish-plan/create │
│  WorkOrderReworkPlanCreate.razor   /workorders/{id}/rework-plan/create │
│  WorkOrderInProcessReworkPlanCreate.razor /workorders/{id}/in-process-rework-plan/create │
│  WorkOrderInProcessReworkPlanCreate.razor /workorders/{id}/in-process-rework-plan/edit/{PlanId:int} │
│  WorkOrderInMainWorkOrderPlanCreate.razor /workorders/{id}/in-main-work-order-plan/create │
│  WorkOrderInMainWorkOrderPlanCreate.razor /workorders/{id}/in-main-work-order-plan/edit/{PlanId:int} │
│                                                           │
│  MaterialPlanOverview.razor /material-plan-overview [列表页]│
│                                                           │
│  WorkOrderExecution.razor         /workorder-execution            [列表页]  │
│                                                           │
│  OrderDemandAdjustment.razor      /order-demand-adjustment        [列表页]  │
│                                                           │
│  FixedLengthWorkOrderView.razor   /fixed-length-work-order-view  [列表页]  │
│                                                           │
│  列表页: WorkOrders, MaterialPlanOverview, WorkOrderExecution, OrderDemandAdjustment, FixedLengthWorkOrderView  │
│  ※ 页面文件在 Pages/WorkOrders/ 目录                        │
└───────────────────────────────────────────────────────────┘
```

### 2.3 计划排程上下文

```
路由前缀: /plan-overview, /raw-material-lock-plan, /scheduling-plans, /cold-roll-plans, /batch-plans, /final-inspection-plan
菜单: 计划排程 → [负载总览, 原锁计划, 工单排程, 冷轧排程, 批次计划, 成检计划]

┌─ 计划排程 ─────────────────────────────────────────────┐
│                                                           │
│  PlanOverview.razor                  /plan-overview                    [只读聚合] │
│  RawMaterialLockPlanAndExecution.razor /raw-material-lock-plan      [列表页]     │
│  WorkOrderSchedules.razor           /scheduling-plans                [列表页]     │
│  ColdRollPlans.razor                /cold-roll-plans                [列表页]     │
│  BatchPlans.razor                   /batch-plans                    [列表页]     │
│  FinalInspectionPlan.razor          /final-inspection-plan          [列表页]     │
│                                                           │
│  列表页: RawMaterialLockPlanAndExecution,                 │
│          WorkOrderSchedules, ColdRollPlans, BatchPlans,   │
│          FinalInspectionPlan                              │
│  只读聚合: PlanOverview（MudTable 客户端模式，无分页/排序/筛选）│
│  ※ 已删除独立页面（数据改经批次计划页内嵌折叠与报表总览消费， │
│     后端接口保留）：                                       │
│     SectionProductionStatus, SectionFlowAnalysis,         │
│     SectionParagraphFlowAnalysis                          │
└───────────────────────────────────────────────────────────┘
```

### 2.4 批次上下文

```
路由前缀: /batches, /production-records, /section-outsources, /outsource-recoveries,
         /pickling-in-records, /pickling-out-records, /process-card-print
菜单: 批次管理 → [批次首页, 生产记录, 工段委外, 工艺卡打印]

┌─ 批次管理 ───────────────────────────────────────────────┐
│                                                           │
│  Batches.razor              /batches           [列表页]     │
│  BatchCreate.razor          /batches/create    [创建页]     │
│  BatchDetail.razor          /batches/{Id:int}  [详情页]     │
│  BatchEdit.razor            /batches/{Id:int}/edit [编辑页] │
│                                                           │
│  ProductionRecords.razor    /production-records [列表页]     │
│  ProductionRecordCreate.razor /production-records/create [创建页]│
│                                                           │
│  SectionOutsources.razor    /section-outsources [列表页]     │
│  SectionOutsourceCreate.razor /section-outsources/create [创建页]│
│  OutsourceRecoveryCreate.razor /section-outsources/create-recovery [创建页]│
│                                                           │
│  OutsourceRecoveries.razor  /outsource-recoveries [列表页]   │
│                                                           │
│  PicklingInRecords.razor    /pickling-in-records [列表页]                 │
│  PicklingInRecordCreate.razor /pickling-in-records/create [创建页]        │
│  PicklingOutRecords.razor   /pickling-out-records [列表页]                │
│                                                           │
│  ProcessCardPrint.razor     /process-card-print [功能页]     │
│                                                           │
│  列表页: Batches, ProductionRecords, SectionOutsources,     │
│          OutsourceRecoveries, PicklingInRecords,            │
│          PicklingOutRecords                                 │
│  ※ Batches.razor 实现了通知轮询（StartNotificationPollingAsync），│
│    每30秒检查工单变更通知                                    │
└───────────────────────────────────────────────────────────┘
```

### 2.5 质量上下文

```
路由前缀: /quality/furnace, /quality/process-inspection, /quality/material-receive-checks, /quality/final-inspection,
         /quality/process-tracking, /quality/ncr,
         /quality/chemical-analysis, /quality/hardness-test, /quality/grain-size-test,
         /quality/pitting-corrosion-test, /quality/intergranular-corrosion-test,
         /quality/tensile-test, /quality/metallographic-test,
         /quality/flattening-test, /quality/flaring-test,
         /quality/lab-testing, /quality/certificates
菜单: 质量管理 → [过程检验, 成检到料, 成品检验, 成检追踪, 不合格报告, 炉号/化学(子组), 理化检测, 质量证明书]
      炉号/化学子组: [炉号登记]

┌─ 质量管理 ───────────────────────────────────────────────┐
│                                                           │
│  FurnaceRegistrations.razor       /quality/furnace              [列表页]│
│  FurnaceRegistrationCreate.razor  /quality/furnace/create       [创建页]│
│                                                           │
│  ProcessInspections.razor         /quality/process-inspection   [列表页]│
│  ProcessInspectionCreate.razor    /quality/process-inspection/create [创建页]│
│                                                           │
│  MaterialReceiveChecks.razor        /quality/material-receive-checks      [列表页]│
│  MaterialReceiveCheckCreate.razor   /quality/material-receive-checks/create [创建页]│
│                                                           │
│  FinalInspections.razor           /quality/final-inspection     [列表页]│
│  FinalInspectionCreate.razor      /quality/final-inspection/create [创建页]│
│                                                           │
│  QualityProcessTracking.razor     /quality/process-tracking    [列表页]│
│                                                           │
│  Ncrs.razor                      /quality/ncr                      [列表页+分页汇总]│
│  NcrForm.razor                   /quality/ncr/create               [创建页]       │
│  NcrForm.razor                   /quality/ncr/{Id:int}             [详情页]       │
│                                                           │
│  --- 理化检测模块（V7.0 新增） ---                          │
│  ChemicalAnalyses.razor          /quality/chemical-analysis    [列表页]      │
│  ChemicalAnalysisCreate.razor    /quality/chemical-analysis/create [创建页]  │
│  HardnessTests.razor             /quality/hardness-test        [列表页]      │
│  HardnessTestCreate.razor        /quality/hardness-test/create [创建页]      │
│  GrainSizeTests.razor            /quality/grain-size-test      [列表页]      │
│  GrainSizeTestCreate.razor       /quality/grain-size-test/create [创建页]    │
│  PittingCorrosionTests.razor     /quality/pitting-corrosion-test [列表页]    │
│  PittingCorrosionTestCreate.razor /quality/pitting-corrosion-test/create [创建页]│
│  IntergranularCorrosionTests.razor /quality/intergranular-corrosion-test [列表页]│
│  IntergranularCorrosionTestCreate.razor /quality/intergranular-corrosion-test/create [创建页]│
│  TensileTests.razor              /quality/tensile-test         [列表页]      │
│  TensileTestCreate.razor         /quality/tensile-test/create  [创建页]      │
│  MetallographicTests.razor       /quality/metallographic-test  [列表页]      │
│  MetallographicTestCreate.razor  /quality/metallographic-test/create [创建页] │
│  FlatteningTests.razor           /quality/flattening-test      [列表页]      │
│  FlatteningTestCreate.razor      /quality/flattening-test/create [创建页]    │
│  FlaringTests.razor              /quality/flaring-test         [列表页]      │
│  FlaringTestCreate.razor         /quality/flaring-test/create  [创建页]      │
│                                                           │
│  --- 质量证明书模块（V17 新增） ---                          │
│  Certificates.razor              /quality/certificates        [列表页]      │
│  CertificateCreate.razor         /quality/certificates/create [创建页]      │
│  CertificateDetail.razor         /quality/certificates/{Id:int} [详情页]    │
│  CertificatePrintSettingsDialog.razor  [打印设置对话框，无路由]│
│                                                           │
│  列表页: FurnaceRegistrations, ProcessInspections,           │
│          MaterialReceiveChecks, FinalInspections,            │
│          QualityProcessTracking, Ncrs,                       │
│          ChemicalAnalyses, HardnessTests, GrainSizeTests,    │
│          PittingCorrosionTests, IntergranularCorrosionTests, │
│          TensileTests, MetallographicTests,                  │
│          FlatteningTests, FlaringTests, Certificates         │
└───────────────────────────────────────────────────────────┘
```

### 2.6 物料上下文

```
路由前缀: /purchase-orders, /subcontract-orders, /subcontract-return-items, /suppliers
菜单: 物料管理 → [采购订单, 圆棒穿孔(子项), 子项查询, 供应商管理]

┌─ 物料管理 ───────────────────────────────────────────────┐
│                                                           │
│  PurchaseOrders.razor        /purchase-orders      [列表页+内联编辑]│
│  PurchaseOrderCreate.razor   /purchase-orders/create [创建页]│
│  PurchaseOrderDetail.razor   /purchase-orders/{id:int} [详情页]│
│                                                           │
│  SubcontractOrders.razor     /subcontract-orders   [列表页+内联编辑]│
│  SubcontractOrderCreate.razor /subcontract-orders/create [创建页]│
│  SubcontractOrderDetail.razor /subcontract-orders/{id:int} [详情页]│
│                                                           │
│  SubcontractReturnItems.razor /subcontract-return-items [列表页] │
│                                                           │
│  Suppliers.razor             /suppliers           [列表页]   │
│  SupplierCreate.razor        /suppliers/create    [创建页]   │
│                                                           │
│  列表页: PurchaseOrders, SubcontractOrders, SubcontractReturnItems,│
│          Suppliers                                        │
└───────────────────────────────────────────────────────────┘
```
（物料档案 /materials 主档已删除，2026-08-18）

### 2.7 仓库上下文

```
路由前缀: /warehouse, /warehouse/{Code}, /warehouse/inbound, /warehouse/outbound,
         /warehouse/inbound-history, /warehouse/outbound-history, /warehouse/pending-delivery
菜单: 仓库管理 → [原料库, 成品库, 在制品库, 次品库, 物料进出存报表, 待发货项]
      （2026-08-23 在制品库移至次品库上方；物料进出存报表并入仓库管理，位于次品库之下；报表系统已删除）

┌─ 仓库管理 ───────────────────────────────────────────────┐
│                                                           │
│  WarehouseInventory.razor     /warehouse          [列表页]   │
│  WarehouseInventory.razor     /warehouse/{Code}   [列表页(复用)]│
│  仓库类型Code: raw(原料库) / fg(成品库) / defect(次品库) / wip(在制品库)  │
│                                                           │
│  WarehouseInbound.razor       /warehouse/inbound  [功能页]   │
│  WarehouseInbound.razor       /warehouse/inbound/{Code} [功能页]│
│                                                           │
│  WarehouseOutbound.razor      /warehouse/outbound [功能页]   │
│                                                           │
│  PendingDelivery.razor        /warehouse/pending-delivery [列表页]│
│                                                           │
│  InboundHistory.razor         /warehouse/inbound-history      [列表页]│
│  InboundHistory.razor         /warehouse/inbound-history/{Code}[列表页(复用)]│
│                                                           │
│  OutboundHistory.razor        /warehouse/outbound-history      [列表页]│
│  OutboundHistory.razor        /warehouse/outbound-history/{Code}[列表页(复用)]│
│  MonthlyStock.razor          /warehouse/monthly-stock          [报表页]   │
│  报表页: MonthlyStock（原生 table，4 报表切换：入库/出库/库存/物料进出存；入库按来源展开、出库按类型展开（含物料汇总合并列）；行=库房×物料类型，库房+物料类型双层合并单元格，无合计行；当前月之后月份单元格留空，「实时结存/实时数据」=截至当前月合计，三值格「入/出,[结]」如 80/15,[65]；打印横向 A4 撑满页宽）│
│  ※ API: InventoryController (api/inventory/monthly-stock-summary) │
│                                                           │
│  列表页: WarehouseInventory, PendingDelivery,                 │
│          InboundHistory, OutboundHistory                      │
│  注: Code参数路由复用同一页面文件，仅查询时区分仓库类型       │
└───────────────────────────────────────────────────────────┘
```

### 2.8 设备上下文

```
路由前缀: /equipment, /repair-orders, /maintenance-orders, /inspection-records
菜单: 设备管理 → [设备台账, 维修工单, 保养工单, 点检记录]

┌─ 设备管理 ───────────────────────────────────────────────┐
│                                                           │
│  Equipments.razor            /equipment          [列表页]   │
│  EquipmentCreate.razor       /equipment/create   [创建页]   │
│                                                           │
│  RepairOrders.razor          /repair-orders      [列表页]   │
│  RepairOrderCreate.razor     /repair-orders/create [创建页] │
│                                                           │
│  MaintenanceOrders.razor     /maintenance-orders [列表页]   │
│  MaintenanceOrderCreate.razor /maintenance-orders/create [创建页]│
│                                                           │
│  InspectionRecords.razor     /inspection-records [列表页]   │
│  InspectionRecordCreate.razor /inspection-records/create [创建页]│
│                                                           │
│  列表页: Equipments, RepairOrders, MaintenanceOrders,      │
│          InspectionRecords                                  │
└───────────────────────────────────────────────────────────┘
```

### 2.9 生产标准上下文

```
路由前缀: /standard-registers, /grade-mappings, /grade-chemical-compositions, /grade-physical-properties, /sub-standard-quick-views, /standard-inspection-requirements, /factory-inspection-requirements, /chemical-composition, /chemical-validate
菜单: 生产标准 → [标准号列表, 标准号检验项要求, 工厂检验项要求, 牌号对照, 标准牌号化学成分, 工厂牌号化学成分, 工厂牌号化分验证, 牌号物理性能, 子标准速览]

┌─ 生产标准 ───────────────────────────────────────────────┐
│                                                           │
│  StandardRegisters.razor          /standard-registers          [列表页]     │
│  StandardRegisterDetail.razor     /standard-registers/create   [创建页]    │
│  StandardRegisterDetail.razor     /standard-registers/{Id:int} [详情页]    │
│                                                           │
│  GradeMappings.razor              /grade-mappings              [列表页+内联编辑]│
│  GradeMappingCreate.razor         /grade-mappings/create       [创建页]    │
│                                                           │
│  GradeChemicalCompositions.razor     /grade-chemical-compositions     [列表页+内联编辑]│
│  GradeChemicalCompositionCreate.razor /grade-chemical-compositions/create [创建页]│
│                                                           │
│  GradePhysicalProperties.razor       /grade-physical-properties     [列表页+内联编辑]│
│  GradePhysicalPropertyCreate.razor   /grade-physical-properties/create [创建页]│
│                                                           │
│  SubStandardQuickViews.razor         /sub-standard-quick-views     [列表页]     │
│  SubStandardQuickViewCreate.razor    /sub-standard-quick-views/create [创建页]  │
│                                                           │
│  StandardInspectionRequirements.razor  /standard-inspection-requirements [列表页]  │
│  StandardInspectionRequirementCreate.razor /standard-inspection-requirements/create [创建页]│
│                                                           │
│  FactoryInspectionRequirements.razor   /factory-inspection-requirements [列表页+内联编辑]│
│  FactoryInspectionRequirementCreate.razor /factory-inspection-requirements/create [创建页]│
│                                                           │
│  ChemicalCompositions.razor       /chemical-composition  [列表页]  │
│  ChemicalCompositionCreate.razor  /chemical-composition/create [创建页]  │
│                                                           │
│  ChemicalValidationRules.razor    /chemical-validate     [列表页]  │
│  ChemicalValidationRuleCreate.razor /chemical-validate/create [创建页]  │
│                                                           │
│  列表页: StandardRegisters, StandardInspectionRequirements, │
│          FactoryInspectionRequirements, GradeMappings,      │
│          GradeChemicalCompositions, GradePhysicalProperties,│
│          SubStandardQuickViews, ChemicalCompositions,      │
│          ChemicalValidationRules                           │
│  ※ StandardRegisterDetail 双模式：Id=0 创建，Id>0 查看/编辑│
│  ※ 详情页含子项目内联表格（StandardRegisterItem）           │
│  ※ StandardRegister Save/SaveItem 返回 int（Id），防子项 SeqNo 重复创建 │
│  ※ GradeMappings 原属订单上下文，2026-06-21 迁移至此      │
│  ※ GradeChemicalCompositions/GradePhysicalProperties 为     │
│     2026-06-21 新增，按 StandardGrade+Category 纯逻辑关联   │
│  ※ ChemicalCompositions/ChemicalValidationRules 原属质量上下文，│
│     已迁移至生产标准上下文，路由同步更新为生产标准前缀        │
│  ※ StandardInspectionRequirements/SubStandardQuickViews      │
│     2026-07-15 全列筛选支持（23 列 ExcelFilter）             │
│  ※ GradeChemicalCompositions/GradePhysicalProperties         │
│     2026-07-15 全列筛选支持（17列/12列 ExcelFilter）         │
│  ※ FactoryInspectionRequirements 2026-08-15 新增，29 检验字段 │
│     内联编辑 + 打印；作为订单技术要求默认值数据源            │
└───────────────────────────────────────────────────────────┘
```

### 2.10 配置上下文

```
路由前缀: /section-flow-category-settings, /section-paragraph-config-settings, /combination-groups, /daily-production-capacities, /daily-output-estimates, /standard-work-days, /standard-work-day-delivery-states, /process-definitions, /enum-display-definitions, /dict-value-definitions, /config-parameters, /workstations, /employees, /cold-roll-capacities, /cold-roll-machine-configs
菜单: 扫码管理 → [扫码报工, 设备扫码, 工位管理, 员工管理]；参数表 → [工序组定义(批次/工艺), 工段工量天数(排程/用料), 交货状态附加天数(排程/用料), 规格日产预估(工单执行), 冷轧产能档案(冷轧排程), 冷轧机台数配置(冷轧排程), 重点工段日产(生产总览), 段落日产配置(段落流转), 组合段落归类(段落流转), 流转类别日产配置(流转分析), 枚举显示配置(全局显示), 字典显示配置(全局显示), 系统参数(全局参数)]

┌─ 系统配置 ───────────────────────────────────────────────┐
│                                                           │
│  SectionFlowCategorySettings.razor  /section-flow-category-settings   [列表页+内联编辑]│
│  SectionParagraphConfigSettings.razor /section-paragraph-config-settings [列表页+内联编辑]│
│  CombinationGroups.razor            /combination-groups               [列表页+内联编辑]│
│  DailyProductionCapacities.razor    /daily-production-capacities      [列表页+内联编辑]│
│  DailyOutputEstimates.razor         /daily-output-estimates           [列表页+内联编辑]│
│  StandardWorkDays.razor              /standard-work-days                [列表页+内联编辑]│
│  StandardWorkDayDeliveryStates.razor /standard-work-day-delivery-states [列表页+内联编辑]│
│  ProcessDefinitions.razor            /process-definitions              [列表页+内联编辑]│
│  ColdRollCapacities.razor            /cold-roll-capacities             [列表页+内联编辑]│
│  ColdRollMachineConfigs.razor       /cold-roll-machine-configs        [列表页+内联编辑]│
│  EnumDisplayDefinitions.razor        /enum-display-definitions         [列表页+内联编辑]│
│  DictValueDefinitions.razor          /dict-value-definitions           [列表页+内联编辑]│
│  ConfigParameters.razor             /config-parameters                [列表页+内联编辑]│
│  Workstations.razor                 /workstations                     [列表页+内联编辑]│
│  Employees.razor                    /employees                        [列表页+内联编辑]│
│                                                           │
│  列表页: SectionFlowCategorySettings, SectionParagraphConfigSettings, │
│          CombinationGroups, DailyProductionCapacities,     │
│          DailyOutputEstimates, StandardWorkDays,            │
│          StandardWorkDayDeliveryStates, ProcessDefinitions, │
│          ColdRollCapacities, ColdRollMachineConfigs,        │
│          EnumDisplayDefinitions, DictValueDefinitions,      │
│          ConfigParameters, Workstations, Employees           │
│  注: AdminOnly，所有业务模块引用其参数参与工量/业务计算          │
│  SectionFlowCategorySettings 数据源和 API 均在 Configuration 上下文│
│  （独立服务：SectionFlowCategoryService，独立控制器：SectionFlowCategorySettingsController）│
└───────────────────────────────────────────────────────────┘
```

### 2.11 其他页

```
┌─ 其他 ────────────────────────────────────────────────────┐
│                                                           │
│  Index.razor               /                  [首页看板（严格 2×2：工单执行/批次生产/质量检验/设备维修）]│
│  Login.razor               /login             [登录页]       │
│  DataExchange.razor        /data-exchange     [数据工具]     │
│  ScanExecute.razor         /mobile-report     [扫码报工]     │
│  EquipmentRepair.razor     /equipment-repair  [扫码报修]     │
│  RepairExecute.razor       /repair-execute    [扫码维修]     │
│  EquipmentScan.razor       /equipment-scan    [设备扫码入口]   │
│  Admin/Users.razor         /admin/users         [用户管理]    │
│                                                           │
│  导航栏：左侧 MudNavMenu 树形导航（200px 宽）            │
│          首页                                          │
│          ▸ 订单管理 / ▸ 工单管理 / ▸ 计划排程            │
│          ▸ 批次管理                                     │
│          ▾ 质量管理（2 级嵌套）                          │
│            过程检验 / 成检到料 / 成品检验 / 成检追踪      │
│            不合格报告                                   │
│            ▾ 炉号/化学 → 炉号登记                    │
│            理化检测 / 质量证明书（质量证明书移至理化检测下方）│
│          ▸ 物料管理 / ▸ 仓库管理 / ▸ 设备管理            │
│          ▸ 生产标准 → 标准号列表 / 标准号检验项要求 /   │
          │            牌号对照 / 标准牌号化学成分 /            │
          │            工厂牌号化学成分 / 工厂牌号化分验证 /   │
          │            牌号物理性能 / 子标准速览             │
│          数据工具                                        │
│          ▸ 扫码管理 → 扫码报工 / 设备扫码 / 工位管理 / 员工管理 │
│          ▸ 参数表 → 工序组定义(批次/工艺) / 工段工量天数(排程/用料) │
│                    交货状态附加天数(排程/用料) / 规格日产预估(工单执行) │
│                    冷轧产能档案(冷轧排程) / 冷轧机台数配置(冷轧排程) │
│                    重点工段日产(生产总览) / 段落日产配置(段落流转) │
│                    组合段落归类(段落流转) / 流转类别日产配置(流转分析) │
│                    枚举显示配置(全局显示) / 字典显示配置(全局显示) │
│                    系统参数(全局参数)                    │
│          用户管理                                        │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

---

## 3. 列表页完整清单（需检查加载/排序/筛选）

共 **72 个列表页**，采用 `ServerData` + `ExcelFilter` 模式：

| # | 页面文件 | 路由 | 上下文 | 内联编辑 | 备注 |
|---|---------|------|-------|---------|------|
| 1 | Orders.razor | /orders | 订单 | ✅ | B23 列分组4组(基本信息/合同交付/订单确认/订单执行) + ExcelFilter + B23分组标题栏 + 搜索栏3组(模糊搜索+签订日期+交货日期) + 工具栏「订单接单·出库及现负荷汇总」折叠卡片(5指标×12月，含打印) |
| 2 | Customers.razor | /customers | 订单 | | |
| 3 | GradeMappings.razor | /grade-mappings | 生产标准 | | |
| 4 | WorkOrders.razor | /workorders | 工单 | ✅ | |
| 5 | MaterialPlanOverview.razor | /material-plan-overview | 工单 | | |
| 6 | **Batches.razor** | /batches | 批次 | | ✅ 已过规范检查 |
| 7 | **ProductionRecords.razor** | /production-records | 批次 | | 列分组4组（G1执行信息/G2产出数据/G3工艺参数/G4追溯信息）+ ExcelFilter + 内联编辑 + 分组标题栏 |
| 8 | **SectionOutsources.razor** | /section-outsources | 批次 | | |
| 9 | **OutsourceRecoveries.razor** | /outsource-recoveries | 批次 | | |
| 10 | PicklingInRecords.razor | /pickling-in-records | 批次 | | |
| 11 | PicklingOutRecords.razor | /pickling-out-records | 批次 | | |
| 12 | FurnaceRegistrations.razor | /quality/furnace | 质量 | | |
| 13 | ChemicalCompositions.razor | /chemical-composition | 生产标准 | | 原属质量上下文，已迁移 |
| 14 | ChemicalValidationRules.razor | /chemical-validate | 生产标准 | | 原属质量上下文，已迁移 |
| 15 | ProcessInspections.razor | /quality/process-inspection | 质量 | | |
| 16 | MaterialReceiveChecks.razor | /quality/material-receive-checks | 质量 | | |
| 17 | FinalInspections.razor | /quality/final-inspection | 质量 | | |
| 18 | Equipments.razor | /equipment | 设备 | | |
| 19 | RepairOrders.razor | /repair-orders | 设备 | | |
| 20 | MaintenanceOrders.razor | /maintenance-orders | 设备 | | |
| 21 | InspectionRecords.razor | /inspection-records | 设备 | | |
| 22 | PurchaseOrders.razor | /purchase-orders | 物料 | ✅ | |
| 23 | SubcontractOrders.razor | /subcontract-orders | 物料 | ✅ | |
| 24 | Suppliers.razor | /suppliers | 物料 | | |
| 25 | WarehouseInventory.razor | /warehouse | 仓库 | | Code复用 |
| 27 | InboundHistory.razor | /warehouse/inbound-history | 仓库 | | Code复用 |
| 28 | OutboundHistory.razor | /warehouse/outbound-history | 仓库 | | Code复用 |
| 29 | WorkOrderExecution.razor | /workorder-execution | 工单 | | ✅ 已过规范检查。列分组14组(G1-G14) + 复选框选择列 + 打印选中+打印全部 + 分组标题栏 + 底部聚合行 |
| 30 | QualityProcessTracking.razor | /quality/process-tracking | 质量 | | 只读列表 |
| 31 | Ncrs.razor | /quality/ncr | 质量 | | 列表页+分页汇总 |
| 32 | OrderDemandAdjustment.razor | /order-demand-adjustment | 工单 | ✅ | 内联编辑催单/分批/暂停开关及调整备注 |
| 33 | RawMaterialLockPlanAndExecution.razor | /raw-material-lock-plan | 计划排程 | ✅ | G15 预执行 MudSwitch 内联编辑 + BudgetInputDate 日期输入（LEFT JOIN 实时查询，无计划安排按钮）；右上角「待投料量汇总」按钮展开汇总卡片（待投料+成购两矩阵表 + 理论待投料截日，可打印） |
| 34 | StandardWorkDays.razor | /standard-work-days | 配置 | ✅ | 查改一体表 |
| 35 | StandardWorkDayDeliveryStates.razor | /standard-work-day-delivery-states | 配置 | ✅ | 查改一体表 |
| 36 | ConfigParameters.razor | /config-parameters | 配置 | ✅ | 查改一体表 |
| 37 | SectionFlowAnalysis.razor | /section-flow-analysis | 计划排程 | | 已删除（2026-08-24），数据改经批次计划页内嵌折叠与报表总览消费 |
| 38 | SectionProductionStatus.razor | /section-production-status | 计划排程 | | 已删除（2026-08-24），数据改经批次计划页内嵌折叠与报表总览消费 |
| 39 | SectionFlowCategorySettings.razor | /section-flow-category-settings | 配置 | ✅ | 主表+子表展开 |
| 40 | WorkOrderSchedules.razor | /scheduling-plans | 计划排程 | | LEFT JOIN 实时查询模式（WorkOrderExecutionSummary + WorkOrderPlan 薄表），G15 内联编辑 + 计划安排按钮 |
| 41 | DailyOutputEstimates.razor | /daily-output-estimates | 配置 | ✅ | 查改一体表 |
| 42 | Workstations.razor | /workstations | 配置 | ✅ | 查改一体表 |
| 43 | Employees.razor | /employees | 配置 | ✅ | 查改一体表 |
| 44 | ColdRollPlans.razor | /cold-roll-plans | 计划排程 | | 冷轧按规格维度聚合时间桶分布计划 + 简化/明细视图切换 + 打印功能 + 排程编辑模式（在轧要求/待轧要求/待轧序/待轧设备号/单机单日量）+ **右上角排机估算折叠表（4行×5列，懒加载，可打印）** + **排程建议折叠卡片（半自动：三步决策 特急锁定→流转保底→产能平衡，组级+行级明细表，「一键采用建议」走 save-all 全量同步）** + 搜索栏+ExcelFilter列筛选 |
| 45 | BatchPlans.razor | /batch-plans | 计划排程 | | 全量加载 Items 模式 + 17 工段 Tab 筛选（冷轧类前移，检验类为荒管检/在制检，过程检/成品检 Tab 已删除）+ 列分组标题栏 + 列显隐（永久隐藏 22 列：冷轧排程 5 组 + 工单需求调整 + 批次基础信息多余字段）+ 客户端排序/筛选 + 6 项 Tab 汇总（批次数/总重量/计划流转批次/重量/计划重点批次/重量，重点按 PlanFlowLevel==1 急+）+ 汇总重量单位吨(t) + G13 批次计划组只读（仅抢单/计划备注内联编辑） |
| 46 | FinalInspectionPlan.razor | /final-inspection-plan | 计划排程 | | 全量加载 Items 模式 + 五档Tab(全部/待到料/待检验/检验中/完成检验待入库) + 待检批支重汇总卡片（行=检验项，列=检验项/待到料/待检验+检验中/汇总数据，0值显"-"，可打印）+ 客户排序/筛选 + 列分组 G1-G6（G1批次/G2排程/G3成检状态/G4技术要求检验项/G5各项检验日期/G6数量）+ 紧急程度 MudChip 颜色渲染 |
| 47 | StandardRegisters.razor | /standard-registers | 生产标准 | | ExcelFilter 列筛选 + RenderCell 模板 + FooterContent 分页汇总 + 导航至详情页；Save/SaveItem 返回 Id 防 SeqNo 重复 |
| 48 | GradeChemicalCompositions.razor | /grade-chemical-compositions | 生产标准 | ✅ | 15元素内联编辑 + 全列ExcelFilter(17列) + 列显隐 |
| 49 | GradePhysicalProperties.razor | /grade-physical-properties | 生产标准 | ✅ | 12物理性能字段内联编辑 + 全列ExcelFilter(12列) + 列显隐 |
| 50 | SubStandardQuickViews.razor | /sub-standard-quick-views | 生产标准 | | 全列ExcelFilter(23列)，按标准号快速查看24项检验项目引用标准 |
| 51 | StandardInspectionRequirements.razor | /standard-inspection-requirements | 生产标准 | ✅ | 全列ExcelFilter(23列)，标准号检验项要求+内联编辑 |
| 52 | FactoryInspectionRequirements.razor | /factory-inspection-requirements | 生产标准 | ✅ | 工厂检验项要求，全列ExcelFilter(30列)，29检验字段内联编辑 + 打印（选中+全部） |
| 53 | ChemicalAnalyses.razor | /quality/chemical-analysis | 质量 | | 理化检测-化学分析 |
| 54 | HardnessTests.razor | /quality/hardness-test | 质量 | | 理化检测-硬度检验 |
| 55 | GrainSizeTests.razor | /quality/grain-size-test | 质量 | | 理化检测-晶粒度检验 |
| 56 | PittingCorrosionTests.razor | /quality/pitting-corrosion-test | 质量 | | 理化检测-点腐蚀检验 |
| 57 | IntergranularCorrosionTests.razor | /quality/intergranular-corrosion-test | 质量 | | 理化检测-晶间腐蚀检验 |
| 58 | TensileTests.razor | /quality/tensile-test | 质量 | | 理化检测-室温拉伸检验 |
| 59 | MetallographicTests.razor | /quality/metallographic-test | 质量 | | 理化检测-金相检验 |
| 60 | FlatteningTests.razor | /quality/flattening-test | 质量 | | 理化检测-压扁检验 |
| 61 | FlaringTests.razor | /quality/flaring-test | 质量 | | 理化检测-扩口检验 |
| 62 | DailyProductionCapacities.razor | /daily-production-capacities | 配置 | ✅ | 查改一体表，仿ConfigParameters模式 |
| 64 | Certificates.razor | /quality/certificates | 质量 | | 质量证明书列表页（打印选中/打印全部 + 打印设置对话框：打印版式/字段布局） |
| 65 | PendingDelivery.razor | /warehouse/pending-delivery | 仓库 | | 待发货项列表页（订单关联组含「工单关注」列：取工单执行状况读模型主号-关注档位，按工单号关联） |
| 66 | SubcontractReturnItems.razor | /subcontract-return-items | 物料 | | 委外子项查询—列表页+复选框选择列+打印选中+ExcelFilter全列筛选；字段两组分组（一、委外信息12列含下单日期/要求到货日/委外备注、二、执行状态6列含退货量/属强制完成）；执行状态4档（已发出/部分收回/已完成/超量到货，MudChip与采购订单一致） |
| 67 | FixedLengthWorkOrderView.razor | /fixed-length-work-order-view | 工单 | | 定尺工单联通视图，主号级按长度实时聚合 + 分组标题栏 + 分页汇总（可汇总列：G1需求支数/G3切后支数/G4到料·成切·非成切·次品·合格·合格盈缺/G5入库·入库盈缺，G6主号级聚合不参与求和） |
| 68 | SectionParagraphConfigSettings.razor | /section-paragraph-config-settings | 配置 | ✅ | 段落日产配置，查改一体表 |
| 69 | CombinationGroups.razor | /combination-groups | 配置 | ✅ | 组合段落归类，查改一体表 |
| 70 | ProcessDefinitions.razor | /process-definitions | 配置 | ✅ | 工序组定义（含默认工段 DefaultSections） |
| 71 | EnumDisplayDefinitions.razor | /enum-display-definitions | 配置 | ✅ | 枚举显示配置（display-map/options-map） |
| 72 | DictValueDefinitions.razor | /dict-value-definitions | 配置 | ✅ | 字典显示配置（display-map/enabled-values） |
| 73 | ColdRollCapacities.razor | /cold-roll-capacities | 配置 | | 冷轧产能档案（四维 ProcessType/BilletSpec/RollingSpec/IsFinished 唯一），查改一体表；排程建议产能平衡输入 |
| 74 | ColdRollMachineConfigs.razor | /cold-roll-machine-configs | 配置 | | 冷轧机台数配置（ProcessType 唯一），查改一体表；排程建议产能平衡输入（方式A兜底 daily） |

---

## 4. 代码分离状态

所有 `*.razor.cs` code-behind 文件为 **已提交**（committed），列表页从单体 `.razor` 向分离模式迁移已完成。

---

## 5. 规范检查覆盖记录

| 上下文 | 列表页 | 加载 | 排序 | 筛选 | 检查日期 |
|-------|-------|------|------|------|---------|
| 批次 | Batches | ✅ | ✅ | ✅ | 2026-05-22 |
| 批次 | ProductionRecords | ✅ | ✅ | ✅ | 2026-07-08 列分组重构 |
| 批次 | SectionOutsources | ✅ | ✅ | ✅ | 2026-05-23 |
| 批次 | OutsourceRecoveries ⚠️ | ✅ | ✅ | ✅ | 2026-05-23 |
| 工单 | WorkOrderExecution | ✅ | ✅ | ✅ | 2026-07-09 新增选择列+打印 |
| 其他 | ... | ❌ | ❌ | ❌ | 未检查 |

---

## 6. 关键文件路径

| 类别 | 路径 |
|------|------|
| 页面文件 | `MES.Blazor/Pages/{Subdir}/*.razor`（按模块划分子目录） |
| Code-behind | `MES.Blazor/Pages/{Subdir}/*.razor.cs` |
| 前端 Service | `MES.Blazor/Services/*Service.cs` |
| 后端 Service | `MES.Services/{Module}/*Service.cs` |
| API 控制器 | `MES.Api/Controllers/{Module}/*Controller.cs` |
| DTO | `MES.Core/DTOs/*QueryParams.cs` |
| 实体 | `MES.Data/Entities/*.cs` |
| 接口 | `MES.Core/Interfaces/I*Service.cs` |
| 筛选扩展 | `MES.Services/Helpers/QueryableExtensions.cs` |
| 枚举映射 | `MES.Blazor/Helpers/DisplayHelper.cs` |
| ExcelFilter | `MES.Blazor/Components/ExcelFilter.razor` |
| 开发规范 | `docs/04_开发规范.md` |
| 导航布局 | `MES.Blazor/Shared/MainLayout.razor` |

---

> 使用方式：询问关于页面结构、上下文归属、列表页检查范围等问题时，可引用此文档作为参考基础。
>
> **最后更新：2026-08-23（V27）** — 产量报表删除：报表系统上下文消亡（后端 ReportController/ReportService/ReportPrintHelper/DailyProductionReportDto、前端 ProductionOutput.razor + ReportService 全删，菜单「报表系统」组移除，页面清单移除 #63，报表系统小节并入仓库上下文）
>
> **最后更新：2026-08-23（V26）** — 仓库管理菜单调整：在制品库移至次品库上方（原料库→成品库→在制品库→次品库→物料进出存报表→待发货项），物料进出存报表菜单自报表系统移入仓库管理组（报表系统仅余产量报表）；待发货项 #65 订单关联组新增「工单关注」列（取工单执行状况读模型「实时关注」组「主号-关注」档位，按工单号关联，可筛选/排序/打印）
>
> **最后更新：2026-08-19（V25）** — 子项查询 #66 字段两组分组 + 4 态显示：委外信息组12列（新增「下单日期」=主表下单日期、「要求到货日」=主表收回期限、「委外备注」默认隐藏）+ 执行状态组6列（新增「退货量」按委外单号→退货-原仓库批→原仓库批SourceOrderNo汇总、「属强制完成」；「截止回收日」归入执行状态组，置于执行状态后）；执行状态新增「超量到货」档（回收>需求×105% 且超出量>100kg，优先于完成判定，MudChip 与采购订单一致）；采购订单 #22 下单日期去 MudChip 改普通文本
>
> **最后更新：2026-08-19（V24）** — 采购订单 #22 G2 执行状态新增 2 列：「退货量」（按采购单号→仓库批→退货出库汇总支/重量，0 显"-"）+「属强制完成」（是/-）；出库历史 #28 + 批量出库页「退货-原批次号」改名「退货-原仓库批」
>
> **最后更新：2026-08-19（V23）** — 仓库出库模块列调整：出库历史 #28 + 批量出库页出库类型精简 8→5 值（删报废出库/检验领用/移库出库，存量移库归入其它出库）；新增「退货-原批次号」列（位于出库工单号后，可编辑+搜索/排序/筛选）；「委外穿孔号」改名「委外-穿孔号」
>
> **最后更新：2026-08-19（V23）** — 采购订单 #22 列表字段调整：改三组（G1采购信息13列含新采购备注、G2执行状态3列含新到货截止日、G3来源销售订单17列默认隐藏）；状态新增「超量到货」档（到料>采购×105% 且超出量>100kg，优先于完成判定）；已到货量 0支/0kg 显示"-"
>
> **最后更新：2026-08-17（V22）** — 成检计划 #46 描述更新：四档Tab→五档Tab（新增完成检验待入库档）、列分组 G1-G4→G1-G6（含技术要求检验项组）、新增待检批支重汇总卡片说明；原锁计划 #33 汇总按钮改名「待投料量汇总」移右上角 + 展开卡片含待投料/成购两矩阵表 + 打印
>
> **最后更新：2026-08-15（V21）** — 文档失效内容清理：§1 计划排程 8→6 页（工段待产量/工段流转分析已从菜单移除，§2.3 同步删菜单项并注明页面/接口保留）；配置上下文 8→13 页（新增生产-段落日产配置/生产-组合归类表/生产-工序组定义/参数-枚举显示配置/参数-字典显示配置）；§2.10 与 §2.12 参数表菜单、路由前缀、列表页清单同步更新；§3 列表页 #37/#38 标注"已从菜单移除"、#45 检验类 Tab 描述更正（过程检/成品检已删）、补入 #67-#71 五个新配置列表页，计数 66→71
>
> **最后更新：2026-08-15（V21）** — 生产标准上下文新增工厂检验项要求模块（FactoryInspectionRequirements + FactoryInspectionRequirementCreate，列表页数 8→9，总页数 16→18，清单表 71→72 重编号）；作为订单技术要求默认值数据源
>
> **最后更新：2026-08-18（V21）** — 质量证明书模块补打印设置对话框（CertificatePrintSettingsDialog，无路由，MudDialog 弹层：打印版式 Tab + 字段布局 Tab）；#64 Certificates 描述补打印能力；模块清单同步
>
> **最后更新：2026-08-18（V22）** — 订单列表新增「订单接单·出库及现负荷汇总」折叠卡片（5 指标 × 本年 12 月：接单量/出库量 + 成品库存(完工/未完工)/订单负荷量(实时)，0 显「-」）+ 打印功能；#1 Orders.razor 备注同步（第 4 组「工单执行」→「订单执行」）
>
> **最后更新：2026-08-09（V20）** — 工单上下文子页补入在产主工单计划页（WorkOrderInMainWorkOrderPlanCreate，create+edit 双路由，页面数 16→17）；§3 #66 定尺工单分页汇总描述由「仅 PlannedQuantity 可求和」更正为多列可求和（G1需求支数/G3切后支数/G4成检支数列/G5入库支数列，G6主号级聚合不参与求和）
>
> **最后更新：2026-08-01（V19）** — 工单上下文新增定尺工单联通视图（FixedLengthWorkOrderView，1页，列表页数 4→5）；§3 列表页清单补入该页并修正编号错乱（原 #6 缺失、#7 重复，现 #1-#66 连续），计数 64→66
>
> **最后更新：2026-07-17（V18）** — 新增质量证明书模块(3页) + 理化检测创建页(8页) + 待发货项列表页 + 生产标准创建页(2页)；生产标准全列筛选支持；code-behind 分离迁移状态更新
