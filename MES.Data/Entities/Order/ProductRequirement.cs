using MES.Core.Enums;

namespace MES.Data.Entities.Order;

/// <summary>
/// 产品要求实体（与订单项次一对一关系）
/// </summary>
public class ProductRequirement : BaseEntity
{
    /// <summary>
    /// 订单项次ID（外键）
    /// </summary>
    public int OrderItemId { get; set; }

    /// <summary>
    /// 订单号（从 OrderItem 冗余，用于数据导入覆盖匹配）
    /// </summary>
    public string? OrderNo { get; set; }

    /// <summary>
    /// 项次号（从 OrderItem 冗余，用于数据导入覆盖匹配）
    /// </summary>
    public int? ItemSequence { get; set; }

    /// <summary>
    /// 技术要求类型
    /// </summary>
    public RequirementType RequirementType { get; set; }

    /// <summary>化学分析(成品)</summary>
    public bool ChemicalComposition { get; set; }

    /// <summary>PMI检验</summary>
    public bool PmiInspection { get; set; }

    /// <summary>表检</summary>
    public bool SurfaceInspection { get; set; }

    /// <summary>尺寸</summary>
    public bool Dimension { get; set; }

    /// <summary>内窥</summary>
    public bool Endoscopy { get; set; }

    /// <summary>液压检验</summary>
    public bool HydrostaticTest { get; set; }

    /// <summary>水下气压</summary>
    public bool UnderwaterPressure { get; set; }

    /// <summary>涡流探伤</summary>
    public bool EddyCurrent { get; set; }

    /// <summary>超声波检验</summary>
    public bool UltrasonicTest { get; set; }

    /// <summary>端口着色</summary>
    public bool PortColoring { get; set; }

    /// <summary>射线探伤</summary>
    public bool RadiographicTest { get; set; }

    /// <summary>硬度(洛氏)</summary>
    public bool HardnessRockwell { get; set; }

    /// <summary>硬度(布氏)</summary>
    public bool HardnessBrinell { get; set; }

    /// <summary>硬度(维氏)</summary>
    public bool HardnessVickers { get; set; }

    /// <summary>拉伸(室温)</summary>
    public bool TensileRoomTemp { get; set; }

    /// <summary>拉伸(高温)</summary>
    public bool TensileHighTemp { get; set; }

    /// <summary>焊接接头拉伸</summary>
    public bool WeldJointTensile { get; set; }

    /// <summary>冲击试验</summary>
    public bool ImpactTest { get; set; }

    /// <summary>焊接接头冲击</summary>
    public bool WeldJointImpact { get; set; }

    /// <summary>压扁试验</summary>
    public bool FlatteningTest { get; set; }

    /// <summary>卷边试验</summary>
    public bool FlaringTest { get; set; }

    /// <summary>扩口试验</summary>
    public bool ExpandingTest { get; set; }

    /// <summary>弯曲试验</summary>
    public bool BendTest { get; set; }

    /// <summary>焊接接头弯曲</summary>
    public bool WeldJointBend { get; set; }

    /// <summary>晶粒度</summary>
    public bool GrainSize { get; set; }

    /// <summary>晶间腐蚀</summary>
    public bool IntergranularCorrosion { get; set; }

    /// <summary>点腐蚀</summary>
    public bool PittingCorrosion { get; set; }

    /// <summary>金相检验</summary>
    public bool FerriteContent { get; set; }

    /// <summary>低倍组织</summary>
    public bool Macrostructure { get; set; }

    /// <summary>
    /// 其他要求
    /// </summary>
    public string? OtherRequirement { get; set; }

    /// <summary>
    /// 所属订单项次
    /// </summary>
    public OrderItem OrderItem { get; set; } = null!;
}
