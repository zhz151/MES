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

## 远程数据库问题记录
- **远程服务器配置**：2 核 CPU、2 GB 内存，SQL Server 2022
- **冷缓存问题**：SQL Server 重启后缓冲池为空，首次查询需读磁盘，`Any()` 查询从 <100ms 变 5-10 秒
- **CommandTimeout 修复**：DbContext 配置 `o.CommandTimeout(120)`，将默认 30 秒超时改为 120 秒，避免 RefreshAll 全表扫描超时
- **远程病毒发现**：`csrss.exe` (PID 2052) 被注入恶意代码，对外发数百个 SQL 1433 端口 SYN_SENT 请求，导致 CPU/内存 90%
- **解决方案**：数据库迁移到本地 SQL Server，连接字符串改为 `Server=localhost;Integrated Security=true`

## 数据库备份/恢复经验
- **不要直接复制运行中的 MDF/LDF**：即使停掉 SQL Server 后复制，仍可能因文件头损坏导致 5171 错误
- **备份恢复更可靠**：用 `BACKUP DATABASE ... WITH FORMAT` + `RESTORE DATABASE ... WITH MOVE, REPLACE`
- **备份路径权限**：SQL Server 默认无 C 盘根目录写入权限，应写 `MSSQL\Backup` 目录
- **版本匹配**：附加/恢复 MDF 要求源和目标 SQL Server 版本一致（上同版本=SQL Server 2022 16.0.1000.6 可交叉）

## Scheduling 模块 — 9 个页面

### BatchPlan（批次看板）— 冷轧排程小表关联
- **3 层冷轧维度推导**：从 `ProductionBatch.ProcessGroups`（导航属性，含全部工序组）按 SequenceNumber 排序后，基于 PendingProcess 的索引位置推导 3 层：
  - 本层（`CurrentCR_*`）：PendingProcess 对应的工序组
  - 下层（`NextCR_*`）：pendingIdx + 1
  - 下下层（`NextNextCR_*`）：pendingIdx + 2
- 每层 4 个维度字段：`ProcessType/BilletSpec/RollingSpec/IsFinished`，仅在 `ProcessNames.IsColdRollOrDraw()` 为 true 时填入
- BilletSpec 推导：本层 = 前一组 ManufacturingSpec，下层 = 本组 ManufacturingSpec，下下层 = 次组 ManufacturingSpec
- 匹配键：`(ProcessType, BilletSpec, RollingSpec, IsFinished)` → `ColdRollSpecSchedule` 四维唯一键

**匹配逻辑（BatchPlanService，分页查询后内存匹配）：**
- **在轧要求**（`CR_CompletionType`）：本层冷轧 + `PendingEquipment` 不为空 → 匹配本层维度 → `CompletionType`
- **待轧要求**（`CR_RollType/CR_RollOrder/CR_SchedMachineNo`）— `else if` 链，互斥：
  - 场景1：本层冷轧 + `PendingEquipment` 为空 → 匹配本层维度
  - 场景2（else if）：下层冷轧 + `PendingEquipment` 为空 → 匹配下层维度
  - 场景3（else if）：下下层冷轧 + `PendingEquipment` 为空 → 匹配下下层维度
- 3 层扩展至 `BatchPlanDto` 共 16 个 G5 字段（12 维度 + 4 匹配结果），`[JsonIgnore] BatchId` 用于 ProcessGroups 查询关联
- G5 列分组标题栏：GroupKey 5（本层维度）/ 6（下层维度）/ 7（本层匹配）/ 8（下层匹配）/ 9（下下层维度）
- `ColdRollPlanService` 和 `ColdRollSpecScheduleService` 必须注入 `AuthHttpClient`（带 JWT Token），非裸 `HttpClient`

