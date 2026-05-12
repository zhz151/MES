# MES项目 - 项目存档

## 页面容器规范（2026-05-11 更新）
- **所有页面统一全宽**：列表页、详情页、创建页均使用 `MudContainer MaxWidth="MaxWidth.False"`
- 列表页额外使用 `Class="mt-4 pl-0"`，详情/创建页使用 `Class="mt-4"`

## 强制工作流
**创建新页面或检查现有页面时，必须逐条对照附录A检查清单（共30+项），不可遗漏任何类别：**
- **通用检查（A01-A20）**：所有页面类型必查
- **列表页（B01-B09）**：仅列表页
- **创建页（C01-C06）**：仅创建页
- **详情/编辑页（D01-D05）**：仅详情编辑页
- **单行表单（E01-E06）**：仅单行表单区块

**每完成一项就在脑中勾掉一项。不允许跳项、漏项。**

## A16 特别检查步骤（A16 是最易遗漏项）
执行 A16 时，按以下步骤精确核查：
1. **逐字段核对**：对 `Status`、`ProductionType`、`InboundSource`、`LengthStatus`、`DeliveryState`、`SettlementMethod`、`MaterialName`、`TechnicalRequirements`、`BatchStatus`、`SectionOutsourceStatus` 这 10 个字段，在页面中搜索出现位置
2. **两处检查**：Razor 模板中的 `@item.Status` / `@item.ProductionType` 等，以及 RenderFragment builder 中的 `builder.AddContent(..., item.Status)` 等
3. **核对方式**：确认每个调用都经过了 `DisplayHelper.GetXxxText()` 而非直接输出。**特别注意 `builder.AddContent()` 模式**（这类违规没有 `.ToString()`，仅凭肉眼识别）

## 附录A 完整检查清单

### A.1 通用检查
- A01: `MudContainer MaxWidth="MaxWidth.False"` 全宽？
- A02: 列表页 `Class="mt-4 pl-0"`，详情/创建页 `Class="mt-4"`？
- A03: `MudNumericField<decimal?>` 有 `Format="G29"` + `HideSpinButtons="true"`？
- A04: `MudNumericField<int?>` 有 `HideSpinButtons="true"`？
- A05: 禁止 `MudDatePicker`（用 `MudTextField<string>` + `DateTime.TryParse`）？
- A06: `DateTimeOffset` 显示用 `.LocalDateTime.ToString(...)`？
- A07: 表单字段无 `Required="true"`/`Immediate="true"`（搜索框例外）？
- A08: 创建/编辑页字段使用 `MudSimpleTable` 表格模式（非 `form-inline-row`）？
- A09: 组件类型来自 §6.14 映射总表？
- A10: 验证用 `Snackbar.Add` 汇总，无 `alert()`？
- A11: 表格/区块有 `Class="auto-table"`？
- A12: 删除/批量操作有 `ConfirmDialog` 确认？
- A13: 提交按钮有 `_isSubmitting` 防重复？
- A14: 必填字段表头红色 `*` 标记（`IsRequired=true` + `<span class="required-star">`）？
- A15: RenderFragment builder 中 `OnClick` 用 `EventCallback.Factory.Create<MouseEventArgs?>`？
- **A16: 枚举字段通过 `DisplayHelper.GetXxxText()` 显示中文 — 检查以下"已知枚举字符串字段"逐处核对：`Status`, `ProductionType`, `InboundSource`, `LengthStatus`, `DeliveryState`, `SettlementMethod`, `MaterialName`, `TechnicalRequirements`, `BatchStatus`, `SectionOutsourceStatus`。注意 RenderFragment builder 中 `AddContent` 直接输出也违规（即使没有 `.ToString()`）**
- **A17: MudAlert 仅用于业务通知（如工单不匹配），禁止用作提示文字/错误信息展示？**
- **A18: 输入框高度统一 32px — CSS 中必须对 `input`/`.mud-input-slot` 设置 `height: 32px !important` + `box-sizing: border-box !important`，确保查看/编辑模式行高一致（38px）？**
- **A19: 页面禁止显示提示性/指导性文字 — 包括但不限于 MudText `Color.Info`/`Color.Warning`/`Color.Secondary` 的操作指引、空状态提示、说明文字（如"点击右上角保存"、"暂未添加"、"批量输入XX记录"）？保留：数据标签（"共X条"、"订单号：XXX"）、运行时错误信息（`Color.Error`）、MudAlert 业务通知（A17）**
- **A20: 页面主标题下禁止显示副标题/描述文字 — 如 `<MudText Typo="Typo.body2" Color="Color.Secondary">客户档案信息</MudText>` 出现在 `CardHeaderContent` 内主标题之后的行，须删除。仅保留主标题（Typo.h5），下方的灰色/次要色描述文字一律移除？**

