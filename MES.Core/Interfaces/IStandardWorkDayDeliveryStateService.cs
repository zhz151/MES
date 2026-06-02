using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 交货状态附加天数服务接口
/// </summary>
public interface IStandardWorkDayDeliveryStateService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<StandardWorkDayDeliveryStateDto>> GetPagedAsync(QueryParams query);

    /// <summary>根据 ID 获取</summary>
    Task<StandardWorkDayDeliveryStateDto?> GetByIdAsync(int id);

    /// <summary>新增或更新</summary>
    Task<bool> SaveAsync(StandardWorkDayDeliveryStateDto dto);

    /// <summary>删除</summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 获取交货状态附加天数映射：key=DeliveryState(枚举名), value=ExtraDays
    /// 含默认配置（key=""）
    /// </summary>
    Task<Dictionary<string, double>> GetDeliveryStateExtraDaysMapAsync();
}
