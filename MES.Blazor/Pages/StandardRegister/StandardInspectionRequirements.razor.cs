using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.Models;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Shared;
using System.Text.Json;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.StandardRegister;

public partial class StandardInspectionRequirements
{
    private MudTable<StandardInspectionRequirementDto>? table;
    private List<StandardInspectionRequirementDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private int _loadVersion;
    private bool _resetToFirstPage;

    // 排序状态
    private string sortColumn = "StandardNo";
    private bool sortDescending = true;

    // ========== 选择/打印 ==========
    private HashSet<int> selectedIds = new();
    private bool allSelected => _pageItems.Count > 0 && _pageItems.All(i => selectedIds.Contains(i.Id));

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _allColumns.Where(c => c.Visible).Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) { Snackbar.Add("请先选择要打印的记录", Severity.Warning); return; }
        try
        {
            var request = new StandardInspectionRequirementPrintBatchRequest { Ids = selectedIds.ToArray(), Columns = GetPrintColumnDefs() };
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.StandardInspectionRequirement}/print-batch-file";
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, JsonSerializer.Serialize(request));
            Snackbar.Add("正在生成PDF...", Severity.Info);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "StandardNo",             Label = "标准号",              SortKey = "standardno",            FilterType = "string", IsRequired = true },
        new() { Key = "ChemicalComposition",    Label = "化学分析(成品)",       SortKey = "chemicalcomposition",   FilterType = "string" },
        new() { Key = "HydrostaticTest",        Label = "液压检验",             SortKey = "hydrostatictest",       FilterType = "string" },
        new() { Key = "EddyCurrent",            Label = "涡流探伤",             SortKey = "eddycurrent",           FilterType = "string" },
        new() { Key = "UltrasonicTest",         Label = "超声波检验",           SortKey = "ultrasonictest",        FilterType = "string" },
        new() { Key = "RadiographicTest",       Label = "射线探伤",             SortKey = "radiographictest",      FilterType = "string" },
        new() { Key = "HardnessRockwell",       Label = "硬度(洛氏)",           SortKey = "hardnessrockwell",      FilterType = "string" },
        new() { Key = "HardnessBrinell",        Label = "硬度(布氏)",           SortKey = "hardnessbrinell",       FilterType = "string" },
        new() { Key = "HardnessVickers",        Label = "硬度(维氏)",           SortKey = "hardnessvickers",       FilterType = "string" },
        new() { Key = "TensileRoomTemp",        Label = "拉伸(室温)",           SortKey = "tensileroomtemp",       FilterType = "string" },
        new() { Key = "TensileHighTemp",        Label = "拉伸(高温)",           SortKey = "tensilehightemp",       FilterType = "string" },
        new() { Key = "WeldJointTensile",       Label = "焊接接头拉伸",         SortKey = "weldjointtensile",      FilterType = "string" },
        new() { Key = "ImpactTest",             Label = "冲击试验",             SortKey = "impacttest",            FilterType = "string" },
        new() { Key = "WeldJointImpact",        Label = "焊接接头冲击",         SortKey = "weldjointimpact",       FilterType = "string" },
        new() { Key = "FlatteningTest",         Label = "压扁试验",             SortKey = "flatteningtest",        FilterType = "string" },
        new() { Key = "FlaringTest",            Label = "卷边试验",             SortKey = "flaringtest",           FilterType = "string" },
        new() { Key = "ExpandingTest",          Label = "扩口试验",             SortKey = "expandingtest",         FilterType = "string" },
        new() { Key = "BendTest",               Label = "弯曲试验",             SortKey = "bendtest",              FilterType = "string" },
        new() { Key = "WeldJointBend",          Label = "焊接接头弯曲",         SortKey = "weldjointbend",         FilterType = "string" },
        new() { Key = "GrainSize",              Label = "晶粒度",               SortKey = "grainsize",             FilterType = "string" },
        new() { Key = "IntergranularCorrosion", Label = "晶间腐蚀",             SortKey = "intergranularcorrosion",FilterType = "string" },
        new() { Key = "PittingCorrosion",       Label = "点腐蚀",               SortKey = "pittingcorrosion",      FilterType = "string" },
        new() { Key = "FerriteContent",         Label = "金相检验",             SortKey = "ferritecontent",        FilterType = "string" },
        new() { Key = "Macrostructure",         Label = "低倍组织",             SortKey = "macrostructure",        FilterType = "string" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<StandardInspectionRequirementDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "standardno";
            var filters = SerializeFilters();

            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            if (_resetToFirstPage)
            {
                state.Page = 0;
                _resetToFirstPage = false;
            }

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
                Filters = filters
            };

            var result = await StandardInspectionRequirementService.GetPagedAsync(query);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<StandardInspectionRequirementDto> { Items = _pageItems, TotalItems = _totalCount };

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

        return new TableData<StandardInspectionRequirementDto>
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
            var result = await StandardInspectionRequirementService.GetFilterContextsAsync();
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
        _resetToFirstPage = true;
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
        await ColumnPrefs.SaveAsync("standard_inspection_requirements", null, _allColumns);
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("standard_inspection_requirements", null);
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

        var savedState = await PageState.LoadAsync("standard_inspection_requirements");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "StandardNo";
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

        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#standard-inspection-requirements-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string StandardNo { get; set; } = string.Empty;
        public string? ChemicalComposition { get; set; }
        public string? HydrostaticTest { get; set; }
        public string? EddyCurrent { get; set; }
        public string? UltrasonicTest { get; set; }
        public string? RadiographicTest { get; set; }
        public string? HardnessRockwell { get; set; }
        public string? HardnessBrinell { get; set; }
        public string? HardnessVickers { get; set; }
        public string? TensileRoomTemp { get; set; }
        public string? TensileHighTemp { get; set; }
        public string? WeldJointTensile { get; set; }
        public string? ImpactTest { get; set; }
        public string? WeldJointImpact { get; set; }
        public string? FlatteningTest { get; set; }
        public string? FlaringTest { get; set; }
        public string? ExpandingTest { get; set; }
        public string? BendTest { get; set; }
        public string? WeldJointBend { get; set; }
        public string? GrainSize { get; set; }
        public string? IntergranularCorrosion { get; set; }
        public string? PittingCorrosion { get; set; }
        public string? FerriteContent { get; set; }
        public string? Macrostructure { get; set; }
    }

    private void StartEdit(StandardInspectionRequirementDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            StandardNo = item.StandardNo,
            ChemicalComposition = item.ChemicalComposition,
            HydrostaticTest = item.HydrostaticTest,
            EddyCurrent = item.EddyCurrent,
            UltrasonicTest = item.UltrasonicTest,
            RadiographicTest = item.RadiographicTest,
            HardnessRockwell = item.HardnessRockwell,
            HardnessBrinell = item.HardnessBrinell,
            HardnessVickers = item.HardnessVickers,
            TensileRoomTemp = item.TensileRoomTemp,
            TensileHighTemp = item.TensileHighTemp,
            WeldJointTensile = item.WeldJointTensile,
            ImpactTest = item.ImpactTest,
            WeldJointImpact = item.WeldJointImpact,
            FlatteningTest = item.FlatteningTest,
            FlaringTest = item.FlaringTest,
            ExpandingTest = item.ExpandingTest,
            BendTest = item.BendTest,
            WeldJointBend = item.WeldJointBend,
            GrainSize = item.GrainSize,
            IntergranularCorrosion = item.IntergranularCorrosion,
            PittingCorrosion = item.PittingCorrosion,
            FerriteContent = item.FerriteContent,
            Macrostructure = item.Macrostructure
        };
    }

    private void CancelEdit(StandardInspectionRequirementDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(StandardInspectionRequirementDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.StandardNo)) errors.Add("标准号不能为空");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateStandardInspectionRequirementRequest
            {
                StandardNo = cache.StandardNo,
                ChemicalComposition = cache.ChemicalComposition,
                HydrostaticTest = cache.HydrostaticTest,
                EddyCurrent = cache.EddyCurrent,
                UltrasonicTest = cache.UltrasonicTest,
                RadiographicTest = cache.RadiographicTest,
                HardnessRockwell = cache.HardnessRockwell,
                HardnessBrinell = cache.HardnessBrinell,
                HardnessVickers = cache.HardnessVickers,
                TensileRoomTemp = cache.TensileRoomTemp,
                TensileHighTemp = cache.TensileHighTemp,
                WeldJointTensile = cache.WeldJointTensile,
                ImpactTest = cache.ImpactTest,
                WeldJointImpact = cache.WeldJointImpact,
                FlatteningTest = cache.FlatteningTest,
                FlaringTest = cache.FlaringTest,
                ExpandingTest = cache.ExpandingTest,
                BendTest = cache.BendTest,
                WeldJointBend = cache.WeldJointBend,
                GrainSize = cache.GrainSize,
                IntergranularCorrosion = cache.IntergranularCorrosion,
                PittingCorrosion = cache.PittingCorrosion,
                FerriteContent = cache.FerriteContent,
                Macrostructure = cache.Macrostructure
            };

            var result = await StandardInspectionRequirementService.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                item.StandardNo = cache.StandardNo;
                item.ChemicalComposition = cache.ChemicalComposition;
                item.HydrostaticTest = cache.HydrostaticTest;
                item.EddyCurrent = cache.EddyCurrent;
                item.UltrasonicTest = cache.UltrasonicTest;
                item.RadiographicTest = cache.RadiographicTest;
                item.HardnessRockwell = cache.HardnessRockwell;
                item.HardnessBrinell = cache.HardnessBrinell;
                item.HardnessVickers = cache.HardnessVickers;
                item.TensileRoomTemp = cache.TensileRoomTemp;
                item.TensileHighTemp = cache.TensileHighTemp;
                item.WeldJointTensile = cache.WeldJointTensile;
                item.ImpactTest = cache.ImpactTest;
                item.WeldJointImpact = cache.WeldJointImpact;
                item.FlatteningTest = cache.FlatteningTest;
                item.FlaringTest = cache.FlaringTest;
                item.ExpandingTest = cache.ExpandingTest;
                item.BendTest = cache.BendTest;
                item.WeldJointBend = cache.WeldJointBend;
                item.GrainSize = cache.GrainSize;
                item.IntergranularCorrosion = cache.IntergranularCorrosion;
                item.PittingCorrosion = cache.PittingCorrosion;
                item.FerriteContent = cache.FerriteContent;
                item.Macrostructure = cache.Macrostructure;

                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                if (table != null) await table.ReloadServerData();
                Snackbar.Add("更新成功", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "更新失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"更新失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    // ========== 删除 ==========

    private void NavigateToCreate()
    {
        Navigation.NavigateTo("/standard-inspection-requirements/create");
    }

    private async Task DeleteItem(StandardInspectionRequirementDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除标准号 \"{item.StandardNo}\" 的检验项要求记录吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await StandardInspectionRequirementService.DeleteAsync(item.Id);
                if (result.Success)
                {
                    Snackbar.Add("删除成功", Severity.Success);
                    if (table != null) await table.ReloadServerData();
                    await LoadFilterContextsAsync();
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

    private RenderFragment RenderCell(StandardInspectionRequirementDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing && _editCache.TryGetValue(item.Id, out var c) ? c : null;

        switch (col.Key)
        {
            case "StandardNo":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.StandardNo);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.StandardNo = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.StandardNo);
                }
                break;
            default:
                RenderTextField(item, col.Key, cache)(builder);
                break;
        }
    };

    private RenderFragment RenderTextField(StandardInspectionRequirementDto item, string key, EditCache? cache) => builder =>
    {
        var val = GetFieldValue(item, key);
        var cacheVal = cache != null ? GetCacheFieldValue(cache, key) : null;

        if (cache != null)
        {
            builder.OpenComponent<MudTextField<string>>(0);
            builder.AddAttribute(1, "Dense", true);
            builder.AddAttribute(2, "Variant", Variant.Outlined);
            builder.AddAttribute(3, "Value", cacheVal);
            var capturedKey = key;
            builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v =>
            {
                SetCacheFieldValue(cache!, capturedKey, v);
            }));
            builder.AddAttribute(5, "Class", "compact-input");
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, val);
        }
    };

    private static string? GetFieldValue(StandardInspectionRequirementDto item, string key) => key switch
    {
        "ChemicalComposition" => item.ChemicalComposition,
        "HydrostaticTest" => item.HydrostaticTest,
        "EddyCurrent" => item.EddyCurrent,
        "UltrasonicTest" => item.UltrasonicTest,
        "RadiographicTest" => item.RadiographicTest,
        "HardnessRockwell" => item.HardnessRockwell,
        "HardnessBrinell" => item.HardnessBrinell,
        "HardnessVickers" => item.HardnessVickers,
        "TensileRoomTemp" => item.TensileRoomTemp,
        "TensileHighTemp" => item.TensileHighTemp,
        "WeldJointTensile" => item.WeldJointTensile,
        "ImpactTest" => item.ImpactTest,
        "WeldJointImpact" => item.WeldJointImpact,
        "FlatteningTest" => item.FlatteningTest,
        "FlaringTest" => item.FlaringTest,
        "ExpandingTest" => item.ExpandingTest,
        "BendTest" => item.BendTest,
        "WeldJointBend" => item.WeldJointBend,
        "GrainSize" => item.GrainSize,
        "IntergranularCorrosion" => item.IntergranularCorrosion,
        "PittingCorrosion" => item.PittingCorrosion,
        "FerriteContent" => item.FerriteContent,
        "Macrostructure" => item.Macrostructure,
        _ => null
    };

    private static string? GetCacheFieldValue(EditCache cache, string key) => key switch
    {
        "ChemicalComposition" => cache.ChemicalComposition,
        "HydrostaticTest" => cache.HydrostaticTest,
        "EddyCurrent" => cache.EddyCurrent,
        "UltrasonicTest" => cache.UltrasonicTest,
        "RadiographicTest" => cache.RadiographicTest,
        "HardnessRockwell" => cache.HardnessRockwell,
        "HardnessBrinell" => cache.HardnessBrinell,
        "HardnessVickers" => cache.HardnessVickers,
        "TensileRoomTemp" => cache.TensileRoomTemp,
        "TensileHighTemp" => cache.TensileHighTemp,
        "WeldJointTensile" => cache.WeldJointTensile,
        "ImpactTest" => cache.ImpactTest,
        "WeldJointImpact" => cache.WeldJointImpact,
        "FlatteningTest" => cache.FlatteningTest,
        "FlaringTest" => cache.FlaringTest,
        "ExpandingTest" => cache.ExpandingTest,
        "BendTest" => cache.BendTest,
        "WeldJointBend" => cache.WeldJointBend,
        "GrainSize" => cache.GrainSize,
        "IntergranularCorrosion" => cache.IntergranularCorrosion,
        "PittingCorrosion" => cache.PittingCorrosion,
        "FerriteContent" => cache.FerriteContent,
        "Macrostructure" => cache.Macrostructure,
        _ => null
    };

    private static void SetCacheFieldValue(EditCache cache, string key, string? value)
    {
        switch (key)
        {
            case "ChemicalComposition": cache.ChemicalComposition = value; break;
            case "HydrostaticTest": cache.HydrostaticTest = value; break;
            case "EddyCurrent": cache.EddyCurrent = value; break;
            case "UltrasonicTest": cache.UltrasonicTest = value; break;
            case "RadiographicTest": cache.RadiographicTest = value; break;
            case "HardnessRockwell": cache.HardnessRockwell = value; break;
            case "HardnessBrinell": cache.HardnessBrinell = value; break;
            case "HardnessVickers": cache.HardnessVickers = value; break;
            case "TensileRoomTemp": cache.TensileRoomTemp = value; break;
            case "TensileHighTemp": cache.TensileHighTemp = value; break;
            case "WeldJointTensile": cache.WeldJointTensile = value; break;
            case "ImpactTest": cache.ImpactTest = value; break;
            case "WeldJointImpact": cache.WeldJointImpact = value; break;
            case "FlatteningTest": cache.FlatteningTest = value; break;
            case "FlaringTest": cache.FlaringTest = value; break;
            case "ExpandingTest": cache.ExpandingTest = value; break;
            case "BendTest": cache.BendTest = value; break;
            case "WeldJointBend": cache.WeldJointBend = value; break;
            case "GrainSize": cache.GrainSize = value; break;
            case "IntergranularCorrosion": cache.IntergranularCorrosion = value; break;
            case "PittingCorrosion": cache.PittingCorrosion = value; break;
            case "FerriteContent": cache.FerriteContent = value; break;
            case "Macrostructure": cache.Macrostructure = value; break;
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
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("standard_inspection_requirements", state);
    }
}
