using System.ComponentModel.DataAnnotations;

namespace MES.Core.Models;

/// <summary>
/// 通用分页查询参数
/// </summary>
public class QueryParams
{
    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    [Range(1, 100000, ErrorMessage = "每页条数必须在1-100000之间")]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 搜索关键字
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 排序字段
    /// </summary>
    public string SortBy { get; set; } = "CreatedTime";

    /// <summary>
    /// 是否降序
    /// </summary>
    public bool IsDescending { get; set; } = true;

    /// <summary>
    /// 每列独立筛选条件列表
    /// </summary>
    public List<FilterDescriptor>? Filters { get; set; }

    /// <summary>
    /// 到料日期范围筛选-开始（仅检验到料使用）
    /// </summary>
    public DateTime? ReceiveDateFrom { get; set; }

    /// <summary>
    /// 到料日期范围筛选-结束（仅检验到料使用）
    /// </summary>
    public DateTime? ReceiveDateTo { get; set; }

    /// <summary>
    /// 执行日期范围筛选-开始（仅生产记录使用）
    /// </summary>
    public DateTime? ExecDateFrom { get; set; }

    /// <summary>
    /// 执行日期范围筛选-结束（仅生产记录使用）
    /// </summary>
    public DateTime? ExecDateTo { get; set; }

    /// <summary>
    /// 检验日期范围筛选-开始（仅过程检验使用）
    /// </summary>
    public DateTime? InspectionDateFrom { get; set; }

    /// <summary>
    /// 检验日期范围筛选-结束（仅过程检验使用）
    /// </summary>
    public DateTime? InspectionDateTo { get; set; }

    /// <summary>
    /// 发出日期范围筛选-开始（仅工段委外使用）
    /// </summary>
    public DateTime? SendOutDateFrom { get; set; }

    /// <summary>
    /// 发出日期范围筛选-结束（仅工段委外使用）
    /// </summary>
    public DateTime? SendOutDateTo { get; set; }

    /// <summary>
    /// 实际回收日期范围筛选-开始（仅工段委外使用）
    /// </summary>
    public DateTime? ActualRecoveryDateFrom { get; set; }

    /// <summary>
    /// 实际回收日期范围筛选-结束（仅工段委外使用）
    /// </summary>
    public DateTime? ActualRecoveryDateTo { get; set; }

    /// <summary>
    /// 回收日期范围筛选-开始（仅委外回收使用）
    /// </summary>
    public DateTime? RecoveryDateFrom { get; set; }

    /// <summary>
    /// 回收日期范围筛选-结束（仅委外回收使用）
    /// </summary>
    public DateTime? RecoveryDateTo { get; set; }

    /// <summary>
    /// 入缸日期范围筛选-开始（仅去油酸洗使用）
    /// </summary>
    public DateTime? InDateFrom { get; set; }

    /// <summary>
    /// 入缸日期范围筛选-结束（仅去油酸洗使用）
    /// </summary>
    public DateTime? InDateTo { get; set; }

    /// <summary>
    /// 完工日期范围筛选-开始（仅去油酸洗使用）
    /// </summary>
    public DateTime? CompleteDateFrom { get; set; }

    /// <summary>
    /// 完工日期范围筛选-结束（仅去油酸洗使用）
    /// </summary>
    public DateTime? CompleteDateTo { get; set; }

    /// <summary>
    /// 来料日期范围筛选-开始（仅炉号登记使用）
    /// </summary>
    public DateTime? IncomingDateFrom { get; set; }

    /// <summary>
    /// 来料日期范围筛选-结束（仅炉号登记使用）
    /// </summary>
    public DateTime? IncomingDateTo { get; set; }

    /// <summary>
    /// 反馈日期范围筛选-开始（仅NCR使用）
    /// </summary>
    public DateTime? ReportDateFrom { get; set; }

    /// <summary>
    /// 反馈日期范围筛选-结束（仅NCR使用）
    /// </summary>
    public DateTime? ReportDateTo { get; set; }

    /// <summary>
    /// 入库日期范围筛选-开始（仅订单成品(实时库存)使用，原「待发货项」）
    /// </summary>
    public DateTime? InboundDateFrom { get; set; }

    /// <summary>
    /// 入库日期范围筛选-结束（仅订单成品(实时库存)使用，原「待发货项」）
    /// </summary>
    public DateTime? InboundDateTo { get; set; }

