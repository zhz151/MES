using FluentAssertions;
using MES.Core.DTOs.Quality;

namespace MES.Tests.Services.Quality;

/// <summary>
/// 三模块「制造状态/是否交付态」非正式成检显示 "-" 的 DTO 契约测试
/// 规则：仅当 成检类型==FormalInspection 时两字段有效；null/预成检/其他一律显示 "-"
/// </summary>
public class QualityStatusDisplayTests
{
    #region MaterialReceiveCheckDto（成检到料）

    [Fact]
    public void 成检到料_正式成检_制造状态显示中文()
    {
        var dto = new MaterialReceiveCheckDto
        {
            InspectionType = MES.Core.Enums.InspectionType.FormalInspection,
            ManufacturingStatus = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled,
            RawDeliveryState = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled
        };

        dto.IsFormalInspection.Should().BeTrue();
        dto.ManufacturingStatusDisplay.Should().Be("固溶酸洗");
        dto.IsDeliveryStatus.Should().Be("是");
    }

    [Fact]
    public void 成检到料_预成检_制造状态与是否交付态显示横线()
    {
        var dto = new MaterialReceiveCheckDto
        {
            InspectionType = MES.Core.Enums.InspectionType.PreInspection,
            ManufacturingStatus = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled,
            RawDeliveryState = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled
        };

        dto.IsFormalInspection.Should().BeFalse();
        dto.ManufacturingStatusDisplay.Should().Be("-");
        dto.IsDeliveryStatus.Should().BeNull();
    }

    [Fact]
    public void 成检到料_成检类型为空_视为非正式成检显示横线()
    {
        var dto = new MaterialReceiveCheckDto
        {
            InspectionType = null,
            ManufacturingStatus = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled
        };

        dto.IsFormalInspection.Should().BeFalse();
        dto.ManufacturingStatusDisplay.Should().Be("-");
        dto.IsDeliveryStatus.Should().BeNull();
    }

    [Fact]
    public void 成检到料_正式成检_制造状态为空_显示横线()
    {
        var dto = new MaterialReceiveCheckDto
        {
            InspectionType = MES.Core.Enums.InspectionType.FormalInspection,
            ManufacturingStatus = null
        };

        dto.IsFormalInspection.Should().BeTrue();
        dto.ManufacturingStatusDisplay.Should().Be("-");
        dto.IsDeliveryStatus.Should().Be("否");
    }

    #endregion

    #region FinalInspectionDto（成品检验）

    [Fact]
    public void 成品检验_正式成检_制造状态与是否交付态正常显示()
    {
        var dto = new FinalInspectionDto
        {
            InspectionType = MES.Core.Enums.InspectionType.FormalInspection,
            ManufacturingStatus = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled,
            DeliveryState = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled
        };

        dto.IsFormalInspection.Should().BeTrue();
        dto.ManufacturingStatusDisplay.Should().Be("固溶酸洗");
        dto.IsDeliveryStatusDisplay.Should().Be("是");
    }

    [Fact]
    public void 成品检验_预成检_制造状态与是否交付态显示横线()
    {
        var dto = new FinalInspectionDto
        {
            InspectionType = MES.Core.Enums.InspectionType.PreInspection,
            ManufacturingStatus = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled,
            DeliveryState = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled
        };

        dto.IsFormalInspection.Should().BeFalse();
        dto.ManufacturingStatusDisplay.Should().Be("-");
        dto.IsDeliveryStatusDisplay.Should().Be("-");
    }

    [Fact]
    public void 成品检验_制造状态与交货状态同为空白_不误判为交付态()
    {
        var dto = new FinalInspectionDto
        {
            InspectionType = MES.Core.Enums.InspectionType.FormalInspection,
            ManufacturingStatus = null,
            DeliveryState = null
        };

        dto.IsFormalInspection.Should().BeTrue();
        dto.ManufacturingStatusDisplay.Should().Be("-");
        dto.IsDeliveryStatusDisplay.Should().Be("否");
    }

    #endregion

    #region QualityProcessTrackingDto（成检追踪）

    [Fact]
    public void 成检追踪_正式成检_制造状态与是否交付态正常显示()
    {
        var dto = new QualityProcessTrackingDto
        {
            InspectionType = MES.Core.Enums.InspectionType.FormalInspection,
            ManufacturingStatus = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled,
            IsDeliveryStatus = "是"
        };

        dto.IsFormalInspection.Should().BeTrue();
        dto.ManufacturingStatusDisplay.Should().Be("固溶酸洗");
        dto.IsDeliveryStatusDisplay.Should().Be("是");
    }

    [Fact]
    public void 成检追踪_预成检_制造状态与是否交付态显示横线()
    {
        var dto = new QualityProcessTrackingDto
        {
            InspectionType = MES.Core.Enums.InspectionType.PreInspection,
            ManufacturingStatus = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled,
            IsDeliveryStatus = "是"
        };

        dto.IsFormalInspection.Should().BeFalse();
        dto.ManufacturingStatusDisplay.Should().Be("-");
        dto.IsDeliveryStatusDisplay.Should().Be("-");
    }

    [Fact]
    public void 成检追踪_成检类型为空_显示横线()
    {
        var dto = new QualityProcessTrackingDto
        {
            InspectionType = null,
            ManufacturingStatus = MES.Core.Enums.DeliveryState.SolutionAnnealedAndPickled,
            IsDeliveryStatus = "是"
        };

        dto.IsFormalInspection.Should().BeFalse();
        dto.ManufacturingStatusDisplay.Should().Be("-");
        dto.IsDeliveryStatusDisplay.Should().Be("-");
    }

    #endregion
}
