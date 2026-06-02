namespace MES.Data.Entities.Configuration;

/// <summary>
/// 交货状态附加天数配置
/// DeliveryState 为空字符串表示默认配置，非空表示具体的交货状态枚举名
/// </summary>
public class StandardWorkDayDeliveryState : BaseEntity
{
    /// <summary>交货状态（枚举名，空字符串表示默认值）</summary>
    public string DeliveryState { get; set; } = string.Empty;

    /// <summary>附加天数</summary>
    public double ExtraDays { get; set; }

    /// <summary>牌号前缀覆盖（预留）</summary>
    public string? PlantGradePrefix { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
