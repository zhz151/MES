# MES项目 - 持久知识

## AI 强制性检查规则（2026-05-16）

### 列表页全字段排序规则
- 每个列表页的 DTO 中**所有直接映射 DB 的字段**必须有前端 `SortKey` + 后端 `SortBy` switch 分支
- **例外**：内存计算字段（RunningStatus/InspectionStatus/MaintStatus 等）不设 SortKey
- SortKey 命名：全小写实体字段名（`EquipmentCode` → `"equipmentcode"`）
- 后端 switch 必须同时处理 `(key, true)` 和 `(key, false)` 两个分支

### 列表页全文本搜索规则
- 模糊搜索（`Keyword`）必须覆盖 DTO 中**所有 string 类型字段**
- 可空字段需要 null 保护：`(x.Field != null && x.Field.Contains(kw))`
- 非文本字段（int/decimal/DateTime/bool）豁免

### AI 新增字段时的强制同步要求
在 DTO 中新增字段时，必须同步：
1. 前端列定义 `GetAllColumnDefs()` 添加对应 ColumnDef
2. 后端 `Keyword` 筛选追加 `.Contains(kw)`（如果是 string 类型）
3. 后端 `SortBy` switch 追加排序分支（如果是可排序字段）

详见 `docs/04_开发规范.md` §18 AI 自动化检查与设计规范

## 附录A 检查清单（2026-05-16 新增项）
- **B10**: 全字段排序 — 逐列核对 ColumnDef SortKey 和 Service switch
- **B11**: 全文本搜索 — 核对 Service Keyword 筛选包含每个 string 字段的 Contains(kw)

## 页面容器规范（2026-05-11 更新）
- **所有页面统一全宽**：列表页、详情页、创建页均使用 `MudContainer MaxWidth="MaxWidth.False"`
- 列表页额外使用 `Class="mt-4 pl-0"`，详情/创建页使用 `Class="mt-4"`

## 强制工作流
**创建新页面或检查现有页面时，必须逐条对照附录A检查清单（共30+项），不可遗漏任何类别：**
- **通用检查（A01-A20）**：所有页面类型必查
- **列表页（B01-B09）**：仅列表页 （加上B10/B11）
- **创建页（C01-C06）**：仅创建页
- **详情/编辑页（D01-D05）**：仅详情编辑页
- **单行表单（E01-E06）**：仅单行表单区块

**每完成一项就在脑中勾掉一项。不允许跳项、漏项。**

## A16 特别检查步骤（A16 是最易遗漏项）
执行 A16 时，按以下步骤精确核查：
1. **逐字段核对**：对 `Status`、`ProductionType`、`InboundSource`、`LengthStatus`、`DeliveryState`、`SettlementMethod`、`MaterialName`、`TechnicalRequirements`、`BatchStatus`、`SectionOutsourceStatus` 这 10 个字段，在页面中搜索出现位置
2. **两处检查**：Razor 模板中的 `@item.Status` / `@item.ProductionType` 等，以及 RenderFragment builder 中的 `builder.AddContent(..., item.Status)` 等
3. **核对方式**：确认每个调用都经过了 `DisplayHelper.GetXxxText()` 而非直接输出。**特别注意 `builder.AddContent()` 模式**（这类违规没有 `.ToString()`，仅凭肉眼识别）

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
- **ProductionBatch**：批次主表，批次号/钢种/规格/类型/比例等。状态枚举：None/InProgress/Completed/**Suspended(挂起)**
- **ProcessGroup**：工序组（=工艺卡一行），工序名称/制造规格/公差/15个工段执行顺序字段
- **ProductionRecord**：工段内部执行记录
- **SectionOutsource**：委外发出记录
- **BatchOperationLog**：操作日志，记录状态变更等关键操作

### 批次状态操作（2026-05-13）
- **暂停**：InProgress → Suspended
- **恢复**：Suspended → InProgress
- **强制完成**：InProgress → Completed（IsForceCompleted=true）
- **转为在产**：Completed(IsForceCompleted=true) → InProgress（IsForceCompleted=false）

## 全局输入框高度/字体统一（2026-05-12）
- 所有 MudTextField/MudSelect/MudNumericField 统一 32px 高度、0.875rem 字号
- 例外：表头筛选输入（`.filter-input`）保留 24px；隐藏式输入（`.form-hidden-input`）保留 24px

## 全局字体配置（2026-05-12 最终确认）
- **字体系列**：`'Helvetica Neue', Helvetica, Arial, '仿宋', 'FangSong', '华文仿宋', STFangsong, sans-serif`
- **根字号**：17px
- **表头字重**：`th.mud-table-cell, .auto-table th { font-weight: 600 }`

## 全局行高统一（2026-05-12）
- 查看模式（纯文本）与编辑模式（有输入框）的表格行高统一 38px
- 输入框高度统一 32px（全局），表头筛选 filter-input 保留 24px 例外
