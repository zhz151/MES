# 按库出库改造方案

## 现状问题

WarehouseOutbound.razor（`/warehouse/outbound`）是**不感知仓库**的整体出库模式：
- 路由不携带仓库代码
- 列定义固定15列，不按仓库适配
- 列偏好保存键为 `"outbound_batch"/"default"`，不区分仓库
- 回退导航依赖 OutboundStateService 传递的代码

## 改造范围

只改 **1 个文件** + 微调 **1 个导航点**，不动其他页面：

| 文件 | 改动量 | 说明 |
|------|--------|------|
| `WarehouseOutbound.razor` | 中 | 路由 + 列适配 + 导航 |
| `WarehouseInventory.razor.cs` → NavigateToOutbound | 1行 | URL 追加 Code 参数 |

不需要改 OutboundStateService（仍用于传递选中批次）。

---

## 详细步骤

### Step 1: 路由增加 Code 参数

```razor
@page "/warehouse/outbound"
@page "/warehouse/outbound/{Code}"
```

注意保留不带 Code 的路由（向前兼容直接访问、或从外部链接进入）。

### Step 2: 解析仓库代码

在 `@code` 块或代码分离文件中：

```razor
[Parameter] public string? Code { get; set; }
```

`OnInitialized` / `OnParametersSet` 中解析：
- 如果 `Code` 有值，优先使用 URL 中的代码
- 否则 fallback 到 `OutboundState.WarehouseCode`
- 通过 `_warehouseCode` 和 `_warehouseName` 字段存储

### Step 3: 列适配（ApplyWarehouseDefaults）

参照 `OutboundHistory.razor.cs:110` 的模式，新增两个私有方法：

```csharp
private static void ApplyWarehouseDefaults(List<ColumnDef> cols, string whCode)
{
    foreach (var c in cols) { c.IsApplicable = true; c.Visible = true; }
    switch (whCode)
    {
        case "RAW":
            SetNotApplicable(cols, "MinLength");
            SetNotApplicable(cols, "MaxLength");
            // ... 原料库隐藏米数/长度/次品等字段
            break;
        case "FG":
            // 成品库隐藏次品字段
            break;
        case "DEFECT":
            // 次品库保持完整
            break;
        case "WIP":
            // 在制品库隐藏工单/订单/来源等字段
            break;
    }
}

private static void SetNotApplicable(List<ColumnDef> cols, string key)
{
    var c = cols.FirstOrDefault(x => x.Key == key);
    if (c != null) { c.IsApplicable = false; c.Visible = false; }
}
```

出库页面特有的列适配规则（参照出库场景，非入库场景）：

| 仓库 | 隐藏列 |
|------|--------|
| RAW | `RemainingMeters`, `OutboundMeters`, `SourceOrderNo`, `TargetCompany` |
| FG | 全部显示 |
| DEFECT | 全部显示 |
| WIP | `SourceOrderNo`, `TargetCompany` |

### Step 4: 列偏好键改为按库

```csharp
// Before:
await ColumnPrefs.SaveAsync("outbound_batch", "default", _allColumns);
// After:
await ColumnPrefs.SaveAsync("outbound_batch", warehouseCode, _allColumns);
```

加载时同理：
```csharp
// Before:
var saved = await ColumnPrefs.LoadAsync("outbound_batch", "default");
// After:
var saved = await ColumnPrefs.LoadAsync("outbound_batch", warehouseCode);
```

### Step 5: 导航目标使用 URL Code

```csharp
// GoBack — 不再依赖 OutboundState，使用 URL Code
Navigation.NavigateTo($"/warehouse/{_warehouseCode?.ToLowerInvariant() ?? OutboundState.WarehouseCode.ToLowerInvariant()}");

// Submit 成功后同理
Navigation.NavigateTo($"/warehouse/{_warehouseCode?.ToLowerInvariant() ?? OutboundState.WarehouseCode.ToLowerInvariant()}");
```

### Step 6: 库存页面导航追加 Code

`WarehouseInventory.razor.cs` 的 `NavigateToOutbound()`:

```csharp
// Before:
Navigation.NavigateTo("/warehouse/outbound");
// After:
Navigation.NavigateTo($"/warehouse/outbound/{warehouseCode.ToLowerInvariant()}");
```

### Step 7: 标题栏显示仓库名称

```razor
<MudText Typo="Typo.h5" Class="font-weight-bold">
    <MudIcon Icon="@Icons.Material.Filled.ExitToApp" Class="mr-2" Color="Color.Warning" />
    批量出库
</MudText>
<MudText Typo="Typo.body2" Color="Color.Secondary">
    仓库：<strong>@_warehouseName</strong> ｜ 待出库 @_items.Count 项
</MudText>
```

_warehouseName 从 URL Code 或 OutboundState 获取。

---

## 不需要改的

| 项目 | 原因 |
|------|------|
| OutboundStateService | 选中批次的传递机制不变，仍然通过 scoped service |
| RenderCell 逻辑 | 编辑控件的渲染逻辑不变，只是哪些列可见变化 |
| Submit 验证逻辑 | 仓库相关的验证（如委外穿孔号）已有，不新增 |
| Backend API | 出库 API `api/inventory/batch-outbound` 已有 warehouseId 字段 |

---

## 影响范围

- **正向兼容**：旧 `/warehouse/outbound` 路由保留，无 Code 时从 OutboundState 获取
- **不影响**：入库、库存、历史页面
- **不影响**：OutboundHistory 页面（它也是按库出库的已用页面，不在改范围内）
