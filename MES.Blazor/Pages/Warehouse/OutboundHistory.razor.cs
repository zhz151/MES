using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using System.Text.Json;

namespace MES.Blazor.Pages.Warehouse;

public partial class OutboundHistory
{
    [Parameter]
    public string? Code { get; set; }

    private int? _filterWarehouseId;
    private string _warehouseName = string.Empty;
    private List<OutboundRecordDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;

    // 日期搜索
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;


    // 排序状态
    private string sortColumn = "outbounddate";
    private bool sortDescending = true;

    // ========== ExcelFilter 状态 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 编辑状态
    private int? _savingItemId;
    private long? _editingRowId;
    private MudTable<OutboundRecordDto>? _table;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private bool _isArrowNavSetup;
    private int _pageSize = 10;
    // B33 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "OutboundQuantity", "OutboundWeight", "OutboundMeters"
    };
    private string _lastResolvedWarehouseCode = string.Empty;
    private bool _allSelected;
    private bool allSelected
    {
        get => _allSelected;
        set
        {
            if (_allSelected == value) return;
            _allSelected = value;
            if (value)
            {
                foreach (var item in _pageItems)
                    _selectedItems.Add(item);
            }
            else
            {
                _selectedItems.Clear();
            }
            StateHasChanged();
        }
    }

    // 编辑暂存状态
    private Dictionary<long, string> _editDateStrings = new();
    private Dictionary<long, string> _editOutboundTypes = new();

    private void ClearEditState()
    {
        _editingRowId = null;
        _editDateStrings.Clear();
        _editOutboundTypes.Clear();
    }

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();
    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "BatchNo",          Label = "仓库批次", SortKey = "batchno", FilterType = "string", Width = "120" },
        new() { Key = "OutboundDate",     Label = "出库日期", SortKey = "outbounddate",     IsRequired = true, Width = "120" },
        new() { Key = "OutboundType",     Label = "出库类型", SortKey = "outboundtype",     IsRequired = true, FilterType = "enum", Width = "120",
            EnumOptions = new() { new("SalesOut", "销售出库"), new("SubcontractOut", "委外出库"), new("ReturnOut", "退货出库"), new("ProductionPick", "生产领用"), new("InspectionPick", "检验领用"), new("TransferOut", "移库出库"), new("OtherOut", "其他出库") } },
        new() { Key = "SourceOrderNo",    Label = "物料单号", SortKey = "sourceorderno", FilterType = "string", Width = "120" },
        new() { Key = "TargetCompany",    Label = "目标单位", SortKey = "targetcompany", FilterType = "string", Width = "120" },
        new() { Key = "OutboundQuantity", Label = "出库支数", SortKey = "outboundquantity", IsRequired = true, Width = "80" },
        new() { Key = "OutboundWeight",   Label = "出库重量", SortKey = "outboundweight",   IsRequired = true, Width = "80" },
        new() { Key = "OutboundMeters",   Label = "出库米数", SortKey = "outboundmeters",   Width = "80" },
        new() { Key = "Remark",           Label = "备注", SortKey = "remark", FilterType = "string", Width = "120" },
        new() { Key = "CreatedBy",        Label = "创建人",   SortKey = "createdby", FilterType = "string", Width = "100" },
    };

    private static void ApplyWarehouseDefaults(List<ColumnDef> cols, string whCode)
    {
        foreach (var c in cols)
        {
            c.IsApplicable = true;
            c.Visible = true;
        }
        // 所有仓库全部字段适用
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        var whCode = Code?.ToUpperInvariant() ?? "";
        await ColumnPrefs.SaveAsync("outbound_history", whCode, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        ApplyWarehouseDefaults(_allColumns, Code?.ToUpperInvariant() ?? "");
        await SaveColumnPrefs();
        if (_table != null) await _table.ReloadServerData();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<OutboundRecordDto>> LoadDataFromServer(TableState state)
    {
        try
        {
            _pageSize = state.PageSize;
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "outbounddate";
            var filtersJson = SerializeFilters();

            // 恢复持久化的页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var query = new OutboundQueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                SortBy = sortBy,
                IsDescending = sortDescending,
                WarehouseId = _filterWarehouseId,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                StartDate = DateTime.TryParse(_dateFrom, out var df) ? df : null,
                EndDate = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
            };

            var result = await InventoryService.GetOutboundRecordsAsync(query, filtersJson);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
                ComputePageSums();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
                _pageSums.Clear();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        return new TableData<OutboundRecordDto>
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
            var result = await InventoryService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                BuildFilterContextOptions(result.Data);
            }
            else
            {
                Snackbar.Add($"获取筛选选项失败: {result.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"获取筛选选项异常: {ex.Message}", Severity.Error);
        }
    }

    private void BuildFilterContextOptions(Dictionary<string, List<string>> filterContexts)
    {
        _filterContextOptions.Clear();
        foreach (var kvp in filterContexts)
        {
            _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = v,
                Count = 0
            }).ToList();
        }

        // OutboundType 列显示中文
        if (_filterContextOptions.TryGetValue("OutboundType", out var typeOptions))
        {
            foreach (var opt in typeOptions)
            {
                opt.Display = GetOutboundTypeText(opt.Value);
            }
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
        if (_table != null) await _table.ReloadServerData();
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
        if (_table != null) await _table.ReloadServerData();
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        ClearEditState();
        await SavePageStateAsync();
        if (_table != null) await _table.ReloadServerData();
    }

    // ========== 日期搜索 ==========

    private async Task OnDateFromChanged(string value)
    {
        _dateFrom = value ?? string.Empty;
        await SavePageStateAsync();
        if (_table != null) await _table.ReloadServerData();
    }

    private async Task OnDateToChanged(string value)
    {
        _dateTo = value ?? string.Empty;
        await SavePageStateAsync();
        if (_table != null) await _table.ReloadServerData();
    }

    // ========== 编辑器类型推断 ==========

    private static string GetEditorType(string key) => key switch
    {
        "OutboundType" => "select",
        "OutboundQuantity" => "int",
        "OutboundWeight" => "decimal",
        "OutboundMeters" => "decimal",
        "OutboundDate" => "date",
        _ => "text"
    };

    private static bool IsReadOnlyField(string key) => key switch
    {
        "BatchNo" or "CreatedBy" => true,
        _ => false
    };

    private readonly List<(string Value, string Text)> _outboundTypeOptions = new()
    {
        ("SalesOut", "销售出库"),
        ("SubcontractOut", "委外出库"),
        ("ReturnOut", "退货出库"),
        ("ProductionPick", "生产领用"),
        ("InspectionPick", "检验领用"),
        ("TransferOut", "移库出库"),
        ("OtherOut", "其他出库"),
    };

    // ========== 只读单元格渲染 ==========

    private RenderFragment RenderCell(OutboundRecordDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "BatchNo":
                builder.AddContent(0, item.BatchNo);
                break;
            case "OutboundDate":
                builder.AddContent(0, item.OutboundDate.ToString("yyyy-MM-dd"));
                break;
            case "OutboundType":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", Color.Warning);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetOutboundTypeText(item.OutboundType))));
                builder.CloseComponent();
                break;
            case "SourceOrderNo":
                if (!string.IsNullOrEmpty(item.SourceOrderNo))
                    builder.AddContent(0, item.SourceOrderNo);
                break;
            case "TargetCompany":
                builder.AddContent(0, item.TargetCompany);
                break;
            case "OutboundQuantity":
                builder.AddContent(0, item.OutboundQuantity);
                break;
            case "OutboundWeight":
                builder.AddContent(0, ((int)item.OutboundWeight).ToString());
                break;
            case "OutboundMeters":
                if (item.OutboundMeters.HasValue)
                    builder.AddContent(0, ((int)item.OutboundMeters.Value).ToString());
                break;
            case "Remark":
                builder.AddContent(0, item.Remark);
                break;
            case "CreatedBy":
                builder.AddContent(0, item.CreatedBy);
                break;
        }
    };

    // ========== 内联编辑渲染 ==========

    private RenderFragment RenderInlineEditor(OutboundRecordDto item, ColumnDef col) => builder =>
    {
        if (IsReadOnlyField(col.Key))
        {
            RenderCell(item, col)(builder);
            return;
        }

        switch (GetEditorType(col.Key))
        {
            case "select":
                if (!_editOutboundTypes.ContainsKey(item.Id))
                    _editOutboundTypes[item.Id] = item.OutboundType.ToString();
                var selVal = _editOutboundTypes[item.Id];
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Value", selVal);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v =>
                {
                    _editOutboundTypes[item.Id] = v ?? item.OutboundType.ToString();
                }));
                builder.AddAttribute(5, "ChildContent", (RenderFragment)(b2 =>
                {
                    foreach (var opt in _outboundTypeOptions)
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", opt.Value);
                        b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt.Text)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                break;

            case "int":
                builder.OpenComponent<MudNumericField<int>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "HideSpinButtons", true);
                builder.AddAttribute(3, "Value", item.OutboundQuantity);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<int>(this, v => item.OutboundQuantity = v));
                builder.CloseComponent();
                break;

            case "decimal":
                if (col.Key == "OutboundMeters")
                {
                    var metersVal = item.OutboundMeters ?? 0m;
                    builder.OpenComponent<MudNumericField<decimal>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "HideSpinButtons", true);
                    builder.AddAttribute(3, "Format", "G29");
                    builder.AddAttribute(4, "Value", metersVal);
                    builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<decimal>(this, v => item.OutboundMeters = v));
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudNumericField<decimal>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "HideSpinButtons", true);
                    builder.AddAttribute(3, "Format", "G29");
                    builder.AddAttribute(4, "Value", item.OutboundWeight);
                    builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<decimal>(this, v => item.OutboundWeight = v));
                    builder.CloseComponent();
                }
                break;

            case "date":
                if (!_editDateStrings.ContainsKey(item.Id))
                    _editDateStrings[item.Id] = item.OutboundDate.ToString("yyyy-MM-dd");
                var dateVal = _editDateStrings[item.Id];
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Value", dateVal);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v =>
                {
                    _editDateStrings[item.Id] = v ?? item.OutboundDate.ToString("yyyy-MM-dd");
                    if (DateTime.TryParse(v, out var dt))
                        item.OutboundDate = dt;
                }));
                builder.CloseComponent();
                break;

            default: // text
                var txtVal = col.Key switch
                {
                    "SourceOrderNo" => item.SourceOrderNo,
                    "TargetCompany" => item.TargetCompany,
                    "Remark" => item.Remark,
                    _ => ""
                };
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Value", txtVal);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v =>
                {
                    switch (col.Key)
                    {
                        case "SourceOrderNo": item.SourceOrderNo = v; break;
                        case "TargetCompany": item.TargetCompany = v; break;
                        case "Remark": item.Remark = v; break;
                    }
                }));
                builder.CloseComponent();
                break;
        }
    };

    // ========== 编辑状态管理 ==========

    private void StartEdit(OutboundRecordDto item)
    {
        _editingRowId = item.Id;
        _editDateStrings[item.Id] = item.OutboundDate.ToString("yyyy-MM-dd");
        _editOutboundTypes[item.Id] = item.OutboundType.ToString();
    }

    private void CancelEdit()
    {
        ClearEditState();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        // 预加载仓库列表
        var warehouses = await WarehouseService.GetAllAsync(true);
        if (warehouses.Success && warehouses.Data != null)
            _warehouses = warehouses.Data;

        await ResolveWarehouse();

        // 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("outboundhistory");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "outbounddate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
            if (savedState.Extras?.ContainsKey("dateFrom") == true)
                _dateFrom = savedState.Extras["dateFrom"] ?? string.Empty;
            if (savedState.Extras?.ContainsKey("dateTo") == true)
                _dateTo = savedState.Extras["dateTo"] ?? string.Empty;
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

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && _table != null)
            await _table.ReloadServerData();

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#outbound-history-list-table"))
                _isArrowNavSetup = false;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(Code))
        {
            var prevCode = _lastResolvedWarehouseCode;
            await ResolveWarehouse();
            // 仅在仓库代码实际变更时重新加载数据（OnInitializedAsync 已完成首次加载）
            if (!string.Equals(prevCode, _lastResolvedWarehouseCode, StringComparison.OrdinalIgnoreCase))
            {
                ClearEditState();
                if (_table != null) await _table.ReloadServerData();
            }
        }
    }

    private List<WarehouseDto> _warehouses = new();

    private async Task ResolveWarehouse()
    {
        var whCode = Code?.ToUpperInvariant() ?? "";

        // 切换仓库时清空该仓库的筛选状态（首次加载不清空，由 OnInitializedAsync 恢复持久化状态）
        if (!string.IsNullOrEmpty(_lastResolvedWarehouseCode) &&
            !string.Equals(_lastResolvedWarehouseCode, whCode, StringComparison.OrdinalIgnoreCase))
        {
            _searchKeyword = string.Empty;
            _columnFilters.Clear();
        }
        _lastResolvedWarehouseCode = whCode;
        var wh = _warehouses.FirstOrDefault(w => w.Code.Equals(whCode, StringComparison.OrdinalIgnoreCase));
        if (wh != null)
        {
            _filterWarehouseId = wh.Id;
            _warehouseName = wh.Name;
        }
        else if (!string.IsNullOrEmpty(Code))
        {
            _filterWarehouseId = null;
            _warehouseName = Code;
        }

        // 列定义
        _allColumns = GetAllColumnDefs();
        ApplyWarehouseDefaults(_allColumns, whCode);

        // 加载用户偏好
        var saved = await ColumnPrefs.LoadAsync("outbound_history", whCode);
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

        // 不适用列强制隐藏
        foreach (var c in _allColumns)
        {
            if (!c.IsApplicable) c.Visible = false;
        }
    }

    // ========== 单元格原始值/显示值 ==========

    private string? GetCellRawValue(OutboundRecordDto item, string key) => key switch
    {
        "BatchNo" => item.BatchNo,
        "OutboundDate" => item.OutboundDate.ToString("yyyy-MM-dd"),
        "OutboundType" => DisplayHelper.GetOutboundTypeText(item.OutboundType),
        "SourceOrderNo" => item.SourceOrderNo,
        "TargetCompany" => item.TargetCompany,
        "OutboundQuantity" => item.OutboundQuantity.ToString("G29"),
        "OutboundWeight" => item.OutboundWeight.ToString("G29"),
        "OutboundMeters" => item.OutboundMeters?.ToString("G29"),
        "Remark" => item.Remark,
        "CreatedBy" => item.CreatedBy,
        _ => null
    };

    private string? GetCellDisplayText(OutboundRecordDto item, string key) => key switch
    {
        "OutboundType" => GetOutboundTypeText(item.OutboundType),
        _ => GetCellRawValue(item, key) ?? ""
    };

    // ========== 行保存 ==========

    private async Task SaveRow(OutboundRecordDto item)
    {
        var errors = new List<string>();
        var parsedDate = item.OutboundDate;
        if (_editDateStrings.TryGetValue(item.Id, out var dateStr))
        {
            if (!DateTime.TryParse(dateStr, out parsedDate))
                errors.Add("出库日期无效，请按 yyyy-MM-dd 格式输入");
        }
        if (item.OutboundQuantity < 1) errors.Add("出库支数必须大于0");
        if (item.OutboundWeight <= 0) errors.Add("出库重量必须大于0");

        if (errors.Any())
        {
            Snackbar.Add(string.Join("\n", errors), Severity.Error);
            return;
        }

        _savingItemId = 1;
        StateHasChanged();

        try
        {
            var outboundType = _editOutboundTypes.TryGetValue(item.Id, out var typ) ? Enum.Parse<OutboundType>(typ) : item.OutboundType;

            var request = new UpdateOutboundRecordRequest
            {
                OutboundType = outboundType.ToString(),
                OutboundDate = parsedDate,
                SourceOrderNo = string.IsNullOrEmpty(item.SourceOrderNo) ? null : item.SourceOrderNo,
                TargetCompany = string.IsNullOrEmpty(item.TargetCompany) ? null : item.TargetCompany,
                OutboundQuantity = item.OutboundQuantity,
                OutboundWeight = item.OutboundWeight,
                OutboundMeters = item.OutboundMeters,
                Remark = string.IsNullOrEmpty(item.Remark) ? null : item.Remark,
            };

            var result = await InventoryService.UpdateOutboundRecordAsync(item.Id, request);
            if (result.Success)
            {
                Snackbar.Add("更改成功", Severity.Success);
                item.OutboundDate = parsedDate;
                item.OutboundType = outboundType;
                _editingRowId = null;
                _editDateStrings.Remove(item.Id);
                _editOutboundTypes.Remove(item.Id);
                if (_table != null) await _table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "更改失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"网络错误: {ex.Message}", Severity.Error);
        }
        finally
        {
            _savingItemId = null;
            StateHasChanged();
        }
    }

    // ========== 删除 ==========

    private async Task ConfirmDelete(OutboundRecordDto item)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认",
            new DialogParameters
            {
                { "ContentText", $"确认物理删除出库记录（批次：{item.BatchNo}，日期：{item.OutboundDate:yyyy-MM-dd}）？\n此操作不可恢复！" },
                { "ConfirmText", "确认删除" },
                { "Color", Color.Error }
            });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await InventoryService.HardDeleteOutboundRecordAsync(item.Id);
            if (result.Success)
            {
                Snackbar.Add("出库记录已删除", Severity.Success);
                ClearEditState();
                if (_table != null) await _table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"网络错误: {ex.Message}", Severity.Error);
        }
    }

    // ========== 多选 ==========
    private HashSet<OutboundRecordDto> _selectedItems = new();

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!_selectedItems.Any())
        {
            Snackbar.Add("请先选择要打印的出库记录", Severity.Warning);
            return;
        }
        try
        {
            var columns = _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();
            var ids = _selectedItems.Select(i => i.Id).ToArray();
            var request = new OutboundPrintSelectedRequest
            {
                Ids = ids,
                Columns = columns
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/inventory/print-outbound-selected-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    private async Task PrintAll()
    {
        try
        {
            var columns = _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();
            var request = new OutboundPrintAllRequest
            {
                Keyword = string.IsNullOrEmpty(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                WarehouseId = _filterWarehouseId,
                StartDate = DateTime.TryParse(_dateFrom, out var df) ? df : null,
                EndDate = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
                Columns = columns
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/inventory/print-outbound-all-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== 辅助方法 ==========

    private static string GetOutboundTypeText(OutboundType type) => DisplayHelper.GetOutboundTypeText(type);
    private static string GetOutboundTypeText(string type) => DisplayHelper.GetOutboundTypeText(type);

    private void GoBack() => Navigation.NavigateTo(!string.IsNullOrEmpty(Code) ? $"/warehouse/{Code.ToLowerInvariant()}" : "/warehouse");

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo)) extras["dateTo"] = _dateTo;
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("outboundhistory", state);
    }

    // ========== B33 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        var props = typeof(OutboundRecordDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var key in _summableColumnKeys)
        {
            var prop = props.FirstOrDefault(p => p.Name == key);
            if (prop == null) continue;
            decimal sum = 0;
            foreach (var item in _pageItems)
            {
                var val = prop.GetValue(item);
                if (val == null) continue;
                sum += Convert.ToDecimal(val);
            }
            _pageSums[key] = ((int)sum).ToString();
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }
}
