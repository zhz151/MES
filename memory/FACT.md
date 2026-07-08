# MES 项目持久知识

## BatchCreate/BatchEdit 横向滚动表单布局模式
### 适用场景
- 批量编辑/创建页面的单行数据块（仓库信息、工单字段信息、基础信息）
- ≥10 列以上的宽表单，需要横向滚动

### 布局模式
```
<div style="overflow-x:auto;">
    <MudSimpleTable Dense="true" Class="auto-table compact-table" Style="width:xxxpx;">
        <thead>
            <tr>
                @if (GetCol("Key").Visible) { <th style="width:xxxpx;">列名</th> }
            </tr>
        </thead>
        <tbody>
            <tr>
                @if (GetCol("Key").Visible) { <td><MudTextField ... FullWidth="true" /></td> }
            </tr>
        </tbody>
    </MudSimpleTable>
</div>
```

### 关键规则
1. **容器**：`overflow-x:auto` + 内部 table 设 `width`（4200px 工单字段 / 1500px 基础信息 / 1600px 仓库信息）
2. **列宽**：仅 `<th>` 设 `style="width:xxxpx;"`，data cells 不设宽度
3. **class**：`auto-table compact-table`（紧凑样式）
4. **输入组件**：`FullWidth="true"`（自适应列宽）
5. **列显隐**：MudSelect/MudCheckBox 等多行组件使用 `@if (GetCol("Key").Visible)` 包裹
6. **多行组件**：MudSelect/MudCheckBox 等需要多行排版的，用 `{ }` 块包裹在 `if` 内，缩进渲染
7. **日期**：`Placeholder="yyyy-MM-dd"`，禁止 MudDatePicker
8. **数值**：`Format="G29"` + `HideSpinButtons="true"`

### Block 结构（BatchCreate.razor / BatchEdit.razor）
- **Block A 仓库信息**：overflow-x:auto + MudSimpleTable（13-14 列，min-width:1600px）
- **Block B 工单字段信息**：overflow-x:auto + MudSimpleTable + ColumnDisplaySelect（28-29 列，width:4200px）
  - 头部工具栏：工单号输入框 + 确认按钮 + 列显隐切换 + 重置
- **Block C/D 基础信息（合并质量要求）**：overflow-x:auto + MudSimpleTable（10 列，min-width:1500px）
  - 质量字段（固溶参数、质量备注）直接追加在基础信息表末尾
- **Block E 工序组**：工具栏（复制上个工序组 + 新增工序 + 列显隐 + 重置 + 批次号输入 + 调取工序）+ MudTable 工序列表

### 工具栏按钮顺序（Block E）
复制上个工序组 → 新增工序 → 列显隐切换 → 重置 → 批次号输入(130px) → 调取工序(130px)

### 关键差异（Edit vs Create）
- Edit 页生产编号使用 `originalBatch.BatchNo`（只读），Create 使用 `batchNoDisplay`
- Edit 页有效支数保留 `@bind-Value:after="RecalcValidWeightOnQtyChange"`
- Edit 页日期使用 `editSignDateStr`/`editDeliveryDateStr`，Create 使用 `signDateStr`/`deliveryDateStr`

## 批次上下文列表页分组标准模式
### 适用页面（共用 ProductionBatchListDto）
- **Batches**（批次首页）— `MES.Blazor/Pages/Batches/Batches.razor.cs`
- **ProcessCardPrint**（工艺卡打印）— `MES.Blazor/Pages/Batches/ProcessCardPrint.razor`
- **ProductionRecords**（生产记录）— `MES.Blazor/Pages/Batches/ProductionRecords.razor.cs`

