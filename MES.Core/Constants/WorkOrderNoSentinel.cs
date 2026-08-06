namespace MES.Core.Constants;

/// <summary>
/// 工单号哨兵值常量。用于替代散落在各服务/页面的 "非工单" 中文字面量，
/// 防止拼写漂移、统一"未关联正式工单"的业务语义。
/// </summary>
public static class WorkOrderNoSentinel
{
    /// <summary>未关联正式工单的哨兵工单号</summary>
    public const string NotWorkOrder = "非工单";
}
