using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Services;
using MES.Core.Constants;

namespace MES.Blazor.Shared;

/// <summary>检验照片弹窗数据源（过程检验 / 成品检验）</summary>
public enum InspectionPhotoKind
{
    Process,
    Final
}

/// <summary>
/// 检验记录照片弹窗 — 查看 / 补拍 / 删除。
/// 过程检验与成品检验仅在数据源上不同，逻辑同源（避免复制两份）。
/// </summary>
public partial class InspectionPhotoDialog
{
    private const string StaleClientHint = "页面脚本未加载（页面版本过旧），请关闭本页重新打开，或按 Ctrl+Shift+R 强制刷新后再试";

    [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>数据源类型</summary>
    [Parameter] public InspectionPhotoKind Kind { get; set; }

    /// <summary>记录主键（过程检验记录 Id / 成品检验记录 Id）</summary>
    [Parameter] public int RecordId { get; set; }

    /// <summary>生产编号（仅标题展示）</summary>
    [Parameter] public string? BatchNo { get; set; }

    /// <summary>照片张数上限</summary>
    [Parameter] public int MaxCount { get; set; } = QualityPhotoLimits.PerRecord;

    private readonly List<ScanPhotoItem> _photos = new();
    private bool _isLoading = true;
    private bool _isBusy;
    private string? _warningMessage;

    private int PendingCount => _photos.Count(p => p.AttachmentId == null);
    private bool HasPending => PendingCount > 0;

    /// <summary>
    /// 照片列表变化（新增 / 移除）→ 重渲染本组件。
    /// ⚠️ 必需：照片在 ScanPhotoPicker 内部增删，子组件事件不会刷新本组件，
    /// 缺此回调会导致「上传照片」按钮的 Disabled 恒为初始值（选完照片仍不可点）。
    /// </summary>
    private Task OnPhotosChanged() => InvokeAsync(StateHasChanged);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var metas = await LoadMetaAsync();
            if (metas == null)
            {
                Snackbar.Add("加载照片失败", Severity.Error);
                return;
            }

            foreach (var m in metas)
            {
                var bytes = await DownloadAsync(m.Id);
                if (bytes is not { Length: > 0 }) continue;

                var base64 = Convert.ToBase64String(bytes);
                string? url;
                try
                {
                    url = await JS.InvokeAsync<string?>("MES.base64ToObjectUrl", base64, m.ContentType);
                }
                catch (JSException)
                {
                    // 脚本不可用 → 预览无法生成，提示后停止（后续照片同样无法预览）
                    Snackbar.Add(StaleClientHint, Severity.Warning);
                    break;
                }
                if (string.IsNullOrEmpty(url)) continue;

                _photos.Add(new ScanPhotoItem
                {
                    AttachmentId = m.Id,
                    FileName = m.FileName,
                    ContentType = m.ContentType,
                    Base64 = base64,
                    PreviewUrl = url
                });
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>上传全部待上传照片（逐张隔离：单张失败不阻断其余）</summary>
    private async Task UploadPendingAsync()
    {
        var pending = _photos.Where(p => p.AttachmentId == null).ToList();
        if (pending.Count == 0) return;

        _isBusy = true;
        _warningMessage = null;
        var failed = 0;

        foreach (var photo in pending)
        {
            try
            {
                var bytes = Convert.FromBase64String(photo.Base64);

                if (Kind == InspectionPhotoKind.Process)
                {
                    var r = await ProcessService.UploadAttachmentAsync(RecordId, bytes, photo.FileName, photo.ContentType);
                    if (r.Success && r.Data != null) photo.AttachmentId = r.Data.Id;
                    else { failed++; _warningMessage = r.Message ?? "上传失败"; }
                }
                else
                {
                    var r = await FinalService.UploadAttachmentAsync(RecordId, bytes, photo.FileName, photo.ContentType);
                    if (r.Success && r.Data != null) photo.AttachmentId = r.Data.Id;
                    else { failed++; _warningMessage = r.Message ?? "上传失败"; }
                }
            }
            catch (Exception ex)
            {
                failed++;
                _warningMessage = ex.Message;
            }
        }

        _isBusy = false;

        if (failed == 0) Snackbar.Add($"已上传 {pending.Count} 张照片", Severity.Success);
        else Snackbar.Add($"有 {failed} 张照片上传失败：{_warningMessage}", Severity.Warning);
    }

    /// <summary>删除既有照片（回调 ScanPhotoPicker，返回 true 后由组件移除本地项）</summary>
    private async Task<bool> DeleteExistingAsync(ScanPhotoItem photo)
    {
        if (!photo.AttachmentId.HasValue) return true;

        try
        {
            var r = Kind == InspectionPhotoKind.Process
                ? await ProcessService.DeleteAttachmentAsync(RecordId, photo.AttachmentId.Value)
                : await FinalService.DeleteAttachmentAsync(RecordId, photo.AttachmentId.Value);

            if (!r.Success)
            {
                Snackbar.Add(r.Message ?? "删除照片失败", Severity.Error);
                return false;
            }

            Snackbar.Add("照片已删除", Severity.Success);
            return true;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"删除照片失败: {ex.Message}", Severity.Error);
            return false;
        }
    }

    private async Task<List<PhotoMeta>?> LoadMetaAsync()
    {
        if (Kind == InspectionPhotoKind.Process)
        {
            var r = await ProcessService.GetAttachmentsAsync(RecordId);
            return r.Success
                ? r.Data?.Select(a => new PhotoMeta(a.Id, a.FileName, a.ContentType)).ToList()
                : null;
        }

        var f = await FinalService.GetAttachmentsAsync(RecordId);
        return f.Success
            ? f.Data?.Select(a => new PhotoMeta(a.Id, a.FileName, a.ContentType)).ToList()
            : null;
    }

    private Task<byte[]?> DownloadAsync(int attachmentId)
        => Kind == InspectionPhotoKind.Process
            ? ProcessService.GetAttachmentBytesAsync(RecordId, attachmentId)
            : FinalService.GetAttachmentBytesAsync(RecordId, attachmentId);

    private void Close() => MudDialog.Close();

    public async ValueTask DisposeAsync()
    {
        try { await JS.InvokeVoidAsync("MES.revokeAllObjectUrls"); } catch { }
    }

    private sealed record PhotoMeta(int Id, string FileName, string ContentType);
}
