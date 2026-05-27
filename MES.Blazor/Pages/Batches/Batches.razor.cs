using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Batches;

public partial class Batches
{
    private MudTable<ProductionBatchListDto>? table;
    private List<ProductionBatchListDto> _pageItems = new();
    private int _totalCount;
    private HashSet<int> selectedIds = new();
    private List<BatchWorkOrderMismatchDto> _workOrderMismatches = new();
    private bool isSyncing = false;
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

    // 排序
    private string sortColumn = "batchno";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "BatchNo",            Label = "生产编号", SortKey = "batchno", FilterType = "string" },
        new() { Key = "TagNo",              Label = "挂牌号",   SortKey = "tagno", FilterType = "string" },
        new() { Key = "CreatedTime",        Label = "创建时间", SortKey = "createdtime" },
        new() { Key = "UpdatedTime",        Label = "最后更新时间", SortKey = "updatedtime" },
        new() { Key = "WorkOrderNo",        Label = "工单号",   SortKey = "workorderno", FilterType = "string" },
        new() { Key = "SalesOrderNo",       Label = "订单号",   SortKey = "salesorderno", FilterType = "string" },
        new() { Key = "ProductionMainNo",   Label = "主号",     SortKey = "productionmainno", FilterType = "string" },
        new() { Key = "ProductionSubNo",    Label = "次号",     SortKey = "productionsubno", FilterType = "string" },
        new() { Key = "ProductionType",     Label = "生产类型", SortKey = "productiontype", FilterType = "enum",
            EnumOptions = new() { new("RoughTube", "荒管生产"), new("InProcess", "在制生产"), new("Inventory", "库存"),
                new("OutsourcedPurchased", "外购"), new("Rework", "返整"), new("Subcontract", "委外生产"), new("ExternalProcessing", "对外加工") } },
        new() { Key = "ManufacturingItem",  Label = "制造物品", SortKey = "manufacturingitem", FilterType = "enum",
            EnumOptions = new() { new("OrderFinishedProduct", "订单成品"), new("PreparedMaterial", "备料成品"),
                new("SurplusStock", "余库料"), new("IntermediateProduct", "中间品"), new("SpecialDeliveryStatus", "特定交态成品") } },
        new() { Key = "Status",             Label = "状态",     SortKey = "status", FilterType = "enum",
            EnumOptions = new() { new("None", "未产"), new("InProgress", "在产"), new("Completed", "完成"), new("Suspended", "挂起"), new("Cancelled", "作废") } },
        new() { Key = "CurrentExecDate",    Label = "截止执行日", SortKey = "currentexecdate", FilterType = "date" },
        new() { Key = "CurrentGroupName",   Label = "当前工序", SortKey = "currentgroupname", FilterType = "string" },
        new() { Key = "CurrentSectionName", Label = "当前工段", SortKey = "currentsectionname", FilterType = "string" },
        new() { Key = "CurrentSectionCompleted", Label = "工段完工", SortKey = null, FilterType = "enum",
            EnumOptions = new() { new("True", "完工"), new("False", "生产中") } },
        new() { Key = "RemainingWorkDays",     Label = "剩余工量", SortKey = "remainingworkdays" },
        new() { Key = "CurrentEquipmentName", Label = "当前设备", SortKey = "currentequipmentname", FilterType = "string" },
        new() { Key = "CurrentOutsource",   Label = "当前委外", SortKey = "currentoutsource", FilterType = "string" },
        new() { Key = "CurrentSpec",        Label = "当前规格", SortKey = "currentspec", FilterType = "string" },
        new() { Key = "NextSectionName",    Label = "下一工段", SortKey = "nextsectionname", FilterType = "string" },
        new() { Key = "CorrespondingSpec",  Label = "对应规格", SortKey = "correspondingspec", FilterType = "string" },
        new() { Key = "CurrentValidQty",    Label = "现有效原料支数", SortKey = "currentvalidqty" },
        new() { Key = "CurrentValidWeight",  Label = "现有效原料重量", SortKey = "currentvalidweight" },
        new() { Key = "ProductionRatio",    Label = "制几率",   SortKey = "productionratio" },
        new() { Key = "SignDate",           Label = "签订日期", SortKey = "signdate", FilterType = "date" },
        new() { Key = "Salesman",           Label = "业务员",   SortKey = "salesman", FilterType = "string" },
        new() { Key = "EndCustomer",        Label = "最终用户", SortKey = "endcustomer", FilterType = "string" },
        new() { Key = "DeliveryDate",       Label = "交货日期", SortKey = "deliverydate", FilterType = "date" },
        new() { Key = "DelayPenalty",       Label = "延期罚款", SortKey = "delaypenalty", FilterType = "enum",
            EnumOptions = new() { new("True", "是"), new("False", "否") } },
        new() { Key = "MaterialName",       Label = "物料名称", SortKey = "materialname", FilterType = "enum",
            EnumOptions = new() { new("SeamlessPipe", "无缝管"), new("WeldedPipe", "焊管") } },
        new() { Key = "SettlementMethod",   Label = "结算方式", SortKey = "settlementmethod", FilterType = "enum",
            EnumOptions = new() { new("Theoretical", "理算"), new("Weighing", "过磅"), new("WeighingNegative", "过磅-负") } },
        new() { Key = "StandardCode",       Label = "产品标准", SortKey = "standardcode", FilterType = "string" },
        new() { Key = "DeliveryState",      Label = "交货状态", SortKey = "deliverystate", FilterType = "enum",
            EnumOptions = new() { new("SolutionAnnealedAndPickled", "固溶酸洗"), new("SolutionAnnealedAndPickledUTube", "固溶酸洗-U型管"),
                new("SolutionAnnealedAndPickledExternalPolished", "固溶酸洗-外抛光"), new("SolutionAnnealedAndPickledInternalPolished", "固溶酸洗-内抛光"),
                new("SolutionAnnealedAndPickledBothPolished", "固溶酸洗-内外抛光"), new("SolutionAnnealedAndPickledCoiled", "固溶酸洗-盘管"),
                new("Bright", "光亮"), new("BrightUTube", "光亮-U型管"), new("BrightCoiled", "光亮-盘管"), new("Hard", "硬态") } },
        new() { Key = "PlantGrade",         Label = "钢种",     SortKey = "plantgrade", FilterType = "string" },
        new() { Key = "Specification",      Label = "规格",     SortKey = "specification", FilterType = "string" },
        new() { Key = "LengthStatus",       Label = "长度状态", SortKey = "lengthstatus", FilterType = "enum",
            EnumOptions = new() { new("Fixed", "定尺"), new("Range", "范围尺"), new("NonFixed", "非定尺") } },
        new() { Key = "TotalQuantity",      Label = "总数量",   SortKey = "totalquantity" },
        new() { Key = "TotalMeters",        Label = "总米数",   SortKey = "totalmeters" },
        new() { Key = "TotalWeight",        Label = "总重量",   SortKey = "totalweight" },
        new() { Key = "TechnicalRequirements", Label = "技术要求", SortKey = "technicalrequirements", FilterType = "enum",
            EnumOptions = new() { new("Normal", "普通"), new("Special", "特殊") } },
        new() { Key = "ValidInputQuestion",   Label = "有效投料疑问", SortKey = null, FilterType = "enum",
            EnumOptions = new() { new("True", "疑问"), new("False", "正常") } },
        new() { Key = "CreatedBy",          Label = "创建人",   SortKey = "createdby", FilterType = "string" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ProductionBatchListDto>> LoadDataFromServer(TableState state)
    {
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
                IsDescending = sortDescending
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
        catch { }
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
        }

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();
        await LoadFilterContextsAsync();
    }

    // ========== 工单号验证 ==========

    private async Task CheckWorkOrdersAsync()
    {
        isSyncing = true;
        try
        {
            var verifyResult = await BatchService.VerifyWorkOrderNosAsync();
            if (verifyResult.Success && verifyResult.Data != null)
            {
                _workOrderMismatches = verifyResult.Data;
                if (_workOrderMismatches.Count > 0)
                    Snackbar.Add($"发现 {_workOrderMismatches.Count} 个批次的工单号不匹配", Severity.Warning);
                else
                    Snackbar.Add("所有批次工单号验证通过", Severity.Success);

                if (table != null) await table.ReloadServerData();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"工单号验证失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            isSyncing = false;
            StateHasChanged();
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
        "CurrentValidQty" => item.CurrentValidQty?.ToString("G29"),
        "CurrentValidWeight" => item.CurrentValidWeight?.ToString("G29"),
        "ProductionRatio" => item.ProductionRatio.ToString("G29"),
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
        "TotalMeters" => item.TotalMeters.ToString("G29"),
        "TotalWeight" => item.TotalWeight.ToString("G29"),
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
        "ManufacturingItem" => DisplayHelper.GetManufacturingItemText(item.ManufacturingItem),
        "CurrentValidQty" => item.CurrentValidQty?.ToString("G29") ?? "",
        "CurrentValidWeight" => item.CurrentValidWeight?.ToString("G29") ?? "",
        "ProductionRatio" => item.ProductionRatio.ToString("G29"),
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "Salesman" => item.Salesman,
        "EndCustomer" => item.EndCustomer ?? "",
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "DelayPenalty" => DisplayHelper.GetYesNoText(item.DelayPenalty),
        "MaterialName" => DisplayHelper.GetMaterialNameText(item.MaterialName),
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod),
        "StandardCode" => item.StandardCode,
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus),
        "TotalQuantity" => item.TotalQuantity.ToString("G29"),
        "TotalMeters" => item.TotalMeters.ToString("G29"),
        "TotalWeight" => item.TotalWeight.ToString("G29"),
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
            var result = await BatchService.PrintBatchSelectedAsync(ids);
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
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
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword
            };
            var result = await BatchService.PrintBatchAllAsync(request);
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
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
