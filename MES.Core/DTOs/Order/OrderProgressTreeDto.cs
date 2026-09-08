namespace MES.Core.DTOs.Order;

/// <summary>
/// 订单进度树 DTO（一级=订单号）。只读树：
/// 二级=订单号+主号（含完结主号），三级=原料锁定/生产执行/成品检验/成品入库四分支，
/// 四级=叶子重量(kg)。零值叶省略、空分支为 null（前端不显示该分支）。
/// 原料锁定/生产执行分支取自 WorkOrderExecutionSummary 快照口径；成品检验/成品入库为实时口径；
/// 主号头的标准牌号/产品标准与整单含项次数为 OrderItem 实时口径，其余头部字段取快照。
/// </summary>
public class OrderProgressTreeDto
{
    /// <summary>订单号</summary>
    public string SalesOrderNo { get; set; } = null!;

    /// <summary>客户名称（快照）</summary>
    public string? CustomerName { get; set; }

    /// <summary>业务员（快照）</summary>
    public string? Salesman { get; set; }

    /// <summary>签订日期（快照，同订单各工单一致取代表行）</summary>
    public DateTime? SignDate { get; set; }

    /// <summary>交期截止（快照，= 该订单各工单交货日期最大值）</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>是否延期罚款（快照，任一工单有即 true）</summary>
    public bool DelayPenalty { get; set; }

    /// <summary>订单总重量(kg) = Σ 全部工单 TotalWeight（快照）</summary>
    public decimal OrderTotalWeightKg { get; set; }

    /// <summary>含项次数 = 该订单被工单引用的去重项次 Sequence 数（OrderItem 实时）</summary>
    public int ItemCount { get; set; }

    /// <summary>主号进度（排序：一律按主号从小到大，混排含完结主号）</summary>
    public List<OrderMainProgressDto> MainNos { get; set; } = new();
}

/// <summary>
/// 二级：订单号+主号
/// </summary>
public class OrderMainProgressDto
{
    /// <summary>主号号（不含订单前缀，如 X01；根节点已含订单号，前端直接显示主号号）</summary>
    public string ProductionMainNo { get; set; } = null!;

    /// <summary>主号关注档位（0=主号暂停 1=主号完成 2=原料锁定 3=生产执行 4=成品检验）</summary>
    public int ScheduleStage { get; set; }

    /// <summary>是否完结主号（ScheduleStage==1，前端灰显「主号完成/已完结」并仅显示成品入库分支）</summary>
    public bool IsCompleted => ScheduleStage == 1;

    /// <summary>紧急程度（英文 Key）</summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>标准牌号（该主号下首个被引用项次的 StandardGrade，OrderItem 实时）</summary>
    public string? StandardGrade { get; set; }

    /// <summary>产品标准（该主号下首个被引用项次的 StandardNo，OrderItem 实时）</summary>
    public string? ProductStandard { get; set; }

    /// <summary>尺寸/规格（快照，主号级代表工单）</summary>
    public string? Specification { get; set; }

    /// <summary>长度状态（快照，存枚举名，前端经 EnumHelper 转中文）</summary>
    public string? LengthStatus { get; set; }

    /// <summary>交货状态（快照，存枚举名，前端经 EnumHelper 转中文）</summary>
    public string? DeliveryState { get; set; }

    /// <summary>支数 = Σ 工单 TotalQuantity（快照）</summary>
    public int QuantitySum { get; set; }

    /// <summary>预计完成日（快照，= Σ 工单 EstimatedProcessCompletionDate 最大值）</summary>
    public DateTime? EstimatedCompletionDate { get; set; }

    /// <summary>合同重量(kg) = Σ 工单 TotalWeight（快照）</summary>
    public decimal TotalWeightKg { get; set; }

    /// <summary>原料锁定分支（仅 ScheduleStage=2 有值，单叶 质量补料/生产返整补足/执行用料计划/完善用料计划）</summary>
    public MainProgressBranchDto? RawMaterialLock { get; set; }

    /// <summary>生产执行[待产]分支（8 在产节点待量叶）</summary>
    public MainProgressBranchDto? Production { get; set; }

    /// <summary>成品检验分支（待到料/待检验/检验中 3 叶，成检计划实时）</summary>
    public MainProgressBranchDto? FinalInspection { get; set; }

    /// <summary>成品入库分支（入库/出库/库存 3 叶，实时，唯一完结主号仍有的分支）</summary>
    public MainProgressBranchDto? Warehousing { get; set; }
}

/// <summary>
/// 三级：阶段分支（原料锁定/生产执行/成品检验/成品入库）
/// </summary>
public class MainProgressBranchDto
{
    /// <summary>分支英文 Key</summary>
    public string Key { get; set; } = null!;

    /// <summary>分支标题（中文）</summary>
    public string Title { get; set; } = null!;

    /// <summary>四级叶子（重量 &gt; 0 才入列；原料锁定单叶恒携带）</summary>
    public List<MainProgressLeafDto> Leaves { get; set; } = new();
}

/// <summary>
/// 四级：叶子（仅重量，kg，前端整数化显示）
/// </summary>
public class MainProgressLeafDto
{
    /// <summary>叶子英文/稳定 Key</summary>
    public string Key { get; set; } = null!;

    /// <summary>叶子标签（中文）</summary>
    public string Text { get; set; } = null!;

    /// <summary>重量(kg)</summary>
    public decimal WeightKg { get; set; }
}