### 分组定义（Batches / ProcessCardPrint 统一）
| 分组 | GroupKey | 内容 |
|------|----------|------|
| G1 批次基本信息 | 1 | BatchNo, TagNo, Status, ProductionType, ManufacturingItem, ProductionRatio, CurrentValidQty, CurrentValidWeight, ValidInputQuestion, CreatedBy, CreatedTime, UpdatedTime 等批次自身字段 |
| G2 工单信息 | 2 | 编号(WorkOrderNo/SalesOrderNo/ProductionMainNo/ProductionSubNo) + 日期(SignDate/DeliveryDate) + 人员/客户(Salesman/EndCustomer) + 商务条款(DelayPenalty/SettlementMethod/MaterialName) + 产品要求(StandardCode/DeliveryState/PlantGrade/Specification/LengthStatus) + 数量汇总(TotalQuantity/TotalMeters/TotalWeight) + TechnicalRequirements |
| G3 生产执行 | 3 | CurrentExecDate, CurrentGroupName, CurrentSectionName, CurrentSectionCompleted, RemainingWorkDays, CurrentEquipmentName, CurrentOutsource, CurrentSpec, NextSectionName, CorrespondingSpec, NextProcess |

### ProcessCardPrint 分组（批次首页子集，分组对齐）
- G1 批次基本信息（6）：BatchNo, TagNo, Status, ProductionType, ManufacturingItem, CreatedTime
- G2 工单信息（4）：WorkOrderNo, SalesOrderNo, ProductionMainNo, ProductionSubNo
- G3 生产执行（9）：CurrentExecDate, CurrentGroupName, CurrentSectionName, CurrentEquipmentName, CurrentOutsource, CurrentSpec, NextSectionName, CorrespondingSpec, NextProcess
- 关键：Status/ProductionType/ManufacturingItem 归入 G1（与批次首页一致），不放在 G3

### ProductionRecords 分组（独立 DTO，4 组）
| 分组 | GroupKey | CSS | 字段 |
|------|----------|-----|------|
| G1 执行信息 | 1 | col-g3 (绿) | ExecDate, BatchNo, ProcessName, ManufacturingSpec, SectionName, SequenceNumber |
| G2 产出数据 | 2 | col-g4 (橙) | EquipmentName, Operator, Shift, Quantity, Weight, IsFinished, CuttingMultiple, FinishedCutLength, PostCutQuantity, FaceCutCount |
| G3 工艺参数 | 3 | col-g5 (紫) | SolutionTemperature（固溶温度）, SoakTime（保温时间）— 仅固溶工段使用 |
| G4 追溯信息 | 4 | col-g6 (青) | TagNo, PlantGrade, Remark, DataSource, UpdatedTime |

### 分组实现模式
- **CSS 映射**：`GetHeaderGroupCss()` / `GetCellGroupCss()` 用 switch 映射 groupKey → col-g{3-6}+cell
- **css/app.css** 已预定义 col-g1~g15 的背景色/badge 色，col-g1/g2 供其他页面使用
- **GroupHeaderInfo** 类 + `GetGroupHeaders()` 渲染分组头 + `_prevGk` 跟踪 + `initGroupHeaders` JS
- **列显隐持久化**：每个页面用独立的 localStorage key（Batches="batches", ProcessCardPrint="process_card_print_list", ProductionRecords="productionrecords"）

### 列本地持久化 key 清单
| 页面 | localStorage key | 文件位置 |
|------|-----------------|----------|
| Batches | `col_prefs_batches` | Batches.razor.cs: SaveColumnPrefs() |
| ProcessCardPrint | `col_prefs_process_card_print_list` | ProcessCardPrint.razor: SaveColumnPrefs() |
| ProductionRecords | `col_prefs_productionrecords` | ProductionRecords.razor.cs: SaveColumnPrefs() |

### 分组列 CSS 类（app.css）
- col-g1 (灰), col-g2 (蓝): 给其他页面使用
- col-g3 (绿), col-g4 (橙), col-g5 (紫), col-g6 (青): 批次上下文使用
- col-g7~g15: 扩展预留
- `col-group-start` / `col-group-start-cell`: 分组分割线（从 G2 开始添加）

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

## DTO 投影原则
- 列表页查询，如 DTO 字段涉及跨表（非实体自身字段），优先采用 DTO 投影模式：
  `select new Dto { ... }` 直接用 LINQ 投影，配合 `ApplyFilters` / `ApplySort`
- 禁止在实体 IQueryable 上手动管理跨表字段的筛选/排序（如 crossTableFields HashSet + 后置填充）
- 投影能处理的场景（聚合/子查询/条件逻辑/字符串处理）不应 fallback 到内存补充
- 投影翻译不了的边界场景（调 Service/复杂业务规则）采用 Hybrid 模式：先投影查询，再内存补充