    /// <summary>
    /// 接单日期范围筛选-开始（仅客户管理使用，取 SalesOrder.SignDate）
    /// </summary>
    /// <remarks>
    /// 选定后「客户往来」的接单类列按 SignDate 落区间重算；与 <see cref="ShipDateFrom"/>/<see cref="ShipDateTo"/>
    /// 互相独立、可叠加。待发货/待在产属存量口径无日期语义，不受影响。
    /// </remarks>
    public DateTime? SignDateFrom { get; set; }

    /// <summary>
    /// 接单日期范围筛选-结束（仅客户管理使用，取 SalesOrder.SignDate；闭区间含结束日）
    /// </summary>
    public DateTime? SignDateTo { get; set; }

    /// <summary>
    /// 发货日期范围筛选-开始（仅客户管理使用，取出库记录 OutboundDate）
    /// </summary>
    /// <remarks>
    /// 选定后「客户往来」的已发货类列按 OutboundDate 落区间重算；与 <see cref="SignDateFrom"/>/<see cref="SignDateTo"/>
    /// 互相独立、可叠加。待发货/待在产属存量口径无日期语义，不受影响。
    /// </remarks>
    public DateTime? ShipDateFrom { get; set; }

    /// <summary>
    /// 发货日期范围筛选-结束（仅客户管理使用，取出库记录 OutboundDate；闭区间含结束日）
    /// </summary>
    public DateTime? ShipDateTo { get; set; }

    /// <summary>
    /// 供应商出单日期范围筛选-开始（仅供应商管理使用，取采购/委外单 OrderDate）
    /// </summary>
    /// <remarks>
    /// 选定后「供应商往来」的出单类列按 OrderDate 落区间重算；与 <see cref="SupplierArrivalDateFrom"/>/<see cref="SupplierArrivalDateTo"/>
    /// 互相独立、可叠加。待收货属存量口径无日期语义，不受影响。
    /// </remarks>
    public DateTime? SupplierOrderDateFrom { get; set; }

    /// <summary>
    /// 供应商出单日期范围筛选-结束（仅供应商管理使用，取采购/委外单 OrderDate；闭区间含结束日）
    /// </summary>
    public DateTime? SupplierOrderDateTo { get; set; }

    /// <summary>
    /// 供应商到货日期范围筛选-开始（仅供应商管理使用，取入厂批 InventoryBatch.InboundDate）
    /// </summary>
    /// <remarks>
    /// 选定后「供应商往来」的到货类列（到货净重/到货货款）与「本年退货」列改按 InboundDate / OutboundDate 落区间重算；
    /// 与 <see cref="SupplierOrderDateFrom"/>/<see cref="SupplierOrderDateTo"/> 互相独立、可叠加。
    /// </remarks>
    public DateTime? SupplierArrivalDateFrom { get; set; }

    /// <summary>
    /// 供应商到货日期范围筛选-结束（仅供应商管理使用，取入厂批 InventoryBatch.InboundDate；闭区间含结束日）
    /// </summary>
    public DateTime? SupplierArrivalDateTo { get; set; }

    /// <summary>
    /// 委外单位发出日期范围筛选-开始（仅委外单位档案使用，取工段委外单 SendOutDate）
    /// </summary>
    /// <remarks>
    /// 选定后「委外单位往来」的发出类列按 SendOutDate 落区间重算；与 <see cref="VendorRecoveryDateFrom"/>/<see cref="VendorRecoveryDateTo"/>
    /// 互相独立、可叠加。在委外未回收属存量口径无日期语义，不受影响。
    /// </remarks>
    public DateTime? VendorSendDateFrom { get; set; }

    /// <summary>
    /// 委外单位发出日期范围筛选-结束（仅委外单位档案使用，取工段委外单 SendOutDate；闭区间含结束日）
    /// </summary>
    public DateTime? VendorSendDateTo { get; set; }

    /// <summary>
    /// 委外单位回收日期范围筛选-开始（仅委外单位档案使用，取委外回收记录 RecoveryDate）
    /// </summary>
    /// <remarks>
    /// 选定后「委外单位往来」的回收/退回类列改按 RecoveryDate 落区间重算；
    /// 与 <see cref="VendorSendDateFrom"/>/<see cref="VendorSendDateTo"/> 互相独立、可叠加。
    /// </remarks>
    public DateTime? VendorRecoveryDateFrom { get; set; }

    /// <summary>
    /// 委外单位回收日期范围筛选-结束（仅委外单位档案使用，取委外回收记录 RecoveryDate；闭区间含结束日）
    /// </summary>
    public DateTime? VendorRecoveryDateTo { get; set; }

    /// <summary>
    /// 计算跳过的记录数
    /// </summary>
    public int Skip => (PageIndex - 1) * PageSize;
}