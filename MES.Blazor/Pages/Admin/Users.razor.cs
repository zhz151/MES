using Microsoft.AspNetCore.Components;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.DTOs.Auth;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Admin;

public partial class Users
{
    private MudTable<UserDto>? table;
    private List<UserDto> _pageItems = new();
    private int _totalCount;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    private string sortColumn = "UserName";
    private bool sortDescending = false;

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // 默认列顺序 = 用户定稿：用户名 邮箱 姓名 备注 角色 状态 最后登录（序号固定最前）
    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "Seq",          Label = "序号",   IsRequired = true },
        new() { Key = "UserName",     Label = "用户名",  SortKey = "username",     IsRequired = true },
        new() { Key = "Email",        Label = "邮箱",    SortKey = "email" },
        new() { Key = "FullName",     Label = "姓名",    SortKey = "fullname" },
        new() { Key = "Remark",       Label = "用户备注", SortKey = "remark" },
        new() { Key = "Roles",        Label = "角色" },
        new() { Key = "IsActive",     Label = "状态",    SortKey = "isactive" },
        new() { Key = "LastLoginAt",  Label = "最后登录", SortKey = "lastloginat" },
    };

    // 版本化列偏好 key：列顺序/默认显隐调整后，已保存过 localStorage 的用户也能看到新默认
    private const string ColumnPrefsVersion = "v1";

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("users", ColumnPrefsVersion, _allColumns);
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<UserDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "username";

            // 首次加载覆盖页码
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
                IsDescending = sortDescending
            };

            var result = await UserService.GetPagedAsync(query);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<UserDto> { Items = _pageItems, TotalItems = _totalCount };

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

        await SavePageStateAsync();
        return new TableData<UserDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 排序 ==========

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
        var saved = await ColumnPrefs.LoadAsync("users", ColumnPrefsVersion);
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

        // 恢复排序/搜索状态
        var savedState = await PageState.LoadAsync("users");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "UserName";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && table != null)
            await table.ReloadServerData();
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage
        };
        await PageState.SaveAsync("users", state);
    }

    // ========== 创建用户弹窗 ==========

    private async Task OpenCreateDialog()
    {
        var model = new CreateUserRequest();
        var parameters = new DialogParameters
        {
            ["Title"] = "新建用户",
            ["Model"] = model,
            ["IsCreate"] = true,
        };
        var dialog = DialogService.Show<UserEditDialog>("新建用户", parameters, new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true
        });
        var result = await dialog.Result;
        if (!result.Canceled && result.Data is CreateUserRequest request)
        {
            try
            {
                var apiResult = await UserService.CreateAsync(request);
                if (apiResult.Success)
                {
                    Snackbar.Add("用户创建成功", Severity.Success);
                    if (table != null) await table.ReloadServerData();
                }
                else
                {
                    Snackbar.Add(apiResult.Message ?? "创建失败", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"创建失败: {ex.Message}", Severity.Error);
            }
        }
    }

    // ========== 编辑用户弹窗 ==========

    private async Task OpenEditDialog(UserDto item)
    {
        var model = new UpdateUserRequest
        {
            FullName = item.FullName,
            Remark = item.Remark,
            IsActive = item.IsActive,
            Roles = item.Roles.ToList()
        };
        var parameters = new DialogParameters
        {
            ["Title"] = "编辑用户",
            ["Model"] = model,
            ["IsCreate"] = false,
        };
        var dialog = DialogService.Show<UserEditDialog>("编辑用户", parameters, new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true
        });
        var result = await dialog.Result;
        if (!result.Canceled && result.Data is UpdateUserRequest request)
        {
            try
            {
                var apiResult = await UserService.UpdateAsync(item.Id, request);
                if (apiResult.Success)
                {
                    Snackbar.Add("用户更新成功", Severity.Success);
                    if (table != null) await table.ReloadServerData();
                }
                else
                {
                    Snackbar.Add(apiResult.Message ?? "更新失败", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"更新失败: {ex.Message}", Severity.Error);
            }
        }
    }

    // ========== 重置密码弹窗 ==========

    private async Task OpenResetPasswordDialog(UserDto item)
    {
        var parameters = new DialogParameters
        {
            ["Title"] = $"重置密码 - {item.Email}",
            ["UserId"] = item.Id,
        };
        var dialog = DialogService.Show<ResetPasswordDialog>("重置密码", parameters, new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true
        });
        var result = await dialog.Result;
        if (!result.Canceled && result.Data is string userId)
        {
            // 密码重置在 Dialog 内已处理，成功则刷新
            if (table != null) await table.ReloadServerData();
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(UserDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除用户 \"{item.Email}\" 吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await UserService.DeleteAsync(item.Id);
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
}
