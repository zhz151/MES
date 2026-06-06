# MES 项目持久知识

## 架构与上下文
- **Configuration 上下文** — 系统配置表，独立于其他业务模块。实体在 `MES.Data.Entities.Configuration`，Service 在 `MES.Services.Configuration`，Controller 在 `MES.Api.Controllers.Configuration`，Blazor 页面在 `MES.Blazor.Pages.Configuration`
- **StandardWorkDay** — 标准工量天数配置表。字段: SectionName, PlantGradePrefix(可空), StandardDays, Remark。唯一索引 (SectionName, PlantGradePrefix)
- **StandardWorkDayDeliveryState** — 交货状态附加天数配置表。字段: DeliveryState, ExtraDays, PlantGradePrefix(可空), Remark。唯一索引 (DeliveryState, PlantGradePrefix)
- **ConfigParameter** — 业务参数配置表（EAV 模式）。字段: Category, ParamKey, ParamValue(decimal), Remark。唯一索引 (Category, ParamKey)。约 50+ 处硬编码数值常量由此替代覆盖 12 个分类
- **配置页面权限** — 均为 `Roles.Policies.AdminOnly`（AdminOnly="Admin"）
- **计算逻辑替换** — `ProductionRecordService` 注入 `IStandardWorkDayService`，计算 RemainingWorkDays/TotalWorkDays 时调用 `GetStandardDaysMapAsync(plantGrade)` 从 DB 读取天数，替代 `SectionDefs.GetStandardDays()` 硬编码
- **ConfigParameter 缓存** — Service 层 `_configMaps` 字典仅缓存单次请求内重复查询（Scoped 生命周期），修改参数后下次请求自动加载新值
- **ConfigParameter 使用模式** — 注入 `IConfigParameterService`，调用 `GetConfigAsync(category, key, defaultValue)` 辅助方法，带 `GetValueOrDefault` 回退默认值
- **ConfigParameter 分类清单：** MaterialPlanStatus(4), DefaultValue(6), MaterialPlanRatio(4), DimensionTolerance(4), LengthDefault(2), ReworkRatio(8), WarehouseThreshold(4), ContractWeight(2), SequenceJump(1), ProcessingDiscount(1), ProductionCapacity(5), DateBucket(5) — 涉及 14 个 Service
- **ApiEndpoints** — 所有 `api/xxx` 路径在 `MES.Shared.Constants.ApiEndpoints` 集中管理，Service 类通过 `ApiEndpoints.Xxx` 引用

## Scheduling 模块 — 8 个页面

### RawMaterialLockPlanAndExecution
- 原锁计划及执行页面（LEFT JOIN 实时查询模式）。G1-G12 来自 `WorkOrderExecutionSummary`（ScheduleStage=1），G13 LEFT JOIN `OrderDemandAdjustment`（IsUrging/IsBatchDelivery/IsPaused/AdjustmentRemark 四个字段），G15 LEFT JOIN `RawMaterialLockPreExecution` 独立小表。ApiEndpoint = `api/raw-material-lock-plan`。无独立物化表，无"计划安排"按钮。G15 字段：IsPreInput, BudgetInputDate, IsMainNoMaterialComplete（系统计算）

### BatchPlan（批次看板）
- 在产明细计划实时视图。`ProductionBatch`（Status=None/InProgress）LEFT JOIN `WorkOrderExecutionSummary`（UrgencyLevel/ScheduleStage） + `WorkOrderSchedule`（ProductionAttentionProcess）。ApiEndpoint = `api/batch-plan`。17 个工段筛选 Tab（含冷轧类工序+工段逻辑、过程/成品检验 SequenceNumber 比较）。IsKeyBatch 逻辑：ScheduleStage==2 && Urgency in ["A+急","A急"] && (PendingProcess=="荒管处理" || PendingProcess==ProductionAttentionProcess || ProductionAttentionProcess=="收尾-成检")。Tab 汇总通过 PagedResult.Extras 字典返回。计算字段客户端排序（`_clientSortableKeys` HashSet 定义）。枚举筛选通过 EnumOptions 映射中文显示。列分组标题栏 G2→G4→G1→G3 排列。

