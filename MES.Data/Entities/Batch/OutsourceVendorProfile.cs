namespace MES.Data.Entities.Batch;

/// <summary>
/// 委外单位档案（工段委外）— 行 = (委外单位名 × 委外工段)，仿供应商档案 SupplierProfile。
/// 委外单位名与外协俗称或本厂车间名同域（对应 SectionOutsource.OutsourceVendor 文本），
/// 作为批次工段委外录入的单位候选与登记主档，不替代材料委外（供应商）体系。
/// </summary>
public class OutsourceVendorProfile : BaseEntity
{
    /// <summary>
    /// 委外单位编码（WV + 4位数字流水，唯一），如 WV0001
    /// </summary>
    public string VendorCode { get; set; } = null!;

    /// <summary>
    /// 委外单位名（外协单位俗称或本厂车间名，如 佳拓 / 三厂）
    /// </summary>
    public string VendorName { get; set; } = null!;

    /// <summary>
    /// 委外工段（SectionKeys 英文 key），如 ColdRollDraw（冷轧拔）
    /// </summary>
    public string SectionName { get; set; } = null!;

    /// <summary>
    /// 是否本厂车间（厂内虚拟发外，如 一/三/四/五厂；勾选时工段锁定冷轧拔）
    /// </summary>
    public bool IsWorkshop { get; set; }

    /// <summary>
    /// 联系人
    /// </summary>
    public string? ContactPerson { get; set; }

    /// <summary>
    /// 联系电话
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
