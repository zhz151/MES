using MES.Core.DTOs.Shared;
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
    /// 获取指定「工单号（订单+主号+次号）」的定尺长度集合（完全匹配判定用）
    /// </summary>
    Task<HashSet<decimal>> GetLengthsByWorkOrderNoAsync(string workOrderNo);

    /// <summary>
    /// 获取全部定尺工单长度映射（按工单号 / 按「订单号|主号」），批量匹配计算与回填一次取全表
    /// </summary>
    Task<FixedLengthLengthMaps> GetLengthMapsAsync();

    /// <summary>
    /// 获取全部定尺工单定尺数据列表（主号级按长度实时聚合）
    /// </summary>
    Task<List<FixedLengthWorkOrderListDto>> GetListAsync();

    /// <summary>
    /// 生成打印 PDF（前端已准备数据，枚举字段已转中文）
    /// </summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns);
}
