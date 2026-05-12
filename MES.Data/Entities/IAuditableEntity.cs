namespace MES.Data.Entities;

/// <summary>
/// 可审计实体接口，统一 CreatedTime/CreatedBy/UpdatedTime/UpdatedBy 审计字段赋值
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedTime { get; set; }
    string CreatedBy { get; set; }
    DateTimeOffset UpdatedTime { get; set; }
    string UpdatedBy { get; set; }
}