### ColdRollPlan（冷轧看板）— ColdRollSpecSchedule 小表保存
- 全量同步模式：前端加载全部排程记录 → 用户编辑 → `SaveAllAsync(List<ColdRollSpecScheduleDto>)` 全量保存
- **数据保留机制**：`SaveScheduleAsync()` 在保存前先调用 `GetAllAsync()` 加载全部现有排程记录，将未在当前编辑范围内的记录一并合并发送。防止 Tab 筛选状态下保存导致其他维度数据丢失。
- `SaveAllAsync` 服务端逻辑：加载全部 DB 记录，按 4 维键匹配，更新/新增 + 删除僵尸数据。但前端已在保存前合并了全部现有记录，因此不会触发僵尸数据删除。

### ColdRollSpecSchedule 实体
- `ColdRollSpecSchedule`：继承 `BaseEntity`，`RollOrder` int 默认 0（DB 默认值 0，由 migration `FixRollOrderDefault` 修正）
- 唯一索引：`(ProcessType, BilletSpec, RollingSpec, IsFinished)`

### FinalInspectionKanban（成检看板）— 新增
- 内存全量加载 + 客户端分页/排序/筛选的看板页面。`FinalInspectionKanbanService` 实现 `IFinalInspectionKanbanService`，调用 `api/final-inspection-kanban` 端点获取全量数据。ApiEndpoint = `api/final-inspection-kanban`。
- DTO: `FinalInspectionKanbanDto`（G1 批次信息: BatchNo/TagNo/PlantGrade/CurrentValidWeight，G2 关联工单: WorkOrderNo/Salesman/Specification/LengthStatus/MinLength/MaxLength，G3 排程信息: ScheduleStage/UrgencyLevel，G4 成检状态: KanbanStage/ReceiveDate/MaxInspectionDate）。
- 三档 Tab 筛选（全部/待到料/待检验/检验中）+ Tab 汇总（批次数/总重量）。全量数据 `_allItems` + `BuildFilterOptionsFromData()` 内存驱动筛选上下文。
- **MudChip 样式渲染** — UrgencyLevel 中 "A+急"/"A急" 显示 Color.Error，"B顺" 显示 Color.Warning；ScheduleStage 中 "成品检验" 显示 Color.Primary，"生产执行" 显示 Color.Info，"工单完成" 显示 Color.Default。
- 列分组 G1-G4 + `initGroupHeaders` JS 函数 + `OnAfterRenderAsync` 对齐组标题。ComputePageSums B33 汇总（仅 CurrentValidWeight）。B22 分页行数持久化。
- GroupHeaderInfo 模型计算分组宽度。`BuildFilterOptionsFromData()` 处理 enum/string 两种 FilterType。

### RawMaterialLockPlanAndExecution
- 原锁计划及执行页面（LEFT JOIN 实时查询模式）。G1-G12 来自 `WorkOrderExecutionSummary`（ScheduleStage=1），G13 LEFT JOIN `OrderDemandAdjustment`（IsUrging/IsBatchDelivery/IsPaused/AdjustmentRemark 四个字段），G15 LEFT JOIN `RawMaterialLockPreExecution` 独立小表。ApiEndpoint = `api/raw-material-lock-plan`。无独立物化表，无"计划安排"按钮。G15 字段：IsPreInput, BudgetInputDate, IsMainNoMaterialComplete（系统计算）
- **待检验到料批次卡片** — 页面顶部 MudExpansionPanels 包裹的 MudExpansionPanel（默认折叠），显示待检验批次的数量和总重量。数据通过 `GetPendingMaterialChecksAsync()` 获取 `PendingMaterialCheckDto` 列表。

### BatchPlan（批次看板）— 其他
- 在产明细计划实时视图。`ProductionBatch`（Status=None/InProgress）LEFT JOIN `WorkOrderExecutionSummary`（UrgencyLevel/ScheduleStage） + `WorkOrderSchedule`（ProductionAttentionProcess）。ApiEndpoint = `api/batch-plan`。17 个工段筛选 Tab（含冷轧类工序+工段逻辑、过程/成品检验 SequenceNumber 比较）。IsKeyBatch 逻辑：ScheduleStage==2 && Urgency in ["A+急","A急"] && (PendingProcess=="荒管处理" || PendingProcess==ProductionAttentionProcess || ProductionAttentionProcess=="收尾-成检")。Tab 汇总通过 PagedResult.Extras 字典返回。计算字段客户端排序（`_clientSortableKeys` HashSet 定义）。枚举筛选通过 EnumOptions 映射中文显示。列分组标题栏 G2→G4→G1→G3→G5 排列。

