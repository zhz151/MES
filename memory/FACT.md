# MES 项目持久知识

## 当前规范版本
- **04_开发规范.md V8.30**（2026-05-28）
- **mes-code-check SKILL.md** 已同步至 V8.28

## 页面状态持久化规范（2026-05-25 更新）
- OnInitializedAsync 中状态恢复后必须调用 `if (savedState != null && table != null) await table.ReloadServerData();`
- 原因：Blazor WASM 首次渲染时 MudTable 的 ServerData 可能已用默认值加载，状态恢复后需重新加载
- 该规则已同步到规范 §6.25.6、附录 A20、§18.4

## OnParametersSetAsync 注意事项
- 带路由参数（`{Code?}`）的页面，OnParametersSetAsync 会在 OnInitializedAsync 之后立即触发
- ResolveWarehouse 等清理方法中不能用 `_isFirstLoad` 做守卫，因为 LoadDataFromServer 会过早将其设为 false
- 正确做法：用 `_lastResolvedWarehouseCode` 字段跟踪上次解析的仓库代码，只在真正切换仓库时才清空筛选状态
- OnParametersSetAsync 中只在仓库代码变化时才调用 ReloadServerData，避免冗余加载覆盖 OnInitializedAsync 恢复的状态

## compact-table 列宽标准（V8.27 更新）
- `table-layout: fixed` + `width: max-content` 确保列宽严格
- 数值列 80px，文本/日期/选择列 120px，项次 45px，复选框/操作 60px
- 编辑宽表（≥20列）必须使用 compact-table + MudTh 显式列宽
- 列表页使用 auto-table（标配），编辑宽表叠加 compact-table

## 列渲染顺序
- `@foreach (var col in _visibleColumns)` 动态迭代
- 硬编码 `@if` 块导致列顺序移动无效
- RenderFragment builder 中 OnClick 必须用 `EventCallback.Factory.Create<MouseEventArgs?>`

## 工序组模板 + 用料计划工序组（2026-05-28 完成）
- ProcessGroupTemplate 独立模板实体（结构与 ProcessGroup 一致，去掉 ProductionBatchId）
- 4个子实体：SemiPlanProcessGroup / FinishedPlanProcessGroup / InventoryPlanProcessGroup / PiercingPlanProcessGroup
- 每个子实体 FK 指向各自的父表，级联删除，Unique index on (ParentFK, SequenceNumber)
- 4个子实体共享 MaterialPlanProcessGroupDto，通过 ParentPlanId 区分
- 调取即复制（快照，非活引用）：ApplyTemplateAsync 事务包裹，从模板库复制到子表
- planType 映射：1=PurchaseSemiPlan, 2=PurchaseFinishedPlan, 3=InventoryPlan, 4=RoundBarPiercingPlan
- 模板编辑页：ProcessName 使用 MudSelect（荒管处理/在制修检/冷轧/冷拔），15个工段使用 MudNumericField int?
- 用料计划页每个 Tab 操作列新增"工序组"按钮（AccountTree 图标），弹出 ProcessGroupDialog
- ProcessGroupDialog 展示工序组列表 + "从模板调取"按钮打开 TemplateSelectionDialog 多选
- MES.Services 项目有 122 个预存编译错误（与本次修改无关，master 分支就有）
- 迁移 SQL 手动编写（migrationBuilder.CreateTable）

## 工单执行状况读模型 G12 扩展（V4.1 2026-05-28）
- G2 新增 ProcessCycle（工艺周期）：4种用料计划 StandardCycle 最大值，无计划默认25
- G7 新增 FlowMaxRemainingWorkDays（最大剩余工量）：关联批次 RemainingWorkDays 最大值
- G12 新增 TotalRemainingWorkDays（剩余总工量）：ScheduleStage=1→主号max ProcessCycle+3, =2→主号max FlowMaxRemainingWorkDays, =3→3
- G12 新增 UrgencyLevel（工单计划性）：A+急(>67)/A急(>60)/B顺(>53)/C缓(>45)/D缓(≤45)
- G12 新增 EstimatedProcessCompletionDate（工艺预计完成日）：Today + TotalRemainingWorkDays
- G12 新增 DaysDiffFromDelivery（交期相差天数）：EstimatedProcessCompletionDate - DeliveryDate
- ScheduleStage=0（无需排产）时所有G12新字段均为 null
- 以上共涉及6次迁移，以最后一次迁移为准
- 文档已全部更新

## 原锁备注字段 RawMaterialLockRemark（2026-05-29 新增）
- WorkOrderExecutionSummary.G12 新增 RawMaterialLockRemark(nvarchar(20))，仅 ScheduleStage=1 时有值
- 判定逻辑（优先级 A>B>C>D）：
  - A质量影响：MainNoInputStatus=2 AND MainNoFlowStatus≠2
  - B已购未回：G5理论成品按主号聚合比值 + MainNoFlowOutputRatio ≥ 100%
  - C计划未执行：MainNoMaterialPlanStatus=3或4
  - D未完善计划：MainNoMaterialPlanStatus=0或1
- 迁移：20260529024024_AddRawMaterialLockRemarkToExecutionSummary

## 上下文归属（2026-05-29 确认）
- WorkOrderExecution（工单执行状况）完全归工单上下文
  - 页面：Pages/WorkOrders/WorkOrderExecution.razor
  - 服务：Services/WorkOrder/WorkOrderListSummaryService.cs + WorkOrderStatusSummaryService.cs
  - 控制器：Controllers/WorkOrder/WorkOrderExecutionController.cs
  - 测试：Tests/Services/WorkOrder/（namespace MES.Tests.Services）
  - 读模型 WorkOrderExecutionSummary 实体在 Data/Entities/
- 计划及执行上下文（Scheduling Context）仅含 2 页面
  - SalesUrgings（销售催单）
  - RawMaterialLockPlanAndExecution（原锁计划及执行）
  - 基于 WorkOrderExecutionSummary 读模型做调度决策（非读模型拥有者）
- 导航菜单：
  - 工单管理：工单首页 / 用料计划总览 / 标准工艺生产周期 / 工单执行状况
  - 计划及执行：销售催单 / 原锁计划及执行

## 待办任务
- 工段产能表（后续排期，独立任务）