### A.2 列表页额外
- B01: `ServerData="OnServerData"` 服务端分页？
- B02: 搜索框 `Immediate="true"` + `DebounceInterval="500"`？
- B03: 无 `Placeholder` 属性（用 Label）？
- B04: 自定义排序：`sortColumn` + `sortDescending` + `ToggleSort()`？
- B05: 多选：`HashSet<int> selectedIds` + `ToggleSelectAll`？
- B06: 打印按钮 + `openPdf()`？
- B07: 右上角"新建"跳转按钮？
- B08: 内联编辑有方向键 id 容器 `#xxx-list-table`？
- B09: 方向键导航：`_isArrowNavSetup` + `OnAfterRenderAsync` + `InvokeAsync<bool>`？

### A.3 创建页额外
- C01: 方向键 `#xxx-create-table` id？
- C02: `_isArrowNavSetup` 防重入？
- C03: `InvokeAsync<bool>` + 失败重试？
- C04: 方向键容器不在 `@if` 内部？
- C05: `Task.WhenAll` 并行加载？
- C06: 右上角"返回"按钮 → 列表页？

### A.4 详情/编辑页额外
- D01: 查看模式表格 `ReadOnly="true"`？
- D02: RenderCell 通过 `_isEditMode` 分支渲染？
- D03: 编辑模型用独立 `_editItem`？
- D04: 编辑模式下列选择器隐藏/禁用？
- D05: 方向键导航（同 C01-C04，id 为 `#xxx-table`）？

### A.5 单行表单区块额外
- E01: 使用 `MudSimpleTable` 表格模式（禁止 `form-inline-row`）？
- E02: 输入组件 `Dense="true" Variant="Variant.Outlined" Size="Size.Small"`？
- E03: MudCheckBox/MudSwitch 不用上述公共参数？
- E04: 多区块用独立 `MudCard Class="mb-4 compact-card"`？
- E05: 无 `MudAlert`、`HelperText` 等提示文字？
- E06: 输入文字使用默认色（非浅色/灰色）？

## 字符串大小写规范
- SQL Server case-insensitive, but C# in-memory comparisons (ToHashSet/Contains/ToDictionary) default to case-sensitive Ordinal
- Any business code string compared in memory after SQL query must use `StringComparer.OrdinalIgnoreCase`

## 枚举中文显示规范
- 所有枚举字段在页面和打印中必须通过 `DisplayHelper.GetXxxText()` 显示中文
- **禁止 `.ToString()` 直出枚举**，也**禁止直接输出 DTO 中的枚举字符串字段**
- **检查方法**：对 `Status`、`ProductionType`、`InboundSource`、`LengthStatus`、`DeliveryState`、`SettlementMethod`、`MaterialName`、`TechnicalRequirements` 每个字段的每处显示位置，逐处核对是否经过 DisplayHelper
- **注意 RenderFragment builder**：`builder.AddContent(0, item.Status)` 虽然没有 `.ToString()`，但输出的是英文枚举值，同样违规

## 页面规范
- 操作列：仅保留"打印"+"取消"按钮
- 创建页："返回" → 对应列表页
- 删除确认：ConfirmDialog（Title/ContentText/ConfirmText/Color）
- 必填字段表头：`IsRequired=true` + `<span class="required-star">*</span>`
- 所有表格添加 `Class="auto-table"`
- MudNumericField 均添加 `HideSpinButtons="true"`
- 禁止表单字段使用 `Required="true"`/`Immediate="true"`（搜索框例外）
- 内联编辑表格启用方向键导航（`enableTableArrowNav` + `_isArrowNavSetup` 防重入）
- 提交时 Snackbar 汇总验证（不用 alert）
- 禁止实时验证
- 物理删除策略（DELETE FROM）
- MudAlert 只能用作业务通知（如工单不匹配），禁止用作提示/错误信息
- 禁止页面出现提示性/指导性文字（操作指引、空状态提示、说明文字等），仅保留数据标签和运行时错误信息
- 页面主标题下禁止显示灰色/次要色副标题描述文字（如 `Typo.body2 Color.Color.Secondary`），仅保留主标题

## 批次上下文
### 核心实体
- **ProductionBatch**：批次主表，批次号/钢种/规格/类型/比例等
- **ProcessGroup**：工序组（=工艺卡一行），工序名称/制造规格/公差/15个工段执行顺序字段
  - `SequenceNumber` = 组内序号（int，批次内排序索引 1,2,3...）
  - 15个工段字段（`ColdRollDraw`/`Straighten`/`Cut`/`Pickle` 等 `int?`）
