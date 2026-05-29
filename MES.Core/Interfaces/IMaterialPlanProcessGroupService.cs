using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// planType: 1=PurchaseSemiPlan, 3=InventoryPlan, 4=RoundBarPiercingPlan
/// </summary>
public interface IMaterialPlanProcessGroupService
{
    Task<List<MaterialPlanProcessGroupDto>> GetByPlanAsync(int planType, int planId);
    Task SaveAsync(int planType, int planId, List<SavePlanProcessGroupItem> items);
}
