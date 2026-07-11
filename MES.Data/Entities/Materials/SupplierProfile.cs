namespace MES.Data.Entities.Materials;

public class SupplierProfile : BaseEntity
{
    /// <summary>
    /// 供应商编码（SU + 4位数字流水）
    /// </summary>
    public string SupplierCode { get; set; } = null!;

    /// <summary>
    /// 供应商名称
    /// </summary>
    public string SupplierName { get; set; } = null!;

    /// <summary>
    /// 物料分类（用于采购时按物料筛选供应商）
    /// </summary>
    public string? MaterialCategory { get; set; }

    /// <summary>
    /// 联系人
    /// </summary>
    public string? ContactPerson { get; set; }

    /// <summary>
    /// 联系电话
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
