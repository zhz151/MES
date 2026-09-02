using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Services.Payroll;
using MES.Core.Models;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 生产计件类别导入弹窗（2026-09-02）。kind=category 类别定义 / tier 维档系数。
/// 流程：下载模板 → 选文件 → 解析预览（新增/覆盖/错误）→ 执行导入（整体成功才提交）。
/// </summary>
public partial class PieceRateCategoryImportDialog
{
    /// <summary>导入类型：category|tier（父级传入）</summary>
    [Parameter] public string Kind { get; set; } = "category";

    [Inject] private PieceRateCategoryImportService Import { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = default!;

    private bool _isTier => Kind == "tier";
    private string _kindLabel => _isTier ? "维档系数" : "类别定义";
    private const long MaxFileBytes = 20 * 1024 * 1024;

    private string? _fileName;
    private byte[]? _fileData;
    private ImportPreviewResult? _preview;
    private bool _isPreviewing;
    private bool _isImporting;
    private bool _hasFile;

    protected override void OnInitialized()
    {
        Kind = _isTier ? "tier" : "category";
    }

    private async Task DownloadTemplateAsync()
    {
        try
        {
            var bytes = await Import.GetTemplateAsync(Kind);
            var fileName = _isTier ? "计件维档模板.xlsx" : "计件类别模板.xlsx";
            await DownloadFileAsync(bytes, fileName);
            Snackbar.Add("模板已下载（1 行为示例，导入前请删除示例行）", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"模板下载失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null)
        {
            ClearFile();
            return;
        }
        if (!file.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            Snackbar.Add("请上传 .xlsx 格式的 Excel 文件", Severity.Error);
            ClearFile();
            return;
        }
        using var ms = new MemoryStream();
        await file.OpenReadStream(MaxFileBytes).CopyToAsync(ms);
        _fileData = ms.ToArray();
        _fileName = file.Name;
        _preview = null;
        _hasFile = true;
    }

    private void ClearFile()
    {
        _fileName = null;
        _fileData = null;
        _preview = null;
        _hasFile = false;
    }

    private async Task PreviewAsync()
    {
        if (!_hasFile || _fileData == null || string.IsNullOrEmpty(_fileName)) return;
        _isPreviewing = true;
        try
        {
            var result = await Import.PreviewImportAsync(Kind, _fileData, _fileName);
            if (result.Success && result.Data != null)
            {
                _preview = result.Data;
                if (_preview.ErrorCount > 0)
                    Snackbar.Add($"存在 {_preview.ErrorCount} 行错误，预览见下方表格", Severity.Warning);
            }
            else
            {
                Snackbar.Add(result.Message ?? "预览失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"预览失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isPreviewing = false;
        }
    }

    private async Task DoImportAsync()
    {
        if (!_hasFile || _fileData == null || string.IsNullOrEmpty(_fileName)) return;
        _isImporting = true;
        try
        {
            var result = await Import.ImportAsync(Kind, _fileData, _fileName);
            if (result.Success && result.Data != null && !result.Data.HasRolledBack)
            {
                Snackbar.Add($"导入成功: {result.Data.SuccessCount} 行已覆盖更新", Severity.Success);
                MudDialog.Close(DialogResult.Ok(true));
            }
            else if (result.Data != null && result.Data.HasRolledBack)
            {
                Snackbar.Add($"导入未提交: {result.Data.RollbackReason ?? "未知错误"}", Severity.Error);
            }
            else
            {
                Snackbar.Add(result.Message ?? "导入失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"导入失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isImporting = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task DownloadFileAsync(byte[] data, string fileName)
    {
        var base64 = Convert.ToBase64String(data);
        await JS.InvokeVoidAsync("downloadFile", base64, fileName);
    }
}
