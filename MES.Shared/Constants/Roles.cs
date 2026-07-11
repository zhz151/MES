namespace MES.Shared.Constants;

public static class Roles
{
    public const string Admin = "Admin";

    public static class Directors
    {
        public const string Order = "OrderDirector";
        public const string WorkOrder = "WorkOrderDirector";
        public const string Batch = "BatchDirector";
        public const string Quality = "QualityDirector";
        public const string Equipment = "EquipmentDirector";
        public const string Warehouse = "WarehouseDirector";
        public const string Material = "MaterialDirector";
        public const string Standard = "StandardDirector";
    }

    public static class Staffs
    {
        public const string Order = "OrderStaff";
        public const string WorkOrder = "WorkOrderStaff";
        public const string Batch = "BatchStaff";
        public const string Quality = "QualityStaff";
        public const string Equipment = "EquipmentStaff";
        public const string Warehouse = "WarehouseStaff";
        public const string Material = "MaterialStaff";
        public const string Standard = "StandardStaff";
    }

    /// <summary>
    /// 常用角色组合策略 — 统一管理 [Authorize(Roles = "...")] 中的字符串，
    /// 避免 70+ 处硬编码。格式为 "Staff,DomainDirector,Admin"（读）或 "DomainDirector,Admin"（写）。
    /// </summary>
    public static class Policies
    {
        public const string BatchRead = "BatchStaff,BatchDirector,Admin";
        public const string BatchWrite = "BatchDirector,Admin";

        public const string WorkOrderRead = "WorkOrderStaff,WorkOrderDirector,Admin";
        public const string WorkOrderWrite = "WorkOrderDirector,Admin";

        public const string OrderRead = "OrderStaff,OrderDirector,Admin";
        public const string OrderWrite = "OrderDirector,Admin";

        public const string MaterialRead = "MaterialStaff,MaterialDirector,Admin";
        public const string MaterialWrite = "MaterialDirector,Admin";

        public const string EquipmentRead = "EquipmentStaff,EquipmentDirector,Admin";
        public const string EquipmentWrite = "EquipmentDirector,Admin";

        public const string QualityRead = "QualityStaff,QualityDirector,Admin";
        public const string QualityWrite = "QualityDirector,Admin";

        public const string WarehouseRead = "WarehouseStaff,WarehouseDirector,Admin";

        public const string AdminOnly = "Admin";

        public const string ConfigurationRead = "Admin";
        public const string ConfigurationWrite = "Admin";

        public const string StandardRead = "StandardStaff,StandardDirector,Admin";
        public const string StandardWrite = "StandardDirector,Admin";
    }

    public static string[] GetAllRoles()
    {
        return new[]
        {
            Admin,
            Directors.Order, Directors.WorkOrder, Directors.Batch, Directors.Quality,
            Directors.Equipment, Directors.Warehouse, Directors.Material, Directors.Standard,
            Staffs.Order, Staffs.WorkOrder, Staffs.Batch, Staffs.Quality,
            Staffs.Equipment, Staffs.Warehouse, Staffs.Material, Staffs.Standard
        };
    }
}
