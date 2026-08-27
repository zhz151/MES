using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Services;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Order;
using MES.Core.Enums;
using MES.Core.Models;

namespace MES.Blazor.Pages.Quality;

public partial class CertificateCreate
{
    // ========== 注入 ==========
    [Inject] private PendingDeliveryService PendingSvc { get; set; } = null!;
    [Inject] private CertificateService Svc { get; set; } = null!;
    [Inject] private StandardRegisterService StdRegSvc { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    // ========== Step 1：头选择表（无重复 4 字段） ==========
    private List<CertificateHeaderOptionDto> _headerOptions = new();
    private string _headerSearchKeyword = string.Empty;
    private CertificateHeaderOptionDto? _selectedHeader;
    private bool _headerConfirmed;

    // ========== StandardRegister 标准号映射（StandardNo → StandardName） ==========
    private Dictionary<string, string> _standardNameMap = new(StringComparer.OrdinalIgnoreCase);
    // 标准化键映射（去空白后的 StandardNo → StandardName），用于空格容错匹配
    private Dictionary<string, string> _standardNoCleanMap = new(StringComparer.OrdinalIgnoreCase);

    // ========== Certificate 头字段（Step 1 确认后自动填入 + 手动补充） ==========
    private string _certOrderNo = string.Empty;
    private string? _certCustomerName;
    private string? _certProductStandard;
    private string? _certDeliveryStatus;
    private string? _certProductName;
    private string? _certRemark;

    // ========== Step 2：批次选择表（按头条件过滤） ==========
    private List<PendingDeliveryItemDto> _allFilteredItems = new();
    private List<PendingDeliveryItemDto> _pageItems = new();
    private string _batchKeyword = string.Empty;
    private string _inboundDateFrom = string.Empty;
    private string _inboundDateTo = string.Empty;
    private HashSet<string> _selectedBatchNos = new();
    private int _pageIndex = 1;
    private int _pageSize = 10;
    private int _totalCount;
    private string _batchSortBy = nameof(PendingDeliveryItemDto.InventoryBatchNo);
    private bool _batchSortDesc;
    private bool _batchTableCollapsed;

    // ========== Step 3：子表（可编辑） ==========
    private List<CertificateItemDto> _subItems = new();
    private bool _isAutoFilling;
    private bool _isSubmitting;

    // ========== 表格状态 ==========
    private bool _isLoadingHeaders = true;
    private bool _isLoadingBatches;

    // ========== 初始化 ==========
    protected override async Task OnInitializedAsync()
    {
        await LoadHeaderOptionsAsync();
    }

    private async Task LoadHeaderOptionsAsync()
    {
        _isLoadingHeaders = true;
        try
        {
            // 加载头选项
            var headerResult = await PendingSvc.GetHeaderOptionsAsync();
            if (headerResult.Success && headerResult.Data != null)
                _headerOptions = headerResult.Data;
            else
                Snackbar.Add(headerResult.Message ?? "加载选项失败", Severity.Warning);

            // 加载标准号映射（ProductStandard → StandardName）
            var stdResult = await StdRegSvc.GetAllAsync();
            if (stdResult.Success && stdResult.Data != null)
            {
                _standardNameMap = stdResult.Data
                    .Where(s => !string.IsNullOrEmpty(s.StandardNo))
                    .ToDictionary(s => s.StandardNo, s => s.StandardName, StringComparer.OrdinalIgnoreCase);

                // 预计算标准化键映射（仅去空白），用于容错匹配（不含年份去尾以避免重复键）
                _standardNoCleanMap = stdResult.Data
                    .Where(s => !string.IsNullOrEmpty(s.StandardNo))
                    .ToDictionary(
                        s => s.StandardNo.Replace(" ", "").Replace("\t", ""),
                        s => s.StandardName,
                        StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingHeaders = false;
        }
    }

    // ========== Step 1：选择头 ==========

    private List<CertificateHeaderOptionDto> FilteredHeaderOptions()
    {
        if (string.IsNullOrWhiteSpace(_headerSearchKeyword))
            return _headerOptions;
        var kw = _headerSearchKeyword.Trim();
        return _headerOptions.Where(o =>
            (o.OrderNo ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
            (o.CustomerName ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
            (o.ProductStandard ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
            (o.DeliveryStatus?.ToString() ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    private async Task ConfirmHeaderSelection()
    {
        if (_selectedHeader == null)
        {
            Snackbar.Add("请先选择一行", Severity.Warning);
            return;
        }

        _certOrderNo = _selectedHeader.OrderNo;
        _certCustomerName = _selectedHeader.CustomerName;
        _certProductStandard = _selectedHeader.ProductStandard;
        _certDeliveryStatus = _selectedHeader.DeliveryStatus?.ToString();

        // 通过 StandardRegister 自动填充产品名称
        _certProductName = await ResolveProductNameAsync(_certProductStandard);

        _headerConfirmed = true;

        // 重置 Step 2 和 Step 3
        _selectedBatchNos.Clear();
        _subItems.Clear();
        _pageIndex = 1;
        _batchKeyword = string.Empty;
        _inboundDateFrom = string.Empty;
        _inboundDateTo = string.Empty;

        Snackbar.Add($"已选择: {_certOrderNo}", Severity.Normal);
        LoadBatchData();
    }

    private void ChangeHeaderSelection()
    {
        _headerConfirmed = false;
        _selectedHeader = null;
        _certOrderNo = string.Empty;
        _certCustomerName = null;
        _certProductStandard = null;
        _certDeliveryStatus = null;
        _selectedBatchNos.Clear();
        _subItems.Clear();
    }

    // ========== 产品名称解析（标准号 → 标准名称，多级容错 + 后端兜底） ==========

    private async Task<string?> ResolveProductNameAsync(string? productStandard)
    {
        if (string.IsNullOrEmpty(productStandard)) return null;

        string? MatchInMap(string ps)
        {
            // 1. 精确匹配
            if (_standardNameMap.TryGetValue(ps, out var name))
                return name;

            // 2. 去掉末尾的年份后缀再试
            var withoutYear = System.Text.RegularExpressions.Regex.Replace(
                ps, @"-\d{4}$", "", System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromMilliseconds(100));
            if (withoutYear != ps && _standardNameMap.TryGetValue(withoutYear, out name))
                return name;

            // 3. 标准化匹配（仅去空白后对比，不含年份去尾）
            var cleaned = ps.Replace(" ", "").Replace("\t", "");
            if (_standardNoCleanMap.TryGetValue(cleaned, out name))
                return name;

            return null;
        }

        // 本地 Map 匹配
        var local = MatchInMap(productStandard);
        if (local != null) return local;

        // 本地未命中，走后端 API 兜底（与保存时的逻辑一致）
        return await StdRegSvc.ResolveNameAsync(productStandard);
    }

    // ========== Step 2：加载批次数据 ==========

    private async void LoadBatchData()
    {
        if (string.IsNullOrEmpty(_certOrderNo)) return;

        _isLoadingBatches = true;
        try
        {
            // 使用 PendingDelivery Service 的 GetAllAsync
            var query = new QueryParams
            {
                PageIndex = _pageIndex,
                PageSize = 5000, // 一次加载所有（按订单+标准+交货状态过滤后数据量不会太大）
                SortBy = "InventoryBatchNo",
                IsDescending = false,
            };

            if (!string.IsNullOrWhiteSpace(_batchKeyword))
                query.Keyword = _batchKeyword;

            var result = await PendingSvc.GetAllAsync(
                query,
                inboundDateFrom: DateTime.TryParse(_inboundDateFrom, out var df) ? df : null,
                inboundDateTo: DateTime.TryParse(_inboundDateTo, out var dt) ? dt : null);

            if (result.Success && result.Data != null)
            {
                // 按选中的头 4 字段过滤
                var all = result.Data.Items.Where(d =>
                    (d.SalesOrderNo ?? "") == _certOrderNo &&
                    (d.CustomerName ?? "") == (_certCustomerName ?? "") &&
                    (d.ProductStandard ?? "") == (_certProductStandard ?? "") &&
                    (d.DeliveryStatus?.ToString() ?? "") == (_certDeliveryStatus ?? "")
                ).ToList();

                _allFilteredItems = all;
                _totalCount = all.Count;
                ApplyBatchPaging();
            }
            else
            {
                _allFilteredItems = new();
                _pageItems = new();
                _totalCount = 0;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载批次失败: {ex.Message}", Severity.Error);
            _allFilteredItems = new();
            _pageItems = new();
        }
        finally
        {
            _isLoadingBatches = false;
            StateHasChanged();
        }
    }

    private void OnBatchSearchChanged(string value)
    {
        _batchKeyword = value ?? string.Empty;
        _pageIndex = 1;
        LoadBatchData();
    }

    private void OnInboundDateFromChanged(string value)
    {
        _inboundDateFrom = value ?? string.Empty;
        _pageIndex = 1;
        LoadBatchData();
    }

    private void OnInboundDateToChanged(string value)
    {
        _inboundDateTo = value ?? string.Empty;
        _pageIndex = 1;
        LoadBatchData();
    }

    private void ApplyBatchPaging()
    {
        var query = _allFilteredItems.AsEnumerable();

        // 应用排序
        query = (_batchSortBy, _batchSortDesc) switch
        {
            (nameof(PendingDeliveryItemDto.InventoryBatchNo), false) => query.OrderBy(i => i.InventoryBatchNo),
            (nameof(PendingDeliveryItemDto.InventoryBatchNo), true) => query.OrderByDescending(i => i.InventoryBatchNo),
            (nameof(PendingDeliveryItemDto.InboundDate), false) => query.OrderBy(i => i.InboundDate),
            (nameof(PendingDeliveryItemDto.InboundDate), true) => query.OrderByDescending(i => i.InboundDate),
            (nameof(PendingDeliveryItemDto.ProductionBatchNo), false) => query.OrderBy(i => i.ProductionBatchNo ?? ""),
            (nameof(PendingDeliveryItemDto.ProductionBatchNo), true) => query.OrderByDescending(i => i.ProductionBatchNo ?? ""),
            (nameof(PendingDeliveryItemDto.StandardGrade), false) => query.OrderBy(i => i.StandardGrade ?? ""),
            (nameof(PendingDeliveryItemDto.StandardGrade), true) => query.OrderByDescending(i => i.StandardGrade ?? ""),
            _ => query.OrderBy(i => i.InventoryBatchNo)
        };

        var list = query.ToList();
        _totalCount = list.Count;
        _pageItems = list.Skip((_pageIndex - 1) * _pageSize).Take(_pageSize).ToList();
        StateHasChanged();
    }

    private void OnBatchPageChanged(int page)
    {
        _pageIndex = page;
        ApplyBatchPaging();
    }

    private void ToggleBatchSelection(string batchNo)
    {
        if (_selectedBatchNos.Contains(batchNo))
            _selectedBatchNos.Remove(batchNo);
        else
            _selectedBatchNos.Add(batchNo);
    }

    private bool IsBatchSelected(string batchNo) => _selectedBatchNos.Contains(batchNo);

    private void SelectAllBatches(bool value)
    {
        if (value)
            _selectedBatchNos = _pageItems.Select(i => i.InventoryBatchNo).ToHashSet();
        else
            _selectedBatchNos.Clear();
    }

    // ========== 批次表排序 ==========

    private void ToggleBatchSort(string field)
    {
        if (_batchSortBy == field)
            _batchSortDesc = !_batchSortDesc;
        else
        {
            _batchSortBy = field;
            _batchSortDesc = false;
        }
        ApplyBatchPaging();
    }

    private string SortIcon(string field)
    {
        if (_batchSortBy != field) return string.Empty;
        return _batchSortDesc ? " ▼" : " ▲";
    }

    private void ExpandBatchTable()
    {
        _batchTableCollapsed = false;
    }

    // ========== 选中批次 → 填充子表 ==========

    private void FillSubItemsFromSelection()
    {
        if (_selectedBatchNos.Count == 0)
        {
            Snackbar.Add("请先选择批次", Severity.Warning);
            return;
        }

        // 按当前排序顺序选取
        var selectedItems = _allFilteredItems
            .Where(i => _selectedBatchNos.Contains(i.InventoryBatchNo))
            .AsEnumerable();

        selectedItems = (_batchSortBy, _batchSortDesc) switch
        {
            (nameof(PendingDeliveryItemDto.InventoryBatchNo), false) => selectedItems.OrderBy(i => i.InventoryBatchNo),
            (nameof(PendingDeliveryItemDto.InventoryBatchNo), true) => selectedItems.OrderByDescending(i => i.InventoryBatchNo),
            (nameof(PendingDeliveryItemDto.InboundDate), false) => selectedItems.OrderBy(i => i.InboundDate),
            (nameof(PendingDeliveryItemDto.InboundDate), true) => selectedItems.OrderByDescending(i => i.InboundDate),
            (nameof(PendingDeliveryItemDto.ProductionBatchNo), false) => selectedItems.OrderBy(i => i.ProductionBatchNo ?? ""),
            (nameof(PendingDeliveryItemDto.ProductionBatchNo), true) => selectedItems.OrderByDescending(i => i.ProductionBatchNo ?? ""),
            (nameof(PendingDeliveryItemDto.StandardGrade), false) => selectedItems.OrderBy(i => i.StandardGrade ?? ""),
            (nameof(PendingDeliveryItemDto.StandardGrade), true) => selectedItems.OrderByDescending(i => i.StandardGrade ?? ""),
            _ => selectedItems.OrderBy(i => i.InventoryBatchNo)
        };

        var selectedList = selectedItems.ToList();
        if (selectedList.Count == 0) return;

        var existingKeys = _subItems
            .Where(s => s.InventoryBatchNo != null)
            .Select(s => s.InventoryBatchNo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nextSeq = _subItems.Count > 0 ? _subItems.Max(s => s.SeqNo) + 1 : 1;

        foreach (var item in selectedList)
        {
            // 避免重复添加
            if (existingKeys.Contains(item.InventoryBatchNo))
                continue;

            existingKeys.Add(item.InventoryBatchNo);

            // 构建长度描述
            string? lengthDesc = null;
            if (item.MinLength.HasValue && item.MaxLength.HasValue)
                lengthDesc = item.MinLength == item.MaxLength
                    ? $"{item.MinLength:G29}"
                    : $"{item.MinLength:G29}-{item.MaxLength:G29}";
            else if (item.MinLength.HasValue)
                lengthDesc = $"{item.MinLength:G29}";
            else if (item.MaxLength.HasValue)
                lengthDesc = $"{item.MaxLength:G29}";

            _subItems.Add(new CertificateItemDto
            {
                SeqNo = nextSeq++,
                InventoryBatchNo = item.InventoryBatchNo,
                ProductionBatchNo = item.ProductionBatchNo,
                HeatNo = item.HeatNo,
                SteelGrade = item.StandardGrade,
                Specification = item.Specification,
                LengthDesc = lengthDesc,
                Quantity = item.RemainingQuantity,
                Meters = item.RemainingMeters,
                Weight = item.RemainingWeight
            });
        }

        // 清空选择
        _selectedBatchNos.Clear();
        _batchTableCollapsed = true;
        Snackbar.Add($"已填充 {selectedList.Count} 项到子表", Severity.Normal);
    }

    // ========== 子表操作 ==========

    private void RemoveSubItem(CertificateItemDto item)
    {
        _subItems.Remove(item);
        // 重新编号
        for (int i = 0; i < _subItems.Count; i++)
            _subItems[i].SeqNo = i + 1;
    }

    // ========== 生成（自动填充检验数据） ==========

    private async Task AutoFillInspection()
    {
        if (_subItems.Count == 0)
        {
            Snackbar.Add("子表为空，请先选择批次", Severity.Warning);
            return;
        }

        _isAutoFilling = true;
        try
        {
            var fillItems = _subItems.Select(s => new AutoFillInspectionItem
            {
                SeqNo = s.SeqNo,
                HeatNo = s.HeatNo,
                ProductionBatchNo = s.ProductionBatchNo
            }).ToList();

            var result = await Svc.AutoFillInspectionDataAsync(fillItems);

            if (result.Success && result.Data != null)
            {
                // 用填充后的数据更新子表（保留已有的仓库信息，只覆盖化学/成品检验/理化）
                var fillDict = result.Data.ToDictionary(f => f.SeqNo, f => f);
                foreach (var item in _subItems)
                {
                    if (fillDict.TryGetValue(item.SeqNo, out var filled))
                    {
                        // Chemical (Group 2)
                        item.ChemC = filled.ChemC;
                        item.ChemSi = filled.ChemSi;
                        item.ChemMn = filled.ChemMn;
                        item.ChemP = filled.ChemP;
                        item.ChemS = filled.ChemS;
                        item.ChemNi = filled.ChemNi;
                        item.ChemCr = filled.ChemCr;
                        item.ChemMo = filled.ChemMo;
                        item.ChemCu = filled.ChemCu;
                        item.ChemN = filled.ChemN;
                        item.ChemNb = filled.ChemNb;
                        item.ChemTi = filled.ChemTi;
                        item.ChemFe = filled.ChemFe;
                        item.ChemAl = filled.ChemAl;
                        item.ChemW = filled.ChemW;

                        // Inspection (Group 3)
                        item.InspPMI = filled.InspPMI;
                        item.InspVisual = filled.InspVisual;
                        item.InspDimension = filled.InspDimension;
                        item.InspEndoscopy = filled.InspEndoscopy;
                        item.InspHydro = filled.InspHydro;
                        item.InspUnderwaterPneumatic = filled.InspUnderwaterPneumatic;
                        item.InspEddyCurrent = filled.InspEddyCurrent;
                        item.InspUltrasonic = filled.InspUltrasonic;
                        item.InspPortDye = filled.InspPortDye;

                        // Physical (Group 4)
                        item.TensileStrength_1 = filled.TensileStrength_1;
                        item.TensileStrength_2 = filled.TensileStrength_2;
                        item.YieldRp02_1 = filled.YieldRp02_1;
                        item.YieldRp02_2 = filled.YieldRp02_2;
                        item.YieldRp10_1 = filled.YieldRp10_1;
                        item.YieldRp10_2 = filled.YieldRp10_2;
                        item.Elongation_1 = filled.Elongation_1;
                        item.Elongation_2 = filled.Elongation_2;
                        item.Hardness_1 = filled.Hardness_1;
                        item.Hardness_2 = filled.Hardness_2;
                        item.GrainSize_1 = filled.GrainSize_1;
                        item.GrainSize_2 = filled.GrainSize_2;
                        item.FerriteContent_1 = filled.FerriteContent_1;
                        item.FerriteContent_2 = filled.FerriteContent_2;
                        item.FlaringResult = filled.FlaringResult;
                        item.FlatteningResult = filled.FlatteningResult;
                        item.IntergranularResult = filled.IntergranularResult;
                        item.PittingResult = filled.PittingResult;
                    }
                }
                Snackbar.Add("检验数据自动填充完成", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "填充失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"填充失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isAutoFilling = false;
        }
    }

    // ========== 提交保存 ==========

    private async Task SubmitCertificate()
    {
        if (string.IsNullOrWhiteSpace(_certOrderNo))
        {
            Snackbar.Add("订单号不能为空", Severity.Warning);
            return;
        }

        if (_subItems.Count == 0)
        {
            Snackbar.Add("子表至少需要一项", Severity.Warning);
            return;
        }

        _isSubmitting = true;
        try
        {
            var request = new CertificateCreateRequest
            {
                OrderNo = _certOrderNo,
                CustomerName = _certCustomerName,
                ProductStandard = _certProductStandard,
                ProductName = _certProductName,
                DeliveryStatus = string.IsNullOrEmpty(_certDeliveryStatus) ? null : Enum.Parse<DeliveryState>(_certDeliveryStatus),
                Remark = _certRemark,
                Items = _subItems.Select(s => new CertificateItemUpdateDto
                {
                    SeqNo = s.SeqNo,
                    InventoryBatchNo = s.InventoryBatchNo,
                    ProductionBatchNo = s.ProductionBatchNo,
                    HeatNo = s.HeatNo,
                    SteelGrade = s.SteelGrade,
                    Specification = s.Specification,
                    LengthDesc = s.LengthDesc,
                    Quantity = s.Quantity,
                    Meters = s.Meters,
                    Weight = s.Weight,
                    ChemC = s.ChemC,
                    ChemSi = s.ChemSi,
                    ChemMn = s.ChemMn,
                    ChemP = s.ChemP,
                    ChemS = s.ChemS,
                    ChemNi = s.ChemNi,
                    ChemCr = s.ChemCr,
                    ChemMo = s.ChemMo,
                    ChemCu = s.ChemCu,
                    ChemN = s.ChemN,
                    ChemNb = s.ChemNb,
                    ChemTi = s.ChemTi,
                    ChemFe = s.ChemFe,
                    ChemAl = s.ChemAl,
                    ChemW = s.ChemW,
                    ChemPREN = s.ChemPREN,
                    InspPMI = s.InspPMI,
                    InspVisual = s.InspVisual,
                    InspDimension = s.InspDimension,
                    InspEndoscopy = s.InspEndoscopy,
                    InspHydro = s.InspHydro,
                    InspUnderwaterPneumatic = s.InspUnderwaterPneumatic,
                    InspEddyCurrent = s.InspEddyCurrent,
                    InspUltrasonic = s.InspUltrasonic,
                    InspPortDye = s.InspPortDye,
                    TensileStrength_1 = s.TensileStrength_1,
                    TensileStrength_2 = s.TensileStrength_2,
                    YieldRp02_1 = s.YieldRp02_1,
                    YieldRp02_2 = s.YieldRp02_2,
                    YieldRp10_1 = s.YieldRp10_1,
                    YieldRp10_2 = s.YieldRp10_2,
                    Elongation_1 = s.Elongation_1,
                    Elongation_2 = s.Elongation_2,
                    Hardness_1 = s.Hardness_1,
                    Hardness_2 = s.Hardness_2,
                    GrainSize_1 = s.GrainSize_1,
                    GrainSize_2 = s.GrainSize_2,
                    FerriteContent_1 = s.FerriteContent_1,
                    FerriteContent_2 = s.FerriteContent_2,
                    FlaringResult = s.FlaringResult,
                    FlatteningResult = s.FlatteningResult,
                    IntergranularResult = s.IntergranularResult,
                    PittingResult = s.PittingResult
                }).ToList()
            };

            var result = await Svc.CreateAsync(request);

            if (result.Success && result.Data != null)
            {
                Snackbar.Add($"质保书 {result.Data.CertificateNo} 创建成功", Severity.Success);
                Navigation.NavigateTo($"/quality/certificates/{result.Data.Id}");
            }
            else
            {
                Snackbar.Add(result.Message ?? "创建失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"提交失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void GoBack() => Navigation.NavigateTo("/quality/certificates");

}
