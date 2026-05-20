using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 数据修复服务接口 — 一键修复所有系统计算字段
/// </summary>
public interface IDataFixService
{
    /// <summary>
    /// 一键修复所有系统计算字段
    /// </summary>
    Task<DataFixReport> FixAllAsync();
}
