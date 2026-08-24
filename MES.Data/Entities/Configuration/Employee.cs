namespace MES.Data.Entities.Configuration;

/// <summary>
/// 员工 — 扫码报工和工资结算基础信息
/// </summary>
public class Employee : BaseEntity
{
    /// <summary>工号（二维码内容）</summary>
    public string Code { get; set; } = null!;

    /// <summary>姓名</summary>
    public string Name { get; set; } = null!;

    /// <summary>部门</summary>
    public string? Department { get; set; }

    /// <summary>岗位</summary>
    public string? Position { get; set; }

    /// <summary>岗位备注</summary>
    public string? PositionRemark { get; set; }

    /// <summary>工资结算模式</summary>
    public string? SalaryMode { get; set; }

    /// <summary>工资结算备注</summary>
    public string? SalaryRemark { get; set; }

    /// <summary>工段名（存工段英文 Key，如 ColdRollDraw）— 扫码报工按工段过滤操作人下拉</summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// 组类（存组类名逗号串，可多组，如 "甲班,乙班"）— 人多的工段扫码先选组类再选人；
    /// 工位配置组类选项后，候选 = 工段 ∩ 组类任一匹配；留空 = 不设组
    /// </summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// 成检项目资质（存 InspectionItem 枚举名逗号串，如 "Ultrasonic,EddyCurrent"）
    /// — 成品检验工位扫码按工位绑定的检验项目过滤操作人（仅按项目，不看工段）
    /// </summary>
    public string? InspectionItems { get; set; }

    /// <summary>
    /// 是否属于过程检验操作人（勾选=true）— 过程检验工位扫码操作人候选 = 此开关为真的启用员工，与检验项目/工段无关
    /// </summary>
    public bool? ProcessInspectionItems { get; set; }

    /// <summary>
    /// 是否属于成检到料确认人（勾选=true）— 成检到料工位扫码确认人候选 = 此开关为真的启用员工，与检验项目/工段无关
    /// </summary>
    public bool? MaterialReceiveCheckItems { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;
}