### ColdRollPlan（冷轧看板）
- 按规格维度聚合的冷轧时间桶分布看板。`ProductionBatch`（Status=None/InProgress，Select 投影仅加载需要的字段）+ ProcessGroups 投影（仅加载 SequenceNumber/ProcessName/ManufacturingSpec + 15 个工段 int? 字段）+ WorkOrderExecutionSummary + WorkOrderSchedule（后两者仅加载关联工单的数据）。ApiEndpoint = `api/cold-roll-plan`。核心逻辑：所有冷轧工序组（IsColdRollOrDraw）均生成行，通过 section seq 差(diff) 映射到各时间桶。近日在轧通过直接状态检查判定（InProgress + 冷轧拔 + 未完工 + 工序组匹配）。与 OrderOverview 对齐关键：同工序组时若 CurrentSectionName=null 或冷轧拔已完成则跳过。总量使用 `_allItems.Sum(r => r.WeightTotal)` 前端直接从列表行总计计算。
- **ColdRollPlan 简化视图切换** — MudSwitch 切换明细/简化视图，`BuildSimplifiedView()` 按 (ProcessType, ShortDisplay, IsFinished) 聚合所有重量字段。`GetGroupHeaders()` 使用 `_visibleColumns` 实例方法，视图切换时标题栏宽度自动匹配列宽。
- **ColdRollPlan 急管列高亮** — `urgent-cell` CSS 类浅红底色深红粗体，`GetCellExtraClass()` 对 WeightProdUrgent/WeightWaitNearUrgent 返回 `urgent-cell` 样式。
- **ColdRollPlan 打印功能** — MudIconButton 打印按钮 + IJSRuntime 调 `window.print()` + `@page { size: landscape }` 横版打印 + `crp-print-hide` 类隐藏工具栏元素。
- **已移除** DistinctTotalWeight 属性、tab-summary 端点、GetTabSummaryAsync 方法（汇总数据全部改为客户端 _allItems.Sum()）

### OrderOverview（订单总览）
- 非 MudTable 布局，纯展示卡片式产能负荷总览。`ProductionOverviewService`（无接口），依赖 `AppDbContext` + `IConfigParameterService`。ApiEndpoint = `api/production-overview`。GET `/overview` 返回 `ApiResponse<ProductionOverviewDto>`。
- 核心逻辑：8 行数据（成品在购、原料待投料、在购荒管、5 个生产工段），根据 WorkOrderExecutionSummary + ProductionBatch 聚合各工段产能负荷，按 7 个时间桶分布。
- 时间桶阈值来自 ConfigParameter "DateBucket" 类别（15/30/45/60/90 天），产能来自 "ProductionCapacity" 类别（如抛光 12 吨/天）。
- 两层判定逻辑：`IsNotReached()` 基于顺序号、`IsNotReachedBySection()` 基于工段名。内部 `record ProcessGroupInfo` 辅助扫描工段。
- DTO: `ProductionOverviewDto`（Rows/DateBuckets/GeneratedTime），每行 `OverviewRowDto`（Seq/Category/Section/InProcurementTons/TotalRemainingTons/EstDays/EstDeadline/DateBucketTons），`DateBucketDto`（StartDate/EndDate/Label）。

### SectionProductionStatus（工段在产状况）
- 按 `(ProcessGroupName, SectionName)` 维度实时汇总批次重量。`SectionProductionStatusService` 实现 `ISectionProductionStatusService`，单方法 `GetStatusAsync()`。ApiEndpoint = `api/section-production-status`。
- 维度来源：所有批次（含已完成，用于推导维度）的 `ProcessGroup.GetNonEmptySections()` 扩展方法。批量查询加 `Where(b => b.Status != Completed)` 过滤，排除已完工批次减少数据量。4 个聚合值：InProduction（生产中）、PendingProduction（待产）、Total、FinalProcessTotal（仅成品工序）。
- 聚合逻辑：字典预聚合（`inProdLookup`/`pendingLookup`）+ O(1) GetValueOrDefault 查找，替代原始嵌套循环 O(dimensions × batches × 4) 模式。
- Blazor 页面：客户端排序 + ExcelFilter 筛选。6 列，无服务端分页，全量获取后内存筛选。通过 `PageState.LoadAsync("section-production-status")` 恢复状态。ExcelFilter 支持 `__NOT_NULL__` 和 `__EXCEL_FILTER_NULL__` 特殊值。

