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
    }

    public static string[] GetAllRoles()
    {
        return new[]
        {
            Admin,
            Directors.Order, Directors.WorkOrder, Directors.Batch, Directors.Quality,
            Directors.Equipment, Directors.Warehouse, Directors.Material,
            Staffs.Order, Staffs.WorkOrder, Staffs.Batch, Staffs.Quality,
            Staffs.Equipment, Staffs.Warehouse, Staffs.Material
        };
    }
}
