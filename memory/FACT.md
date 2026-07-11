# MES 项目持久知识

## 订单管理列表页模式（Orders.razor）
- **页面路由**: `/orders`，Controller: `api/order`
- **数据源**: 读模型 `OrderListSummary`（物化表），聚合 SalesOrders + OrderItems + CustomerProfiles + ProductRequirements + WorkOrderExecutionSummary
- **列表模式**: ServerData + ExcelFilter + 列显隐（ColumnDisplaySelect）+ B23 分组列标题栏
- **搜索栏布局**: 3 组各占 1/3（MudGrid md="4"）
  - 组1: 模糊搜索（Immediate + DebounceInterval=500）
  - 组2: 签订日期从/至（flex 等分，Style="flex:1;min-width:0"，Placeholder="yyyy-MM-dd"）
  - 组3: 交货日期从/至（同上 flex 布局，映射后端 DeliveryStart 字段）
- **分组列标题**: `#orders-list-table` div 必须包裹工具栏 + 分组标题栏 + MudTable 三者，JS 的 `initGroupHeaders` 在内部查找 `.col-group-header-scroll`
- **`<MudTh>` 必须加分组 CSS 类**: `GetHeaderGroupCss()` 返回 `col-g1`/`col-g2` 等，否则 JS 无法识别分组边界
- **`<MudTd>` 必须加分组 CSS 类**: `GetCellGroupCss()` 返回 `col-g1-cell`/`col-g2-cell` 等
- **选择列**: `<MudTh Class="col-selection-th">`，`<MudTd Class="col-selection-td">
- **表头 checkbox**: 实时表达式 `Value="@(_pageItems.Count > 0 && _pageItems.All(i => selectedOrderIds.Contains(i.Id)))"`（禁止 field-cache 模式）
- **数值显示**: decimal 字段用 `ToString("G29")` 去零（如 TotalContractWeight）
- **分组结构**: 4 组（①基本信息 ②合同交付 ③订单确认 ④工单执行）

## StandardRegister（标准号）模块
...

## 日期范围搜索规范（§6.30.2）
...

## 文档同步规则
...