### SectionFlowAnalysis（工段流量分析）
- 按段落类别计算可持续天数 + 变异量 + 状态判定。`SectionFlowAnalysisService` 实现 `ISectionFlowAnalysisService`（8 方法），依赖 `AppDbContext` + `ISectionProductionStatusService`（组合模式）。ApiEndpoint = `api/section-flow-analysis`。
- 分析逻辑：委托 `ISectionProductionStatusService` 获取状态数据，按 `SectionFlowCategorySetting` + `SectionFlowCategoryItem`（N:1）数据库驱动类别映射重新聚合。变异量 = Coefficient × baseAmount，可持续天数 = VariationTotal / DailyProductionTarget。通过与 LowerLimitDays/UpperLimitDays 比较判定状态（偏少/正常/过多）。
- 类别代码变体："K" = 半成品总计 = Total - FinalProcessTotal；"L" = 成品总计 = FinalProcessTotal；其他 = Total。
- 管理端点（需 Director.WorkOrder 或 Admin）：PUT setting（快速更新 3 阈值）、GET settings（全部设置+明细）、PUT settings、POST settings/{id}/items、PUT/DELETE items/{id}。设置是完整 CRUD 子页面。
- Blazor 页面：客户端排序 + 筛选。4 列（Category/PendingTotal/SustainableDays/StatusJudgment）。通过 `ColumnPrefs.LoadAsync("section-flow-analysis")` 恢复列定制 + `PageState` 恢复排序/搜索。Category 和 StatusJudgment 标记 `IsRequired=true`。
- 实体：`SectionFlowCategorySetting`（CategoryCode/CategoryName/DailyProductionTarget/LowerLimitDays/UpperLimitDays/Remark，1:N Items），`SectionFlowCategoryItem`（SettingId/ProcessGroupName/SectionName/Coefficient/DisplayOrder）。

### WorkOrderSchedules（工单排程）
- 标准服务端分页列表页，WorkOrderExecutionSummary LEFT JOIN OrderDemandAdjustment。`WorkOrderScheduleService` 实现 `IWorkOrderScheduleService`，方法 `GetPagedAsync(query)` + `GetFilterContextsAsync()`。ApiEndpoint = `api/workorder-schedule`。
- 筛选逻辑：ScheduleStage==2 或（ScheduleStage==1 && IsUrging && IsBatchDelivery）。keyword 跨 16 列搜索。filters JSON 反序列化为 `List<FilterDescriptor>` 通过 `.ApplyFilters()` 扩展方法（泛型反射）。排序通过 `.ApplySort()` 扩展方法。pageSize > 5000 上限防护。
- 列分组：G1（基础：工单标识/销售/合同/规格/重量共 20 列）+ G7（有效流转：FlowOutputRatio/FlowStatus 等 6 列）+ G12（实时关注：ScheduleStage/UrgencyLevel/剩余天数等 8 列）+ G13（催货标记：IsUrging/IsBatchDelivery/IsPaused/AdjustmentRemark）+ G14（待经工序：PendingSection 8 列 + ProductionAttentionProcess）。14 个 B33 可汇总列。
- EnumOptions 列：SettlementMethod/DeliveryState/MaterialName/LengthStatus/FlowStatus 硬编码枚举映射。
- WorkOrderSchedule 物化表实体（继承 BaseEntity）：字段与 DTO 一致。通过"计划安排"按钮从 WorkOrderExecutionSummary 全量刷新。筛选规则：块1 = ScheduleStage==2，块2 = IsMainNoMaterialComplete，块3 = ScheduleStage==1 + IsUrging + IsBatchDelivery。`WorkOrderId` 唯一。

## 当前规范版本
- **04_开发规范.md V8.42**（2026-06-06）
- **mes-code-check SKILL.md V8.42**（2026-06-06）

## 分页汇总行规范（§6.4.4）— B18
- 涉及支数/米数/重量/批次数的列表页，必须在 MudTable FooterContent 中添加分页汇总行
- `_summableColumnKeys` 静态 HashSet 定义所有可汇总列 Key
- `ComputePageSums()` 在 LoadDataFromServer 加载 _pageItems 后调用，反射读取类型求和
- int 类型直接 `sum.ToString()`，decimal 类型用 `((int)sum).ToString()`（§10.7 整数显示）
- 样式：`col-footer-cell`（顶部 2px 分隔线 + #FAFAFA 背景）+ `col-footer-sum`（#1565C0 蓝色字体）

## 列表页整数显示规则（§10.7）— B19
- 支数/米数/重量/批次数四类列在列表页中必须显示为整数值
- decimal 属性用 `((int)val).ToString()`，int 属性用 `val.ToString()`
- 百分比/比率列不受此约束，使用 §10.8 规则
- 编辑/详情页保留原始精度

## 列表页百分比显示规则（§10.8）— B20
- 所有百分比/比率列在列表页中必须使用 `ToString("F1")` 显示 1 位小数
- 仅适用于列表页，编辑/详情页保留原始精度

