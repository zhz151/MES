using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixOrderItemIdsUseSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 数据迁移：将 OrderItemIds 从存储 Id 改为存储 Sequence（项次序号）
            // 3 张表需要修复：WorkOrder, InventoryBatch, ProductionBatch
            //
            // 注意：表名使用 EF Core 定义的单数形式（[WorkOrder] 而非 WorkOrders）

            // 注意：使用游标+FOR XML PATH方案，兼容所有SQL Server版本
            // STRING_AGG 在低版本兼容级别下不支持 NVARCHAR 分隔符

            migrationBuilder.Sql(@"
-- ============================================================
-- 1. 修复 WorkOrder 表
-- ============================================================
DECLARE @Id INT, @OrderItemIds NVARCHAR(MAX), @NewValue NVARCHAR(MAX);
DECLARE WO_CURSOR CURSOR LOCAL FAST_FORWARD FOR
SELECT Id, OrderItemIds FROM [WorkOrder] WHERE OrderItemIds IS NOT NULL AND OrderItemIds != N'';

OPEN WO_CURSOR;
FETCH NEXT FROM WO_CURSOR INTO @Id, @OrderItemIds;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @NewValue = ISNULL(STUFF((
        SELECT N',' + CAST(oi.Sequence AS NVARCHAR(10))
        FROM STRING_SPLIT(@OrderItemIds, N',') ss
        INNER JOIN [OrderItem] oi ON oi.Id = TRY_CAST(ss.value AS INT)
        ORDER BY oi.Sequence
        FOR XML PATH(N''), TYPE
    ).value(N'.', N'NVARCHAR(MAX)'), 1, 1, N''), N'');

    UPDATE [WorkOrder] SET OrderItemIds = @NewValue WHERE Id = @Id;
    FETCH NEXT FROM WO_CURSOR INTO @Id, @OrderItemIds;
END;
CLOSE WO_CURSOR;
DEALLOCATE WO_CURSOR;
");

            migrationBuilder.Sql(@"
-- ============================================================
-- 2. 修复 InventoryBatch 表
-- ============================================================
DECLARE @Id INT, @OrderItemIds NVARCHAR(MAX), @NewValue NVARCHAR(MAX);
DECLARE IB_CURSOR CURSOR LOCAL FAST_FORWARD FOR
SELECT Id, OrderItemIds FROM [InventoryBatch] WHERE OrderItemIds IS NOT NULL AND OrderItemIds != N'';

OPEN IB_CURSOR;
FETCH NEXT FROM IB_CURSOR INTO @Id, @OrderItemIds;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @NewValue = ISNULL(STUFF((
        SELECT N',' + CAST(oi.Sequence AS NVARCHAR(10))
        FROM STRING_SPLIT(@OrderItemIds, N',') ss
        INNER JOIN [OrderItem] oi ON oi.Id = TRY_CAST(ss.value AS INT)
        ORDER BY oi.Sequence
        FOR XML PATH(N''), TYPE
    ).value(N'.', N'NVARCHAR(MAX)'), 1, 1, N''), N'');

    UPDATE [InventoryBatch] SET OrderItemIds = @NewValue WHERE Id = @Id;
    FETCH NEXT FROM IB_CURSOR INTO @Id, @OrderItemIds;
END;
CLOSE IB_CURSOR;
DEALLOCATE IB_CURSOR;
");

            migrationBuilder.Sql(@"
-- ============================================================
-- 3. 修复 ProductionBatch 表
-- ============================================================
DECLARE @Id INT, @OrderItemIds NVARCHAR(MAX), @NewValue NVARCHAR(MAX);
DECLARE PB_CURSOR CURSOR LOCAL FAST_FORWARD FOR
SELECT Id, OrderItemIds FROM [ProductionBatch] WHERE OrderItemIds IS NOT NULL AND OrderItemIds != N'';

OPEN PB_CURSOR;
FETCH NEXT FROM PB_CURSOR INTO @Id, @OrderItemIds;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @NewValue = ISNULL(STUFF((
        SELECT N',' + CAST(oi.Sequence AS NVARCHAR(10))
        FROM STRING_SPLIT(@OrderItemIds, N',') ss
        INNER JOIN [OrderItem] oi ON oi.Id = TRY_CAST(ss.value AS INT)
        ORDER BY oi.Sequence
        FOR XML PATH(N''), TYPE
    ).value(N'.', N'NVARCHAR(MAX)'), 1, 1, N''), N'');

    UPDATE [ProductionBatch] SET OrderItemIds = @NewValue WHERE Id = @Id;
    FETCH NEXT FROM PB_CURSOR INTO @Id, @OrderItemIds;
END;
CLOSE PB_CURSOR;
DEALLOCATE PB_CURSOR;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 数据迁移不可逆，无法自动还原旧数据
        }
    }
}
