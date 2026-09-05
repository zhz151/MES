# MES 前端页面结构参考

> 版本：V45（2026-09-05；生成 2026-08-19）
> 用途/状态：Quick Reference - 按导航菜单分组的前端页面结构参考，§1 上下文总览 / §2 各上下文页面块 / §3 列表页全量清单。
> 上次实质变更（V45）：前端导航菜单**单源树化**——菜单统一由 `MES.Blazor/Shared/AppMenu.cs`（`AppMenu.Root`）驱动，桌面/手机共用一棵树（见 §6），根治手机菜单漂移（订单组残留「牌号对照」等）；手机横屏大宽（landscape 且 innerWidth≥700）自动切桌面布局（`ResponsiveLayout`）。菜单结构/页面归属本身未变。
> 历史变更（V44）：生产计件类别 #76 模拟测算搜索与金额口径三项增强——工段/工序中文子串反查、元/头折算接线、断切率同源。

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
| 工资结算 | 工资结算 | SalaryViewer/Editor/Full + Admin | 11 | 10 |
| 用户管理 | (Admin按钮) | UserViewer/Editor/Full + Admin | 1 | 0 |

---

## 2. 各上下文详细页面清单

### 2.1 订单上下文

```
路由前缀: /orders, /customers, /orders/pending-delivery
菜单: 订单管理 → [订单列表, 客户管理, 订单成品(实时库存)]

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
│  PendingDelivery.razor  /orders/pending-delivery [列表页]  │
│                                                           │
│  列表页: Orders, Customers, PendingDelivery               │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

### 2.2 工单上下文

```
路由前缀: /workorders, /material-plan-overview, /workorder-overview, /workorder-execution, /workorders-demand-adjustment, /fixed-length-work-order-view
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
│  WorkOrderInMainWorkOrderPlanCreate.razor /workorders/{id}/in-main-work-order-plan/create │
│                                                           │
│  MaterialPlanOverview.razor /material-plan-overview [列表页]│
│                                                           │
│  WorkOrderExecution.razor         /workorder-execution            [列表页]  │
│                                                           │
│  OrderDemandAdjustment.razor      /workorders-demand-adjustment        [列表页]  │
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
│     SectionProductionStatus, SectionParagraphFlowAnalysis │
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
│  --- 理化检测模块 ---                                      │
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
│  --- 质量证明书模块 ---                                      │
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
         /warehouse/inbound-history, /warehouse/outbound-history
菜单: 仓库管理 → [原料库, 成品库, 在制品库, 次品库, 物料进出存报表]
      （报表系统已删除并入仓库管理；原「待发货项」已迁至订单管理上下文，更名「订单成品(实时库存)」）

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
│  InboundHistory.razor         /warehouse/inbound-history      [列表页]│
│  InboundHistory.razor         /warehouse/inbound-history/{Code}[列表页(复用)]│
│                                                           │
│  OutboundHistory.razor        /warehouse/outbound-history      [列表页]│
│  OutboundHistory.razor        /warehouse/outbound-history/{Code}[列表页(复用)]│
│  MonthlyStock.razor          /warehouse/monthly-stock          [报表页]   │
│  报表页: MonthlyStock（原生 table，4 报表切换：入库/出库/库存/物料进出存；入库按来源展开、出库按类型展开（含物料汇总合并列）；行=库房×物料类型，库房+物料类型双层合并单元格，无合计行；当前月之后月份单元格留空，「实时结存/实时数据」=截至当前月合计，三值格「入/出,[结]」如 80/15,[65]；打印横向 A4 撑满页宽）│
│  ※ API: InventoryController (api/inventory/monthly-stock-summary) │
│                                                           │
│  列表页: WarehouseInventory, InboundHistory, OutboundHistory  │
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
路由前缀: /section-paragraph-config-settings, /daily-production-capacities, /daily-output-estimates, /standard-work-days, /standard-work-day-delivery-states, /process-definitions, /enum-display-definitions, /dict-value-definitions, /config-parameters, /workstations, /employees, /cold-roll-capacities, /cold-roll-machine-configs, /cold-roll-machine-group-configs
菜单: 扫码管理 → [扫码报工, 设备扫码, 工位管理, 员工管理]；参数表 → [工序组定义(批次/工艺), 工段工量天数(排程/用料), 交货状态附加天数(排程/用料), 规格日产预估(工单执行), 冷轧产能档案(冷轧排程), 冷轧机台数配置(冷轧排程), 冷轧机台组配置(冷轧排程), 重点工段日产(生产总览), 段落日产配置(段落流转), 枚举显示配置(全局显示), 字典显示配置(全局显示), 系统参数(全局参数)]

