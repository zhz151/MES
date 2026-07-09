using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.DTOs;
using MES.Core.Models;
using System.Text.Json;

namespace MES.Blazor.Pages.Equipment;

public partial class Equipments
{
    private MudTable<EquipmentListDto>? table;
    private List<EquipmentListDto> _pageItems = new();
    private int _totalCount;
    private HashSet<int> selectedIds = new();
    private bool _isArrowNavSetup;
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
    private int _currentPage = 1;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    private string sortColumn = "createdtime";
    private bool sortDescending = true;

    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 内联编辑 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, EquipmentEditCache> _editCache = new();

    private class EquipmentEditCache
    {
        public string EquipmentCode { get; set; } = null!;
        public string EquipmentName { get; set; } = null!;
        public string? ModelNumber { get; set; }
        public string? TechnicalParams { get; set; }
        public string? Manufacturer { get; set; }
        public string? InstallationDateText { get; set; }
        public string? Remark { get; set; }
        public string? Location { get; set; }
        public string? RelatedSection { get; set; }
        public bool NeedInspection { get; set; }
        public string? InspectionPerson { get; set; }
        public int InspectionCycleDays { get; set; }
        public string? CurrentInspectionStartDateText { get; set; }
        public bool NeedMaintenance { get; set; }
        public string? MaintPerson { get; set; }
        public int MaintCycleDays { get; set; }
        public string? CurrentMaintStartDateText { get; set; }
        public string? LastRepairDateText { get; set; }
        public string LifecycleStatus { get; set; } = null!;
        public string UsageType { get; set; } = null!;
    }

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "EquipmentCode",     Label = "设备编码",   SortKey = "equipmentcode", FilterType = "string", Width = "120", IsRequired = true },
        new() { Key = "EquipmentName",     Label = "设备名称",   SortKey = "equipmentname", FilterType = "string", Width = "120", IsRequired = true },
        new() { Key = "ModelNumber",       Label = "型号规格",   SortKey = "modelnumber", FilterType = "string", Width = "120" },
        new() { Key = "TechnicalParams",   Label = "技术参数",   SortKey = "technicalparams", FilterType = "string", Width = "120" },
        new() { Key = "Manufacturer",      Label = "制造商",     SortKey = "manufacturer", FilterType = "string", Width = "120" },
        new() { Key = "InstallationDate",  Label = "安装日期",   SortKey = "installationdate", FilterType = "date", Width = "120" },
        new() { Key = "Location",          Label = "所在区域",   SortKey = "location", FilterType = "string", Width = "120" },
        new() { Key = "RelatedSection",    Label = "关联工段",   SortKey = "relatedsection", FilterType = "string", Width = "120" },
        new() { Key = "LifecycleStatus",   Label = "生命周期",   SortKey = "lifecyclestatus", FilterType = "enum", Width = "120",
            EnumOptions = new() { new("Active", "在用"), new("Standby", "备用"), new("Scrapped", "报废") } },
        new() { Key = "UsageType",         Label = "作用类型",   SortKey = "usagetype", FilterType = "enum", Width = "120",
            EnumOptions = new() { new("Primary", "主生产"), new("Secondary", "辅生产"), new("Other", "其它") } },
        new() { Key = "RunningStatus",     Label = "运行状态", SortKey = "runningstatus", FilterType = "enum", Width = "120",
            EnumOptions = new() { new("Normal", "正常"), new("Pending", "待维修"), new("InProgress", "维修中") } },
        new() { Key = "InspectionStatus",  Label = "点检状况", SortKey = "inspectionstatus", FilterType = "enum", Width = "120",
            EnumOptions = new() { new("Pending", "待执行"), new("Normal", "正常"), new("Overdue", "逾期"), new("NotApplicable", "不适用") } },
        new() { Key = "NeedInspection",    Label = "需点检",     SortKey = "needinspection", FilterType = "boolean", Width = "60" },
        new() { Key = "InspectionPerson",  Label = "点检负责人", SortKey = "inspectionperson", FilterType = "string", Width = "120" },
        new() { Key = "InspectionCycleDays",Label = "点检周期",  SortKey = "inspectioncycledays", Width = "80" },
        new() { Key = "CurrentInspectionStartDate",Label = "本次点检日起始", SortKey = "currentinspectionstartdate", FilterType = "date", Width = "120" },
        new() { Key = "MaintStatus",       Label = "保养状况", SortKey = "maintstatus", FilterType = "enum", Width = "120",
            EnumOptions = new() { new("Pending", "待执行"), new("Normal", "正常"), new("Overdue", "逾期"), new("NotApplicable", "不适用") } },
        new() { Key = "NeedMaintenance",   Label = "需保养",     SortKey = "needmaintenance", FilterType = "boolean", Width = "60" },
        new() { Key = "MaintPerson",       Label = "保养负责人", SortKey = "maintperson", FilterType = "string", Width = "120" },
        new() { Key = "MaintCycleDays",    Label = "保养周期",   SortKey = "maintcycledays", Width = "80" },
        new() { Key = "CurrentMaintStartDate",Label = "本次保养日起始", SortKey = "currentmaintstartdate", FilterType = "date", Width = "120" },
        new() { Key = "LastRepairDate",    Label = "最近维修日期", SortKey = "lastrepairdate", FilterType = "date", Width = "120" },
        new() { Key = "Remark",            Label = "备注",       SortKey = "remark", FilterType = "string", Width = "120" },
    };

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("equipment", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<EquipmentListDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "createdtime";
            var filters = SerializeFilters();

            var query = new EquipmentQueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = string.IsNullOrEmpty(sortBy) ? "createdtime" : sortBy,
                IsDescending = sortDescending
            };
            if (filters != null)
                query.Filters = filters;

            var result = await EquipmentService.GetPagedAsync(query);

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

        return new TableData<EquipmentListDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    private List<FilterDescriptor>? SerializeFilters()
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
        return descriptors.Count > 0 ? descriptors : null;
    }

    // ========== 筛选上下文加载（ExcelFilter 下拉选项） ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await EquipmentService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                BuildFilterContextOptions(result.Data);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载筛选选项失败: {ex.Message}", Severity.Warning);
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
        if (_filterContextOptions.TryGetValue("NeedInspection", out var niOptions))
        {
            foreach (var opt in niOptions)
                opt.Display = opt.Value == "True" ? "是" : "否";
        }
        if (_filterContextOptions.TryGetValue("NeedMaintenance", out var nmOptions))
        {
            foreach (var opt in nmOptions)
                opt.Display = opt.Value == "True" ? "是" : "否";
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("equipment", null);
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

        var savedState = await PageState.LoadAsync("equipments");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "createdtime";
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

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#equipment-list-table"))
                _isArrowNavSetup = false;
        }
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/equipment/create");

    // ========== 内联编辑 ==========

    private void StartEdit(EquipmentListDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EquipmentEditCache
        {
            EquipmentCode = item.EquipmentCode,
            EquipmentName = item.EquipmentName,
            ModelNumber = item.ModelNumber,
            TechnicalParams = item.TechnicalParams,
            Manufacturer = item.Manufacturer,
            InstallationDateText = item.InstallationDate?.ToString("yyyy-MM-dd"),
            Remark = item.Remark,
            Location = item.Location,
            RelatedSection = item.RelatedSection,
            NeedInspection = item.NeedInspection,
            InspectionPerson = item.InspectionPerson,
            InspectionCycleDays = item.InspectionCycleDays,
            CurrentInspectionStartDateText = item.CurrentInspectionStartDate?.ToString("yyyy-MM-dd"),
            NeedMaintenance = item.NeedMaintenance,
            MaintPerson = item.MaintPerson,
            MaintCycleDays = item.MaintCycleDays,
            CurrentMaintStartDateText = item.CurrentMaintStartDate?.ToString("yyyy-MM-dd"),
            LastRepairDateText = item.LastRepairDate?.ToString("yyyy-MM-dd"),
            LifecycleStatus = item.LifecycleStatus,
            UsageType = item.UsageType
        };
    }

    private void CancelEdit(int id)
    {
        _editingIds.Remove(id);
        _editCache.Remove(id);
    }

    private async Task SaveEdit(int id)
    {
        if (!_editCache.TryGetValue(id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.EquipmentCode)) errors.Add("设备编码不能为空");
        if (string.IsNullOrWhiteSpace(cache.EquipmentName)) errors.Add("设备名称不能为空");

        DateTime? installationDate = null;
        DateTime? inspStartDate = null;
        DateTime? maintStartDate = null;

        if (!string.IsNullOrWhiteSpace(cache.InstallationDateText))
        {
            if (DateTime.TryParse(cache.InstallationDateText, out var parsedInst))
                installationDate = parsedInst;
            else
                errors.Add("安装日期格式无效");
        }

        if (!string.IsNullOrWhiteSpace(cache.CurrentInspectionStartDateText))
        {
            if (DateTime.TryParse(cache.CurrentInspectionStartDateText, out var parsedInsp))
                inspStartDate = parsedInsp;
            else
                errors.Add("本次点检日起始格式无效");
        }

        if (!string.IsNullOrWhiteSpace(cache.CurrentMaintStartDateText))
        {
            if (DateTime.TryParse(cache.CurrentMaintStartDateText, out var parsedMaint))
                maintStartDate = parsedMaint;
            else
                errors.Add("本次保养日起始格式无效");
        }

        if (errors.Any())
        {
            Snackbar.Add(string.Join("；", errors), Severity.Error);
            return;
        }

        try
        {
            var request = new UpdateEquipmentRequest
            {
                EquipmentCode = cache.EquipmentCode,
                EquipmentName = cache.EquipmentName,
                ModelNumber = cache.ModelNumber,
                TechnicalParams = cache.TechnicalParams,
                Manufacturer = cache.Manufacturer,
                InstallationDate = installationDate,
                Remark = cache.Remark,
                Location = cache.Location ?? "",
                RelatedSection = cache.RelatedSection,
                NeedInspection = cache.NeedInspection,
                InspectionPerson = cache.InspectionPerson,
                InspectionCycleDays = cache.InspectionCycleDays,
                CurrentInspectionStartDate = inspStartDate,
                NeedMaintenance = cache.NeedMaintenance,
                MaintPerson = cache.MaintPerson,
                MaintCycleDays = cache.MaintCycleDays,
                CurrentMaintStartDate = maintStartDate,
                LifecycleStatus = cache.LifecycleStatus,
                UsageType = cache.UsageType
            };

            var result = await EquipmentService.UpdateAsync(id, request);
            if (result.Success)
            {
                Snackbar.Add("保存成功", Severity.Success);
                _editingIds.Remove(id);
                _editCache.Remove(id);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(EquipmentListDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除设备 \"{item.EquipmentName}\" 吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await EquipmentService.DeleteAsync(item.Id);
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

    // ========== 单元格原始值/显示值 ==========

    private string? GetCellRawValue(EquipmentListDto item, string key) => key switch
    {
        "EquipmentCode" => item.EquipmentCode,
        "EquipmentName" => item.EquipmentName,
        "ModelNumber" => item.ModelNumber,
        "TechnicalParams" => item.TechnicalParams,
        "Manufacturer" => item.Manufacturer,
        "InstallationDate" => item.InstallationDate?.ToString("yyyy-MM-dd"),
        "Location" => item.Location,
        "RelatedSection" => item.RelatedSection,
        "LifecycleStatus" => item.LifecycleStatus,
        "UsageType" => item.UsageType,
        "RunningStatus" => item.RunningStatus,
        "InspectionStatus" => item.InspectionStatus,
        "NeedInspection" => item.NeedInspection.ToString(),
        "InspectionPerson" => item.InspectionPerson,
        "InspectionCycleDays" => item.InspectionCycleDays.ToString(),
        "CurrentInspectionStartDate" => item.CurrentInspectionStartDate?.ToString("yyyy-MM-dd"),
        "MaintStatus" => item.MaintStatus,
        "NeedMaintenance" => item.NeedMaintenance.ToString(),
        "MaintPerson" => item.MaintPerson,
        "MaintCycleDays" => item.MaintCycleDays.ToString(),
        "CurrentMaintStartDate" => item.CurrentMaintStartDate?.ToString("yyyy-MM-dd"),
        "LastRepairDate" => item.LastRepairDate?.ToString("yyyy-MM-dd"),
        "Remark" => item.Remark,
        _ => null
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
            Snackbar.Add("请先选择要打印的设备记录", Severity.Warning);
            return;
        }
        try
        {
            var ids = selectedIds.ToArray();
            var columns = GetPrintColumnDefs();
            var request = new EquipmentPrintBatchRequest { Ids = ids, Columns = columns };
            var apiUrl = $"{Http.BaseAddress}api/equipment/print-batch-file";
            var json = JsonSerializer.Serialize(request);
            Snackbar.Add("正在生成PDF...", Severity.Info);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 打印选中设备的二维码标签
    /// </summary>
    private async Task PrintQrCodes()
    {
        var items = _pageItems.Where(i => selectedIds.Contains(i.Id)).ToList();
        if (items.Count == 0) return;

        var codes = items.Select(i => i.EquipmentCode).ToList();
        await JS.InvokeVoidAsync("MES.printQrCodes", codes);
    }

    /// <summary>
    /// 打印单个设备的二维码标签
    /// </summary>
    private async Task PrintSingleQrCode(EquipmentListDto item)
    {
        await JS.InvokeVoidAsync("MES.printQrCodes", new List<string> { item.EquipmentCode });
    }

    private async Task PrintAll()
    {
        try
        {
            var columns = GetPrintColumnDefs();
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "createdtime";
            var request = new EquipmentPrintAllRequest
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
                Columns = columns
            };
            var apiUrl = $"{Http.BaseAddress}api/equipment/print-all-file";
            var json = JsonSerializer.Serialize(request);
            Snackbar.Add("正在生成PDF...", Severity.Info);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }
    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(EquipmentListDto item, ColumnDef col) => builder =>
    {
        if (_editingIds.Contains(item.Id) && _editCache.TryGetValue(item.Id, out var cache))
        {
            RenderEditCell(cache, col)(builder);
            return;
        }

        switch (col.Key)
        {
            case "EquipmentCode":
                builder.AddContent(0, item.EquipmentCode);
                break;
            case "EquipmentName":
                builder.AddContent(0, item.EquipmentName);
                break;
            case "ModelNumber":
                builder.AddContent(0, item.ModelNumber);
                break;
            case "TechnicalParams":
                builder.AddContent(0, item.TechnicalParams);
                break;
            case "Manufacturer":
                builder.AddContent(0, item.Manufacturer);
                break;
            case "InstallationDate":
                builder.AddContent(0, item.InstallationDate?.ToString("yyyy-MM-dd"));
                break;
            case "Location":
                builder.AddContent(0, item.Location);
                break;
            case "RelatedSection":
                builder.AddContent(0, item.RelatedSection);
                break;
            case "LifecycleStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetLifecycleStatusColor(item.LifecycleStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetLifecycleStatusText(item.LifecycleStatus))));
                builder.CloseComponent();
                break;
            case "UsageType":
                builder.AddContent(0, DisplayHelper.GetUsageTypeText(item.UsageType));
                break;
            case "RunningStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetRunningStatusColor(item.RunningStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetRunningStatusText(item.RunningStatus))));
                builder.CloseComponent();
                break;
            case "InspectionStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetEquipmentTaskStatusColor(item.InspectionStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetEquipmentTaskStatusText(item.InspectionStatus))));
                builder.CloseComponent();
                break;
            case "NeedInspection":
                builder.AddContent(0, DisplayHelper.GetYesNoText(item.NeedInspection));
                break;
            case "InspectionPerson":
                builder.AddContent(0, item.InspectionPerson);
                break;
            case "InspectionCycleDays":
                builder.AddContent(0, item.InspectionCycleDays > 0 ? item.InspectionCycleDays.ToString() : "");
                break;
            case "CurrentInspectionStartDate":
                builder.AddContent(0, item.CurrentInspectionStartDate?.ToString("yyyy-MM-dd"));
                break;
            case "MaintStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetEquipmentTaskStatusColor(item.MaintStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetEquipmentTaskStatusText(item.MaintStatus))));
                builder.CloseComponent();
                break;
            case "NeedMaintenance":
                builder.AddContent(0, DisplayHelper.GetYesNoText(item.NeedMaintenance));
                break;
            case "MaintPerson":
                builder.AddContent(0, item.MaintPerson);
                break;
            case "MaintCycleDays":
                builder.AddContent(0, item.MaintCycleDays > 0 ? item.MaintCycleDays.ToString() : "");
                break;
            case "CurrentMaintStartDate":
                builder.AddContent(0, item.CurrentMaintStartDate?.ToString("yyyy-MM-dd"));
                break;
            case "LastRepairDate":
                builder.AddContent(0, item.LastRepairDate?.ToString("yyyy-MM-dd"));
                break;
            case "Remark":
                builder.AddContent(0, item.Remark);
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private RenderFragment RenderEditCell(EquipmentEditCache cache, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "EquipmentCode":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.EquipmentCode);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.EquipmentCode = v));
                builder.CloseComponent();
                break;
            case "EquipmentName":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.EquipmentName);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.EquipmentName = v));
                builder.CloseComponent();
                break;
            case "ModelNumber":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.ModelNumber);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.ModelNumber = v));
                builder.CloseComponent();
                break;
            case "TechnicalParams":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.TechnicalParams);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.TechnicalParams = v));
                builder.CloseComponent();
                break;
            case "Manufacturer":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.Manufacturer);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.Manufacturer = v));
                builder.CloseComponent();
                break;
            case "InstallationDate":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.InstallationDateText);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.InstallationDateText = v));
                builder.AddAttribute(6, "Placeholder", "yyyy-MM-dd");
                builder.CloseComponent();
                break;
            case "Location":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.Location);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.Location = v));
                builder.CloseComponent();
                break;
            case "RelatedSection":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.RelatedSection);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.RelatedSection = v));
                builder.CloseComponent();
                break;
            case "LifecycleStatus":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.LifecycleStatus);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.LifecycleStatus = v));
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(cb =>
                {
                    cb.OpenComponent<MudSelectItem<string>>(0);
                    cb.AddAttribute(1, "Value", "Active");
                    cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "在用")));
                    cb.CloseComponent();
                    cb.OpenComponent<MudSelectItem<string>>(0);
                    cb.AddAttribute(1, "Value", "Standby");
                    cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "备用")));
                    cb.CloseComponent();
                    cb.OpenComponent<MudSelectItem<string>>(0);
                    cb.AddAttribute(1, "Value", "Scrapped");
                    cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "报废")));
                    cb.CloseComponent();
                }));
                builder.CloseComponent();
                break;
            case "UsageType":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.UsageType);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.UsageType = v));
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(cb =>
                {
                    cb.OpenComponent<MudSelectItem<string>>(0);
                    cb.AddAttribute(1, "Value", "Primary");
                    cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "主生产")));
                    cb.CloseComponent();
                    cb.OpenComponent<MudSelectItem<string>>(0);
                    cb.AddAttribute(1, "Value", "Secondary");
                    cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "辅生产")));
                    cb.CloseComponent();
                    cb.OpenComponent<MudSelectItem<string>>(0);
                    cb.AddAttribute(1, "Value", "Other");
                    cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "其它")));
                    cb.CloseComponent();
                }));
                builder.CloseComponent();
                break;
            case "RunningStatus":
            case "InspectionStatus":
            case "MaintStatus":
                // 状态字段只读显示
                var statusItem = _pageItems.FirstOrDefault(i => _editCache.ContainsKey(i.Id));
                if (statusItem != null)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    if (col.Key == "RunningStatus")
                    {
                        builder.AddAttribute(2, "Color", DisplayHelper.GetRunningStatusColor(statusItem.RunningStatus));
                        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetRunningStatusText(statusItem.RunningStatus))));
                    }
                    else
                    {
                        var st = col.Key == "InspectionStatus" ? statusItem.InspectionStatus : statusItem.MaintStatus;
                        builder.AddAttribute(2, "Color", DisplayHelper.GetEquipmentTaskStatusColor(st));
                        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetEquipmentTaskStatusText(st))));
                    }
                    builder.CloseComponent();
                }
                break;
            case "NeedInspection":
                builder.OpenComponent<MudCheckBox<bool>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Value", cache.NeedInspection);
                builder.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<bool>(this, v => cache.NeedInspection = v));
                builder.CloseComponent();
                break;
            case "InspectionPerson":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.InspectionPerson);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.InspectionPerson = v));
                builder.CloseComponent();
                break;
            case "InspectionCycleDays":
                builder.OpenComponent<MudNumericField<int?>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "HideSpinButtons", true);
                builder.AddAttribute(5, "Value", cache.InspectionCycleDays);
                builder.AddAttribute(6, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.InspectionCycleDays = v ?? 0));
                builder.CloseComponent();
                break;
            case "CurrentInspectionStartDate":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.CurrentInspectionStartDateText);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.CurrentInspectionStartDateText = v));
                builder.AddAttribute(6, "Placeholder", "yyyy-MM-dd");
                builder.CloseComponent();
                break;
            case "NeedMaintenance":
                builder.OpenComponent<MudCheckBox<bool>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Value", cache.NeedMaintenance);
                builder.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<bool>(this, v => cache.NeedMaintenance = v));
                builder.CloseComponent();
                break;
            case "MaintPerson":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.MaintPerson);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.MaintPerson = v));
                builder.CloseComponent();
                break;
            case "MaintCycleDays":
                builder.OpenComponent<MudNumericField<int?>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "HideSpinButtons", true);
                builder.AddAttribute(5, "Value", cache.MaintCycleDays);
                builder.AddAttribute(6, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.MaintCycleDays = v ?? 0));
                builder.CloseComponent();
                break;
            case "CurrentMaintStartDate":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.CurrentMaintStartDateText);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.CurrentMaintStartDateText = v));
                builder.AddAttribute(6, "Placeholder", "yyyy-MM-dd");
                builder.CloseComponent();
                break;
            case "LastRepairDate":
                builder.AddContent(0, cache.LastRepairDateText);
                break;
            case "Remark":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.Remark);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.Remark = v));
                builder.CloseComponent();
                break;
        }
    };

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
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("equipments", state);
    }
}
