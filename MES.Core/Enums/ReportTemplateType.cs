namespace MES.Core.Enums;

/// <summary>
/// 报工模板类型（决定报工写入哪张表以及使用哪个表单模板）
/// </summary>
public enum ReportTemplateType
{
    /// <summary>普通报工</summary>
    ProductionRecord,
    /// <summary>入缸</summary>
    PicklingInRecord,
    /// <summary>出缸完工</summary>
    PicklingOutRecord,
    /// <summary>工段委外</summary>
    SectionOutsource,
    /// <summary>委外回收</summary>
    OutsourceRecovery,
    /// <summary>过程检验</summary>
    ProcessInspection,
    /// <summary>成品检验</summary>
    FinalInspection,
    /// <summary>成检到料</summary>
    MaterialReceiveCheck
}
