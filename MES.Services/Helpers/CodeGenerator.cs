using Microsoft.EntityFrameworkCore;

namespace MES.Services.Helpers;

/// <summary>
/// 编号生成器（6位：2字母前缀 + 4位数字流水）
/// 示例：MA0001, SU0042
/// </summary>
public static class CodeGenerator
{
    /// <summary>
    /// 生成下一个可用编码
    /// </summary>
    /// <param name="existingCodes">已存在的编码查询（IQueryable<string>）</param>
    /// <param name="prefix">编码前缀，如 "MA"、"SU"</param>
    /// <returns>下一个可用编码，如 "MA0042"</returns>
    public static async Task<string> GenerateNextAsync(IQueryable<string> existingCodes, string prefix)
    {
        var maxCode = await existingCodes
            .Where(c => c.StartsWith(prefix) && c.Length == 6)
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();

        if (maxCode == null)
            return $"{prefix}0001";

        var number = int.Parse(maxCode[2..]) + 1;
        return $"{prefix}{number:D4}";
    }
}