## 排序筛选验证规范（§6.4.8）— 7 步清单
- Step 2 三角验证：对每一列检查 FilterType + 后端 GetFilterContextsAsync Dictionary + 后端 ApplySorting

## 页面状态持久化规范（§6.25）
- OnInitializedAsync 中状态恢复后必须调用 `if (savedState != null && table != null) await table.ReloadServerData();`

## MudSwitch 内联编辑规范（§6 补充）— B21
- Blazor 中 MudSwitch<bool> 使用 `Value` + `ValueChanged` 做内联编辑时，EventCallback 必须用 `async v => await` 模式，禁止 `_ = Handler(item, v)` fire-and-forget
- 原因：fire-and-forget 导致 EventCallback 立即返回 Task.CompletedTask，MudSwitch 在 `OnParametersSet` 中检测到 `Value(旧值) != _value(新值)` 立即弹回，API 成功也不会触发重新渲染
- 正确模式：
  ```csharp
  builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
  {
      await Handler(item, v);
  }));
  ```
- 这样 MudSwitch 会等待 API 返回，API 成功则保持新状态，API 失败则自动弹回

## 分页行数持久化规范（§6.26）— B22
- ServerData 模式 MudTable 的 `RowsPerPage="@_pageSize"` 是单向绑定，排序/筛选后 `_pageSize` 未同步导致行数复位
- 修复：`LoadDataFromServer(TableState state)` 首行必须添加 `_pageSize = state.PageSize;`
- 要求：全项目所有 ServerData 列表页必须遵循此规范（包括必须声明 `_pageSize` 字段 + Razor 绑定 `RowsPerPage="@_pageSize"` + LoadDataFromServer 首行持久化）
- 截至 V8.37 已修复全项目 34 个列表页，覆盖 Scheduling/Orders/WorkOrders/Batches/Quality/Equipment/Materials/Warehouse/Configuration 所有模块

## PagedResult.Extras 模式（新增于批次看板）
- 用于返回分页数据之外的附加信息（如 Tab 汇总等）
- Service 层分页查询后在分页前计算聚合数据，通过 `PagedResult<T>.Extras` Dictionary 返回
- 前端在 LoadDataFromServer 中读取 `result.Extras` 并赋值到对应字段

## 客户端排序模式（新增于批次看板）
- 计算字段（[JsonIgnore] 不参与 SQL 查询）无法后端排序
- 定义 `_clientSortableKeys` HashSet 标识哪些排序键需要客户端执行
- `ApplyClientSideSort()` 在 LoadDataFromServer 加载 _pageItems 后调用，根据 sortColumn/sortDescending 对内存数据排序

## 枚举筛选中文显示模式
- 后端 GetFilterContextsAsync 返回的是原始数据库值（非中文）
- 前端 BuildFilterContextOptions 中对枚举列通过 EnumOptions 映射中文显示文本
- 映射代码：`col.EnumOptions!.ToDictionary(e => e.Value, e => e.Display)`

## Scheduling 模块整体架构
- **8 个 Blazor 页面**：OrderOverview / SectionProductionStatus / SectionFlowAnalysis / WorkOrderSchedules / RawMaterialLockPlanAndExecution / BatchPlans / ColdRollPlans / OrderDemandAdjustment（旧称 SalesUrging，位于 Orders 目录）
- **核心数据源**：`WorkOrderExecutionSummary`（ScheduleStage 1/2+），各页面通过 LEFT JOIN 或 ProductionBatch 驱动查询
- **物化表**：WorkOrderSchedule（ScheduleStage=2 全量刷新）、RawMaterialLockPlanAndExecution（ScheduleStage=1 全量刷新）。OrderDemandAdjustment 为薄表仅存手工标记
- **实时查询**：RawMaterialLockPlanAndExecution（LEFT JOIN 非物化）、BatchPlans/ColdRollPlans/OrderDemandAdjustment/SectionProductionStatus 均基于 ProductionBatch 实时聚合
- **组合模式**：SectionFlowAnalysisService 委托 SectionProductionStatusService 获取数据后重映射
- **ExcelFilter 系统**：WorkOrderSchedules/SectionProductionStatus 使用，GetFilterContextsAsync 返回字典 + 前端 _columnFilters + PageState 持久化。特殊值 `__NOT_NULL__` / `__EXCEL_FILTER_NULL__`
- **EnumOptions 硬编码**：WorkOrderSchedules 对小型稳定枚举列直接内嵌映射
- **服务端筛选扩展**：`.ApplyFilters()` 和 `.ApplySort()` 泛型反射扩展方法，filters JSON 序列化为 `List<FilterDescriptor>`