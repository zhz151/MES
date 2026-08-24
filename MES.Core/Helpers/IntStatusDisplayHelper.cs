using MES.Core.DTOs.Configuration;

namespace MES.Core.Helpers;

/// <summary>
/// int 档位字段（第三类·算法档位）的"存储值 → 中文"统一出口。
/// 集中定义所有 int 档位映射，消除 DTO 内嵌 switch / DisplayHelper 硬编码 / 前端 EnumOptions 的多点重复。
/// 消费端（DTO XxxText、DisplayHelper、前端筛选选项、打印）一律调用本类，保证中文显示全链路一致。
/// </summary>
public static class IntStatusDisplayHelper
{
    // ========== 3 档：未投料/部分/满足（InputStatus/MainNoInputStatus/FlowStatus/ReworkMainNoStatus） ==========

    /// <summary>投料/流转状态 4 档文本（0=未投料 1=部分 2=满足 3=超量）</summary>
    public static string GetInputStatusText(int status) => status switch
    {
        0 => "未投料",
        1 => "部分",
        2 => "满足",
        3 => "超量",
        _ => "未知"
    };

    /// <summary>主号流转状态 4 档文本（0=未计划 1=部分 2=满足 3=超量）</summary>
    public static string GetMainNoFlowStatusText(int status) => status switch
    {
        0 => "未计划",
        1 => "部分",
        2 => "满足",
        3 => "超量",
        _ => "未知"
    };

    // ========== 主号关注 5 档 ==========

    /// <summary>主号关注 5 档文本（0=主号暂停 1=主号完成 2=原料锁定 3=生产执行 4=成品检验）。null/未知 → fallback</summary>
    public static string GetScheduleStageText(int? stage, string? fallback = "未知") => stage switch
    {
        0 => "主号暂停",
        1 => "主号完成",
        2 => "原料锁定",
        3 => "生产执行",
        4 => "成品检验",
        _ => fallback ?? ""
    };

    /// <summary>计划覆盖档位 4 档文本（0=主号完成 1=原料锁定 2=生产执行 3=成品检验）</summary>
    public static string GetPlanScheduleStageText(int? stage) => stage switch
    {
        0 => "主号完成",
        1 => "原料锁定",
        2 => "生产执行",
        3 => "成品检验",
        _ => "未知"
    };

    // ========== 入库状态 ==========

    /// <summary>入库状态 4 档文本（0=无入库 1=入库部分 2=入库完结 3=入库超额；WoWarehousingStatus 4 档 / OrderWarehousingStatus 值域仅 0~2）</summary>
    public static string GetWarehousingStatusText(int status) => status switch
    {
        0 => "无入库",
        1 => "入库部分",
        2 => "入库完结",
        3 => "入库超额",
        _ => "未知"
    };

    /// <summary>主号入库状态 4 档文本（0=无入库 1=入库部分 2=入库完结 3=入库超额）</summary>
    public static string GetMainNoWarehousingStatusText(int status) => status switch
    {
        0 => "无入库",
        1 => "入库部分",
        2 => "入库完结",
        3 => "入库超额",
        _ => "未知"
    };

    // ========== 用料计划执行 5 档 ==========

    /// <summary>用料计划执行状态 5 档文本（0=无计划 1=未执行 2=部分 3=已完成 4=异常；G4~G10 共用）</summary>
    public static string GetPlanExecutionStatusText(int status) => status switch
    {
        0 => "无计划",
        1 => "未执行",
        2 => "部分",
        3 => "已完成",
        4 => "异常",
        _ => "未知"
    };

    // ========== 主号计划执行状态 4 档（三数字判定：无计划/未执行/执行中/计划落实） ==========

    /// <summary>主号计划执行状态 4 档文本（0=无计划 1=未执行 2=执行中 3=计划落实）</summary>
    public static string GetMainNoPlanExecutionStatusText(int status) => status switch
    {
        0 => "无计划",
        1 => "未执行",
        2 => "执行中",
        3 => "计划落实",
        _ => "未知"
    };

    /// <summary>到料实投一致性 7 档文本（0=一致 1=待投 2=疑问-到料少投 3=疑问-到料超投 4=错误-无料已投 5=错误-无需投料 6=略）。
    /// 档 5/6 为阶段门控：主号关注=生产执行/成品检验/主号完成（已过投料期）时，理论缺失总料重&gt;0 → 5 错误-无需投料（应已无需投料却缺料）；=0 → 6 略（不细看）</summary>
    public static string GetPlanInputConsistencyText(int status) => status switch
    {
        0 => "一致",
        1 => "待投",
        2 => "疑问-到料少投",
        3 => "疑问-到料超投",
        4 => "错误-无料已投",
        5 => "错误-无需投料",
        6 => "略",
        _ => "未知"
    };

