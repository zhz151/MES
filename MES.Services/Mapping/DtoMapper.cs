using MES.Core.DTOs;
using MES.Data.Entities;

namespace MES.Services.Mapping;

/// <summary>
/// Entity → DTO 映射扩展方法
/// </summary>
public static class DtoMapper
{
    public static ProductionStandardDto ToDto(this ProductionStandard entity) => new()
    {
        Id = entity.Id,
        StandardCode = entity.StandardCode,
        StandardName = entity.StandardName,
        Remark = entity.Remark,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive
    };

    public static StandardGradeMappingDto ToDto(this StandardGradeMapping entity) => new()
    {
        Id = entity.Id,
        StandardGrade = entity.StandardGrade,
        PlantGrade = entity.PlantGrade,
        Density = entity.Density,
        HeatTreatment = entity.HeatTreatment,
        SpecialMaterial = entity.SpecialMaterial,
        SpecialNote = entity.SpecialNote,
        Remark = entity.Remark
    };

    public static CustomerProfileDto ToDto(this CustomerProfile entity) => new()
    {
        Id = entity.Id,
        CustomerCode = entity.CustomerCode,
        Salesman = entity.Salesman,
        CustomerUnit = entity.CustomerUnit,
        EndCustomer = entity.EndCustomer,
        ContactPerson = entity.ContactPerson,
        ContactPhone = entity.ContactPhone,
        Address = entity.Address,
        Status = entity.Status,
        Remark = entity.Remark
    };

    public static OrderChangeNotificationDto ToDto(this OrderChangeNotification entity) => new()
    {
        Id = entity.Id,
        OrderNumber = entity.OrderNumber,
        ChangeType = entity.ChangeType,
        WorkOrderCount = entity.WorkOrderCount,
        IsRead = entity.IsRead,
        CreatedTime = entity.CreatedTime
    };

    public static WorkOrderListDto ToListDto(this WorkOrder entity) => new()
    {
        Id = entity.Id,
        WorkOrderNo = entity.WorkOrderNo,
        SalesOrderNo = entity.SalesOrderNo,
        ProductionMainNo = entity.ProductionMainNo,
        ProductionSubNo = entity.ProductionSubNo,
        MaterialName = entity.MaterialName.ToString(),
        Specification = entity.Specification,
        DeliveryDate = entity.DeliveryDate,
        TotalQuantity = entity.TotalQuantity,
        TotalWeight = entity.TotalWeight,
        Status = (int)entity.Status,
        CreatedTime = entity.CreatedTime
    };

    public static WorkOrderDetailDto ToDetailDto(this WorkOrder entity) => new()
    {
        Id = entity.Id,
        WorkOrderNo = entity.WorkOrderNo,
        SalesOrderNo = entity.SalesOrderNo,
        ProductionMainNo = entity.ProductionMainNo,
        ProductionSubNo = entity.ProductionSubNo,
        OrderItemIds = entity.OrderItemIds,
        Status = (int)entity.Status,
        SignDate = entity.SignDate,
        Salesman = entity.Salesman,
        EndCustomer = entity.EndCustomer,
        DeliveryDate = entity.DeliveryDate,
        DelayPenalty = entity.DelayPenalty,
        MaterialName = entity.MaterialName.ToString(),
        SettlementMethod = entity.SettlementMethod.ToString(),
        StandardCode = entity.StandardCode,
        DeliveryState = entity.DeliveryState.ToString(),
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        OuterDiameterNegative = entity.OuterDiameterNegative,
        OuterDiameterPositive = entity.OuterDiameterPositive,
        WallThicknessNegative = entity.WallThicknessNegative,
        WallThicknessPositive = entity.WallThicknessPositive,
        LengthStatus = entity.LengthStatus.ToString(),
        MinLength = entity.MinLength,
        MaxLength = entity.MaxLength,
        TotalQuantity = entity.TotalQuantity,
        TotalMeters = entity.TotalMeters,
        TotalWeight = entity.TotalWeight,
        TotalItemCount = entity.TotalItemCount,
        ItemDetails = entity.ItemDetails,
        TechnicalRequirements = entity.TechnicalRequirements.ToString(),
        RowVersion = entity.RowVersion,
        CreatedTime = entity.CreatedTime,
        CreatedBy = entity.CreatedBy,
        UpdatedTime = entity.UpdatedTime,
        UpdatedBy = entity.UpdatedBy
    };

    public static ProductRequirementDto ToDto(this ProductRequirement entity, int sequence) => new()
    {
        Id = entity.Id,
        OrderItemId = entity.OrderItemId,
        Sequence = sequence,
        RequirementType = entity.RequirementType,
        ChemicalComposition = entity.ChemicalComposition,
        MechanicalProperty = entity.MechanicalProperty,
        ToleranceRequirement = entity.ToleranceRequirement,
        SurfaceQuality = entity.SurfaceQuality,
        NdtRequirement = entity.NdtRequirement,
        OtherRequirement = entity.OtherRequirement,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };
}
