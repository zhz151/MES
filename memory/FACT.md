# MES 项目持久知识

## 行为规则
- **失败自愈上限**：连续失败 2 次后必须停下来问用户，不得继续自行重试
- **所有业务和代码规则见 `docs/04_开发规范.md`**
- **严禁未经用户同意执行 git 回滚/重置操作**：任何形式的 `git checkout --`、`git reset --hard`、`git revert` 等可能丢失代码的破坏性 git 操作，必须先获得用户明确批准。用户授权一次仅代表本次有效，不构成后续授权。
- **严禁凭空猜测 EnumOptions 值**：ColumnDef 中 EnumOptions.Value 必须与 `GetCellRawValue()` 返回的数据库原始值一致，添加前必须核查 DisplayHelper 映射和实体字段注释，禁止编造。

## 关键陷阱：列筛选必须用 GetCellRawValue（所有模式通用）
- ExcelFilter 内部存储的是 `GetCellRawValue()` 返回的**原始值**（enum 名、bool 字符串、原始数值、日期字符串等）
- ServerData 模式：后端 filter-contexts 端点返回原始值，前端 `GetCellRawValue` 也返回原始值，两者必须一致，ExcelFilter 基于原始值做筛选匹配
- **禁止使用 `GetCellDisplayText`**（返回中文/格式化显示文本，与原始值不匹配导致筛选无效）
- 此规则已在 `docs/04_开发规范.md` 的 §9.4（禁止事项）、§18.3（工作流/强制检查）、B13 检查清单中明确写入

## 关键陷阱：FilterDescriptor.Operator 必须显式设置 "in"
- `FilterDescriptor.Operator` 的默认值是 `"contains"`（类定义），不是 `"in"`
- `SerializeFilters()` 中必须显式设置 `Operator = "in"`，否则 `BuildStringContains` 使用 `null Value` 返回空，筛选被静默跳过
- 当前所有页面已正确设置 `Operator = "in"`（2026-05-22 检查确认）

## 关键陷阱：导航属性列需单独处理筛选
- `ApplyFilters` 通过反射在实体类型上查找属性名，导航属性字段（如 `ProductionRecord.BatchNo` 来自 `r.ProductionBatch.BatchNo`）无法被反射到
- 必须在 `ApplyFilters` 调用**之前**单独处理导航属性筛选，然后从 `Filters` 中移除该条件
- 示例：`ProductionRecordService.GetAllProductionRecordsAsync` 中 BatchNo 的导航属性筛选处理

## 关键陷阱：后处理覆盖字段的筛选查询也必须指向同一数据源
- 仅修正 filter-contexts 的显示层不够。如果 `GetPagedAsync` 中有后处理覆盖了字段的显示值（如 `PatchCustomerFieldsAsync` 从 CustomerProfile 覆盖 Salesman/EndCustomer），该字段的筛选查询也必须指向覆盖后的数据源
- 否则用户选了筛选值，`ApplyFilters` 在旧快照上搜不到 → 返回空结果
- 做法：在 `ApplyFilters` 前单独处理（通过 SalesOrderNo 等关联字段桥接查询），然后 Remove 出 Filters 列表

## 关键陷阱：BuildIn DateTime 用 .Date 截断匹配（2026-05-23 更新）
- `BuildIn` 的 DateTime 分支中，memberForContains 必须通过 `Expression.Property(memberForContains, "Date")` 截取到日期部分
- 这样 `2026-05-23 08:30:00` 在数据库中可以被 `"2026-05-23"` 的筛选值匹配
- 同时保留 `Nullable<DateTime>` 的 `.Value` 处理（针对 `DateTime?` 类型）
- 影响所有 `DateTime` 类型的筛选列（如 IncomingDate、InspectionDate 等业务日期字段）
- `DateTimeOffset` 类型（CreatedTime/UpdatedTime 等时间戳）不设 FilterType，仅排序

## 关键陷阱：筛选上下文排除枚举列
- `GetFilterContextsAsync` 禁止返回有固定 `EnumOptions` 的枚举列（Status/ProductionType/MaterialName/DelayPenalty 等）
- 否则前端 `EnumOptions fallback` 被绕过，筛选项显示英文而非中文
- 枚举列的筛选选项由前端 `BuildFilterContextOptions` 的 `EnumOptions fallback` 直接提供中文 Display
- `GetFilterContextsAsync` 仅返回 string/bool/date 等非枚举列的 distinct 值

## 关键陷阱：布尔 EnumOptions 大小写
- `bool.ToString()` 返回 PascalCase（"True"/"False"），EnumOptions.Value 必须用 PascalCase
- 小写 "true"/"false" 会导致 `GetCellRawValue` 返回的 "True" 与 EnumOptions 不匹配 → 筛选无效

## 关键陷阱：EnumOptions 值必须与数据库实际值匹配
- 所有枚举存储为字符串类型的字段（ProductionType/MaterialName/SettlementMethod 等），EnumOptions.Value 必须与数据库中存储的原始值完全一致
- 添加前必须核查两处：① `DisplayHelper` 中该字段的映射（如 `GetProductionTypeText("RoughTube")`）；② 实体字段注释
- 禁止凭空猜测 EnumOptions 值，否则筛选永远返回空结果

## 关键陷阱：ExcelFilter 不提供排序（2026-05-23 更新）
- ExcelFilter 组件**只负责列筛选**，不包含排序按钮
- 排序统一由列名点击 `ToggleSort(col.Key)` 提供
- Code-behind 中不应有 `OnExcelSortRequested` 方法
- sortColumn 只来自 ToggleSort（col.Key，PascalCase），`LoadDataFromServer` 直接按 `c.Key == sortColumn` 匹配

## 关键陷阱：Controller 筛选参数绑定（2026-05-23 新增）
- `[FromQuery] QueryParams query` 中 `query.Filters`（`List<FilterDescriptor>?`）是复杂类型，ASP.NET Core 默认模型绑定器无法从查询字符串反序列化 JSON 数组到此属性
- filters 被静默置 null，筛选不生效且没有任何报错——这是最隐蔽的筛选失效原因
- 必须写为 `[FromQuery] string? filters = null` + `JsonSerializer.Deserialize<List<FilterDescriptor>>(filters)`
- 已修复 3 个 Controller（ProductionStandardController、GradeMappingController、WorkOrderExecutionController）
