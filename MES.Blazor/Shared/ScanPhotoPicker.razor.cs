using System.Text.Json.Serialization;
using MES.Core.Constants;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace MES.Blazor.Shared;

/// <summary>
/// 扫码报工照片选择器（巡检照片 / 整改验证照片 / 问题照片 共用）。
/// 只管理本地列表：压缩、预览、待上传项移除；既有附件（AttachmentId 非空）的删除交回父组件落库。
/// </summary>
public partial class ScanPhotoPicker
{
    /// <summary>图片压缩最长边</summary>
    private const int MaxImageSize = 1600;

    /// <summary>JPEG 压缩质量</summary>
    private const double ImageQuality = 0.8;

    private const string StaleClientHint = "页面脚本未加载（页面版本过旧），请关闭本页重新打开，或按 Ctrl+Shift+R 强制刷新后再试";

    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    /// <summary>标题（如「巡检照片」）</summary>
    [Parameter] public string Title { get; set; } = "照片";

    /// <summary>照片列表（父子共用同一实例，组件内直接增删）</summary>
    [Parameter] public List<ScanPhotoItem> Photos { get; set; } = new();

    /// <summary>张数上限（默认取通用按记录上限；巡检按类型上限请显式传参）</summary>
    [Parameter] public int MaxCount { get; set; } = QualityPhotoLimits.PerRecord;

    /// <summary>禁用（上传/提交中）</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>是否允许新增（false = 只读展示既有照片，如第二次扫码时的第一次巡检照片）</summary>
    [Parameter] public bool AllowAdd { get; set; } = true;

    /// <summary>
    /// 删除既有附件回调（AttachmentId 非空时触发）；返回 true 表示已删除，组件据此移除本地项。
    /// 未提供时既有附件不可删除（仅展示）。
    /// </summary>
    [Parameter] public Func<ScanPhotoItem, Task<bool>>? OnDeleteExisting { get; set; }

    /// <summary>
    /// 列表变化回调（新增 / 移除后触发）。
    /// ⚠️ 父组件若存在依赖照片数量/张数的 UI（如「上传照片(N)」按钮的 Disabled），**必须接线本回调**：
    /// Photos 为父子共用实例、由本组件直接增删，子组件的事件只会重渲染自身，
    /// 父组件不会自动刷新 —— 否则表现为「选完照片后按钮仍是灰的、点了没反应」。
    /// </summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    private ElementReference _fileInput;
    private bool _isReading;

    private async Task OpenFilePicker()
    {
        try
        {
            await JS.InvokeVoidAsync("MES.clickElement", _fileInput);
        }
        catch (JSException)
        {
            Snackbar.Add(StaleClientHint, Severity.Warning);
        }
    }

    private async Task OnFilesSelected(ChangeEventArgs e)
    {
        if (Photos.Count >= MaxCount)
        {
            Snackbar.Add($"最多上传 {MaxCount} 张照片", Severity.Warning);
            return;
        }

        _isReading = true;
        try
        {
            var results = await JS.InvokeAsync<List<CompressedImage>>(
                "MES.readCompressedFiles", _fileInput, MaxImageSize, ImageQuality);

            foreach (var r in results)
            {
                if (Photos.Count >= MaxCount)
                {
                    Snackbar.Add($"超出上限，仅保留前 {MaxCount} 张", Severity.Warning);
                    break;
                }
                if (string.IsNullOrEmpty(r.Data)) continue;

                var contentType = string.IsNullOrWhiteSpace(r.ContentType) ? "image/jpeg" : r.ContentType;
                var url = await JS.InvokeAsync<string?>("MES.base64ToObjectUrl", r.Data, contentType);
                if (string.IsNullOrEmpty(url)) continue;

                Photos.Add(new ScanPhotoItem
                {
                    FileName = string.IsNullOrWhiteSpace(r.FileName) ? "photo.jpg" : r.FileName,
                    ContentType = contentType,
                    Base64 = r.Data,
                    PreviewUrl = url
                });
            }
        }
        catch (JSException)
        {
            Snackbar.Add(StaleClientHint, Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"读取照片失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isReading = false;
            // 放 finally：部分成功（中途 JS 异常/超上限）也要让父组件刷新照片数量
            await OnChanged.InvokeAsync();
        }
    }

    private async Task RemoveAsync(ScanPhotoItem photo)
    {
        if (photo.AttachmentId.HasValue)
        {
            if (OnDeleteExisting == null)
            {
                Snackbar.Add("该照片已保存，无法在此删除", Severity.Warning);
                return;
            }
            var deleted = await OnDeleteExisting(photo);
            if (!deleted) return;
        }
        Photos.Remove(photo);
        await InvokeAsync(StateHasChanged);
        await OnChanged.InvokeAsync();
    }

    /// <summary>JS 压缩结果（与 MES.compressImage 返回结构一致）</summary>
    private class CompressedImage
    {
        [JsonPropertyName("data")] public string Data { get; set; } = "";
        [JsonPropertyName("fileName")] public string FileName { get; set; } = "";
        [JsonPropertyName("contentType")] public string ContentType { get; set; } = "";
    }
}