### ColdRollPlan（冷轧看板）— 查看/排程
- 按规格维度聚合的冷轧时间桶分布看板。`ProductionBatch`（Status=None/InProgress，Select 投影仅加载需要的字段）+ ProcessGroups 投影（仅加载 SequenceNumber/ProcessName/ManufacturingSpec + 15 个工段 int? 字段）+ WorkOrderExecutionSummary + WorkOrderSchedule（后两者仅加载关联工单的数据）。ApiEndpoint = `api/cold-roll-plan`。核心逻辑：所有冷轧工序组（IsColdRollOrDraw）均生成行，通过 section seq 差(diff) 映射到各时间桶。近日在轧通过直接状态检查判定（InProgress + 冷轧拔 + 未完工 + 工序组匹配）。与 OrderOverview 对齐关键：同工序组时若 CurrentSectionName=null 或冷轧拔已完成则跳过。总量使用 `_allItems.Sum(r => r.WeightTotal)` 前端直接从列表行总计计算。
- ColdRollPlan 简化视图切换、急管列高亮、打印功能、排程编辑模式（全量同步模式）、搜索栏恢复、ExcelFilter 列筛选、ColumnDisplaySelect 列显隐、MudTablePager 分页、B33 分页汇总

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
- **04_开发规范.md V8.43**（2026-06-06）
- **mes-code-check SKILL.md V8.43**（2026-06-06）

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

## PagedResult.Extras 模式
- 用于返回分页数据之外的附加信息（如 Tab 汇总等）
- Service 层分页查询后在分页前计算聚合数据，通过 `PagedResult<T>.Extras` Dictionary 返回
- 前端在 LoadDataFromServer 中读取 `result.Extras` 并赋值到对应字段

## 客户端排序模式
- 计算字段（[JsonIgnore] 不参与 SQL 查询）无法后端排序
- 定义 `_clientSortableKeys` HashSet 标识哪些排序键需要客户端执行
- `ApplyClientSideSort()` 在 LoadDataFromServer 加载 _pageItems 后调用，根据 sortColumn/sortDescending 对内存数据排序

## 枚举筛选中文显示模式
- 后端 GetFilterContextsAsync 返回的是原始数据库值（非中文）
- 前端 BuildFilterContextOptions 中对枚举列通过 EnumOptions 映射中文显示文本
- 映射代码：`col.EnumOptions!.ToDictionary(e => e.Value, e => e.Display)`

## Scheduling 模块整体架构
- **9 个 Blazor 页面**：OrderOverview / SectionProductionStatus / SectionFlowAnalysis / WorkOrderSchedules / RawMaterialLockPlanAndExecution / BatchPlans / ColdRollPlans / FinalInspectionKanban / OrderDemandAdjustment（位于 Orders 目录）
- **核心数据源**：`WorkOrderExecutionSummary`（ScheduleStage 1/2+），各页面通过 LEFT JOIN 或 ProductionBatch 驱动查询
- **物化表**：WorkOrderSchedule（ScheduleStage=2 全量刷新）、RawMaterialLockPlanAndExecution（ScheduleStage=1 全量刷新）。OrderDemandAdjustment 为薄表仅存手工标记
- **实时查询**：RawMaterialLockPlanAndExecution（LEFT JOIN 非物化）、BatchPlans/ColdRollPlans/OrderDemandAdjustment/SectionProductionStatus/FinalInspectionKanban 均基于 ProductionBatch 实时聚合
- **组合模式**：SectionFlowAnalysisService 委托 SectionProductionStatusService 获取数据后重映射
- **全量加载模式**：FinalInspectionKanban 全量加载后客户端分页/排序/筛选（`_allItems` 缓存 + `BuildFilterOptionsFromData` 内存驱动筛选上下文）
- **ExcelFilter 系统**：WorkOrderSchedules/SectionProductionStatus/FinalInspectionKanban 使用，GetFilterContextsAsync 返回字典 + 前端 _columnFilters + PageState 持久化。特殊值 `__NOT_NULL__` / `__EXCEL_FILTER_NULL__`
- **EnumOptions 硬编码**：WorkOrderSchedules/FinalInspectionKanban 对小型稳定枚举列直接内嵌映射
- **服务端筛选扩展**：`.ApplyFilters()` 和 `.ApplySort()` 泛型反射扩展方法，filters JSON 序列化为 `List<FilterDescriptor>`

