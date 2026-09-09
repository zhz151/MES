namespace MES.Core.DTOs.Batch;

/// <summary>
/// 委外单位档案（工段委外）行 DTO。行 = (委外单位名 × 委外工段)。
/// 委外工段存 SectionKeys 英文 key，中文显示由前端 SectionDisplayHelper 负责。
/// </summary>
public class OutsourceVendorProfileDto
{
    public int Id { get; set; }
    public string VendorCode { get; set; } = null!;
    public string VendorName { get; set; } = null!;
    public string SectionName { get; set; } = null!;
    public bool IsWorkshop { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }

    // ========== ② 往来信息（工段委外统计，委外单位列表 AttachTradeStatsAsync 回填） ==========
    // 单位：Count=委外单数、Weight=kg、Amount=元（发出单 TotalAmount）；厂内/档外不计（累计 0 → 前端显「—」）
    /// <summary>累计委外单数（该档案行名下非厂内发出记录行数，全期）</summary>
    public int TotalOrderCount { get; set; }

    /// <summary>累计委外重量(kg) = Σ SendWeight（全期）</summary>
    public decimal TotalWeight { get; set; }

    /// <summary>累计委外金额(元) = Σ TotalAmount（全期）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>本年委外单数（发出日期 SendOutDate.Year == 今年）</summary>
    public int YearOrderCount { get; set; }

    /// <summary>本年委外重量(kg) = Σ SendWeight（发出日期 == 今年）</summary>
    public decimal YearWeight { get; set; }

    /// <summary>本年委外金额(元) = Σ TotalAmount（发出日期 == 今年）</summary>
    public decimal YearAmount { get; set; }

    /// <summary>本年回收(kg) = 回收记录 RecoveryDate.Year == 今年 的 Σ RecoveryWeight（正常回收）</summary>
    public decimal YearRecoveredWeight { get; set; }

    /// <summary>本年回收金额(元, 参考) = 各发出单 TotalAmount × 本年正常回收重量/发出重量 分摊（退回不产生金额），重量份额截于发出重量</summary>
    public decimal YearRecoveredAmount { get; set; }

    /// <summary>在委外未回收(kg) = 当前时点 Status==PendingRecovery 的行 Σ (SendWeight − Σ(RecoveryWeight+UnprocessedWeight))，负数截 0</summary>
    public decimal PendingWeight { get; set; }

    /// <summary>委外未回收金额(元, 参考) = 待回收发出单 TotalAmount × 未回收净欠重量/发出重量 分摊</summary>
    public decimal PendingAmount { get; set; }

    /// <summary>本年退回(kg) = 回收记录 RecoveryDate.Year == 今年 的 Σ UnprocessedWeight（非正常退回）</summary>
    public decimal YearReturnWeight { get; set; }
}

/// <summary>
/// 新建委外单位档案行（单行/批量共用）
/// </summary>
public class CreateOutsourceVendorRequest
{
    public string VendorName { get; set; } = null!;
    public string SectionName { get; set; } = null!;
    public bool IsWorkshop { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新委外单位档案行（字段级打补丁，仅非 null 覆盖）
/// </summary>
public class UpdateOutsourceVendorRequest
{
    public string? VendorName { get; set; }
    public string? SectionName { get; set; }
    public bool? IsWorkshop { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public bool? IsActive { get; set; }
    public string? Remark { get; set; }
}