- **ProductionRecord**：工段内部执行记录
  - `SequenceNumber` = 从 ProcessGroup 工段字段解析的执行序号
- **SectionOutsource**：委外发出记录
  - `SequenceNumber` = 同上

### SequenceNumber 语义
- `ProcessGroup.SequenceNumber` ≠ `ProductionRecord/SectionOutsource.SequenceNumber`
- 前者是组内显示顺序；后者是工段在工序组中的执行顺序值
- 解析方法：SectionName → CASE WHEN → 对应 ProcessGroup 字段值

### 批次跟踪
- 方法位于 `ProductionRecordService.cs:695`
- 截止执行日 = max(生产记录ExecDate, 委外SendOutDate, 回收RecoveryDate)
- 当前工序/工段/设备/委外/规格：取最大SequenceNumber所在记录
- 下一工段：最大SeqNum+1，在全局工段列表查找匹配项

## 表格方向键导航关键实现（2026-05-12）
- JS 文件：`MES.Blazor/wwwroot/js/table-nav.js`
- **下拉模式速度优化**：当 MudSelect 弹出菜单打开且处于下拉选择模式时（`_mudSelectActive=true`），ArrowUp/ArrowDown 由 JS 直接在 DOM 上添加/移除 `.mud-selected-item` 类，完全绕过 Blazor WASM 互调（JS→.NET→StateHasChanged→Render→DOM），实现即时响应
- 确认选择：Enter 键触发当前高亮项的 `.click()` 让 Blazor 处理实际选择逻辑
- 弹出层通过 `document.querySelector('.mud-popover-open .mud-list')` 查找（MudBlazor 渲染为 portal 在 document 根级）
- **MudCheckBox/MudSwitch**：Enter 时 JS 调用 `nativeInput.click()` 切换值（MudBlazor 默认不响应 Enter）
- **CSS 要点**：
  - `.mud-selected-item` 在 MudBlazor 默认 CSS 中无可见样式，须在 `app.css` 中补充 `background-color`
  - 聚焦指示用 `box-shadow: inset 0 0 0 1px` 替代 `border-color`，避免边框线在单元格间产生视觉延申
  - MudSelect 的 `.mud-input-slot` 须覆盖默认 `padding: 18.5px 14px` 为 `padding: 0 0 0 8px; line-height: 32px` 以对齐文本和箭头

## 全局输入框高度/字体统一（2026-05-12）
- 所有 MudTextField/MudSelect/MudNumericField 统一 32px 高度、0.875rem 字号
- 规则从 `.mud-table` 作用域扩展为全局 `.mud-input.mud-input-text`，覆盖搜索框、表单输入、状态筛选下拉等所有文本输入框
- 例外：表头筛选输入（`.filter-input`）保留 24px；隐藏式输入（`.form-hidden-input`）保留 24px
- 表格内的输入框叠加无边框+box-shadow 焦点样式，表格外的使用 MudBlazor 默认边框

## 全局字体配置（2026-05-12 最终确认）
- **字体系列**：`'Helvetica Neue', Helvetica, Arial, '仿宋', 'FangSong', '华文仿宋', STFangsong, sans-serif`（英文无衬线 + 汉字仿宋）
- **根字号**：17px（所有 rem 值基于此缩放，0.875rem ≈ 14.9px）
- **表头字重**：`th.mud-table-cell, .auto-table th { font-weight: 600 }`

## 全局行高统一（2026-05-12）
- 查看模式（纯文本）与编辑模式（有输入框）的表格行高统一 38px
- 覆盖：`auto-table td`、`compact-table.mud-table .mud-table-cell`、`mud-table-dense .mud-table-row .mud-table-cell`
- 输入框高度统一 32px（全局），表头筛选 filter-input 保留 24px 例外

## MudChip 状态标签字体统一（2026-05-12）
- 所有 `Size="Size.Small"` 的 MudChip 状态标签统一字体 0.875rem，与表格文字一致
- 实现：`.mud-chip.mud-chip-size-small { font-size: 0.875rem !important; }`

## batch-create-table 无边框风格统一（2026-05-12）
- batch-create-table 的输入框边框样式与表格内联编辑一致
- 默认边框 `transparent`，悬浮 `rgba(0,0,0,0.15)`，聚焦用 `box-shadow` 替代边框
- 实现：与 `.mud-table .mud-input-outlined` 相同规则