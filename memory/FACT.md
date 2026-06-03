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
- **RawMaterialLockPlanAndExecution** — 原锁计划及执行页面（Scheduling）。实体在 `MES.Data.Entities.Scheduling`，Service 在 `MES.Services.Scheduling`，Controller 在 `MES.Api.Controllers.Scheduling`，Blazor 页面在 `MES.Blazor.Pages.Scheduling`。ApiEndpoint = `api/raw-material-lock-plan`

## 当前规范版本
- **04_开发规范.md V8.37**（2026-06-03）
- **mes-code-check SKILL.md V8.37**（2026-06-03）

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