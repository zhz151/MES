using MES.Core.DTOs;
using MES.Core.Enums;
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
        SignDate = entity.SignDate,
        Salesman = entity.Salesman,
        EndCustomer = entity.EndCustomer,
        DeliveryDate = entity.DeliveryDate,
        DelayPenalty = entity.DelayPenalty,
        SettlementMethod = entity.SettlementMethod,
        PlantGrade = entity.PlantGrade,
        MaterialName = entity.MaterialName,
        Specification = entity.Specification,
        LengthStatus = entity.LengthStatus,
        MinLength = entity.MinLength,
        MaxLength = entity.MaxLength,
        TotalQuantity = entity.TotalQuantity,
        TotalWeight = entity.TotalWeight,
        DeliveryState = entity.DeliveryState,
        TotalItemCount = entity.TotalItemCount,
        Status = (int)entity.Status,
        MaterialPlanStatus = (int)entity.MaterialPlanStatus,
        MaterialPlanRate = entity.MaterialPlanRate,
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
        MaterialName = entity.MaterialName,
        SettlementMethod = entity.SettlementMethod,
        StandardCode = entity.StandardCode,
        DeliveryState = entity.DeliveryState,
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        OuterDiameterNegative = entity.OuterDiameterNegative,
        OuterDiameterPositive = entity.OuterDiameterPositive,
        WallThicknessNegative = entity.WallThicknessNegative,
        WallThicknessPositive = entity.WallThicknessPositive,
        LengthStatus = entity.LengthStatus,
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
        UpdatedBy = entity.UpdatedBy,
        MaterialPlanStatus = (int)entity.MaterialPlanStatus,
        MaterialPlanRate = entity.MaterialPlanRate,
        UnitWeight = CalculateUnitWeight(entity)
    };

    private static decimal? CalculateUnitWeight(WorkOrder entity)
    {
        if (string.IsNullOrEmpty(entity.Specification)) return null;

        var nominalOd = SpecificationParser.ParseOuterDiameter(entity.Specification);
        var nominalWt = SpecificationParser.ParseWallThickness(entity.Specification);
        if (nominalOd == null || nominalWt == null || nominalOd <= 0 || nominalWt <= 0) return null;

        var odActual = nominalOd - 0.5m * entity.OuterDiameterNegative + 0.5m * entity.OuterDiameterPositive;
        var wtActual = nominalWt - 0.5m * entity.WallThicknessNegative + 0.5m * entity.WallThicknessPositive;

        if (odActual <= 0 || wtActual <= 0) return null;

        var weightPerMeter = (odActual - wtActual) * wtActual * 0.02466m;
        var maxLengthMm = entity.LengthStatus == LengthStatus.Fixed
            ? entity.MaxLength ?? 4500m
            : 4500m;
        var unitWeight = weightPerMeter * maxLengthMm / 1000m;

        return unitWeight.HasValue ? Math.Round(unitWeight.Value, 3) : null;
    }

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

    // ========== 仓库上下文 Mapping ==========

    public static WarehouseDto ToDto(this Warehouse entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive,
        Remark = entity.Remark
    };

    public static InventoryBatchDto ToDto(this InventoryBatch entity) => new()
    {
        Id = entity.Id,
        BatchNo = entity.BatchNo,
        WarehouseId = entity.WarehouseId,
        MaterialType = entity.MaterialType,
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        InboundSource = entity.InboundSource,
        SourceName = entity.SourceName,
        InboundDate = entity.InboundDate,
        HeatNo = entity.HeatNo,
        ProductionBatchNo = entity.ProductionBatchNo,
        LengthStatus = entity.LengthStatus,
        MinLength = entity.MinLength,
        MaxLength = entity.MaxLength,
        InitialQuantity = entity.InitialQuantity,
        InitialWeight = entity.InitialWeight,
        UnitWeight = entity.UnitWeight,
        Meters = entity.Meters,
        RemainingQuantity = entity.RemainingQuantity,
        RemainingWeight = entity.RemainingWeight,
        ActualSpecification = entity.ActualSpecification,
        ActualOuterDiameter = entity.ActualOuterDiameter,
        ActualWallThickness = entity.ActualWallThickness,
        SurfaceCondition = entity.SurfaceCondition,
        LocationArea = entity.LocationArea,
        LocationRack = entity.LocationRack,
        Remark = entity.Remark,
        DefectReason = entity.DefectReason,
        LiabilityType = entity.LiabilityType,
        OriginalSupplier = entity.OriginalSupplier,
        TagNo = entity.TagNo,
        DefectRemark = entity.DefectRemark,
        IsLinkedToWorkOrder = entity.IsLinkedToWorkOrder,
        WorkOrderNo = entity.WorkOrderNo,
        SalesOrderNo = entity.SalesOrderNo,
        OrderItemIds = entity.OrderItemIds,
        SourceOrderNo = entity.SourceOrderNo
    };

    public static OutboundRecordDto ToDto(this OutboundRecord entity) => new()
    {
        Id = entity.Id,
        InventoryBatchId = entity.InventoryBatchId,
        OutboundType = entity.OutboundType.ToString(),
        SourceOrderNo = entity.SourceOrderNo,
        TargetCompany = entity.TargetCompany,
        OutboundQuantity = entity.OutboundQuantity,
        OutboundWeight = entity.OutboundWeight,
        OutboundDate = entity.OutboundDate,
        Remark = entity.Remark,
        CreatedBy = entity.CreatedBy,
        CreatedTime = entity.CreatedTime
    };

    // ========== 批次上下文 Mapping ==========

    public static ProductionBatchListDto ToListDto(this ProductionBatch entity) => new()
    {
        Id = entity.Id,
        BatchNo = entity.BatchNo,
        Status = entity.Status.ToString(),
        TagNo = entity.TagNo,
        WorkOrderNo = entity.WorkOrderNo,
        CurrentGroupName = entity.CurrentGroupName,
        CurrentSectionName = entity.CurrentSectionName,
        CurrentExecDate = entity.CurrentExecDate,
        CreatedTime = entity.CreatedTime,
        CreatedBy = entity.CreatedBy
    };

    public static ProductionBatchDetailDto ToDetailDto(this ProductionBatch entity) => new()
    {
        Id = entity.Id,
        BatchNo = entity.BatchNo,
        Status = entity.Status.ToString(),
        TagNo = entity.TagNo,
        IsForceCompleted = entity.IsForceCompleted,
        QualityRemark = entity.QualityRemark,
        SolutionParams = entity.SolutionParams,
        CurrentExecDate = entity.CurrentExecDate,
        CurrentGroupName = entity.CurrentGroupName,
        CurrentSectionName = entity.CurrentSectionName,
        CurrentEquipmentName = entity.CurrentEquipmentName,
        CurrentOutsource = entity.CurrentOutsource,
        NextSectionName = entity.NextSectionName,
        Remark = entity.Remark,
        WorkOrderNo = entity.WorkOrderNo,
        SalesOrderNo = entity.SalesOrderNo,
        ProductionMainNo = entity.ProductionMainNo,
        ProductionSubNo = entity.ProductionSubNo,
        OrderItemIds = entity.OrderItemIds,
        SignDate = entity.SignDate,
        Salesman = entity.Salesman,
        EndCustomer = entity.EndCustomer,
        DeliveryDate = entity.DeliveryDate,
        DelayPenalty = entity.DelayPenalty,
        MaterialName = entity.MaterialName,
        SettlementMethod = entity.SettlementMethod,
        StandardCode = entity.StandardCode,
        DeliveryState = entity.DeliveryState,
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        OuterDiameterNegative = entity.OuterDiameterNegative,
        OuterDiameterPositive = entity.OuterDiameterPositive,
        WallThicknessNegative = entity.WallThicknessNegative,
        WallThicknessPositive = entity.WallThicknessPositive,
        LengthStatus = entity.LengthStatus,
        MinLength = entity.MinLength,
        MaxLength = entity.MaxLength,
        TotalQuantity = entity.TotalQuantity,
        TotalMeters = entity.TotalMeters,
        TotalWeight = entity.TotalWeight,
        TotalItemCount = entity.TotalItemCount,
        ItemDetails = entity.ItemDetails,
        TechnicalRequirements = entity.TechnicalRequirements,
        SourceBatchNo = entity.SourceBatchNo,
        WarehouseId = entity.WarehouseId,
        SourceMaterialType = entity.SourceMaterialType,
        InboundSource = entity.InboundSource,
        SourceName = entity.SourceName,
        InboundDate = entity.InboundDate,
        SourceHeatNo = entity.SourceHeatNo,
        InputQuantity = entity.InputQuantity,
        InputWeight = entity.InputWeight,
        CreatedTime = entity.CreatedTime,
        CreatedBy = entity.CreatedBy,
        UpdatedTime = entity.UpdatedTime,
        UpdatedBy = entity.UpdatedBy,
        RowVersion = entity.RowVersion,
        ProcessGroups = entity.ProcessGroups?.Select(ToGroupDto).ToList() ?? new()
    };

    public static ProcessGroupDto ToGroupDto(this ProcessGroup entity) => new()
    {
        Id = entity.Id,
        ProductionBatchId = entity.ProductionBatchId,
        SequenceNumber = entity.SequenceNumber,
        ProcessName = entity.ProcessName,
        ManufacturingSpec = entity.ManufacturingSpec,
        OuterDiameterTolerance = entity.OuterDiameterTolerance,
        WallThicknessTolerance = entity.WallThicknessTolerance,
        ManufacturingLength = entity.ManufacturingLength,
        CuttingTreatment = entity.CuttingTreatment,
        ManufacturingMultiple = entity.ManufacturingMultiple,
        Remark = entity.Remark,
        ColdRollDraw = entity.ColdRollDraw,
        OilPipeCut = entity.OilPipeCut,
        Degrease = entity.Degrease,
        Solution = entity.Solution,
        Straighten = entity.Straighten,
        Cut = entity.Cut,
        ThicknessMeasure = entity.ThicknessMeasure,
        Pickle = entity.Pickle,
        OuterPolish = entity.OuterPolish,
        InnerGrinding = entity.InnerGrinding,
        OuterSpotGrinding = entity.OuterSpotGrinding,
        Inspection = entity.Inspection,
        WeldingHead = entity.WeldingHead,
        Lubrication = entity.Lubrication,
        Warehouse = entity.Warehouse,
        CreatedTime = entity.CreatedTime,
        CreatedBy = entity.CreatedBy
    };
}
