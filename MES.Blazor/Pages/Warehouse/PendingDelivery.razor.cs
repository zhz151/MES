using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs.Warehouse;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Warehouse;

public partial class PendingDelivery
{
    private MudTable<PendingDeliveryItemDto>? table;
    private List<PendingDeliveryItemDto> _pageItems = new();
    private int _totalCount;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private string _inboundDateFrom = string.Empty;
    private string _inboundDateTo = string.Empty;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;

    private string sortColumn = "inventorybatchno";
    private bool sortDescending = false;

    // ========== 选择列 ==========
    private HashSet<string> _selectedIds = new();

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "remainingquantity", "remainingweight", "remainingmeters"
    };

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // Group 1：订单关联
        new() { Key = "salesorderno",       Label = "订单号",     SortKey = "SalesOrderNo",    FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 订单关联" },
        new() { Key = "productionmainno",   Label = "主号",       SortKey = "ProductionMainNo",FilterType = "string", Width = "100", GroupKey = 1, GroupName = "① 订单关联" },
        new() { Key = "workorderno",        Label = "工单号",     SortKey = "WorkOrderNo",     FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 订单关联" },
        new() { Key = "salesman",           Label = "业务员",     SortKey = "Salesman",        FilterType = "string", Width = "60",  GroupKey = 1, GroupName = "① 订单关联" },
        new() { Key = "customername",       Label = "客户名称",   SortKey = "CustomerName",    FilterType = "string", Width = "100", GroupKey = 1, GroupName = "① 订单关联" },
        new() { Key = "endcustomer",        Label = "最终客户",   SortKey = "EndCustomer",     FilterType = "string", Width = "100", GroupKey = 1, GroupName = "① 订单关联" },
        // Group 2：材料规格
        new() { Key = "productstandard",    Label = "产品标准",   SortKey = "ProductStandard", FilterType = "string", Width = "100", GroupKey = 2, GroupName = "② 材料规格" },
        new() { Key = "deliverystatus",     Label = "交货状态",   SortKey = "DeliveryStatus",  FilterType = "string", Width = "80",  GroupKey = 2, GroupName = "② 材料规格" },
        new() { Key = "productionbatchno",  Label = "生产批号",   SortKey = "ProductionBatchNo",FilterType = "string",Width = "100", GroupKey = 2, GroupName = "② 材料规格" },
        new() { Key = "heatno",             Label = "炉号",       SortKey = "HeatNo",          FilterType = "string", Width = "100", GroupKey = 2, GroupName = "② 材料规格" },
        new() { Key = "plantgrade",         Label = "工厂牌号",   SortKey = "PlantGrade",      FilterType = "string", Width = "80",  GroupKey = 2, GroupName = "② 材料规格" },
        new() { Key = "standardgrade",      Label = "标准牌号",   SortKey = "StandardGrade",   FilterType = "string", Width = "80",  GroupKey = 2, GroupName = "② 材料规格" },
        new() { Key = "specification",      Label = "名义规格",   SortKey = "Specification",   FilterType = "string", Width = "100", GroupKey = 2, GroupName = "② 材料规格" },
        new() { Key = "lengthstatus",       Label = "长度状态",   SortKey = "LengthStatus",    FilterType = "string", Width = "60",  GroupKey = 2, GroupName = "② 材料规格",
               DisplayConverter = v => DisplayHelper.GetLengthStatusText(v as string) },
        new() { Key = "minlength",          Label = "最小长度",   SortKey = "MinLength",       FilterType = "string", Width = "80",  GroupKey = 2, GroupName = "② 材料规格" },
        new() { Key = "maxlength",          Label = "最大长度",   SortKey = "MaxLength",       FilterType = "string", Width = "80",  GroupKey = 2, GroupName = "② 材料规格" },
        // Group 3：仓库信息
        new() { Key = "inventorybatchno",   Label = "仓库批次",   SortKey = "InventoryBatchNo",FilterType = "string", Width = "120", GroupKey = 3, GroupName = "③ 仓库信息" },
        new() { Key = "inboundsource",      Label = "来源",       SortKey = "InboundSource",   FilterType = "string", Width = "60",  GroupKey = 3, GroupName = "③ 仓库信息",
               DisplayConverter = v => DisplayHelper.GetInboundSourceText(v as string) },
        new() { Key = "sourcename",         Label = "来料单位",   SortKey = "SourceName",      FilterType = "string", Width = "80",  GroupKey = 3, GroupName = "③ 仓库信息" },
        new() { Key = "inbounddate",        Label = "入库日期",   SortKey = "InboundDate",     Width = "90",  GroupKey = 3, GroupName = "③ 仓库信息" },
        new() { Key = "remainingquantity",  Label = "剩余支数",   SortKey = "RemainingQuantity",FilterType = "string",Width = "60",  GroupKey = 3, GroupName = "③ 仓库信息" },
        new() { Key = "remainingweight",    Label = "剩余重量",   SortKey = "RemainingWeight", FilterType = "string", Width = "80",  GroupKey = 3, GroupName = "③ 仓库信息" },
        new() { Key = "remainingmeters",    Label = "剩余米数",   SortKey = "RemainingMeters", FilterType = "string", Width = "80",  GroupKey = 3, GroupName = "③ 仓库信息" },
        new() { Key = "materialtype",       Label = "物料类型",   SortKey = "MaterialType",    FilterType = "string", Width = "60",  GroupKey = 3, GroupName = "③ 仓库信息" },
    };

    // ========== B23 分组列标题栏 ==========
    private int _totalTableWidth =>
        40 + _visibleColumns.Sum(c => int.TryParse(c.Width, out var w) ? w : 100);

    private List<GroupHeaderInfo> _groupHeaders => GetGroupHeaders();

    private class GroupHeaderInfo
    {
        public int GroupKey { get; init; }
        public string GroupName { get; init; } = "";
        public int TotalWidth { get; init; }
        public int ColumnCount { get; init; }
        public string CssClass { get; init; } = "";
    }

    private List<GroupHeaderInfo> GetGroupHeaders()
    {
        var result = new List<GroupHeaderInfo>();

        // 选择列占位（40px）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 40,
            ColumnCount = 0,
            CssClass = ""
        });

        int? lastKey = null; int totalWidth = 0;
        var groupKey = 0; var groupName = ""; var count = 0;
        foreach (var col in _visibleColumns)
        {
            var gk = col.GroupKey ?? 0;
            if (gk != lastKey && lastKey.HasValue)
            {
                result.Add(new GroupHeaderInfo
                {
                    GroupKey = groupKey,
                    GroupName = groupName,
                    TotalWidth = totalWidth,
                    ColumnCount = count,
                    CssClass = GetHeaderGroupCss(groupKey, true)
                });
                totalWidth = 0; count = 0;
            }
            groupKey = gk; groupName = col.GroupName ?? "";
            totalWidth += int.TryParse(col.Width, out var w) ? w : 100;
            count++; lastKey = gk;
        }
        if (count > 0)
            result.Add(new GroupHeaderInfo
            {
                GroupKey = groupKey,
                GroupName = groupName,
                TotalWidth = totalWidth,
                ColumnCount = count,
                CssClass = GetHeaderGroupCss(groupKey, true)
            });
        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1", 2 => "col-g2", 3 => "col-g3", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1-cell", 2 => "col-g2-cell", 3 => "col-g3-cell", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(PendingDeliveryItemDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);
        foreach (var col in _visibleColumns.Where(c => _summableColumnKeys.Contains(c.Key)))
        {
            if (!props.TryGetValue(col.Key, out var prop)) continue;
            var type = prop.PropertyType;
            try
            {
                if (type == typeof(int))
                {
                    var sum = _pageItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = _pageItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
            }
            catch { }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum)) return sum;
        return "-";
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<PendingDeliveryItemDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "InventoryBatchNo";
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
            };

            if (!string.IsNullOrEmpty(filtersJson))
            {
                try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson); }
                catch { }
            }

            var result = await PendingSvc.GetAllAsync(
                query,
                inboundDateFrom: DateTime.TryParse(_inboundDateFrom, out var df) ? df : null,
                inboundDateTo: DateTime.TryParse(_inboundDateTo, out var dt) ? dt : null);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        ComputePageSums();

        // 清除当前页不在选择中的已删除项
        var currentIds = _pageItems.Select(i => i.InventoryBatchNo).ToHashSet();
        _selectedIds.RemoveWhere(id => !currentIds.Contains(id));

        await SavePageStateAsync();

        return new TableData<PendingDeliveryItemDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    private string? SerializeFilters()
    {
        if (_columnFilters.Count == 0) return null;
        var descriptors = new List<FilterDescriptor>();
        foreach (var kvp in _columnFilters)
        {
            if (kvp.Value.Count == 0) continue;
            descriptors.Add(new FilterDescriptor
            {
                Field = kvp.Key,
                Operator = "in",
                Values = kvp.Value.ToList()
            });
        }
        return descriptors.Count > 0 ? JsonSerializer.Serialize(descriptors) : null;
    }

    // ========== 筛选上下文加载（ExcelFilter 下拉选项） ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await PendingSvc.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                BuildFilterContextOptions(result.Data);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载筛选上下文失败: {ex.Message}", Severity.Warning);
        }
    }

    private void BuildFilterContextOptions(Dictionary<string, List<string>> filterContexts)
    {
        _filterContextOptions.Clear();
        foreach (var kvp in filterContexts)
        {
            var key = kvp.Key.ToLower(); // backend returns PascalCase, columns use lowercase
            _filterContextOptions[key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = v,
                Count = 0
            }).ToList();
        }

        // InboundSource 列显示中文
        if (_filterContextOptions.TryGetValue("inboundsource", out var inboundOptions))
        {
            foreach (var opt in inboundOptions)
                opt.Display = DisplayHelper.GetInboundSourceText(opt.Value);
        }

        // DeliveryStatus 列显示中文（枚举名 → 中文）
        if (_filterContextOptions.TryGetValue("deliverystatus", out var deliveryOptions))
        {
            foreach (var opt in deliveryOptions)
                opt.Display = DisplayHelper.GetDeliveryStateText(opt.Value);
        }

        // LengthStatus 列显示中文
        if (_filterContextOptions.TryGetValue("lengthstatus", out var lengthOptions))
        {
            foreach (var opt in lengthOptions)
                opt.Display = DisplayHelper.GetLengthStatusText(opt.Value);
        }

        // MaterialType 列显示中文（后端返回枚举英文名）
        if (_filterContextOptions.TryGetValue("materialtype", out var materialOptions))
        {
            foreach (var opt in materialOptions)
                opt.Display = DisplayHelper.GetMaterialTypeText(opt.Value);
        }

        // 补充枚举列筛选选项（后端不返回枚举列 DISTINCT 值）
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "enum" && col.EnumOptions != null && !_filterContextOptions.ContainsKey(col.Key))
            {
                _filterContextOptions[col.Key] = col.EnumOptions.Select(e => new ExcelFilterOption
                {
                    Value = e.Value,
                    Display = e.Display,
                    Count = 0
                }).ToList();
            }
        }

        // 补充布尔列筛选选项
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "boolean" && !_filterContextOptions.ContainsKey(col.Key))
            {
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = "True", Display = col.BoolTrueLabel ?? "是", Count = 0 },
                    new() { Value = "False", Display = col.BoolFalseLabel ?? "否", Count = 0 }
                };
            }
        }
    }

    // ========== ExcelFilter 事件 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task ToggleSort(string sortKey)
    {
        if (sortColumn == sortKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = sortKey;
            sortDescending = false;
        }
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnInboundDateFromChanged(string value)
    {
        _inboundDateFrom = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnInboundDateToChanged(string value)
    {
        _inboundDateTo = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 选择列操作 ==========

    private void ToggleSelection(string batchNo)
    {
        if (_selectedIds.Contains(batchNo))
            _selectedIds.Remove(batchNo);
        else
            _selectedIds.Add(batchNo);
    }

    private bool IsSelected(string batchNo) => _selectedIds.Contains(batchNo);

    private void SelectAllItems()
    {
        var allIds = _pageItems.Select(i => i.InventoryBatchNo).ToList();
        var allSelected = allIds.All(id => _selectedIds.Contains(id));
        if (allSelected)
        {
            foreach (var id in allIds) _selectedIds.Remove(id);
        }
        else
        {
            foreach (var id in allIds) _selectedIds.Add(id);
        }
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("warehouse_pending_delivery", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    // ========== 打印 ==========

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private object GetPrintValue(PendingDeliveryItemDto item, ColumnDef col)
    {
        // 优先用 DisplayConverter 转换
        if (col.DisplayConverter != null)
            return col.DisplayConverter(GetRawPropertyValue(item, col.Key)) ?? "";

        return col.Key switch
        {
            "remainingweight" => item.RemainingWeight.ToString("G29"),
            "remainingmeters" => item.RemainingMeters?.ToString("G29") ?? "-",
            "inbounddate" => item.InboundDate.ToString("yyyy-MM-dd"),
            "materialtype" => DisplayHelper.GetMaterialTypeText(item.MaterialType),
            "deliverystatus" => DisplayHelper.GetDeliveryStateText(item.DeliveryStatus),
            _ => GetRawPropertyValue(item, col.Key) ?? "-"
        };
    }

    private object? GetRawPropertyValue(PendingDeliveryItemDto item, string key) => key switch
    {
        "inventorybatchno" => item.InventoryBatchNo,
        "materialtype" => DisplayHelper.GetMaterialTypeText(item.MaterialType),
        "inboundsource" => item.InboundSource,
        "sourcename" => item.SourceName,
        "productionbatchno" => item.ProductionBatchNo,
        "heatno" => item.HeatNo,
        "plantgrade" => item.PlantGrade,
        "specification" => item.Specification,
        "lengthstatus" => item.LengthStatus,
        "minlength" => item.MinLength,
        "maxlength" => item.MaxLength,
        "remainingquantity" => item.RemainingQuantity,
        "remainingweight" => item.RemainingWeight,
        "remainingmeters" => item.RemainingMeters,
        "inbounddate" => item.InboundDate,
        "salesorderno" => item.SalesOrderNo,
        "productionmainno" => item.ProductionMainNo,
        "workorderno" => item.WorkOrderNo,
        "customername" => item.CustomerName,
        "salesman" => item.Salesman,
        "endcustomer" => item.EndCustomer,
        "productstandard" => item.ProductStandard,
        "deliverystatus" => item.DeliveryStatus,
        "standardgrade" => item.StandardGrade,
        _ => null
    };

    private async Task PrintAll()
    {
        try
        {
            var allItems = _pageItems.Select(item =>
            {
                var dict = new Dictionary<string, object>();
                foreach (var col in _visibleColumns)
                    dict[col.Key] = GetPrintValue(item, col);
                return dict;
            }).ToList();

            var request = new
            {
                title = "待发货订单成品",
                items = allItems,
                columns = GetPrintColumnDefs()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/pending-delivery/print-all-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task PrintSelected()
    {
        if (!_selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的项", Severity.Warning);
            return;
        }
        try
        {
            var selectedItems = _pageItems
                .Where(i => _selectedIds.Contains(i.InventoryBatchNo))
                .Select(item =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var col in _visibleColumns)
                        dict[col.Key] = GetPrintValue(item, col);
                    return dict;
                }).ToList();

            var request = new
            {
                title = "待发货订单成品",
                items = selectedItems,
                columns = GetPrintColumnDefs()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/pending-delivery/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(PendingDeliveryItemDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "inboundsource":
                builder.AddContent(0, DisplayHelper.GetInboundSourceText(item.InboundSource));
                break;
            case "lengthstatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus?.ToString()));
                break;
            case "remainingweight":
                builder.AddContent(0, item.RemainingWeight.ToString("G29"));
                break;
            case "remainingmeters":
                builder.AddContent(0, item.RemainingMeters?.ToString("G29") ?? "-");
                break;
            case "minlength":
                builder.AddContent(0, item.MinLength?.ToString("G29") ?? "-");
                break;
            case "maxlength":
                builder.AddContent(0, item.MaxLength?.ToString("G29") ?? "-");
                break;
            case "inbounddate":
                builder.AddContent(0, item.InboundDate.ToString("yyyy-MM-dd"));
                break;
            case "customername":
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "table-cell-clamp-1");
                builder.AddAttribute(2, "title", item.CustomerName ?? "-");
                builder.AddContent(3, item.CustomerName ?? "-");
                builder.CloseElement();
                break;
            case "salesorderno":
                builder.AddContent(0, item.SalesOrderNo ?? "-");
                break;
            case "productionmainno":
                builder.AddContent(0, item.ProductionMainNo ?? "-");
                break;
            case "workorderno":
                builder.AddContent(0, item.WorkOrderNo ?? "-");
                break;
            case "productionbatchno":
                builder.AddContent(0, item.ProductionBatchNo ?? "-");
                break;
            case "heatno":
                builder.AddContent(0, item.HeatNo ?? "-");
                break;
            case "inventorybatchno":
                builder.AddContent(0, item.InventoryBatchNo);
                break;
            case "plantgrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "specification":
                builder.AddContent(0, item.Specification);
                break;
            case "remainingquantity":
                builder.AddContent(0, item.RemainingQuantity);
                break;
            case "materialtype":
                builder.AddContent(0, DisplayHelper.GetMaterialTypeText(item.MaterialType));
                break;
            case "sourcename":
                builder.AddContent(0, item.SourceName);
                break;
            case "salesman":
                builder.AddContent(0, item.Salesman ?? "-");
                break;
            case "endcustomer":
                builder.AddContent(0, item.EndCustomer ?? "-");
                break;
            case "productstandard":
                builder.AddContent(0, item.ProductStandard ?? "-");
                break;
            case "deliverystatus":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryStatus));
                break;
            case "standardgrade":
                builder.AddContent(0, item.StandardGrade ?? "-");
                break;
        }
    };

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("warehouse_pending_delivery", null);
        if (saved.Count > 0)
        {
            foreach (var s in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null)
                    match.Visible = s.Visible;
            }
            var reordered = new List<ColumnDef>();
            foreach (var s in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null && !reordered.Contains(match))
                    reordered.Add(match);
            }
            foreach (var c in _allColumns)
            {
                if (!reordered.Contains(c))
                    reordered.Add(c);
            }
            _allColumns = reordered;
        }

        var savedState = await PageState.LoadAsync("warehouse_pending_delivery");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "inventorybatchno";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            if (savedState.Extras?.ContainsKey("columnFilters") == true)
            {
                try
                {
                    var raw = savedState.Extras["columnFilters"];
                    var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(raw);
                    if (dict != null)
                        _columnFilters = dict.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value));
                }
                catch { }
            }
        }

        if (savedState != null)
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);

        if (savedState?.Extras?.ContainsKey("inboundDateFrom") == true)
            _inboundDateFrom = savedState.Extras["inboundDateFrom"] ?? string.Empty;
        if (savedState?.Extras?.ContainsKey("inboundDateTo") == true)
            _inboundDateTo = savedState.Extras["inboundDateTo"] ?? string.Empty;

        if (savedState != null && table != null)
            await table.ReloadServerData();
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#pending-delivery-table");
        }
        catch { }
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#pending-delivery-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_inboundDateFrom))
            extras["inboundDateFrom"] = _inboundDateFrom;
        if (!string.IsNullOrEmpty(_inboundDateTo))
            extras["inboundDateTo"] = _inboundDateTo;
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("warehouse_pending_delivery", state);
    }
}
