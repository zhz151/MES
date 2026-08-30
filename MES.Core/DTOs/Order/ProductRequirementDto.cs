// 文件路径: MES.Core/DTOs/ProductRequirementDto.cs
using MES.Core.Enums;

namespace MES.Core.DTOs.Order;

/// <summary>
/// 产品要求 DTO
/// </summary>
public class ProductRequirementDto
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public RequirementType RequirementType { get; set; }
    public bool ChemicalComposition { get; set; }
    public InspectionRequirementStage PmiInspection { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage SurfaceInspection { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage Dimension { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage Endoscopy { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage HydrostaticTest { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage UnderwaterPressure { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage EddyCurrent { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage UltrasonicTest { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage PortColoring { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage RadiographicTest { get; set; } = InspectionRequirementStage.FinalOnly;
    public bool HardnessRockwell { get; set; }
    public bool HardnessBrinell { get; set; }
    public bool HardnessVickers { get; set; }
    public bool TensileRoomTemp { get; set; }
    public bool TensileHighTemp { get; set; }
    public bool WeldJointTensile { get; set; }
    public bool ImpactTest { get; set; }
    public bool WeldJointImpact { get; set; }
    public bool FlatteningTest { get; set; }
    public bool FlaringTest { get; set; }
    public bool ExpandingTest { get; set; }
    public bool BendTest { get; set; }
    public bool WeldJointBend { get; set; }
    public bool GrainSize { get; set; }
    public bool IntergranularCorrosion { get; set; }
    public bool PittingCorrosion { get; set; }
    public bool FerriteContent { get; set; }
    public bool Macrostructure { get; set; }
    public string? OtherRequirement { get; set; }

    /// <summary>
    /// 项次号（用于前端展示）
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset UpdatedTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string CreatedBy { get; set; } = "";

    /// <summary>
    /// 更新人
    /// </summary>
    public string UpdatedBy { get; set; } = "";
}

/// <summary>
/// 创建/更新产品要求请求
/// </summary>
public class CreateProductRequirementRequest
{
    public RequirementType RequirementType { get; set; } = RequirementType.Normal;
    public bool ChemicalComposition { get; set; }
    public InspectionRequirementStage PmiInspection { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage SurfaceInspection { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage Dimension { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage Endoscopy { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage HydrostaticTest { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage UnderwaterPressure { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage EddyCurrent { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage UltrasonicTest { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage PortColoring { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage RadiographicTest { get; set; } = InspectionRequirementStage.FinalOnly;
    public bool HardnessRockwell { get; set; }
    public bool HardnessBrinell { get; set; }
    public bool HardnessVickers { get; set; }
    public bool TensileRoomTemp { get; set; }
    public bool TensileHighTemp { get; set; }
    public bool WeldJointTensile { get; set; }
    public bool ImpactTest { get; set; }
    public bool WeldJointImpact { get; set; }
    public bool FlatteningTest { get; set; }
    public bool FlaringTest { get; set; }
    public bool ExpandingTest { get; set; }
    public bool BendTest { get; set; }
    public bool WeldJointBend { get; set; }
    public bool GrainSize { get; set; }
    public bool IntergranularCorrosion { get; set; }
    public bool PittingCorrosion { get; set; }
    public bool FerriteContent { get; set; }
    public bool Macrostructure { get; set; }
    public string? OtherRequirement { get; set; }

    /// <summary>
    /// 交货状态（同步保存到订单项次 OrderItem.DeliveryState）
    /// </summary>
    public DeliveryState DeliveryState { get; set; }
}

/// <summary>
/// 新建技术要求默认值（按标准号从工厂检验项要求带出，必检→true）
/// </summary>
public class ProductRequirementDefaultsDto
{
    public bool ChemicalComposition { get; set; }
    public InspectionRequirementStage PmiInspection { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage SurfaceInspection { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage Dimension { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage Endoscopy { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage HydrostaticTest { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage UnderwaterPressure { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage EddyCurrent { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage UltrasonicTest { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage PortColoring { get; set; } = InspectionRequirementStage.FinalOnly;
    public InspectionRequirementStage RadiographicTest { get; set; } = InspectionRequirementStage.FinalOnly;
    public bool HardnessRockwell { get; set; }
    public bool HardnessBrinell { get; set; }
    public bool HardnessVickers { get; set; }
    public bool TensileRoomTemp { get; set; }
    public bool TensileHighTemp { get; set; }
    public bool WeldJointTensile { get; set; }
    public bool ImpactTest { get; set; }
    public bool WeldJointImpact { get; set; }
    public bool FlatteningTest { get; set; }
    public bool FlaringTest { get; set; }
    public bool ExpandingTest { get; set; }
    public bool BendTest { get; set; }
    public bool WeldJointBend { get; set; }
    public bool GrainSize { get; set; }
    public bool IntergranularCorrosion { get; set; }
    public bool PittingCorrosion { get; set; }
    public bool FerriteContent { get; set; }
    public bool Macrostructure { get; set; }
}