┌─ 系统配置 ───────────────────────────────────────────────┐
│                                                           │
│  SectionParagraphConfigSettings.razor /section-paragraph-config-settings [3类配置驱动自动生成+Tab筛选+仅参数可编辑]│
│  DailyProductionCapacities.razor    /daily-production-capacities      [列表页+内联编辑]│
│  DailyOutputEstimates.razor         /daily-output-estimates           [列表页+内联编辑]│
│  StandardWorkDays.razor              /standard-work-days                [列表页+内联编辑]│
│  StandardWorkDayDeliveryStates.razor /standard-work-day-delivery-states [列表页+内联编辑]│
│  ProcessDefinitions.razor            /process-definitions              [列表页+内联编辑]│
│  ColdRollCapacities.razor            /cold-roll-capacities             [列表页+内联编辑]│
│  ColdRollMachineConfigs.razor       /cold-roll-machine-configs        [列表页+内联编辑]│
│  ColdRollMachineGroupConfigs.razor  /cold-roll-machine-group-configs  [列表页+内联编辑]│
│  EnumDisplayDefinitions.razor        /enum-display-definitions         [列表页+内联编辑]│
│  DictValueDefinitions.razor          /dict-value-definitions           [列表页+内联编辑]│
│  ConfigParameters.razor             /config-parameters                [列表页+内联编辑]│
│  Workstations.razor                 /workstations                     [列表页+内联编辑]│
│  Employees.razor                    /employees                        [列表页+内联编辑]│
│                                                           │
│  列表页: SectionParagraphConfigSettings, DailyProductionCapacities,   │
│          DailyOutputEstimates, StandardWorkDays,            │
│          StandardWorkDayDeliveryStates, ProcessDefinitions, │
│          ColdRollCapacities, ColdRollMachineConfigs,        │
│          EnumDisplayDefinitions, DictValueDefinitions,      │
│          ConfigParameters, Workstations, Employees           │
│  注: AdminOnly，所有业务模块引用其参数参与工量/业务计算          │
└───────────────────────────────────────────────────────────┘
```

### 2.11 工资结算上下文

```
路由前缀: /payroll
菜单: 工资结算 → [生产计件类别, 成检计件类别, 考勤表, 杂辅工记录, 集体计件评分, 津贴与处罚, 非计件工资, 个人计件工资, 集体计件月结, 靠工计件月结, 月工资津贴汇总]
     （独立主菜单，位于扫码管理下方、参数表上方）

┌─ 工资结算 ───────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                                                                                              │
│ Attendance.razor                /payroll/attendance                 [考勤表月视图网格页]                     │
│ MonthlyWages.razor              /payroll/wages/non-piece            [非计件工资月视图网格页]          │
│                                /payroll/wages/piece                 [个人计件工资月视图网格页（单组件双路由）] │
│ CollectiveScores.razor          /payroll/collective-scores          [集体计件评分页]                  │
│ CollectiveMonthly.razor         /payroll/collective-monthly         [集体计件月结页]                  │
│ PieceAttendanceMonthly.razor    /payroll/attendance-monthly         [靠工计件月结页]                  │
│ MiscWorkMonthly.razor           /payroll/misc-work                  [杂辅工记录台账页]                │
│ AllowanceMonthly.razor          /payroll/allowance                  [津贴与处罚月度网格页]            │
│ MonthlySummary.razor            /payroll/monthly-summary            [月工资津贴汇总页]                    │
│ PieceRateProductionCategories.razor  /payroll/piece-rate-categories  [生产计件类别列表页]                    │
│ PieceRateProductionCategoryEdit.razor                                                                        │
│         /payroll/piece-rate-categories/create        [创建页]                                                │
│         /payroll/piece-rate-categories/edit/{Id:int}  [编辑页]                                               │
│ FinalInspectionCategories.razor   /payroll/final-inspection-categories  [成检计件类别列表页]                  │
│ FinalInspectionCategoryEdit.razor /payroll/final-inspection-categories/create、/edit/{Id:int} [成检类别编辑页]│
│                                                                                                              │
│ 列表页: Attendance, PieceRateProductionCategories                                                            │
│  计件类别体系 = 「类别主表 + 维档子表」两表模型：类别 = 工段×工序/产类/阶段约束（空=全选；生产计件）或成检项目（成检单键）+ 基准价 + 结算单位 + 启停；
│  结算单价 = 基准价 × 命中维档系数连乘（不配某维 = 系数 1）；同覆盖仅允许一个启用类别；
│  编辑页 = 上区类别定义 + 下区同页整组编辑维档（区间/等值维），同维重叠/重复本地标红、跨类别覆盖冲突服务端权威校验，保存整类一次落库（删除级联删档）。
│  各页特性明细（模拟测算按记录点选计价、每日工资引擎带出/保存快照、集体/靠工月结重算、津贴整元规约、月汇总整表打印等）见 §3 #76-#85；考勤月视图网格见本块首行 Attendance。
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.12 其他页

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
│          ▸ 工资结算 → 生产计件类别 / 成检计件类别 / 考勤表 / 杂辅工记录 / │
│                       集体计件评分 / 津贴与处罚 / 非计件工资 / 个人计件工资 / │
│                       集体计件月结 / 靠工计件月结 / 月工资津贴汇总 │
│          ▸ 参数表 → 工序组定义(批次/工艺) / 工段工量天数(排程/用料) │
│                    交货状态附加天数(排程/用料) / 规格日产预估(工单执行) │
│                    冷轧产能档案(冷轧排程) / 冷轧机台数配置(冷轧排程) │
│                    冷轧机台组配置(冷轧排程)               │
│                    重点工段日产(生产总览) / 段落日产配置(段落流转) │
│                    枚举显示配置(全局显示) / 字典显示配置(全局显示) │
│                    系统参数(全局参数)                    │
│          用户管理                                        │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

