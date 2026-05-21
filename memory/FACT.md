# MES 项目持久知识

## 行为规则
- **失败自愈上限**：连续失败 2 次后必须停下来问用户，不得继续自行重试
- **所有业务和代码规则见 `docs/04_开发规范.md`**
- **严禁未经用户同意执行 git 回滚/重置操作**：任何形式的 `git checkout --`、`git reset --hard`、`git revert` 等可能丢失代码的破坏性 git 操作，必须先获得用户明确批准。用户授权一次仅代表本次有效，不构成后续授权。

## 关键陷阱：Items 模式列筛选必须用 GetCellRawValue
- ExcelFilter 内部存储的是 `GetCellRawValue()` 返回的**原始值**（enum 名、bool 字符串、原始数值等）
- `RefreshFilteredData` 中遍历 `_columnFilters` 做 `Where` 比对时，**必须用 `GetCellRawValue(item, col.Key)`**
- **禁止使用 `GetCellDisplayText`**（返回中文/格式化显示文本，与原始值不匹配导致筛选无效）
- 联动筛选 `_filterContexts` 的比对同样必须用 `GetCellRawValue`，禁止用 `GetCellDisplayText`
- 此规则已在 `docs/04_开发规范.md` 的 §9.4（禁止事项）、§18.3（工作流/强制检查）、B13 检查清单中明确写入

## 修改页面 checklist（防止遗漏）
修改 Blazor 列表页时，必须逐项核对以下内容是否被影响：
1. **持久化**：任何影响数据展示的用户交互状态（列筛选、排序、关键字、状态下拉、列显隐顺序），必须在 `SavePageStateAsync` 中保存、在 `OnAfterRenderAsync` 中恢复。新增交互状态后要同时更新持久化。
2. **回调持久化**：`OnColumnFilterChanged`/`OnExcelSortRequested`/`OnSearchChanged`/`ToggleSort` 等交互回调**必须调用 `await SavePageStateAsync()`**，缺少任何一个就是 bug。
3. **全字段排序/筛选**：所有列必须同时定义 `SortKey`（排序）和 `FilterType`（筛选），缺少任何一个就是规范违规。
4. **联动筛选**：Excel 列筛选的上下文（_filterContexts）必须在 `RefreshFilteredData` 中更新，并且在页面模板中传递正确的上下文给每个 ExcelFilter。
5. **Snackbar 合并通知**：多条同类异常必须合并为一条 Snackbar（`string.Join` 汇总+总数），禁止在 `foreach` 循环中逐条调用 `Snackbar.Add`。
6. **模板判空**：Items 模式 Razor 模板中所有 `_allItems` 引用必须判空（`_allItems?.Count ?? 0`），Blazor WASM 首次渲染时数据尚未加载。
7. **GetCellRawValue enum 须 ToString**：`GetCellRawValue` 返回 `string?`，enum 类型属性必须加 `.ToString()`。

## 重要文件路径
- **工单执行状况服务**: `MES.Services/WorkOrderExecutionService.cs`
- **用料计划满足率计算**: `MES.Services/PlanRateCalculator.cs`
- **订单服务**: `MES.Services/Order/OrderService.cs`
- **订单列表读模型服务**: `MES.Services/Order/OrderListSummaryService.cs`
- **订单列表读模型实体**: `MES.Data.Entities/OrderListSummary`
- **订单列表页**: `MES.Blazor/Pages/Orders.razor`

## 关键决策记录

### 读模型 RowVersion 处理（2026-05-21）
- 读模型表 OrderListSummary 有自己的 `rowversion` 列（SQL Server 自动管理）
- `GetPagedAsync` 返回列表 DTO 时，RowVersion 必须来自 **SalesOrder** 表，不是 OrderListSummary
- 读模型的 RowVersion 仅用于读模型自身的并发控制，与业务表无关
- 实现方式：在 `GetPagedAsync` 中 `Select` 之后批量查询 `SalesOrders` 取 RowVersion

### EF Core 关键陷阱
- `IsRowVersion()` 列在 INSERT 时被 EF Core 自动排除，UPDATE 时在 WHERE 中用 original value
- `SetValues` 可能隐式修改 `IsRowVersion` 属性的 current value，应手动逐属性复制
- `rowversion` 列的值由 SQL Server 自动生成，不可显式写入

## 实体关系
- OrderListSummary ↔ SalesOrder：通过 OrderId 关联
- OrderListSummary 是物化读模型，从 SalesOrders + OrderItems + CustomerProfiles + ProductRequirements 聚合计算

### 全站 Items+ExcelFilter 模式迁移完成（2026-05-22）
- 所有 29 个列表页已从 ServerData 模式迁移为 Items+ExcelFilter 模式
- 所有外部筛选器（MudSelect 状态筛选、日期范围筛选）已全部移除，由列头 ExcelFilter 替代
- 搜索框仅保留模糊搜索（MudTextField + 搜索图标），无其他外部筛选组件
