namespace MES.Core.Constants;

/// <summary>
/// 成检计划看板（KanbanStage）四档中文状态串常量。
/// 存储值即显示值（中文）——由 FinalInspectionPlanService.GetKanbanAsync 判定产生（四档判定顺序：
/// 检验 &gt; 到料 &gt; 裸批次），前端 Tab/筛选、生产总览、工单执行状况 Stage3 复用同一判定。
/// 属固定四值状态机（枚举化，非配置字典），改名需前后端同步，故集中收口。
/// </summary>
public static class KanbanStageKeys
{
    /// <summary>待到料：无检验记录、无到料（裸批次）</summary>
    public const string WaitingMaterial = "待到料";

    /// <summary>待检验：无检验记录、已有到料</summary>
    public const string WaitingInspection = "待检验";

    /// <summary>检验中：已有检验记录但未全部检验完毕，且未入库</summary>
    public const string Inspecting = "检验中";

    /// <summary>完成检验待入库：全部要求项均检验完毕且未入库</summary>
    public const string CompletedAwaitingInbound = "完成检验待入库";

    /// <summary>全部四档（按看板顺序）</summary>
    public static readonly string[] All =
    [
        WaitingMaterial, WaitingInspection, Inspecting, CompletedAwaitingInbound
    ];

    /// <summary>是否为合法看板档位（Ordinal）</summary>
    public static bool IsStage(string? value)
        => !string.IsNullOrEmpty(value)
           && (value == WaitingMaterial || value == WaitingInspection
               || value == Inspecting || value == CompletedAwaitingInbound);
}