    // ========== 筛选选项（Value=档位数字，Display=中文，供列表筛选下拉） ==========

    /// <summary>投料/流转状态筛选选项（4 档含超量）</summary>
    public static List<EnumDisplayOptionDto> GetInputStatusOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "未投料" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "部分" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "满足" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "超量" }
    };

    /// <summary>主号流转状态筛选选项（4 档含超量，0=未计划）</summary>
    public static List<EnumDisplayOptionDto> GetMainNoFlowStatusOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "未计划" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "部分" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "满足" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "超量" }
    };

    /// <summary>主号关注筛选选项（summary 5 档）</summary>
    public static List<EnumDisplayOptionDto> GetScheduleStageOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "主号暂停" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "主号完成" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "原料锁定" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "生产执行" },
        new EnumDisplayOptionDto { Value = "4", DisplayName = "成品检验" }
    };

    /// <summary>计划覆盖档位筛选选项（4 档）</summary>
    public static List<EnumDisplayOptionDto> GetPlanScheduleStageOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "主号完成" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "原料锁定" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "生产执行" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "成品检验" }
    };

    /// <summary>入库状态筛选选项（4 档；WoWarehousingStatus 用，OrderWarehousingStatus 值域仅 0~2）</summary>
    public static List<EnumDisplayOptionDto> GetWarehousingStatusOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "无入库" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "入库部分" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "入库完结" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "入库超额" }
    };

    /// <summary>主号入库状态筛选选项（4 档）</summary>
    public static List<EnumDisplayOptionDto> GetMainNoWarehousingStatusOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "无入库" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "入库部分" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "入库完结" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "入库超额" }
    };

    // ========== 用料计划状态（MaterialPlanStatus，实体存 int，DTO 转枚举显示） ==========

    /// <summary>用料计划状态筛选选项（MaterialPlanStatus 4 档：0=未计划 1=部分 2=满足 3=超量）。
    /// 实体存 int，筛选 Value 用档位数字；Display 与 EnumHelper 中 MaterialPlanStatus 中文一致</summary>
    public static List<EnumDisplayOptionDto> GetMaterialPlanStatusOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "未计划" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "部分" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "满足" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "超量" }
    };

    // ========== 用料计划执行 5 档筛选选项（G4~G10 共用） ==========

    /// <summary>用料计划执行状态筛选选项（5 档）</summary>
    public static List<EnumDisplayOptionDto> GetPlanExecutionStatusOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "无计划" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "未执行" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "部分" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "已完成" },
        new EnumDisplayOptionDto { Value = "4", DisplayName = "异常" }
    };

    /// <summary>到料实投一致性筛选选项（7 档：0=一致 1=待投 2=疑问-到料少投 3=疑问-到料超投 4=错误-无料已投 5=错误-无需投料 6=略）</summary>
    public static List<EnumDisplayOptionDto> GetPlanInputConsistencyOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "一致" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "待投" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "疑问-到料少投" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "疑问-到料超投" },
        new EnumDisplayOptionDto { Value = "4", DisplayName = "错误-无料已投" },
        new EnumDisplayOptionDto { Value = "5", DisplayName = "错误-无需投料" },
        new EnumDisplayOptionDto { Value = "6", DisplayName = "略" }
    };

    /// <summary>主号计划执行状态筛选选项（4 档：0=无计划 1=未执行 2=执行中 3=计划落实）</summary>
    public static List<EnumDisplayOptionDto> GetMainNoPlanExecutionStatusOptions() => new()
    {
        new EnumDisplayOptionDto { Value = "0", DisplayName = "无计划" },
        new EnumDisplayOptionDto { Value = "1", DisplayName = "未执行" },
        new EnumDisplayOptionDto { Value = "2", DisplayName = "执行中" },
        new EnumDisplayOptionDto { Value = "3", DisplayName = "计划落实" }
    };

    // ========== 批次排程档位（6 档：急+/急/急-/顺/带/略，ScheduleTier；V5.26 档位序） ==========

    /// <summary>批次实际排程档位显示文本（1=急+ 2=急 3=急- 4=顺 5=带 6=略）</summary>
    public static string GetScheduleTierText(int tier) => tier switch
    {
        1 => "急+",
        2 => "急",
        3 => "急-",
        4 => "顺",
        5 => "带",
        _ => "略",
    };

    // ========== 批次计划薄表等级（5 档：急+/急/急-/一般/略，PlanFlowLevel；V5.28 五档） ==========

    /// <summary>薄表计划等级显示文本（1=急+ 2=急 3=急- 4=一般 5=略；V5.28 特急A/B 手工档已删，急+ 直接透传实时档位）</summary>
    public static string GetPlanFlowLevelText(int level) => level switch
    {
        1 => "急+",
        2 => "急",
        3 => "急-",
        4 => "一般",
        5 => "略",
        _ => level.ToString(),
    };
}
