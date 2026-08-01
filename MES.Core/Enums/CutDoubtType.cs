namespace MES.Core.Enums;

/// <summary>
/// 成切存疑类型
/// QuantityMismatch = 疑问-数量（有断切记录，但 |成切支数−理论成品支|/理论成品支 &gt; 5%）
/// MissingRecords   = 疑问-缺少（需求=是、执行=否，但批次已到成检/完成且非强制完成 → 缺少成品切割记录）
/// Normal           = 正常（有断切记录，数量偏差在 5% 内）
/// null（略）：无成切需求 / 执行=否未到成检/完成 / 理论成品支不可得
/// </summary>
public enum CutDoubtType
{
    QuantityMismatch,
    MissingRecords,
    Normal
}