---

## 3. 列表页完整清单（需检查加载/排序/筛选）

共 **79 个列表页**，采用 `ServerData` + `ExcelFilter` 模式：

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
| 32 | OrderDemandAdjustment.razor | /workorders-demand-adjustment | 工单 | ✅ | 内联编辑催单/分批/暂停开关及调整备注 |
| 33 | RawMaterialLockPlanAndExecution.razor | /raw-material-lock-plan | 计划排程 | ✅ | G15 预执行 MudSwitch 内联编辑 + BudgetInputDate 日期输入（LEFT JOIN 实时查询，无计划安排按钮）；右上角「待投料量汇总」按钮展开汇总卡片（待投料+成购两矩阵表 + 理论待投料截日，可打印） |
| 34 | StandardWorkDays.razor | /standard-work-days | 配置 | ✅ | 查改一体表 |
| 35 | StandardWorkDayDeliveryStates.razor | /standard-work-day-delivery-states | 配置 | ✅ | 查改一体表 |
| 36 | ConfigParameters.razor | /config-parameters | 配置 | ✅ | 查改一体表 |
| 40 | WorkOrderSchedules.razor | /scheduling-plans | 计划排程 | | LEFT JOIN 实时查询模式（WorkOrderExecutionSummary + WorkOrderPlan 薄表），G15 内联编辑 + 计划安排按钮 |
| 41 | DailyOutputEstimates.razor | /daily-output-estimates | 配置 | ✅ | 查改一体表 |
| 42 | Workstations.razor | /workstations | 配置 | ✅ | 查改一体表 |
| 43 | Employees.razor | /employees | 配置 | ✅ | 查改一体表（列偏好 v5，2026-09-03 靠工计件六期：「靠工系数」前新增**靠工岗位**多选列 AttendancePositions，候选=计件活岗 GET api/employee/piece-positions，保存岗位英文 Key 逗号串，显示逐项中文） |
| 44 | ColdRollPlans.razor | /cold-roll-plans | 计划排程 | | 冷轧按规格维度聚合时间桶分布计划 + 简化/明细视图切换 + 打印功能 + 排程编辑模式（在轧要求/待轧要求/待轧序/待轧设备号/单机单日量）+ **右上角排机估算折叠表（4行×5列，懒加载，可打印）** + **排程建议折叠卡片（半自动：三步决策 特急锁定→流转保底→产能平衡，组级+行级明细表，「一键采用建议」走 save-all 全量同步）** + 搜索栏+ExcelFilter列筛选 |
| 45 | BatchPlans.razor | /batch-plans | 计划排程 | | 全量加载 Items 模式 + **工段筛选 Tab 配置驱动**（`GET api/batch-plan/section-tab-options`：冷轧/冷拔=工序组定义启用冷轧拔工序逐工序、普通工段=工段工量天数启用工段扣除冷轧拔/检验/入库且内抛/内修磨独立、末尾固定荒管检/在制检，2026-08-30 起新增工序自动出现）+ 列分组标题栏 + 列显隐（永久隐藏 22 列：冷轧排程 5 组 + 工单需求调整 + 批次基础信息多余字段）+ 客户端排序/筛选 + 6 项 Tab 汇总（批次数/总重量/计划流转批次/重量/计划重点批次/重量，重点按 PlanFlowLevel==1 急+）+ 汇总重量单位吨(t) + G13 批次计划组只读（仅抢单/计划备注内联编辑） |
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
| 62 | DailyProductionCapacities.razor | /daily-production-capacities | 配置 | ✅ | 查改一体表，仿ConfigParameters模式；行键=荒管抛光固定 Polish + 冷轧机台组 GroupKey（2026-08-30 起配置表驱动下拉） |
| 64 | Certificates.razor | /quality/certificates | 质量 | | 质量证明书列表页（打印选中/打印全部 + 打印设置对话框：打印版式/字段布局） |
| 65 | PendingDelivery.razor | /orders/pending-delivery | 订单 | | 订单成品(实时库存)列表页（原仓库「待发货项」，2026-08-26 迁入订单上下文；订单关联组含「工单关注」列：取工单执行状况读模型主号-关注档位，按工单号关联） |
| 66 | SubcontractReturnItems.razor | /subcontract-return-items | 物料 | | 委外子项查询—列表页+复选框选择列+打印选中+ExcelFilter全列筛选；字段两组分组（一、委外信息12列含下单日期/要求到货日/委外备注、二、执行状态6列含退货量/属强制完成）；执行状态4档（已发出/部分收回/已完成/超量到货，MudChip与采购订单一致） |
| 67 | FixedLengthWorkOrderView.razor | /fixed-length-work-order-view | 工单 | | 定尺工单联通视图，主号级按长度实时聚合 + 分组标题栏 + 分页汇总（可汇总列：G1需求支数/G3切后支数/G4到料·成切·非成切·次品·合格·合格盈缺/G5入库·入库盈缺，G6主号级聚合不参与求和） |
| 68 | SectionParagraphConfigSettings.razor | /section-paragraph-config-settings | 配置 | ✅ | 段落日产配置（3类配置驱动自动生成：冷轧拔/普通工段/检验，段落仅参数可编辑），Tab 筛选 |
| 70 | ProcessDefinitions.razor | /process-definitions | 配置 | ✅ | 工序组定义（含默认工段 DefaultSections） |
| 71 | EnumDisplayDefinitions.razor | /enum-display-definitions | 配置 | ✅ | 枚举显示配置（display-map/options-map） |
| 72 | DictValueDefinitions.razor | /dict-value-definitions | 配置 | ✅ | 字典显示配置（display-map/enabled-values） |
| 73 | ColdRollCapacities.razor | /cold-roll-capacities | 配置 | | 冷轧产能档案（四维 ProcessType/BilletSpec/RollingSpec/IsFinished 唯一），查改一体表；排程建议产能平衡输入 |
| 74 | ColdRollMachineConfigs.razor | /cold-roll-machine-configs | 配置 | | 冷轧机台数配置（ProcessType 唯一），查改一体表；排程建议产能平衡输入（方式A兜底 daily） |
| 75 | ColdRollMachineGroupConfigs.razor | /cold-roll-machine-group-configs | 配置 | | 冷轧机台组配置（GroupKey 唯一），归组配置表驱动；工序多选（仅启用的冷轧/冷拔工序，显示走 GetProcessNameText 中文）+供给目标组列（供需链显式化，组角色字段已移除；链合法性校验：凡配目标则目标存在+无环，允许多链/多级链，2030→冷拔(None) 末端合法）；保存/删除失效三引擎缓存键；工序禁用时自动从组内移除（ProcessDefinitionService） |
| 76 | PieceRateProductionCategories.razor | /payroll/piece-rate-categories | 工资结算 | | 生产计件类别列表页（两表模型：PieceRateProductionCategory + PieceRateProductionTier 维档子表；类别 = 工段×工序/产类/阶段约束 + 基准价 + 维档系数，结算单价 = 类别基准价 × 命中维档系数连乘，不配某维=系数1，工段×工序×产类×阶段同覆盖仅允许一个启用类别）；列：自动组合名/工段中文/基准价(G29)/单位/维档数/是否启用/备注/更新时间/创建时间 + 列显隐/排序；顶部工段下拉 + 启停下拉 + 模糊搜索（自动组合名/工段/备注）；行操作：编辑走独立页 `/payroll/piece-rate-categories/edit/{Id}`、删除弹 ConfirmDialog（级联删维档），新增走 `/payroll/piece-rate-categories/create` |
| 77 | FinalInspectionCategories.razor | /payroll/final-inspection-categories | 工资结算 | | 成检计件类别列表页：主表 PieceRateFinalInspectionCategory = 成检项目 InspectionItem 单键 + 基准价 + 单位 + 启停（同项目启用唯一）+ 子表 8 维档（区间 外径/壁厚/长度/检验支数整数闭带 + 等值 长度状态/特殊牌号/特殊制造状态/特殊设备号）；列 + 行内展开「模拟测算」按**成检记录点选计价**：候选=全局任意跨期成检记录，顶部成检项目下拉 + 关键字(生产编号/设备/操作人)，服务端分页记录小表，行「试算」按 Id 计价 → 命中类别 基准价×总系数=单价 + 整行计件额(与月结同口径、未按人头均分)/缺数量灰字提示/未定价提示；手动填维度试算表单已删，match-price 端点保留）+ 专用批量导出/导入弹窗 |
| 78 | FinalInspectionCategoryEdit.razor | /payroll/final-inspection-categories/create、/edit/{Id:int} | 工资结算 | | 成检计件类别编辑页：定义（成检项目单选 + 基准价/单位/启停/备注）+ 8 维档同页整组编辑，保存整类落库（档行整组替换） |
| 79 | MonthlyWages.razor | /payroll/wages/non-piece、/payroll/wages/piece | 工资结算 | | 每日工资两表月视图网格页（单组件双路由）：仿考勤网格（attendance-scroll/grid + enableAttendanceKeyNav）单元格=每日工资额（原生 input 失焦提交），引擎自动带出 + 常编辑 + 显式「引擎重算」（ConfirmDialog 覆盖网格）+「保存本月」落库（Amount>0 存/空删，SalaryMode 归口快照）；非计件=Hourly 小时×时薪 / Daily 日薪×min(出勤,8)/8，个人计件=PieceIndividual 当月产量+成检按现行单价逐行折算（成检合作行按人数均分、Range/NonFixed 定尺 6000mm 兜底、PerTon/PerPiece/PerKm 换算）；顶部 年/月/«»/工号姓名搜索/岗位类别/岗位筛选 + 表头排序；员工集=归口∈组启用员工 ∪ 当月历史快照（换归口历史月仍显示）；写操作 SalaryEdit 门控 |
| 80 | CollectiveScores.razor | /payroll/collective-scores | 工资结算 | | 集体计件评分页：年月选择 → 员工按岗位分组卡片（工号/姓名/岗位/分值输入 1–10 一位小数如 8.5 + 已评分/新录入/未评分备注）→「保存评分」整月 upsert；只显示在册集体成员 + 当月已有评分历史员工补集；写操作 SalaryEdit 门控 |
| 81 | CollectiveMonthly.razor | /payroll/collective-monthly | 工资结算 | | 集体计件月结页：年月选择 → 每岗位结算卡片（成员行 出勤/分值/权重只读 + 实得金额整元可改，默认 已存?Saved:引擎草稿；卡标题=岗位中文+岗位池+Σw）；顶部「引擎重算」(ConfirmDialog 覆盖在册集体成员)/「全量重算(清历史)」(双重确认 PayrollFullRecalcDialogs 清历史快照成员)/「保存本月」；写操作 SalaryEdit 门控 |
| 82 | PieceAttendanceMonthly.razor | /payroll/attendance-monthly | 工资结算 | | 靠工计件月结页：年月选择 → 单张 auto-table 员工行（靠工无岗位池不分组）：工号/姓名/靠工岗位(中文)/出勤/靠工系数/平均时薪(G29 只读)/实得金额(整元可改，默认 已存?Saved:RoundYuan引擎草稿)/备注(历史快照·未配岗·无计件参照·无出勤)；员工集=在册靠工 ∪ 当月快照员工（停用/换模式历史月仍显示）；顶部「引擎重算」/「全量重算(清历史)」双重确认/「保存本月」；写操作 SalaryEdit 门控 |
| 83 | MiscWorkMonthly.razor | /payroll/misc-work | 工资结算 | ✅ | 杂辅工记录台账页：杂项辅助手工登记（完整月工资 = 各类工资 + 杂辅）。MudTable 台账列表页，行=一条杂辅任务（日期/工号/姓名/内容/小时/金额 G29/备注），金额=手工录入源头保留小数不取整、同人同日可多条；行内编辑（编辑不改员工归属）+ 新增面板（员工下拉=全量启用员工）+ ConfirmDialog 删除；顶部 MudPaper 描述文字 + 月份导航（Chevron + 年月 MudSelect）+ 当月合计 chips（N 条 · Σ小时 · Σ金额，整月口径）+ 页内关键词筛选（工号/姓名/内容，客户端，合计不变）；日期 MudTextField string yyyy-MM-dd（禁 MudDatePicker）；写操作 SalaryEdit 门控 |
| 84 | AllowanceMonthly.razor | /payroll/allowance | 工资结算 | ✅ | 津贴与处罚月度网格页：月度金额录入，行=员工、列=固定 9 金额项目（满勤奖/工龄奖/夜班津贴/岗位补贴/高温费/工伤补贴/带班费/处罚/代缴社保，参考 Excel《津贴与处罚.xlsx》），宽表每人每月一行（EmployeeId+Year+Month 唯一）；金额强制整元（RoundYuan AwayFromZero、空/0=null、禁负数，OnCellChanged 即时规约与后端 NormalizeAmount 同口径）；员工月历 = IsActive 在册 ∪ 当月已有记录（停用员工当月行浅灰回显可改）；考勤同款 attendance-grid 宽表（attendance-scroll/grid 类 + 原生 input 每格失焦提交 + enableAttendanceKeyNav 方向键导航复用；sticky-left 工号/姓名/岗位类别/岗位列，岗位中文经 DictValueDisplayHelper）+ tfoot 各列整月合计 + 页内关键词筛选（工号/姓名/岗位，客户端，合计不变）+ @foreach 渲染防闭包 + OverrideMap 事件订阅重渲染；顶部「清空本月」(ConfirmDialog Error，提交空 Rows)「保存本月」(Snackbar 计数) SalaryEdit 门控 |
| 85 | MonthlySummary.razor | /payroll/monthly-summary | 工资结算 | ✅ | 月工资津贴汇总页：员工某结算月「完整应发/实发」汇总表（参考 Excel《工资条及打印.xlsx》17 列：工号/姓名/月份常量/出勤天数/本月基础工资/本月杂辅工资 + 岗位补贴·工龄奖·满勤奖·带班费·夜班津贴·高温费·工伤补贴 7 正津贴 + 处罚·代缴社保(存负)/应发/实发）；基础工资按各子页「已保存金额」归口（Fixed=Employee.MonthlyWage、PieceCollective→集体月结快照、PieceAttendance→靠工月结快照、Hourly/Daily/PieceIndividual→每日工资当月Σ），出勤天数=当月考勤去重日期数；应发=基础+杂辅+7 正津贴，实发=应发+处罚+代缴（后两列存负）；行集=IsActive 在册 ∪ 当月任一来源有行（停用行灰显），工号升序 + 页内关键词（工号/姓名）+ 金额 0 网格留空 + tfoot 列合计；顶部年/月导航 + 已保存/未保存徽标 +「保存本月」(SalaryEdit，整月重算替换快照 PayrollMonthlySummaryRecord 每人每月一行 UK)+「全部打印」A4 横向整表 +「个人打印」每员工一条带表头工资条（两打印读已保存快照、未保存禁用提示「先保存本月」）；写操作 SalaryEdit 门控 |

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
| 菜单树（单一数据源） | `MES.Blazor/Shared/AppMenu.cs` + `AppMenuNode.cs`（桌面/手机共用 `AppMenu.Root`；**改动菜单只许改这里**，回归断言 `MES.Tests/Components/AppMenuTests.cs`） |
| 布局外壳 / 菜单渲染 | `ResponsiveLayout.razor`（移动判定 + 横屏切桌面）→ `MainLayout.razor`（桌面）/ `MobileLayout.razor`（手机）；菜单渲染 `DesktopMenuNode.razor` / `MobileMenuNode.razor` |

---

> 使用方式：询问关于页面结构、上下文归属、列表页检查范围等问题时，可引用此文档作为参考基础。
