using Microsoft.AspNetCore.Components;
using MudBlazor;
using MES.Blazor.Components;
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
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private int _loadVersion;
    private bool _resetToFirstPage;

    private string sortColumn = "CreatedTime";
    private bool sortDescending = true;

    // ========== 服务端数据加载 ==========

    private async Task<TableData<UserDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
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
                SortBy = sortColumn.ToLower(),
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
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        _resetToFirstPage = true;
        if (table != null) await table.ReloadServerData();
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
