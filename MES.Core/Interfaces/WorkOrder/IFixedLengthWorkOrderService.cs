using MES.Core.DTOs.WorkOrder;

namespace MES.Core.Interfaces.WorkOrder;

/// <summary>
/// 定尺工单服务接口
/// </summary>
public interface IFixedLengthWorkOrderService
{
    /// <summary>
    /// 获取指定「订单号+主号」的定尺长度集合（跨模块校验用：批次 → 订单号+主号 → 长度集合）
    /// </summary>
    Task<HashSet<decimal>> GetLengthsByMainNoAsync(string salesOrderNo, string productionMainNo);

    /// <summary>
    /// 获取全部定尺工单定尺数据列表（主号级按长度实时聚合）
    /// </summary>
    Task<List<FixedLengthWorkOrderListDto>> GetListAsync();
}