## Quality 模块 — 质量检验

### 整体架构
- **13 个 Blazor 页面**：ChemicalCompositions / ChemicalCompositionCreate / ChemicalValidationRules / ChemicalValidationRuleCreate / FurnaceRegistrations / FurnaceRegistrationCreate / FinalInspections / FinalInspectionCreate / ProcessInspections / ProcessInspectionCreate / MaterialChecks / MaterialCheckCreate / QualityProcessTracking
- **实体**（`MES.Data.Entities`）：ChemicalComposition（牌号化学成分）、ChemicalValidationRule（牌号验证）、FurnaceRegistration（来料炉号登记）、FinalInspection（成品检验）、ProcessInspection（过程检验）、MaterialReceiveCheck（成检到料）、InspectionRecord（点检记录，跨 Equipment/Quality）
- **Service**（`MES.Services.Quality`）：ChemicalCompositionService / ChemicalValidationRuleService / FurnaceRegistrationService / FinalInspectionService / ProcessInspectionService / QualityProcessTrackingService
- **Controller**（`MES.Api.Controllers.Quality`）：7 个，路由 `api/化学-composition`、`api/final-inspection` 等。均 `[Authorize]` + Roles.Staffs.Quality / Roles.Directors.Quality / Roles.Admin
- **ApiEndpoints 常量**：ChemicalComposition / ChemicalValidationRule / FinalInspection / FurnaceRegistration / ProcessInspection / QualityProcessTracking
- **标准的服务端分页列表页**：ChemicalCompositions / ChemicalValidationRules / FurnaceRegistrations / FinalInspections / ProcessInspections — 均实现 GetFilterContextsAsync + GetPagedAsync + B22 分页行数持久化 + ComputePageSums B33 汇总
- **创建页**：每个列表对应一个独立创建页（`/xxx/create`），URL 路径区分
- **只读追踪页**：QualityProcessTracking（仅有 GetPagedAsync + GetFilterContextsAsync，无写操作），聚合 MaterialReceiveCheck + FinalInspection + Warehouse 数据

### MaterialChecks（检验到料）
- 检验到料管理页，ApiEndpoint 由 `MES.Blazor.Services.ProductionRecordService` 提供。数据源 `MaterialReceiveCheck` 实体。Service 方法在 `MES.Services.Batch.ProductionRecordService` 中（非 Quality Service 目录）。
- **DTO**：`MaterialReceiveCheckDto`、`UpdateMaterialReceiveCheckRequest`（ReceiveDate/Shift/Checker/Remark/IsForceCompleted）、`PendingMaterialCheckDto`
- **列定义**：17 列（ReceiveDate/BatchNo/ManufacturingItem/PlantGrade/Specification/TagNo/WorkOrderNo/SalesOrderNo/FurnaceNo/SourceUnit/ProductionType/ProductionCutQuantity/DataSource/Shift/Checker/IsForceCompleted/Remark/CreatedTime/UpdatedTime）。列分组 G1-G4。`IsApplicable` 属性控制条件显示。
- **内联编辑**：ReceiveDate（MudTextField yyyy-MM-dd）/Shift/Checker/Remark 四字段，双击行启动编辑（`_editingIds` 跟踪），EditCache 备份旧值用于取消恢复。`SaveEdit` 调用 `UpdateMaterialReceiveCheckAsync` 保存。
- **IsForceCompleted MudSwitch 内联编辑** — 直接 RenderCell 中 MudSwitch，`CheckedChanged` 回调中调用 `UpdateMaterialReceiveCheckAsync` 仅传 `IsForceCompleted` 参数。**ReceiveDate 防覆盖**：Service 层 `if (request.ReceiveDate != default)` 判断跳过默认值赋值，防止切换强制完成时到料日期被重置为 `0001-01-01`。
- **DataSource 枚举显示** — RenderCell 中 `switch` 映射 "SCAN"→"扫码"、"MANUAL"→"手动"。列 FilterType="enum"，EnumOptions 关联中文显示。
- **筛选上下文**：`GetMaterialCheckFilterContextsAsync` 返回字典（含 ReceiveDate 格式化为 `yyyy-MM-dd`）。前端 `BuildFilterContextOptions` 中枚举列通过 `col.EnumOptions!.ToDictionary(e => e.Value, e => e.Display)` 映射中文显示；布尔列自动补充 True/False 选项；后端未返回的枚举列自动从 EnumOptions 补充。
- **待检验到料卡片**：页面顶部显示待检验批次概览，通过 `GetPendingMaterialChecksAsync()` 获取 `PendingMaterialCheckDto` 列表。`_showPending` 控制展开/折叠。
- **B22 分页行数持久化**：`_pageSize = state.PageSize` 在 LoadDataFromServer 首行。
- **箭头导航**：`enableTableArrowNav` JS 函数支持键盘上下键导航。

