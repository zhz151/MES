# MES 项目持久知识

## 当前规范版本
- **04_开发规范.md V8.28**（2026-05-25）
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
