# MES 项目持久知识

## 枚举字段从 MudTextField 改为 MudSelect 的完整模式（已验证 SourceLengthStatus / SubcontractProcessType / DeliveryState）

### Entity 层
- 保留 `string?` 字段，不改为枚举类型（DB 列不变，无 schema 变更）

### DTO 层
- 列表 DTO、详情 DTO、创建/更新请求 DTO：`string?` → `枚举类型?`
- 必须添加 `using MES.Core.Enums;`

### Service 层
- **写入 DB（Create/Update）**: `request.EnumField?.ToString()`
- **读取（LINQ 投影）**: 拆分为两步 — ① `Select` 到匿名类型保留 `c.EnumField` (string) ② `.ToListAsync()` 后用 `Enum.TryParse<T>()` 转 DTO
- **筛选/排序**: 操作实体字段，保持 `c.EnumField` (string) 不变
- **FilterContexts**: 返回实体 DISTINCT string 值（仍是英文枚举名）

### 前端 Blazor 层

#### Detail 页（编辑模式）
- MudTextField → MudSelect 下拉（枚举版本）
- 查看模式用 `DisplayHelper.GetXxxText()` 显示中文

#### 列表页（List.razor.cs）
- **列定义**: `FilterType = "enum"` + `EnumOptions = GetXxxOptions()`
- **单元格**: `DisplayHelper.GetXxxText(item.EnumField)` 显示中文
- **BuildFilterContextOptions()**: 在基础循环后，对 FilterContexts 中已有的枚举字段，添加显示转换：
  ```csharp
  if (_filterContextOptions.TryGetValue("columnkey", out var options))
      foreach (var opt in options) opt.Display = DisplayHelper.GetXxxText(opt.Value);
  ```
- 如果 API 不返回该枚举字段的 filter contexts（如 SubcontractOrder.ProcessType），则 "补充枚举列筛选选项" 段落的 `EnumOptions` 自动生效

### 测试文件
- DTO 赋值用 `EnumType.Member`（非 `.ToString()`）
- Entity 种子数据赋值用 `EnumType.Member.ToString()`（Entity 仍是 string）
- 断言：DTO 用 `.Be(EnumType.Member)`，Entity 用 `.Be("MemberName")`
- Filter contexts 值断言用 `.Contain("MemberName")`（DB 原始字串）
