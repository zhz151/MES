using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaterialReceiveCheckEnumNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== Shift（班次）：中文→枚举名 =====
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [Shift] = N'DayShift' WHERE [Shift] = N'白班';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [Shift] = N'MiddleShift' WHERE [Shift] = N'中班';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [Shift] = N'NightShift' WHERE [Shift] = N'夜班';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [Shift] = NULL WHERE [Shift] = N'';");

            // ===== ManufacturingItem（物料类型）：旧枚举名→新枚举名 + 中文→枚举名 =====
            // MaterialType 枚举名变更历史
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'OrderFinished' WHERE [ManufacturingItem] = N'OrderFinishedProduct';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'Finished' WHERE [ManufacturingItem] IN (N'PreparedMaterial', N'PreparedFinished', N'StockFinished');");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'Surplus' WHERE [ManufacturingItem] = N'SurplusStock';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'SemiFinished' WHERE [ManufacturingItem] = N'IntermediateProduct';");
            // MaterialType 中文→枚举名
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'Finished' WHERE [ManufacturingItem] = N'备料成品';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'OrderFinished' WHERE [ManufacturingItem] = N'订单成品';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'CriticalFinished' WHERE [ManufacturingItem] = N'临界成品';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'Surplus' WHERE [ManufacturingItem] = N'余库料';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'SemiFinished' WHERE [ManufacturingItem] = N'半成品';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'DefectSemi' WHERE [ManufacturingItem] = N'次品半成品';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'DefectFinished' WHERE [ManufacturingItem] = N'次品成品';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'RoughTube' WHERE [ManufacturingItem] = N'荒管';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'RoundBar' WHERE [ManufacturingItem] = N'圆棒';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'DefectRoundBar' WHERE [ManufacturingItem] = N'次品圆棒';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'DefectRoughTube' WHERE [ManufacturingItem] = N'次品荒管';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'Scrap' WHERE [ManufacturingItem] = N'报废品';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'SpecialDeliveryStatus' WHERE [ManufacturingItem] = N'特定交态成品';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'WorkInProgress' WHERE [ManufacturingItem] = N'在制品';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = N'DefectWIP' WHERE [ManufacturingItem] = N'次品在制';");
            // 空串→null
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ManufacturingItem] = NULL WHERE [ManufacturingItem] = N'';");

            // ===== ProductionType（生产类型）：中文→枚举名 =====
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ProductionType] = N'RoughTube' WHERE [ProductionType] = N'荒管生产';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ProductionType] = N'InProcess' WHERE [ProductionType] = N'在制生产';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ProductionType] = N'Inventory' WHERE [ProductionType] = N'库存';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ProductionType] = N'OutsourcedPurchased' WHERE [ProductionType] = N'外购';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ProductionType] = N'Rework' WHERE [ProductionType] = N'返整';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ProductionType] = N'Subcontract' WHERE [ProductionType] = N'委外生产';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ProductionType] = N'ExternalProcessing' WHERE [ProductionType] = N'对外加工';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [ProductionType] = NULL WHERE [ProductionType] = N'';");

            // ===== LengthStatus（长度状态）：中文→枚举名 =====
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [LengthStatus] = N'Fixed' WHERE [LengthStatus] = N'定尺';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [LengthStatus] = N'Range' WHERE [LengthStatus] = N'范围尺';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [LengthStatus] = N'NonFixed' WHERE [LengthStatus] = N'非定尺';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [LengthStatus] = NULL WHERE [LengthStatus] = N'';");

            // ===== DeliveryState（交态）：中文→枚举名 =====
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'SolutionAnnealedAndPickled' WHERE [DeliveryState] = N'固溶酸洗';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'SolutionAnnealedAndPickledUTube' WHERE [DeliveryState] = N'固溶酸洗-U型管';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'SolutionAnnealedAndPickledExternalPolished' WHERE [DeliveryState] = N'固溶酸洗-外抛光';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'SolutionAnnealedAndPickledInternalPolished' WHERE [DeliveryState] = N'固溶酸洗-内抛光';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'SolutionAnnealedAndPickledBothPolished' WHERE [DeliveryState] = N'固溶酸洗-内外抛光';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'SolutionAnnealedAndPickledCoiled' WHERE [DeliveryState] = N'固溶酸洗-盘管';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'Bright' WHERE [DeliveryState] = N'光亮';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'BrightUTube' WHERE [DeliveryState] = N'光亮-U型管';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'BrightCoiled' WHERE [DeliveryState] = N'光亮-盘管';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = N'Hard' WHERE [DeliveryState] = N'硬态';");
            migrationBuilder.Sql("UPDATE [MaterialReceiveCheck] SET [DeliveryState] = NULL WHERE [DeliveryState] = N'';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 数据不可逆恢复，Down 无操作
        }
    }
}
