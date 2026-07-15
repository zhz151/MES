using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.WorkOrder;
using System.Text.Json;

namespace MES.Blazor.Pages.Batches;

public partial class Batches
{
    private MudTable<ProductionBatchListDto>? table;
    private List<ProductionBatchListDto> _pageItems = new();
    private int _totalCount;
    private HashSet<int> selectedIds = new();
    private List<BatchWorkOrderMismatchDto> _workOrderMismatches = new();
    private List<PendingPlanBatchDto> _pendingInProcessReworkPlans = new();
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
                    selectedIds.Add(item.Id);
            }
            else
            {
                selectedIds.Clear();
            }
            StateHasChanged();
        }
    }
    private int _currentPageIndex;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    // 排序
    private string sortColumn = "batchno";
    private bool sortDescending = true;

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();

    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "CurrentValidQty", "CurrentValidWeight",
        "TotalQuantity", "TotalMeters", "TotalWeight",
    };

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // ===== Group 1: 批次基本信息（含原料库存字段） =====
        new() { Key = "BatchNo",            Label = "生产编号", SortKey = "batchno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次基本信息" },
        new() { Key = "TagNo",              Label = "挂牌号",   SortKey = "tagno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次基本信息" },
        new() { Key = "Status",             Label = "状态",     SortKey = "status", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "批次基本信息",
            EnumOptions = new() { new("None", "未产"), new("InProgress", "在产"), new("Completed", "完成"), new("Suspended", "挂起"), new("Cancelled", "作废") } },
        new() { Key = "ProductionType",     Label = "生产类型", SortKey = "productiontype", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "批次基本信息",
            EnumOptions = new() { new("RoughTube", "荒管生产"), new("InProcess", "在制生产"), new("Inventory", "库存"),
                new("OutsourcedPurchased", "外购"), new("Rework", "返整"), new("Subcontract", "委外生产"), new("ExternalProcessing", "对外加工") } },
        new() { Key = "ManufacturingItem",  Label = "制造物品", SortKey = "manufacturingitem", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "批次基本信息",
            EnumOptions = new() { new("OrderFinishedProduct", "订单成品"), new("PreparedMaterial", "备料成品"),
                new("SurplusStock", "余库料"), new("SpecialDeliveryStatus", "特定交态成品") } },
        new() { Key = "ProductionRatio",    Label = "制成倍数", SortKey = "productionratio", Width = "80", GroupKey = 1, GroupName = "批次基本信息" },
        new() { Key = "CurrentValidQty",    Label = "现有效原料支数", SortKey = "currentvalidqty", Width = "80", GroupKey = 1, GroupName = "批次基本信息" },
        new() { Key = "CurrentValidWeight",  Label = "现有效原料重量", SortKey = "currentvalidweight", Width = "80", GroupKey = 1, GroupName = "批次基本信息" },
        new() { Key = "ValidInputQuestion",   Label = "有效投料疑问", SortKey = null, FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "批次基本信息",
            EnumOptions = new() { new("True", "疑问"), new("False", "正常") } },
        new() { Key = "CreatedBy",          Label = "创建人",   SortKey = "createdby", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次基本信息" },
        new() { Key = "CreatedTime",        Label = "创建时间", SortKey = "createdtime", Width = "120", GroupKey = 1, GroupName = "批次基本信息" },
        new() { Key = "UpdatedTime",        Label = "最后更新时间", SortKey = "updatedtime", Width = "120", GroupKey = 1, GroupName = "批次基本信息" },

        // ===== Group 2: 工单信息 =====
        // -- 编号 --
        new() { Key = "WorkOrderNo",        Label = "工单号",   SortKey = "workorderno", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "SalesOrderNo",       Label = "订单号",   SortKey = "salesorderno", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "ProductionMainNo",   Label = "主号",     SortKey = "productionmainno", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "ProductionSubNo",    Label = "次号",     SortKey = "productionsubno", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        // -- 日期 --
        new() { Key = "SignDate",           Label = "签订日期", SortKey = "signdate", FilterType = "date", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "DeliveryDate",       Label = "交货日期", SortKey = "deliverydate", FilterType = "date", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        // -- 人员/客户 --
        new() { Key = "Salesman",           Label = "业务员",   SortKey = "salesman", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "EndCustomer",        Label = "最终用户", SortKey = "endcustomer", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        // -- 商务条款 --
        new() { Key = "DelayPenalty",       Label = "延期罚款", SortKey = "delaypenalty", FilterType = "enum", Width = "120", GroupKey = 2, GroupName = "工单信息",
            EnumOptions = new() { new("True", "是"), new("False", "否") } },
        new() { Key = "SettlementMethod",   Label = "结算方式", SortKey = "settlementmethod", FilterType = "enum", Width = "120", GroupKey = 2, GroupName = "工单信息",
            EnumOptions = new() { new("Theoretical", "理算"), new("Weighing", "过磅"), new("WeighingNegative", "过磅-负") } },
        new() { Key = "MaterialName",       Label = "物料名称", SortKey = "materialname", FilterType = "enum", Width = "120", GroupKey = 2, GroupName = "工单信息",
            EnumOptions = new() { new("SeamlessPipe", "无缝管"), new("WeldedPipe", "焊管") } },
        // -- 产品要求 --
        new() { Key = "StandardCode",       Label = "产品标准", SortKey = "standardcode", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "DeliveryState",      Label = "交货状态", SortKey = "deliverystate", FilterType = "enum", Width = "120", GroupKey = 2, GroupName = "工单信息",
            EnumOptions = new() { new("SolutionAnnealedAndPickled", "固溶酸洗"), new("SolutionAnnealedAndPickledUTube", "固溶酸洗-U型管"),
                new("SolutionAnnealedAndPickledExternalPolished", "固溶酸洗-外抛光"), new("SolutionAnnealedAndPickledInternalPolished", "固溶酸洗-内抛光"),
                new("SolutionAnnealedAndPickledBothPolished", "固溶酸洗-内外抛光"), new("SolutionAnnealedAndPickledCoiled", "固溶酸洗-盘管"),
                new("Bright", "光亮"), new("BrightUTube", "光亮-U型管"), new("BrightCoiled", "光亮-盘管"), new("Hard", "硬态") } },
        new() { Key = "PlantGrade",         Label = "工厂牌号", SortKey = "plantgrade", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "Specification",      Label = "规格",     SortKey = "specification", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "LengthStatus",       Label = "长度状态", SortKey = "lengthstatus", FilterType = "enum", Width = "120", GroupKey = 2, GroupName = "工单信息",
            EnumOptions = new() { new("Fixed", "定尺"), new("Range", "范围尺"), new("NonFixed", "非定尺") } },
        // -- 数量汇总 --
        new() { Key = "TotalQuantity",      Label = "总支数",   SortKey = "totalquantity", Width = "80", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "TotalMeters",        Label = "总米数",   SortKey = "totalmeters", Width = "80", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "TotalWeight",        Label = "总重量",   SortKey = "totalweight", Width = "80", GroupKey = 2, GroupName = "工单信息" },
        new() { Key = "TechnicalRequirements", Label = "技术要求", SortKey = "technicalrequirements", FilterType = "enum", Width = "120", GroupKey = 2, GroupName = "工单信息",
            EnumOptions = new() { new("Normal", "普通"), new("Special", "特殊") } },

        // ===== Group 3: 生产执行 =====
        new() { Key = "CurrentExecDate",    Label = "截止执行日", SortKey = "currentexecdate", FilterType = "date", Width = "120", GroupKey = 3, GroupName = "生产执行" },
        new() { Key = "CurrentGroupName",   Label = "当前工序", SortKey = "currentgroupname", FilterType = "string", Width = "120", GroupKey = 3, GroupName = "生产执行" },
        new() { Key = "CurrentSectionName", Label = "当前工段", SortKey = "currentsectionname", FilterType = "string", Width = "120", GroupKey = 3, GroupName = "生产执行" },
        new() { Key = "CurrentSectionCompleted", Label = "工段完工", SortKey = null, FilterType = "enum", Width = "120", GroupKey = 3, GroupName = "生产执行",
            EnumOptions = new() { new("True", "完工"), new("False", "生产中") } },
        new() { Key = "RemainingWorkDays",     Label = "剩余工量", SortKey = "remainingworkdays", Width = "80", GroupKey = 3, GroupName = "生产执行" },
        new() { Key = "CurrentEquipmentName", Label = "当前设备", SortKey = "currentequipmentname", FilterType = "string", Width = "120", GroupKey = 3, GroupName = "生产执行" },
        new() { Key = "CurrentOutsource",   Label = "当前委外", SortKey = "currentoutsource", FilterType = "string", Width = "120", GroupKey = 3, GroupName = "生产执行" },
        new() { Key = "CurrentSpec",        Label = "当前规格", SortKey = "currentspec", FilterType = "string", Width = "120", GroupKey = 3, GroupName = "生产执行" },
        new() { Key = "NextSectionName",    Label = "下一工段", SortKey = "nextsectionname", FilterType = "string", Width = "120", GroupKey = 3, GroupName = "生产执行" },
        new() { Key = "CorrespondingSpec",  Label = "对应规格", SortKey = "correspondingspec", FilterType = "string", Width = "120", GroupKey = 3, GroupName = "生产执行" },
        new() { Key = "NextProcess",        Label = "下一工序", SortKey = "nextprocess", FilterType = "string", Width = "120", GroupKey = 3, GroupName = "生产执行" },
    };

    // ========== 分页汇总计算 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(ProductionBatchListDto)
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
            catch
            {
                // ignore individual column sum errors
            }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ProductionBatchListDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var sortCol = _allColumns.FirstOrDefault(c => c.Key == sortColumn);
            var sortBy = sortCol?.SortKey ?? sortColumn ?? "batchno";
            var filtersJson = SerializeFilters();

            var query = new BatchQueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
                StartDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
                StartDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
            };

            if (!string.IsNullOrEmpty(filtersJson))
            {
                try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson); }
                catch { }
            }

            var result = await BatchService.GetPagedAsync(query);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = result.Data.PageIndex;
                ComputePageSums();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
            }

            await SavePageStateAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        return new TableData<ProductionBatchListDto>
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
            var result = await BatchService.GetFilterContextsAsync();
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
            _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = v,
                Count = 0
            }).ToList();
        }

        // 枚举列显示中文标签
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "enum" && col.EnumOptions != null && _filterContextOptions.TryGetValue(col.Key, out var options))
            {
                var displayMap = col.EnumOptions.ToDictionary(e => e.Value, e => e.Display);
                foreach (var opt in options)
                {
                    if (displayMap.TryGetValue(opt.Value, out var display))
                        opt.Display = display;
                }
            }
        }

        // 布尔列显示中文标签
        if (_filterContextOptions.TryGetValue("DelayPenalty", out var dpOptions))
        {
            foreach (var opt in dpOptions)
            {
                opt.Display = opt.Value == "True" ? "是" : "否";
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
        if (selectedValues?.Any() == true)
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
        selectedIds.Clear();
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDateFromChanged(string value)
    {
        _dateFrom = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDateToChanged(string value)
    {
        _dateTo = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("batches", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        // 清除上次的工单号校验结果，避免跨导航残留旧警告
        _workOrderMismatches.Clear();

        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("batches", null);
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

        // 从 PageState 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("batches");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "batchno";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
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
            if (savedState.Extras?.TryGetValue("dateFrom", out var dateFrom) == true)
                _dateFrom = dateFrom ?? string.Empty;
            if (savedState.Extras?.TryGetValue("dateTo", out var dateTo) == true)
                _dateTo = dateTo ?? string.Empty;
        }

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();
        await LoadFilterContextsAsync();

        // 自动加载待处理在产改制计划通知
        await LoadPendingInProcessReworkPlansAsync();

        // 自动加载工单号不匹配通知
        await CheckWorkOrdersAsync();
    }

    // ========== 在产改制计划通知 ==========

    private async Task LoadPendingInProcessReworkPlansAsync()
    {
        try
        {
            var result = await MaterialPlanService.GetPendingInProcessReworkPlansAsync();
            if (result.Success && result.Data != null)
            {
                _pendingInProcessReworkPlans = result.Data;
            }
        }
        catch
        {
            // 静默失败，不影响页面加载
        }
    }

    // ========== 工单号验证 ==========

    private async Task CheckWorkOrdersAsync()
    {
        try
        {
            var verifyResult = await BatchService.VerifyWorkOrderNosAsync();
            if (verifyResult.Success && verifyResult.Data != null)
                _workOrderMismatches = verifyResult.Data;
            else
                _workOrderMismatches.Clear();
        }
        catch
        {
            _workOrderMismatches.Clear();
        }
    }

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/batches/create");
    private void ViewDetail(int id) => Navigation.NavigateTo($"/batches/{id}");
    private void GoToEdit(int id) => Navigation.NavigateTo($"/batches/{id}/edit");

    private async Task NavigateToWorkOrder(string workOrderNo)
    {
        if (workOrderNo == "非工单" || string.IsNullOrWhiteSpace(workOrderNo))
            return;

        try
        {
            var result = await WorkOrderService.GetByWorkOrderNoAsync(workOrderNo);
            if (result.Success && result.Data != null)
            {
                Navigation.NavigateTo($"/workorders/{result.Data.Id}");
            }
            else
            {
                Snackbar.Add($"未找到工单 {workOrderNo}", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"跳转失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task NavigateToSalesOrder(string salesOrderNo)
    {
        try
        {
            var result = await OrderService.GetIdByOrderNumberAsync(salesOrderNo);
            if (result.Success && result.Data.HasValue)
            {
                Navigation.NavigateTo($"/orders/{result.Data.Value}");
            }
            else
            {
                Snackbar.Add($"未找到订单 {salesOrderNo}", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"跳转失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(ProductionBatchListDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除生产批次 \"{item.BatchNo}\" 吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await BatchService.DeleteAsync(item.Id);
                if (result.Success)
                {
                    Snackbar.Add("删除成功", Severity.Success);
                    if (table != null) await table.ReloadServerData();
                }
                else
                {
                    Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"删除失败: {ex.Message}", Severity.Error);
            }
        }
    }

    // ========== 单元格渲染 ==========

    private string? GetCellRawValue(ProductionBatchListDto item, string key) => key switch
    {
        "BatchNo" => item.BatchNo,
        "TagNo" => item.TagNo,
        "CreatedTime" => item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "ProductionSubNo" => item.ProductionSubNo,
        "ProductionType" => item.ProductionType,
        "ManufacturingItem" => item.ManufacturingItem,
        "Status" => item.Status,
        "CurrentExecDate" => item.CurrentExecDate?.ToString("yyyy-MM-dd"),
        "CurrentGroupName" => item.CurrentGroupName,
        "CurrentSectionName" => item.CurrentSectionName,
        "CurrentEquipmentName" => item.CurrentEquipmentName,
        "CurrentOutsource" => item.CurrentOutsource,
        "CurrentSpec" => item.CurrentSpec,
        "NextSectionName" => item.NextSectionName,
        "CorrespondingSpec" => item.CorrespondingSpec,
        "CurrentValidQty" => DisplayHelper.FormatNullableInt(item.CurrentValidQty),
        "CurrentValidWeight" => $"{(int)(item.CurrentValidWeight ?? 0)}",
        "ProductionRatio" => item.ProductionRatio.ToString(),
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "Salesman" => item.Salesman,
        "EndCustomer" => item.EndCustomer,
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "DelayPenalty" => item.DelayPenalty.ToString(),
        "MaterialName" => item.MaterialName,
        "SettlementMethod" => item.SettlementMethod,
        "StandardCode" => item.StandardCode,
        "DeliveryState" => item.DeliveryState,
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "LengthStatus" => item.LengthStatus,
        "TotalQuantity" => item.TotalQuantity.ToString("G29"),
        "TotalMeters" => ((int)item.TotalMeters).ToString(),
        "TotalWeight" => ((int)item.TotalWeight).ToString(),
        "TechnicalRequirements" => item.TechnicalRequirements,
        "RemainingWorkDays" => item.RemainingWorkDays.ToString("G29"),
        "CreatedBy" => item.CreatedBy,
        _ => null
    };

    private static string GetColumnValue(ProductionBatchListDto item, string key) => key switch
    {
        "TagNo" => item.TagNo ?? "",
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "ProductionSubNo" => item.ProductionSubNo ?? "",
        "ProductionType" => DisplayHelper.GetProductionTypeText(item.ProductionType),
        "CurrentGroupName" => item.CurrentGroupName ?? "",
        "CurrentSectionName" => item.CurrentSectionName ?? "",
        "CurrentEquipmentName" => item.CurrentEquipmentName ?? "",
        "CurrentOutsource" => item.CurrentOutsource ?? "",
        "CurrentSpec" => item.CurrentSpec ?? "",
        "NextSectionName" => item.NextSectionName ?? "",
        "CorrespondingSpec" => item.CorrespondingSpec ?? "",
        "NextProcess" => item.NextProcess ?? "",
        "ManufacturingItem" => DisplayHelper.GetManufacturingItemText(item.ManufacturingItem),
        "CurrentValidQty" => DisplayHelper.FormatNullableInt(item.CurrentValidQty),
        "CurrentValidWeight" => $"{(int)(item.CurrentValidWeight ?? 0)}",
        "ProductionRatio" => item.ProductionRatio.ToString(),
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "Salesman" => item.Salesman,
        "EndCustomer" => item.EndCustomer ?? "",
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "DelayPenalty" => DisplayHelper.GetYesNoText(item.DelayPenalty),
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName),
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod),
        "StandardCode" => item.StandardCode,
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus),
        "TotalQuantity" => item.TotalQuantity.ToString("G29"),
        "TotalMeters" => ((int)item.TotalMeters).ToString(),
        "TotalWeight" => ((int)item.TotalWeight).ToString(),
        "TechnicalRequirements" => DisplayHelper.GetTechnicalRequirementsText(item.TechnicalRequirements),
        "ValidInputQuestion" => item.ValidInputQuestion.HasValue ? DisplayHelper.GetYesNoText(item.ValidInputQuestion.Value) : "",
        "CurrentSectionCompleted" => DisplayHelper.GetSectionCompletedText(item.CurrentSectionCompleted),
        "RemainingWorkDays" => item.RemainingWorkDays == 0 ? "0" : $"{item.RemainingWorkDays}天",
        "CreatedBy" => item.CreatedBy,
        _ => ""
    };

    private RenderFragment RenderCell(ProductionBatchListDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "BatchNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewDetail(item.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.BatchNo)));
                builder.CloseComponent();
                break;
            case "Status":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetBatchStatusColor(item.Status));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetBatchStatusText(item.Status))));
                builder.CloseComponent();
                break;
            case "CreatedTime":
                builder.AddContent(0, item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "WorkOrderNo":
                if (!string.IsNullOrEmpty(item.WorkOrderNo) && item.WorkOrderNo != "非工单")
                {
                    builder.OpenComponent<MudLink>(0);
                    builder.AddAttribute(1, "Typo", Typo.body2);
                    builder.AddAttribute(2, "Style", "cursor:pointer; color:#1976d2;");
                    builder.AddAttribute(3, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => NavigateToWorkOrder(item.WorkOrderNo)));
                    builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.WorkOrderNo)));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.WorkOrderNo ?? "");
                }
                break;
            case "SalesOrderNo":
                if (!string.IsNullOrEmpty(item.SalesOrderNo))
                {
                    builder.OpenComponent<MudLink>(0);
                    builder.AddAttribute(1, "Typo", Typo.body2);
                    builder.AddAttribute(2, "Style", "cursor:pointer; color:#1976d2;");
                    builder.AddAttribute(3, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => NavigateToSalesOrder(item.SalesOrderNo)));
                    builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.SalesOrderNo)));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "");
                }
                break;
            case "ValidInputQuestion":
                if (item.ValidInputQuestion.HasValue)
                {
                    var vq = item.ValidInputQuestion.Value;
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", vq ? Color.Warning : Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, vq ? "疑问" : "正常")));
                    builder.CloseComponent();
                }
                break;
            case "CurrentSectionCompleted":
                if (item.CurrentSectionCompleted.HasValue)
                {
                    var sc = item.CurrentSectionCompleted.Value;
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", sc ? Color.Success : Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, sc ? "完工" : "生产中")));
                    builder.CloseComponent();
                }
                break;
            case "CurrentExecDate":
                builder.AddContent(0, item.CurrentExecDate?.ToString("yyyy-MM-dd") ?? "");
                break;
            default:
                var val = GetColumnValue(item, col.Key);
                builder.AddContent(0, val);
                break;
        }
    };

    // ========== 打印方法 ==========

    private List<PrintColumnDef> GetPrintColumnDefs()
    {
        return _visibleColumns.Select(c => new PrintColumnDef
        {
            Key = c.Key,
            Label = c.Label
        }).ToList();
    }

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的批次", Severity.Warning);
            return;
        }
        try
        {
            var ids = selectedIds.ToArray();
            var request = new BatchPrintSelectedRequest
            {
                Ids = ids,
                Columns = GetPrintColumnDefs()
            };
            var apiUrl = $"{Http.BaseAddress}api/batch/print-selected-file";
            var json = JsonSerializer.Serialize(request);
            Snackbar.Add("正在生成PDF...", Severity.Info);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task PrintAll()
    {
        try
        {
            var request = new BatchPrintAllRequest
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                StartDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
                StartDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
                Columns = GetPrintColumnDefs()
            };
            var apiUrl = $"{Http.BaseAddress}api/batch/print-all-file";
            var json = JsonSerializer.Serialize(request);
            Snackbar.Add("正在生成PDF...", Severity.Info);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 分组渲染 ==========

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

        // 选择列占位（40px），对齐表格最左侧的 checkbox 列
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 40,
            ColumnCount = 0,
            CssClass = ""
        });

        int? lastKey = null;
        int totalWidth = 0;
        var groupKey = 0;
        var groupName = "";
        var count = 0;

        foreach (var col in _visibleColumns)
        {
            var gk = col.GroupKey ?? 0;
            if (lastKey.HasValue && gk != lastKey.Value)
            {
                if (count > 0)
                {
                    result.Add(new GroupHeaderInfo
                    {
                        GroupKey = groupKey,
                        GroupName = groupName,
                        TotalWidth = totalWidth,
                        ColumnCount = count,
                        CssClass = GetHeaderGroupCss(groupKey, true)
                    });
                }
                totalWidth = 0;
                count = 0;
            }
            groupKey = gk;
            groupName = col.GroupName ?? "";
            totalWidth += int.TryParse(col.Width, out var w) ? w : 100;
            count++;
            lastKey = gk;
        }
        if (count > 0)
        {
            result.Add(new GroupHeaderInfo
            {
                GroupKey = groupKey,
                GroupName = groupName,
                TotalWidth = totalWidth,
                ColumnCount = count,
                CssClass = GetHeaderGroupCss(groupKey, true)
            });
        }

        // 操作列占位，对齐表格最右侧的操作按钮列（无 col-g 类，JS 按 gk=0 单独测量）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 90,
            ColumnCount = 0,
            CssClass = ""
        });

        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g3",
            2 => "col-g4",
            3 => "col-g5",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g3-cell",
            2 => "col-g4-cell",
            3 => "col-g5-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#batches-list-table");
        }
        catch { }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo)) extras["dateTo"] = _dateTo;
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("batches", state);
    }
}
