using Microsoft.EntityFrameworkCore;

using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.Configuration;
using MES.Data;

namespace MES.Services.Configuration;

/// <summary>
/// 操作人实名校验：硬校验「姓名(工号)/姓名」必须命中启用员工表。
/// </summary>
public class OperatorNameValidator : IOperatorNameValidator
{
    private readonly AppDbContext _context;

    public OperatorNameValidator(AppDbContext context) => _context = context;

    public async Task<ActiveEmployeeSet> LoadActiveAsync()
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new { e.Name, e.Code })
            .ToListAsync();

        var set = new ActiveEmployeeSet();
        foreach (var e in employees)
        {
            if (!string.IsNullOrWhiteSpace(e.Name)) set.Names.Add(e.Name);
            if (!string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(e.Code))
                set.ByCode[e.Code] = e.Name;
        }
        return set;
    }

    public async Task EnsureValidOrThrowAsync(string? operatorText, string? rowLabel = null)
    {
        var active = await LoadActiveAsync();
        var unmatched = OperatorNameHelper.FindUnmatched(active, operatorText);
        if (unmatched.Count > 0)
        {
            var prefix = string.IsNullOrEmpty(rowLabel) ? "" : $"{rowLabel}：";
            throw new BusinessException($"{prefix}操作人「{string.Join("、", unmatched)}」不在启用员工表中，请选择有效操作人");
        }
    }
}