### 关键枚举
- **InspectionItem** — PMIInspection / VisualInspection / Dimension / Endoscopy / HydrostaticPressure / UnderwaterPneumatic / EddyCurrent / Ultrasonic / PortColoring
- **DataSource** — SCAN / MANUAL（跨 FinalInspection / ProcessInspection / MaterialReceiveCheck）
- **ManufacturingItem** — OrderFinishedProduct / PreparedMaterial / SurplusStock / IntermediateProduct / SpecialDeliveryStatus
- **ProductionType** — RoughTube / InProcess / Inventory / OutsourcedPurchased / Rework / Subcontract / ExternalProcessing

## Equipment 模块 — 维修工单
- **RepairOrder 实体**（继承 `BaseEntity`）：字段包含 RepairOrderNo(WX-YYYYMMDD-XXX)/EquipmentId/FaultDescription/FaultType/Priority/RepairStatus(自动推导)/ReportPerson/ReportTime/RepairPerson(逗号分隔多人)/RepairCategory(厂内维修/外协维修/换模)/RepairStartTime/RepairEndTime/RepairContent/SparePartUsed
- **RepairCategory**：三类 — 厂内维修（Default灰色）/ 外协维修（Warning橙色）/ 换模（Primary蓝色），完成维修时设置
- **维修执行**（扫码维修 `/repair-execute`）：两步流程。开始维修 `PUT /api/repair-order/{id}/start` 设置 RepairPerson+RepairStartTime(→InProgress)；完成维修 `PUT /api/repair-order/{id}/complete` 设置 RepairCategory+RepairContent+SparePartUsed+OtherRepairPersons(→Completed)。待处理工单查询 `GET /api/repair-order/by-equipment/{equipmentId}`
- **多人协作**：OtherRepairPersons 追加到 RepairPerson（逗号分隔，自动去重）
- **列表页**（`/repair-orders`）：服务端分页 + 列显隐 + ExcelFilter + 内联编辑 + 批量打印
- **扫码报修**（`/equipment-repair`）：3 步流程（Scan→Form→Success），BarcodeDetector+jsQR 双引擎
- **状态推导**：endTime!=null→Completed, startTime!=null→InProgress, 都为空→Pending
- **导航入口**：MainLayout 中"扫码维修"按钮位于"扫码报修"之前；MobileLayout 同理

## 自我约束规则
- **严格任务边界**：只做用户明确说的任务，多说一个字都先问。看到"顺手能修"的问题必须先问"要不要顺便修"，不能直接动手。
- **禁止跑偏**：在执行当前任务过程中发现的其他问题，记录在案等当前任务完成后再问用户，而不是中途切换方向。
- **单一任务原则**：一次只做一件事。更新文档就只更新文档，不夹带修代码、改样式等其他变更。
