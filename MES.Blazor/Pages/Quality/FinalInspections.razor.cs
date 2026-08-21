using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Quality;

public partial class FinalInspections
{
    private MudTable<FinalInspectionDto>? table;
    private List<FinalInspectionDto> _pageItems = new();
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
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    private string sortColumn = "inspectiondate";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private int _totalTableWidth =>
        _visibleColumns.Sum(c => int.TryParse(c.Width, out var w) ? w : 100) + 40 + 90;

    // ========== 实时健康校验通知条 ==========
    private FinalInspectionHealthSummaryDto? _healthSummary;

    // ========== 成检量统计（近日/月度成检量数据，折叠卡片，按检验项目统计） ==========
    private bool _showRecentSummaryCard;
    private bool _showMonthlySummaryCard;
    private List<FinalInspectionSummaryRowDto> _recentSummaryRows = new();
    private List<FinalInspectionMonthlySummaryRowDto> _monthlySummaryRows = new();
    private List<string> _monthlyLabels = new();
    private bool _isLoadingRecentSummary;
    private bool _isLoadingMonthlySummary;

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "Quantity", "Weight", "QualifiedQuantity", "QualifiedWeight",
        "QualifiedConcessionQuantity", "DefectReworkQuantity",
        "DefectWarehouseQuantity", "DefectScrapQuantity",
        "DefectReworkWeight", "DefectWarehouseWeight", "DefectScrapWeight",
        "ProductionCutQuantity", "ProductionWeight"
    };

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // G1: 检验执行
        new() { Key = "InspectionItem",        Label = "检验项目",   SortKey = "inspectionitem", FilterType = "enum", Width = "120",
               GroupKey = 1, GroupName = "G1 检验执行",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<InspectionItem>() },
        new() { Key = "InspectionDate",        Label = "检验日期",   SortKey = "inspectiondate", FilterType = "date", Width = "120",
            GroupKey = 1, GroupName = "G1 检验执行" },
        new() { Key = "EquipmentName",          Label = "设备名称",   SortKey = "equipmentname", FilterType = "string", Width = "120",
            GroupKey = 1, GroupName = "G1 检验执行" },
        new() { Key = "Shift",                  Label = "班次",       SortKey = "shift", FilterType = "enum", Width = "120",
            GroupKey = 1, GroupName = "G1 检验执行",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<ShiftType>() },
        new() { Key = "Operator",               Label = "操作员",     SortKey = "operator", FilterType = "string", Width = "120",
            GroupKey = 1, GroupName = "G1 检验执行" },
        new() { Key = "InspectionType",         Label = "成检类型",   SortKey = "inspectiontype", FilterType = "enum", Width = "100",
            GroupKey = 1, GroupName = "G1 检验执行",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<InspectionType>() },
        new() { Key = "IsDeliveryStatus",      Label = "是否交付态", SortKey = "isdeliverystatus", FilterType = "enum", Width = "100",
            GroupKey = 1, GroupName = "G1 检验执行",
            EnumOptions = new() { new("是","是"), new("否","否") } },
        new() { Key = "QualificationLevel",    Label = "资格等级",   SortKey = "qualificationlevel", FilterType = "string", Width = "100",
            GroupKey = 1, GroupName = "G1 检验执行" },
        new() { Key = "BatchNo",                Label = "生产编号",   SortKey = "batchno", FilterType = "string", Width = "120",
            GroupKey = 1, GroupName = "G1 检验执行" },

        // G2: 生产批次（均来自 ProductionBatch 导航属性的 DTO 字段）
        new() { Key = "TagNo",                  Label = "挂牌号",     SortKey = "tagno", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "ProductionType",         Label = "生产类型",   SortKey = "productiontype", FilterType = "enum", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<ProductionType>() },
        new() { Key = "ManufacturingItem",     Label = "制造物品",   SortKey = "manufacturingitem", FilterType = "enum", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<MaterialType>() },
        new() { Key = "ManufacturingStatus",   Label = "制造状态",   SortKey = "manufacturingstatus", FilterType = "enum", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>() },
        new() { Key = "DeliveryState",          Label = "交货状态",   SortKey = "deliverystate", FilterType = "enum", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>() },
        new() { Key = "WorkOrderNo",            Label = "工单号",     SortKey = "workorderno", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "SalesOrderNo",           Label = "订单号",     SortKey = "salesorderno", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "ProductionMainNo",       Label = "主号",       SortKey = "productionmainno", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "Salesman",               Label = "业务员",     SortKey = "salesman", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "EndCustomer",            Label = "最终用户",   SortKey = "endcustomer", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "SourceUnit",             Label = "来料单位",   SortKey = "sourceunit", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "FurnaceNo",              Label = "炉号",       SortKey = "furnaceno", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "PlantGrade",             Label = "工厂牌号",   SortKey = "plantgrade", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "Specification",          Label = "规格",       SortKey = "specification", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "LengthStatus",           Label = "长度状态",   SortKey = "lengthstatus", FilterType = "enum", Width = "120",
            GroupKey = 2, GroupName = "G2 生产批次",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>() },
        new() { Key = "ProductionCutQuantity",  Label = "生产支数",   SortKey = "productioncutquantity", FilterType = "number", Width = "80",
            GroupKey = 2, GroupName = "G2 生产批次" },
        new() { Key = "ProductionWeight",       Label = "生产重量(kg)", SortKey = "productionweight", FilterType = "number", Width = "80",
            GroupKey = 2, GroupName = "G2 生产批次" },

        // G3: 检验结果
        new() { Key = "FixedLength",            Label = "定尺长度",   SortKey = "fixedlength", FilterType = "string", Width = "120",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "CutLengthMatchType",     Label = "符合工单长度", SortKey = "cutlengthmatchtype", FilterType = "enum", Width = "100",
            GroupKey = 3, GroupName = "G3 检验结果",
            EnumOptions = DisplayHelper.GetCutLengthMatchOptions() },
        new() { Key = "NonFixedLengthRange",    Label = "非定尺长度范围", SortKey = "nonfixedlengthrange", FilterType = "string", Width = "120",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "Quantity",               Label = "检验支数",   SortKey = "quantity", Width = "80",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "Weight",                 Label = "理论检验重量",   SortKey = "weight", Width = "80",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "QualifiedQuantity",      Label = "合格支数",     SortKey = "qualifiedquantity", Width = "80",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "QualifiedWeight",        Label = "理论合格重量",     SortKey = "qualifiedweight", Width = "80",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "QualifiedConcessionQuantity", Label = "含让步放行支", SortKey = "qualifiedconcessionquantity", Width = "80",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "ConcessionRemark",       Label = "让步说明",     SortKey = "concessionremark", FilterType = "string", Width = "120",
            GroupKey = 3, GroupName = "G3 检验结果" },

        // G4: 不合格处理
        new() { Key = "DefectReworkQuantity",   Label = "次品返整支",   SortKey = "defectreworkquantity", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectWarehouseQuantity",Label = "次品入库支",   SortKey = "defectwarehousequantity", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectScrapQuantity",    Label = "次品报废支",   SortKey = "defectscrapquantity", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectReworkWeight",     Label = "次品返整重",   SortKey = "defectreworkweight", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectWarehouseWeight",  Label = "次品入库重",   SortKey = "defectwarehouseweight", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectScrapWeight",      Label = "次品报废重",   SortKey = "defectscrapweight", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectDescription",      Label = "次品情况描述", SortKey = "defectdescription", FilterType = "string", Width = "120",
            GroupKey = 4, GroupName = "G4 不合格处理" },

        // G5: 尺寸值
        new() { Key = "OuterDiameterRange",     Label = "外径范围",   SortKey = "outerdiameterrange", FilterType = "string", Width = "120",
            GroupKey = 5, GroupName = "G5 尺寸值" },
        new() { Key = "WallThicknessRange",     Label = "壁厚范围",   SortKey = "wallthicknessrange", FilterType = "string", Width = "120",
            GroupKey = 5, GroupName = "G5 尺寸值" },
        new() { Key = "LengthAllowanceRange",   Label = "长度余量范围", SortKey = "lengthallowancerange", FilterType = "string", Width = "120",
            GroupKey = 5, GroupName = "G5 尺寸值" },

        // G6: 压力值
        new() { Key = "Pressure",               Label = "压力Mpa",    SortKey = "pressure", Width = "80",
            GroupKey = 6, GroupName = "G6 压力值" },
        new() { Key = "HoldTime",               Label = "保压时间s",  SortKey = "holdtime", Width = "80",
            GroupKey = 6, GroupName = "G6 压力值" },

        // G7: 涡流/超声波探伤
        new() { Key = "InspectionStandard",    Label = "检验标准",   SortKey = "inspectionstandard", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "InspectionGrade",       Label = "检验等级",   SortKey = "inspectiongrade", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "InstrumentModel",       Label = "仪器型号",   SortKey = "instrumentmodel", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "NdtMethod",             Label = "检验方式",   SortKey = "ndtmethod", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "StandardSampleSize",    Label = "标样尺寸",   SortKey = "standardsamplesize", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "StandardSampleDefect",  Label = "标样缺陷",   SortKey = "standardsampledefect", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "ProbeType",             Label = "探头类型",   SortKey = "probetype", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "Couplant",              Label = "耦合剂",     SortKey = "couplant", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "CalibrationFrequency",  Label = "校准频率",   SortKey = "calibrationfrequency", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "DetectionFrequency",    Label = "检测频率",   SortKey = "detectionfrequency", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "DetectionSensitivity",  Label = "检测灵敏度", SortKey = "detectionsensitivity", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "DetectionPhase",        Label = "检测相位",   SortKey = "detectionphase", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },
        new() { Key = "DetectionSpeed",        Label = "检测速度",   SortKey = "detectionspeed", FilterType = "string", Width = "100",
            GroupKey = 7, GroupName = "G7 涡流/超声波探伤" },

        // G8: 辅助信息
        new() { Key = "Remark",                 Label = "检验备注",   SortKey = "remark", FilterType = "string", Width = "120",
            GroupKey = 8, GroupName = "G8 辅助信息" },
        new() { Key = "DataSource",             Label = "数据来源",   SortKey = "datasource", FilterType = "enum", Width = "80",
            GroupKey = 8, GroupName = "G8 辅助信息",
            EnumOptions = DisplayHelper.GetDataSourceOptions() },
        new() { Key = "UpdatedTime",            Label = "更新日期",   SortKey = "updatedtime", Width = "120",
            GroupKey = 8, GroupName = "G8 辅助信息" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<FinalInspectionDto>> LoadDataFromServer(TableState state)
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "inspectiondate";
            var filtersJson = SerializeFilters();

            DateTime? dateFrom = null;
            DateTime? dateTo = null;
            if (DateTime.TryParse(_dateFrom, out var df)) dateFrom = df;
            if (DateTime.TryParse(_dateTo, out var dt)) dateTo = dt;

            var result = await FinalInspectionService.GetAllAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                inspectionDateFrom: dateFrom,
                inspectionDateTo: dateTo,
                filters: filtersJson);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
                ComputePageSums();
                await LoadHealthSummaryAsync(dateFrom, dateTo, filtersJson);
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
                _pageSums.Clear();
                _healthSummary = null;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
            _pageSums.Clear();
        }

        return new TableData<FinalInspectionDto>
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
            var result = await FinalInspectionService.GetFilterContextsAsync();
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

        // InspectionItem 列显示中文
        if (_filterContextOptions.TryGetValue("InspectionItem", out var itemOptions))
        {
            foreach (var opt in itemOptions)
            {
                opt.Display = DisplayHelper.GetInspectionItemText(
                    Enum.TryParse<InspectionItem>(opt.Value, out var item) ? item : InspectionItem.PMIInspection);
            }
        }

        // 枚举列：用 EnumOptions 的中文显示名替换 API 返回的原始值
        foreach (var col in _allColumns.Where(c => c.FilterType == "enum" && c.EnumOptions != null))
        {
            if (_filterContextOptions.TryGetValue(col.Key, out var options))
            {
                var enumMap = col.EnumOptions!.ToDictionary(e => e.Value, e => e.Display);
                foreach (var opt in options.Where(o => enumMap.ContainsKey(o.Value)))
                    opt.Display = enumMap[opt.Value];
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
        await ColumnPrefs.SaveAsync("final-inspection", null, _allColumns);
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

    // ========== 成检量统计（近日/月度成检量数据，折叠卡片，按检验项目统计） ==========

    /// <summary>切换「近日成检量数据」折叠卡片（首次展开时懒加载）</summary>
    private async Task ToggleRecentSummaryCard()
    {
        _showRecentSummaryCard = !_showRecentSummaryCard;
        if (_showRecentSummaryCard && _recentSummaryRows.Count == 0)
            await LoadRecentSummaryAsync();
    }

    /// <summary>切换「月度成检量数据」折叠卡片（首次展开时懒加载）</summary>
    private async Task ToggleMonthlySummaryCard()
    {
        _showMonthlySummaryCard = !_showMonthlySummaryCard;
        if (_showMonthlySummaryCard && _monthlySummaryRows.Count == 0)
            await LoadMonthlySummaryAsync();
    }

    private async Task LoadRecentSummaryAsync()
    {
        try
        {
            _isLoadingRecentSummary = true;
            StateHasChanged();
            var result = await FinalInspectionService.GetRecentSummaryAsync();
            _recentSummaryRows = result.Data ?? new();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"近日成检量加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingRecentSummary = false;
            StateHasChanged();
        }
    }

    private async Task LoadMonthlySummaryAsync()
    {
        try
        {
            _isLoadingMonthlySummary = true;
            StateHasChanged();
            var result = await FinalInspectionService.GetMonthlySummaryAsync();
            _monthlySummaryRows = result.Data ?? new();
            _monthlyLabels = Enumerable.Range(1, 12)
                .Select(m => new DateTime(DateTime.Today.Year, m, 1).ToString("yyyy-MM"))
                .ToList();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"月度成检量加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingMonthlySummary = false;
            StateHasChanged();
        }
    }

    /// <summary>重量(t) 格式化：kg /1000 显示 t（保留 1 位），0 值留空（防视觉污染）</summary>
    private static string FormatT(decimal kg)
        => kg > 0 ? (kg / 1000m).ToString("F1") : string.Empty;

    /// <summary>打印「近日成检量数据」卡片（前端 printRawHtml 直接打印 DOM 表格）</summary>
    private async Task PrintRecentSummaryTable()
    {
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#final-inspection-recent-summary-table");
            if (!string.IsNullOrEmpty(html))
                await JS.InvokeVoidAsync("printRawHtml", html, "近日成检量数据");
            else
                Snackbar.Add("未找到可打印的近日成检量表格", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>打印「月度成检量数据」卡片（前端 printRawHtml 直接打印 DOM 表格）</summary>
    private async Task PrintMonthlySummaryTable()
    {
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#final-inspection-monthly-summary-table");
            if (!string.IsNullOrEmpty(html))
                await JS.InvokeVoidAsync("printRawHtml", html, "月度成检量数据");
            else
                Snackbar.Add("未找到可打印的月度成检量表格", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("final-inspection", null);
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
        var savedState = await PageState.LoadAsync("final-inspection");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "inspectiondate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
            if (savedState.Extras?.ContainsKey("dateFrom") == true)
                _dateFrom = savedState.Extras["dateFrom"];
            if (savedState.Extras?.ContainsKey("dateTo") == true)
                _dateTo = savedState.Extras["dateTo"];
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

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#final-inspection-list-table");
        }
        catch { }

        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#final-inspection-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string InspectionDate { get; set; } = "";
        public string? InspectionType { get; set; }
        public string? EquipmentName { get; set; }
        public ShiftType? Shift { get; set; }
        public string? Operator { get; set; }
        public string? FixedLength { get; set; }
        public string? NonFixedLengthRange { get; set; }
        public int? Quantity { get; set; }
        public int? Weight { get; set; }
        public int? QualifiedQuantity { get; set; }
        public int? QualifiedWeight { get; set; }
        public int? QualifiedConcessionQuantity { get; set; }
        public string? ConcessionRemark { get; set; }
        public int? DefectReworkQuantity { get; set; }
        public int? DefectWarehouseQuantity { get; set; }
        public int? DefectScrapQuantity { get; set; }
        public int? DefectReworkWeight { get; set; }
        public int? DefectWarehouseWeight { get; set; }
        public int? DefectScrapWeight { get; set; }
        public string? DefectDescription { get; set; }
        public string? OuterDiameterRange { get; set; }
        public string? WallThicknessRange { get; set; }
        public string? LengthAllowanceRange { get; set; }
        public decimal? Pressure { get; set; }
        public int? HoldTime { get; set; }
        public string? QualificationLevel { get; set; }
        public string? InspectionStandard { get; set; }
        public string? InspectionGrade { get; set; }
        public string? InstrumentModel { get; set; }
        public string? NdtMethod { get; set; }
        public string? StandardSampleSize { get; set; }
        public string? StandardSampleDefect { get; set; }
        public string? ProbeType { get; set; }
        public string? Couplant { get; set; }
        public string? CalibrationFrequency { get; set; }
        public string? DetectionFrequency { get; set; }
        public string? DetectionSensitivity { get; set; }
        public string? DetectionPhase { get; set; }
        public string? DetectionSpeed { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(FinalInspectionDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            InspectionDate = item.InspectionDate.ToString("yyyy-MM-dd"),
            InspectionType = item.InspectionType?.ToString(),
            EquipmentName = item.EquipmentName,
            Shift = item.Shift,
            Operator = item.Operator,
            FixedLength = item.FixedLength,
            NonFixedLengthRange = item.NonFixedLengthRange,
            Quantity = item.Quantity,
            Weight = item.Weight,
            QualifiedQuantity = item.QualifiedQuantity,
            QualifiedWeight = item.QualifiedWeight,
            QualifiedConcessionQuantity = item.QualifiedConcessionQuantity,
            ConcessionRemark = item.ConcessionRemark,
            DefectReworkQuantity = item.DefectReworkQuantity,
            DefectWarehouseQuantity = item.DefectWarehouseQuantity,
            DefectScrapQuantity = item.DefectScrapQuantity,
            DefectReworkWeight = item.DefectReworkWeight,
            DefectWarehouseWeight = item.DefectWarehouseWeight,
            DefectScrapWeight = item.DefectScrapWeight,
            DefectDescription = item.DefectDescription,
            OuterDiameterRange = item.OuterDiameterRange,
            WallThicknessRange = item.WallThicknessRange,
            LengthAllowanceRange = item.LengthAllowanceRange,
            Pressure = item.Pressure,
            HoldTime = item.HoldTime,
            QualificationLevel = item.QualificationLevel,
            InspectionStandard = item.InspectionStandard,
            InspectionGrade = item.InspectionGrade,
            InstrumentModel = item.InstrumentModel,
            NdtMethod = item.NdtMethod,
            StandardSampleSize = item.StandardSampleSize,
            StandardSampleDefect = item.StandardSampleDefect,
            ProbeType = item.ProbeType,
            Couplant = item.Couplant,
            CalibrationFrequency = item.CalibrationFrequency,
            DetectionFrequency = item.DetectionFrequency,
            DetectionSensitivity = item.DetectionSensitivity,
            DetectionPhase = item.DetectionPhase,
            DetectionSpeed = item.DetectionSpeed,
            Remark = item.Remark
        };
    }

    private void CancelEdit(FinalInspectionDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(FinalInspectionDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        if (!DateTime.TryParse(cache.InspectionDate, out var inspectionDate))
        {
            Snackbar.Add("检验日期格式无效", Severity.Error);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateFinalInspectionRequest
            {
                InspectionDate = inspectionDate,
                InspectionType = EnumHelper.TryParse<InspectionType>(cache.InspectionType),
                EquipmentName = cache.EquipmentName,
                Shift = cache.Shift,
                Operator = cache.Operator,
                FixedLength = cache.FixedLength,
                NonFixedLengthRange = cache.NonFixedLengthRange,
                Quantity = cache.Quantity,
                Weight = cache.Weight,
                QualifiedQuantity = cache.QualifiedQuantity,
                QualifiedWeight = cache.QualifiedWeight,
                QualifiedConcessionQuantity = cache.QualifiedConcessionQuantity,
                ConcessionRemark = cache.ConcessionRemark,
                DefectReworkQuantity = cache.DefectReworkQuantity,
                DefectWarehouseQuantity = cache.DefectWarehouseQuantity,
                DefectScrapQuantity = cache.DefectScrapQuantity,
                DefectReworkWeight = cache.DefectReworkWeight,
                DefectWarehouseWeight = cache.DefectWarehouseWeight,
                DefectScrapWeight = cache.DefectScrapWeight,
                DefectDescription = cache.DefectDescription,
                OuterDiameterRange = cache.OuterDiameterRange,
                WallThicknessRange = cache.WallThicknessRange,
                LengthAllowanceRange = cache.LengthAllowanceRange,
                Pressure = cache.Pressure,
                HoldTime = cache.HoldTime,
                QualificationLevel = cache.QualificationLevel,
                InspectionStandard = cache.InspectionStandard,
                InspectionGrade = cache.InspectionGrade,
                InstrumentModel = cache.InstrumentModel,
                NdtMethod = cache.NdtMethod,
                StandardSampleSize = cache.StandardSampleSize,
                StandardSampleDefect = cache.StandardSampleDefect,
                ProbeType = cache.ProbeType,
                Couplant = cache.Couplant,
                CalibrationFrequency = cache.CalibrationFrequency,
                DetectionFrequency = cache.DetectionFrequency,
                DetectionSensitivity = cache.DetectionSensitivity,
                DetectionPhase = cache.DetectionPhase,
                DetectionSpeed = cache.DetectionSpeed,
                Remark = cache.Remark
            };

            var result = await FinalInspectionService.UpdateAsync(item.Id, request);
            if (result.Success && result.Data != null)
            {
                item.InspectionDate = result.Data.InspectionDate;
                item.InspectionType = result.Data.InspectionType;
                item.EquipmentName = result.Data.EquipmentName;
                item.Shift = result.Data.Shift;
                item.Operator = result.Data.Operator;
                item.FixedLength = result.Data.FixedLength;
                item.CutLengthMatchType = result.Data.CutLengthMatchType;
                item.NonFixedLengthRange = result.Data.NonFixedLengthRange;
                item.Quantity = result.Data.Quantity;
                item.Weight = result.Data.Weight;
                item.QualifiedQuantity = result.Data.QualifiedQuantity;
                item.QualifiedWeight = result.Data.QualifiedWeight;
                item.QualifiedConcessionQuantity = result.Data.QualifiedConcessionQuantity;
                item.ConcessionRemark = result.Data.ConcessionRemark;
                item.DefectReworkQuantity = result.Data.DefectReworkQuantity;
                item.DefectWarehouseQuantity = result.Data.DefectWarehouseQuantity;
                item.DefectScrapQuantity = result.Data.DefectScrapQuantity;
                item.DefectReworkWeight = result.Data.DefectReworkWeight;
                item.DefectWarehouseWeight = result.Data.DefectWarehouseWeight;
                item.DefectScrapWeight = result.Data.DefectScrapWeight;
                item.DefectDescription = result.Data.DefectDescription;
                item.OuterDiameterRange = result.Data.OuterDiameterRange;
                item.WallThicknessRange = result.Data.WallThicknessRange;
                item.LengthAllowanceRange = result.Data.LengthAllowanceRange;
                item.Pressure = result.Data.Pressure;
                item.HoldTime = result.Data.HoldTime;
                item.QualificationLevel = result.Data.QualificationLevel;
                item.InspectionStandard = result.Data.InspectionStandard;
                item.InspectionGrade = result.Data.InspectionGrade;
                item.InstrumentModel = result.Data.InstrumentModel;
                item.NdtMethod = result.Data.NdtMethod;
                item.StandardSampleSize = result.Data.StandardSampleSize;
                item.StandardSampleDefect = result.Data.StandardSampleDefect;
                item.ProbeType = result.Data.ProbeType;
                item.Couplant = result.Data.Couplant;
                item.CalibrationFrequency = result.Data.CalibrationFrequency;
                item.DetectionFrequency = result.Data.DetectionFrequency;
                item.DetectionSensitivity = result.Data.DetectionSensitivity;
                item.DetectionPhase = result.Data.DetectionPhase;
                item.DetectionSpeed = result.Data.DetectionSpeed;
                item.Remark = result.Data.Remark;
                item.UpdatedTime = result.Data.UpdatedTime;

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

    // ========== 单元格原始值/显示值 ==========

    private string? GetCellRawValue(FinalInspectionDto item, string key) => key switch
    {
        "InspectionItem" => DisplayHelper.GetInspectionItemText(item.InspectionItem),
        "InspectionDate" => item.InspectionDate.ToString("yyyy-MM-dd"),
        "BatchNo" => item.BatchNo,
        "ManufacturingItem" => DisplayHelper.GetMaterialTypeText(item.ManufacturingItem),
        "TagNo" => item.TagNo,
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "SourceUnit" => item.SourceUnit,
        "FurnaceNo" => item.FurnaceNo,
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "Salesman" => item.Salesman,
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "ManufacturingStatus" => item.ManufacturingStatusDisplay,
        "EndCustomer" => item.EndCustomer,
        "ProductionCutQuantity" => item.ProductionCutQuantity?.ToString(),
        "ProductionWeight" => item.ProductionWeight?.ToString("G29"),
        "IsDeliveryStatus" => item.IsDeliveryStatusDisplay,
        "LengthStatus" => item.LengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(item.LengthStatus.Value) : "-",
        "FixedLength" => item.FixedLength,
        "NonFixedLengthRange" => item.NonFixedLengthRange,
        "EquipmentName" => item.EquipmentName,
        "Shift" => DisplayHelper.GetShiftTypeText(item.Shift),
        "Operator" => item.Operator,
        "InspectionType" => DisplayHelper.GetInspectionTypeText(item.InspectionType),
        "Quantity" => item.Quantity?.ToString(),
        "Weight" => DisplayHelper.FormatNullableInt(item.Weight),
        "QualifiedQuantity" => item.QualifiedQuantity?.ToString(),
        "QualifiedWeight" => DisplayHelper.FormatNullableInt(item.QualifiedWeight),
        "QualifiedConcessionQuantity" => item.QualifiedConcessionQuantity?.ToString(),
        "ConcessionRemark" => item.ConcessionRemark,
        "DefectReworkQuantity" => item.DefectReworkQuantity?.ToString(),
        "DefectWarehouseQuantity" => item.DefectWarehouseQuantity?.ToString(),
        "DefectScrapQuantity" => item.DefectScrapQuantity?.ToString(),
        "DefectReworkWeight" => DisplayHelper.FormatNullableInt(item.DefectReworkWeight),
        "DefectWarehouseWeight" => DisplayHelper.FormatNullableInt(item.DefectWarehouseWeight),
        "DefectScrapWeight" => DisplayHelper.FormatNullableInt(item.DefectScrapWeight),
        "DefectDescription" => item.DefectDescription,
        "OuterDiameterRange" => item.OuterDiameterRange,
        "WallThicknessRange" => item.WallThicknessRange,
        "LengthAllowanceRange" => item.LengthAllowanceRange,
        "Pressure" => item.Pressure?.ToString("G29"),
        "HoldTime" => item.HoldTime?.ToString(),
        "Remark" => item.Remark,
        "QualificationLevel" => item.QualificationLevel,
        "InspectionStandard" => item.InspectionStandard,
        "InspectionGrade" => item.InspectionGrade,
        "InstrumentModel" => item.InstrumentModel,
        "NdtMethod" => item.NdtMethod,
        "StandardSampleSize" => item.StandardSampleSize,
        "StandardSampleDefect" => item.StandardSampleDefect,
        "ProbeType" => item.ProbeType,
        "Couplant" => item.Couplant,
        "CalibrationFrequency" => item.CalibrationFrequency,
        "DetectionFrequency" => item.DetectionFrequency,
        "DetectionSensitivity" => item.DetectionSensitivity,
        "DetectionPhase" => item.DetectionPhase,
        "DetectionSpeed" => item.DetectionSpeed,
        "DataSource" => item.DataSource,
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => null
    };

    private string? GetCellDisplayText(FinalInspectionDto item, string key) => key switch
    {
        "InspectionItem" => DisplayHelper.GetInspectionItemText(item.InspectionItem),
        "ManufacturingItem" => DisplayHelper.GetMaterialTypeText(item.ManufacturingItem),
        "InspectionType" => DisplayHelper.GetInspectionTypeText(item.InspectionType),
        _ => GetCellRawValue(item, key) ?? ""
    };

    // ========== 实时健康校验通知条 ==========

    private async Task LoadHealthSummaryAsync(DateTime? dateFrom, DateTime? dateTo, string? filtersJson)
    {
        try
        {
            var result = await FinalInspectionService.GetFinalInspectionHealthSummaryAsync(
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                inspectionDateFrom: dateFrom,
                inspectionDateTo: dateTo,
                filters: filtersJson);
            if (result.Success && result.Data != null)
                _healthSummary = result.Data;
            else
                _healthSummary = null;
        }
        catch
        {
            _healthSummary = null;
        }
    }

    // ========== 分页汇总（B33） ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(FinalInspectionDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var key in _summableColumnKeys)
        {
            if (!props.TryGetValue(key, out var prop)) continue;
            var type = prop.PropertyType;
            try
            {
                if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[key] = sum.ToString();
                }
            }
            catch { }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }

    // ========== 分组标题栏 ==========

    private List<GroupHeaderInfo> GetGroupHeaders()
    {
        var result = new List<GroupHeaderInfo>();
        // 选择列起始占位符（必须最前，对应 JS 遍历的第一个 <th> checkbox，gk=0）
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
        // 操作列尾随占位符（必须最后，对应 JS 遍历的最后一个 <th> 操作列，gk=0）
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
            1 => "col-g1",
            2 => "col-g2",
            3 => "col-g3",
            4 => "col-g4",
            5 => "col-g5",
            6 => "col-g6",
            7 => "col-g7",
            8 => "col-g8",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1-cell",
            2 => "col-g2-cell",
            3 => "col-g3-cell",
            4 => "col-g4-cell",
            5 => "col-g5-cell",
            6 => "col-g6-cell",
            7 => "col-g7-cell",
            8 => "col-g8-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    /// <summary>
    /// 定尺长度显示去掉 "mm" 后缀（历史/导入数据可能带单位，列表仅显示数值）
    /// </summary>
    private static string? FormatFixedLength(string? fixedLength)
    {
        if (string.IsNullOrWhiteSpace(fixedLength)) return fixedLength;
        var trimmed = fixedLength.Trim();
        return trimmed.EndsWith("mm", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^2].Trim()
            : trimmed;
    }

    private class GroupHeaderInfo
    {
        public int GroupKey { get; set; }
        public string GroupName { get; set; } = "";
        public int TotalWidth { get; set; }
        public int ColumnCount { get; set; }
        public string CssClass { get; set; } = "";
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom))
            extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo))
            extras["dateTo"] = _dateTo;
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("final-inspection", state);
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/final-inspection/create");

    private async Task DeleteItem(FinalInspectionDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除生产编号 \"{item.BatchNo}\" 的成品检验记录吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await FinalInspectionService.DeleteAsync(item.Id);
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

    // ========== 打印 ==========

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) return;
        var apiUrl = $"{Http.BaseAddress}api/final-inspection/print-batch-file";
        var request = new FinalInspectionPrintBatchRequest
        {
            Ids = selectedIds.ToArray(),
            Columns = GetPrintColumnDefs()
        };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private async Task PrintAll()
    {
        var apiUrl = $"{Http.BaseAddress}api/final-inspection/print-all-file";
        var request = new FinalInspectionPrintAllRequest
        {
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            SortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "inspectiondate",
            IsDescending = sortDescending,
            InspectionDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
            InspectionDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
            Columns = GetPrintColumnDefs(),
            Filters = SerializeFilters()
        };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    // ========== 单元格渲染 ==========

    private bool IsCellEditable(string key) => key switch
    {
        "InspectionDate" or "EquipmentName" or "Shift" or "Operator"
            or "FixedLength" or "NonFixedLengthRange"
            or "Quantity" or "Weight"
            or "QualifiedQuantity" or "QualifiedWeight"
            or "QualifiedConcessionQuantity" or "ConcessionRemark"
            or "DefectReworkQuantity" or "DefectWarehouseQuantity" or "DefectScrapQuantity"
            or "DefectReworkWeight" or "DefectWarehouseWeight" or "DefectScrapWeight"
            or "DefectDescription" or "OuterDiameterRange" or "WallThicknessRange"
            or "LengthAllowanceRange" or "Pressure" or "HoldTime"
            or "QualificationLevel" or "InspectionStandard" or "InspectionGrade"
            or "InstrumentModel" or "NdtMethod" or "StandardSampleSize"
            or "StandardSampleDefect" or "ProbeType" or "Couplant"
            or "CalibrationFrequency" or "DetectionFrequency" or "DetectionSensitivity"
            or "DetectionPhase" or "DetectionSpeed" or "Remark" => true,
        _ => false
    };

    private RenderFragment RenderCell(FinalInspectionDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing ? _editCache.GetValueOrDefault(item.Id) : null;

        switch (col.Key)
        {
            case "InspectionItem":
                builder.AddContent(0, DisplayHelper.GetInspectionItemText(item.InspectionItem));
                break;
            case "InspectionDate":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.InspectionDate);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.InspectionDate = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "Placeholder", "yyyy-MM-dd");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.InspectionDate.ToString("yyyy-MM-dd"));
                }
                break;
            case "BatchNo":
                builder.AddContent(0, item.BatchNo);
                break;
            case "ManufacturingItem":
                builder.AddContent(0, DisplayHelper.GetMaterialTypeText(item.ManufacturingItem));
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo);
                break;
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
                break;
            case "SalesOrderNo":
                builder.AddContent(0, item.SalesOrderNo);
                break;
            case "ProductionMainNo":
                builder.AddContent(0, item.ProductionMainNo);
                break;
            case "SourceUnit":
                builder.AddContent(0, item.SourceUnit);
                break;
            case "FurnaceNo":
                builder.AddContent(0, item.FurnaceNo);
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "ProductionType":
                builder.AddContent(0, item.ProductionType.HasValue ? DisplayHelper.GetProductionTypeText(item.ProductionType.Value) : "-");
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "Salesman":
                builder.AddContent(0, item.Salesman);
                break;
            case "LengthStatus":
                builder.AddContent(0, item.LengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(item.LengthStatus.Value) : "-");
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState));
                break;
            case "ManufacturingStatus":
                builder.AddContent(0, item.ManufacturingStatusDisplay);
                break;
            case "EndCustomer":
                builder.AddContent(0, item.EndCustomer);
                break;
            case "ProductionCutQuantity":
                builder.AddContent(0, item.ProductionCutQuantity?.ToString());
                break;
            case "ProductionWeight":
                builder.AddContent(0, item.ProductionWeight?.ToString("G29"));
                break;
            case "IsDeliveryStatus":
                builder.AddContent(0, item.IsDeliveryStatusDisplay);
                break;
            case "FixedLength":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.FixedLength);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.FixedLength = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    // 定尺长度显示去掉 "mm" 后缀（历史/导入数据可能带单位）
                    builder.AddContent(0, FormatFixedLength(item.FixedLength));
                }
                break;
            case "CutLengthMatchType":
                {
                    var matchText = item.CutLengthMatchTypeDisplay;
                    if (string.IsNullOrEmpty(matchText))
                    {
                        builder.AddContent(0, "");
                    }
                    else
                    {
                        builder.OpenComponent<MudChip>(0);
                        builder.AddAttribute(1, "Size", Size.Small);
                        builder.AddAttribute(2, "Color", DisplayHelper.GetCutLengthMatchColor(item.CutLengthMatchType));
                        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, matchText)));
                        builder.CloseComponent();
                    }
                }
                break;
            case "NonFixedLengthRange":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.NonFixedLengthRange);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.NonFixedLengthRange = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.NonFixedLengthRange);
                }
                break;
            case "EquipmentName":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.EquipmentName);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.EquipmentName = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.EquipmentName);
                }
                break;
            case "Shift":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSelect<ShiftType?>>(0);
                    builder.AddAttribute(1, "Value", cache.Shift);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<ShiftType?>(this, v => cache.Shift = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
                    {
                        foreach (var opt in DisplayHelper.GetEnumOptions<ShiftType>())
                        {
                            b.OpenComponent<MudSelectItem<ShiftType?>>(0);
                            b.AddAttribute(1, "Value", (ShiftType?)Enum.Parse<ShiftType>(opt.Value));
                            b.AddAttribute(2, "Text", opt.Display);
                            b.AddAttribute(3, "ChildContent", (RenderFragment)(b2 =>
                                b2.AddContent(0, opt.Display)));
                            b.CloseComponent();
                        }
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.GetShiftTypeText(item.Shift));
                }
                break;
            case "Operator":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.Operator);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Operator = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Operator);
                }
                break;
            case "InspectionType":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSelect<string?>>(0);
                    builder.AddAttribute(1, "Value", cache.InspectionType);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.InspectionType = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
                    {
                        b.OpenComponent<MudSelectItem<string?>>(0);
                        b.AddAttribute(1, "Value", nameof(InspectionType.FormalInspection));
                        b.AddAttribute(2, "Text", DisplayHelper.GetInspectionTypeText(nameof(InspectionType.FormalInspection)));
                        b.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetInspectionTypeText(nameof(InspectionType.FormalInspection)))));
                        b.CloseComponent();
                        b.OpenComponent<MudSelectItem<string?>>(0);
                        b.AddAttribute(1, "Value", nameof(InspectionType.PreInspection));
                        b.AddAttribute(2, "Text", DisplayHelper.GetInspectionTypeText(nameof(InspectionType.PreInspection)));
                        b.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetInspectionTypeText(nameof(InspectionType.PreInspection)))));
                        b.CloseComponent();
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.GetInspectionTypeText(item.InspectionType));
                }
                break;
            case "Quantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.Quantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.Quantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.Quantity));
                }
                break;
            case "Weight":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.Weight);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.Weight = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.Weight));
                }
                break;
            case "QualifiedQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.QualifiedQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.QualifiedQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.QualifiedQuantity));
                }
                break;
            case "QualifiedWeight":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.QualifiedWeight);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.QualifiedWeight = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.QualifiedWeight));
                }
                break;
            case "QualifiedConcessionQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.QualifiedConcessionQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.QualifiedConcessionQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.QualifiedConcessionQuantity));
                }
                break;
            case "ConcessionRemark":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.ConcessionRemark);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.ConcessionRemark = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ConcessionRemark);
                }
                break;
            case "DefectReworkQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectReworkQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.DefectReworkQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.DefectReworkQuantity));
                }
                break;
            case "DefectWarehouseQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectWarehouseQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.DefectWarehouseQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.DefectWarehouseQuantity));
                }
                break;
            case "DefectScrapQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectScrapQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.DefectScrapQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.DefectScrapQuantity));
                }
                break;
            case "DefectReworkWeight":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectReworkWeight);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.DefectReworkWeight = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.DefectReworkWeight));
                }
                break;
            case "DefectWarehouseWeight":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectWarehouseWeight);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.DefectWarehouseWeight = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.DefectWarehouseWeight));
                }
                break;
            case "DefectScrapWeight":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectScrapWeight);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.DefectScrapWeight = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableIntZeroAsEmpty(item.DefectScrapWeight));
                }
                break;
            case "DefectDescription":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectDescription);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.DefectDescription = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.DefectDescription);
                }
                break;
            case "OuterDiameterRange":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.OuterDiameterRange);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.OuterDiameterRange = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.OuterDiameterRange);
                }
                break;
            case "WallThicknessRange":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.WallThicknessRange);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.WallThicknessRange = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.WallThicknessRange);
                }
                break;
            case "LengthAllowanceRange":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.LengthAllowanceRange);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.LengthAllowanceRange = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.LengthAllowanceRange);
                }
                break;
            case "Pressure":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<decimal?>>(0);
                    builder.AddAttribute(1, "Value", cache.Pressure);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => cache.Pressure = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.AddAttribute(5, "Format", "G29");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableDecimal(item.Pressure));
                }
                break;
            case "HoldTime":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.HoldTime);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.HoldTime = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.HoldTime));
                }
                break;
            case "QualificationLevel":
                RenderNdtEditField(builder, cache, item, col, c => c.QualificationLevel, (c, v) => c.QualificationLevel = v);
                break;
            case "InspectionStandard":
                RenderNdtEditField(builder, cache, item, col, c => c.InspectionStandard, (c, v) => c.InspectionStandard = v);
                break;
            case "InspectionGrade":
                RenderNdtEditField(builder, cache, item, col, c => c.InspectionGrade, (c, v) => c.InspectionGrade = v);
                break;
            case "InstrumentModel":
                RenderNdtEditField(builder, cache, item, col, c => c.InstrumentModel, (c, v) => c.InstrumentModel = v);
                break;
            case "NdtMethod":
                RenderNdtEditField(builder, cache, item, col, c => c.NdtMethod, (c, v) => c.NdtMethod = v);
                break;
            case "StandardSampleSize":
                RenderNdtEditField(builder, cache, item, col, c => c.StandardSampleSize, (c, v) => c.StandardSampleSize = v);
                break;
            case "StandardSampleDefect":
                RenderNdtEditField(builder, cache, item, col, c => c.StandardSampleDefect, (c, v) => c.StandardSampleDefect = v);
                break;
            case "ProbeType":
                RenderNdtEditField(builder, cache, item, col, c => c.ProbeType, (c, v) => c.ProbeType = v);
                break;
            case "Couplant":
                RenderNdtEditField(builder, cache, item, col, c => c.Couplant, (c, v) => c.Couplant = v);
                break;
            case "CalibrationFrequency":
                RenderNdtEditField(builder, cache, item, col, c => c.CalibrationFrequency, (c, v) => c.CalibrationFrequency = v);
                break;
            case "DetectionFrequency":
                RenderNdtEditField(builder, cache, item, col, c => c.DetectionFrequency, (c, v) => c.DetectionFrequency = v);
                break;
            case "DetectionSensitivity":
                RenderNdtEditField(builder, cache, item, col, c => c.DetectionSensitivity, (c, v) => c.DetectionSensitivity = v);
                break;
            case "DetectionPhase":
                RenderNdtEditField(builder, cache, item, col, c => c.DetectionPhase, (c, v) => c.DetectionPhase = v);
                break;
            case "DetectionSpeed":
                RenderNdtEditField(builder, cache, item, col, c => c.DetectionSpeed, (c, v) => c.DetectionSpeed = v);
                break;
            case "Remark":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.Remark);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Remark = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Remark);
                }
                break;
            case "DataSource":
                var dsText = item.DataSource switch
                {
                    "SCAN" => "扫码",
                    "MANUAL" => "手动",
                    _ => item.DataSource ?? ""
                };
                builder.AddContent(0, dsText);
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private void RenderNdtEditField(RenderTreeBuilder builder, EditCache? cache, FinalInspectionDto item, ColumnDef col,
        Func<EditCache, string?> getter, Action<EditCache, string?> setter)
    {
        if (cache != null)
        {
            builder.OpenComponent<MudTextField<string>>(0);
            builder.AddAttribute(1, "Value", getter(cache));
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => setter(cache, v)));
            builder.AddAttribute(3, "Class", "compact-input");
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, item.GetType().GetProperty(col.Key)?.GetValue(item) as string ?? "");
        }
    }
}
