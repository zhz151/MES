using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.ProductionStandard;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Order;
using MES.Data.Entities.ProductionStandard;
using WorkOrderEntity = MES.Data.Entities.WorkOrder.WorkOrder;

namespace MES.Services.Mapping;

/// <summary>
/// Entity → DTO 映射扩展方法
/// </summary>
public static class DtoMapper
{
    public static StandardGradeMappingDto ToDto(this StandardGradeMapping entity) => new()
    {
        Id = entity.Id,
        StandardGrade = entity.StandardGrade,
        StandardGradeCategory = entity.StandardGradeCategory,
        PlantGrade = entity.PlantGrade,
        Density = entity.Density,
        HeatTreatment = entity.HeatTreatment,
        SpecialMaterial = entity.SpecialMaterial,
        SpecialNote = entity.SpecialNote,
        SteelProperty = entity.SteelProperty,
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

    public static WorkOrderListDto ToListDto(this WorkOrderEntity entity) => new()
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
        Status = entity.Status,
        MaterialPlanStatus = (int)entity.MaterialPlanStatus,
        MaterialPlanRate = entity.MaterialPlanRate,
        CreatedTime = entity.CreatedTime
    };

    public static WorkOrderListItemDto ToListItemDto(this WorkOrderEntity entity) => new()
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
        Status = entity.Status,
        CreatedTime = entity.CreatedTime
    };

    public static WorkOrderDetailDto ToDetailDto(this WorkOrderEntity entity) => new()
    {
        Id = entity.Id,
        WorkOrderNo = entity.WorkOrderNo,
        SalesOrderNo = entity.SalesOrderNo,
        ProductionMainNo = entity.ProductionMainNo,
        ProductionSubNo = entity.ProductionSubNo,
        OrderItemIds = entity.OrderItemIds,
        Status = entity.Status,
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

    private static decimal? CalculateUnitWeight(WorkOrderEntity entity)
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

    public static WarehouseDto ToDto(this MES.Data.Entities.Warehouse.Warehouse entity) => new()
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
        RemainingMeters = entity.RemainingMeters,
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
        BatchNo = entity.BatchNo,
        OutboundType = entity.OutboundType.ToString(),
        SourceOrderNo = entity.SourceOrderNo,
        TargetCompany = entity.TargetCompany,
        OutboundQuantity = entity.OutboundQuantity,
        OutboundWeight = entity.OutboundWeight,
        OutboundMeters = entity.OutboundMeters,
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
        CurrentSectionCompleted = entity.CurrentSectionCompleted,
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
        CurrentSectionCompleted = entity.CurrentSectionCompleted,
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

    // ========== 用料计划工序组（子实体共用MaterialPlanProcessGroupDto） ==========

    private static MaterialPlanProcessGroupDto ToPlanGroupDto(
        int parentPlanId, int sequenceNumber,
        string processName, string? manufacturingSpec, string? outerDiameterTolerance,
        string? wallThicknessTolerance, string? manufacturingLength, string? cuttingTreatment,
        int manufacturingMultiple, string? remark,
        int? coldRollDraw, int? oilPipeCut, int? degrease, int? solution,
        int? straighten, int? cut, int? thicknessMeasure, int? pickle,
        int? outerPolish, int? innerGrinding, int? outerSpotGrinding,
        int? inspection, int? weldingHead, int? lubrication, int? warehouse,
        int id, DateTimeOffset createdTime, string createdBy) => new()
        {
            Id = id,
            ParentPlanId = parentPlanId,
            SequenceNumber = sequenceNumber,
            ProcessName = processName,
            ManufacturingSpec = manufacturingSpec,
            OuterDiameterTolerance = outerDiameterTolerance,
            WallThicknessTolerance = wallThicknessTolerance,
            ManufacturingLength = manufacturingLength,
            CuttingTreatment = cuttingTreatment,
            ManufacturingMultiple = manufacturingMultiple,
            Remark = remark,
            ColdRollDraw = coldRollDraw,
            OilPipeCut = oilPipeCut,
            Degrease = degrease,
            Solution = solution,
            Straighten = straighten,
            Cut = cut,
            ThicknessMeasure = thicknessMeasure,
            Pickle = pickle,
            OuterPolish = outerPolish,
            InnerGrinding = innerGrinding,
            OuterSpotGrinding = outerSpotGrinding,
            Inspection = inspection,
            WeldingHead = weldingHead,
            Lubrication = lubrication,
            Warehouse = warehouse,
            CreatedTime = createdTime,
            CreatedBy = createdBy
        };

    public static MaterialPlanProcessGroupDto ToDto(this SemiPlanProcessGroup entity) =>
        ToPlanGroupDto(
            entity.PurchaseSemiPlanId, entity.SequenceNumber,
            entity.ProcessName, entity.ManufacturingSpec, entity.OuterDiameterTolerance,
            entity.WallThicknessTolerance, entity.ManufacturingLength, entity.CuttingTreatment,
            entity.ManufacturingMultiple, entity.Remark,
            entity.ColdRollDraw, entity.OilPipeCut, entity.Degrease, entity.Solution,
            entity.Straighten, entity.Cut, entity.ThicknessMeasure, entity.Pickle,
            entity.OuterPolish, entity.InnerGrinding, entity.OuterSpotGrinding,
            entity.Inspection, entity.WeldingHead, entity.Lubrication, entity.Warehouse,
            entity.Id, entity.CreatedTime, entity.CreatedBy);

    public static MaterialPlanProcessGroupDto ToDto(this InventoryPlanProcessGroup entity) =>
        ToPlanGroupDto(
            entity.InventoryPlanId, entity.SequenceNumber,
            entity.ProcessName, entity.ManufacturingSpec, entity.OuterDiameterTolerance,
            entity.WallThicknessTolerance, entity.ManufacturingLength, entity.CuttingTreatment,
            entity.ManufacturingMultiple, entity.Remark,
            entity.ColdRollDraw, entity.OilPipeCut, entity.Degrease, entity.Solution,
            entity.Straighten, entity.Cut, entity.ThicknessMeasure, entity.Pickle,
            entity.OuterPolish, entity.InnerGrinding, entity.OuterSpotGrinding,
            entity.Inspection, entity.WeldingHead, entity.Lubrication, entity.Warehouse,
            entity.Id, entity.CreatedTime, entity.CreatedBy);

    public static MaterialPlanProcessGroupDto ToDto(this PiercingPlanProcessGroup entity) =>
        ToPlanGroupDto(
            entity.RoundBarPiercingPlanId, entity.SequenceNumber,
            entity.ProcessName, entity.ManufacturingSpec, entity.OuterDiameterTolerance,
            entity.WallThicknessTolerance, entity.ManufacturingLength, entity.CuttingTreatment,
            entity.ManufacturingMultiple, entity.Remark,
            entity.ColdRollDraw, entity.OilPipeCut, entity.Degrease, entity.Solution,
            entity.Straighten, entity.Cut, entity.ThicknessMeasure, entity.Pickle,
            entity.OuterPolish, entity.InnerGrinding, entity.OuterSpotGrinding,
            entity.Inspection, entity.WeldingHead, entity.Lubrication, entity.Warehouse,
            entity.Id, entity.CreatedTime, entity.CreatedBy);

    public static GradeChemicalCompositionDto ToChemicalCompositionDto(this GradeChemicalComposition entity) => new()
    {
        Id = entity.Id,
        StandardGrade = entity.StandardGrade,
        StandardGradeCategory = entity.StandardGradeCategory,
        Carbon = entity.Carbon,
        Silicon = entity.Silicon,
        Manganese = entity.Manganese,
        Phosphorus = entity.Phosphorus,
        Sulfur = entity.Sulfur,
        Nickel = entity.Nickel,
        Chromium = entity.Chromium,
        Molybdenum = entity.Molybdenum,
        Copper = entity.Copper,
        Nitrogen = entity.Nitrogen,
        Niobium = entity.Niobium,
        Titanium = entity.Titanium,
        Iron = entity.Iron,
        Aluminum = entity.Aluminum,
        Tungsten = entity.Tungsten
    };

    public static GradePhysicalPropertyDto ToPhysicalPropertyDto(this GradePhysicalProperty entity) => new()
    {
        Id = entity.Id,
        StandardGrade = entity.StandardGrade,
        StandardGradeCategory = entity.StandardGradeCategory,
        Density = entity.Density,
        HeatTreatmentTemp = entity.HeatTreatmentTemp,
        HardnessRockwell = entity.HardnessRockwell,
        HardnessVickers = entity.HardnessVickers,
        HardnessBrinell = entity.HardnessBrinell,
        TensileStrength = entity.TensileStrength,
        YieldStrength02 = entity.YieldStrength02,
        YieldStrength10 = entity.YieldStrength10,
        Elongation = entity.Elongation,
        GrainSize = entity.GrainSize
    };

    public static MaterialPlanProcessGroupDto ToDto(this InProcessReworkPlanProcessGroup entity) =>
        ToPlanGroupDto(
            entity.InProcessReworkPlanId, entity.SequenceNumber,
            entity.ProcessName, entity.ManufacturingSpec, entity.OuterDiameterTolerance,
            entity.WallThicknessTolerance, entity.ManufacturingLength, entity.CuttingTreatment,
            entity.ManufacturingMultiple, entity.Remark,
            entity.ColdRollDraw, entity.OilPipeCut, entity.Degrease, entity.Solution,
            entity.Straighten, entity.Cut, entity.ThicknessMeasure, entity.Pickle,
            entity.OuterPolish, entity.InnerGrinding, entity.OuterSpotGrinding,
            entity.Inspection, entity.WeldingHead, entity.Lubrication, entity.Warehouse,
            entity.Id, entity.CreatedTime, entity.CreatedBy);
}
