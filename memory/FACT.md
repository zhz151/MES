# MES 项目持久知识

## 数据一致性原则（物料/仓库/工单/订单）
### 唯一保证路径：写路径增量更新
- 数据一致性只靠**写路径增量更新**保证，不依赖定时任务或轮询
- 用户操作（翻页/排序/搜索/筛选）自然触发 `table.ReloadServerData()`，读到的是最新数据
- 读模型页面不添加手动刷新按钮，不添加页面轮询
### Hangfire 定时任务（仅一个）
- `cleanup-old-notifications`：每天凌晨2点，删除30天前的通知
- `CheckOrderChangeJob`（已被删除）和 `MaterialSyncJob`（已被删除）本质都是兜底保险，非必须，已移除
- 后续如需重建兜底任务：在 `MES.Api.Services.HangfireJobService` 加方法，在 `Program.cs` 注册 cron
### DTO 投影（§3.3）— 全项目评估结论
- 检查了物料/仓库/工单/订单共 11 个列表 Service，DTO 投影不合规的有 8 个
- **全部为单表或反范式化读模型查询，无跨表字段需要 JOIN**
- 无功能缺陷，维护成本 > 收益，**全项目不改**
### 关键代码位置
- `SyncSourceOrdersAsync`：`MES.Services.Warehouse.InventoryService`（含 ReturnItem 子表同步）
- `TryRefreshExecutionSummaryAsync`：分散在各 Service，fire-and-forget 调用
- `StartPollingAsync`：Blazor 页面代码（PurchaseOrders/SubcontractOrders 每2分钟刷新状态面板 + WorkOrders 每2分钟刷新通知）
- `RefreshAllAsync`（现有但未被 Hangfire 调度的方法）：
  - `WorkOrderListSummaryRefreshService.RefreshAllAsync()` — 全量刷新 WorkOrderListSummary 读模型
  - `WorkOrderExecutionService.RefreshAllAsync()` — 全量刷新 WorkOrderExecutionSummary 读模型
  - `OrderService.RefreshAllAsync()` — 全量刷新 OrderListSummary 读模型

## StandardRegister（标准号）模块
- **实体**：`StandardRegister`（标准号头）、`StandardRegisterItem`（子项目）
- **StandardRegister 字段**：StandardNo(唯一)/Version/StandardName/RefSpecification/StandardLevel/ManufactureMethod/SteelType/Remark
- **StandardRegisterItem 字段**：StandardRegisterId(FK)/SeqNo/InspectionCategory/InspectionItem/IsMandatory/SamplingRequirement/ApplicableRange/RefStandard/DetailRequirement
- **命名空间**：`MES.Services.StandardRegister`（Service）、`MES.Api.Controllers.ProductionStandard`（Controller）
  - 注意：命名空间曾与实体名冲突，最终 Service 从 `MES.Services.ProductionStandard` 改为 `MES.Services.StandardRegister`
- **Blazor 页面**：StandardRegisters（列表页）、StandardRegisterDetail（详情/编辑/创建页）
- **列表页模式**：仿 Orders.razor 模式（ExcelFilter + RenderCell + FooterContent + 分页导航按钮）
  - `RenderFragment RenderCell(T item, ColumnDef col)` 模式渲染单元格
  - `RenderFooterCell(ColumnDef col)` 渲染页脚
  - `NavigateToCreate()` → `/standard-registers/create`，`ViewDetail(id)` → `/standard-registers/{id}`
  - `_columnFilters` + `_filterContextOptions` + `SerializeFilters()` + `LoadFilterContextsAsync()` + `BuildFilterContextOptions()`
  - ExcelFilter 列筛选 + PageState 持久化
- **详情页**：`/standard-registers/create`（创建）和 `/standard-registers/{Id:int}`（查看/编辑）
  - 双模式：创建(Id=0)/查看编辑(Id>0)
  - 查看模式显示文本，编辑模式显示 MudTextField/MudSelect
  - 子项目内联表格（MudTable + 新增/删除行）
  - 保存流程：先保存头(SaveAsync)，再遍历子项(SaveItemAsync)
  - 创建成功后导航回列表，编辑后刷新数据回到查看模式
  - 编辑取消通过 `CopyToEdit` + `CancelEdit` 逻辑恢复备份
  - 基本信息布局：2 行 md 网格（标准号/版本 md="2", 标准名称 md="4", 引用规范/标准级别 md="2", 制造方式/钢类 md="2", 备注 md="8"）
- **Controller 路由**：`api/standard-register`
- **权限**：`Roles.Policies.StandardRead`（列表/查看）、`Roles.Policies.StandardWrite`（编辑/创建）
- **ApiEndpoint**：ApiEndpoints.StandardRegister = "api/standard-register"
- **DataExchange 注册**：StandardRegister（主表，排序1）+ StandardRegisterItem（子表，排序2，FK指向StandardRegister）

## 命名空间冲突解决模式
- 当 Service 命名空间与 Entity 类名冲突时，Controller 和 Service 使用不同于 Entity 的命名空间，或在引用处使用 `Data.Entities.XXX` 完全限定名
- 避免使用 `MES.Services.XXX` 作为命名空间（XXX 与实体同名时），改用 `MES.Services.XXXRegister` 或类似区分

## Blazor 服务端筛选模式（ExcelFilter）
- Blazor Service 必须添加 `GetFilterContextsAsync()` 调用 `{BaseUrl}/filter-contexts` 端点
- 返回 `ApiResponse<Dictionary<string, List<string>>>`
- 前端 `_columnFilters` 字典跟踪 + `_filterContextOptions` 存储选项
- `SerializeFilters()` 将筛选条件序列化为 JSON（通过 PageState 持久化）

## 详情页 MudGrid 布局模式
- 创建/编辑页使用 `MudGrid` + `MudItem` 控制列宽
- `md` 属性控制桌面端行内列数（md=12/每列md值=每行列数）
- `xs` 属性控制移动端宽度
- 字段类别：短文本/下拉框用 md="2"，名称类用 md="4"，备注类用 md="8"
- 查看模式显示 `MudText Typo="Typo.body2"`（标签）+ `MudText Typo="Typo.body1"`（值）
- 编辑模式显示 `MudTextField` 或 `MudSelect` + `Variant="Variant.Outlined"`
- 创建时标准号可编辑，编辑时标准号只读(`Disabled="@(!_isCreateMode)"`)

## MudSelect 中文值绑定模式
- MudSelectItem 的 Value 必须用 `Value="@("中文")"` 表达式（Razor 识别）
- 禁止使用 `Value=""` 表示"不限"（导致 RZ2008 编译错误）
- 示例：`<MudSelectItem Value="@("国标")">国标</MudSelectItem>`
