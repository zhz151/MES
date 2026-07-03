using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixWarehouseThresholdContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sql = @"
-- CompleteRatio/Deviation 用于工单执行入库完结判定
UPDATE ConfigParameters SET CategoryDisplay = N'工单-入库完结比率', Context = N'工单' WHERE Category = 'WarehouseThreshold' AND ParamKey = 'CompleteRatio';
UPDATE ConfigParameters SET CategoryDisplay = N'工单-入库完结偏差', Context = N'工单' WHERE Category = 'WarehouseThreshold' AND ParamKey = 'CompleteDeviation';

-- PurchaseCompleteRatio/Deviation 用于采购订单完工判定
UPDATE ConfigParameters SET CategoryDisplay = N'采购-完工比率', Context = N'物料' WHERE Category = 'WarehouseThreshold' AND ParamKey = 'PurchaseCompleteRatio';
UPDATE ConfigParameters SET CategoryDisplay = N'采购-完工偏差', Context = N'物料' WHERE Category = 'WarehouseThreshold' AND ParamKey = 'PurchaseCompleteDeviation';

-- SubcontractCompleteRatio 用于委外回收入库判定（物料上下文）
UPDATE ConfigParameters SET CategoryDisplay = N'委外-回收比率', Context = N'物料' WHERE Category = 'WarehouseThreshold' AND ParamKey = 'SubcontractCompleteRatio';
";
            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var sql = @"
UPDATE ConfigParameters SET CategoryDisplay = N'仓库-完工阈值', Context = N'采购+仓库' WHERE Category = 'WarehouseThreshold' AND ParamKey IN ('CompleteRatio','CompleteDeviation','PurchaseCompleteRatio','PurchaseCompleteDeviation','SubcontractCompleteRatio');
";
            migrationBuilder.Sql(sql);
        }
    }
}
