BEGIN TRANSACTION;
GO

ALTER TABLE [InventoryPlan] ADD [LocationArea] nvarchar(100) NULL;
GO

ALTER TABLE [InventoryPlan] ADD [LocationRack] nvarchar(100) NULL;
GO

ALTER TABLE [InventoryPlan] ADD [MaterialType] nvarchar(50) NOT NULL DEFAULT N'';
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260504113233_AddInventoryPlanMaterialTypeLocation', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SupplierProfile] ADD [SupplierCode] nvarchar(6) NOT NULL DEFAULT N'';
GO

ALTER TABLE [Material] ADD [MaterialCode] nvarchar(6) NOT NULL DEFAULT N'';
GO

                UPDATE m
                SET m.MaterialCode = t.NewCode
                FROM Material m
                INNER JOIN (
                    SELECT Id, CONCAT('MA', RIGHT('0000' + CAST(ROW_NUMBER() OVER(ORDER BY Id) AS NVARCHAR(4)), 4)) AS NewCode
                    FROM Material
                ) t ON m.Id = t.Id
                WHERE m.MaterialCode = '';
GO

                UPDATE s
                SET s.SupplierCode = t.NewCode
                FROM SupplierProfile s
                INNER JOIN (
                    SELECT Id, CONCAT('SU', RIGHT('0000' + CAST(ROW_NUMBER() OVER(ORDER BY Id) AS NVARCHAR(4)), 4)) AS NewCode
                    FROM SupplierProfile
                ) t ON s.Id = t.Id
                WHERE s.SupplierCode = '';
GO

CREATE UNIQUE INDEX [UK_Supplier_Code] ON [SupplierProfile] ([SupplierCode]) WHERE [IsDeleted] = 0;
GO

CREATE UNIQUE INDEX [UK_Material_Code] ON [Material] ([MaterialCode]) WHERE [IsDeleted] = 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260504183547_AddMaterialAndSupplierCode', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [PurchaseOrder] ADD [DeliveryState] nvarchar(max) NULL;
GO

ALTER TABLE [PurchaseOrder] ADD [LengthStatus] nvarchar(max) NULL;
GO

ALTER TABLE [PurchaseOrder] ADD [MaxLength] decimal(18,2) NULL;
GO

ALTER TABLE [PurchaseOrder] ADD [MinLength] decimal(18,2) NULL;
GO

ALTER TABLE [PurchaseOrder] ADD [OuterDiameterNegative] decimal(18,2) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [PurchaseOrder] ADD [OuterDiameterPositive] decimal(18,2) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [PurchaseOrder] ADD [PlanType] nvarchar(max) NULL;
GO

ALTER TABLE [PurchaseOrder] ADD [WallThicknessNegative] decimal(18,2) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [PurchaseOrder] ADD [WallThicknessPositive] decimal(18,2) NOT NULL DEFAULT 0.0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260504203613_AddPurchaseOrderPlanFields', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'DeliveryState');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [DeliveryState];
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'LengthStatus');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [LengthStatus];
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'MaxLength');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [MaxLength];
GO

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'MinLength');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [MinLength];
GO

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'OuterDiameterNegative');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [OuterDiameterNegative];
GO

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'OuterDiameterPositive');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [OuterDiameterPositive];
GO

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'PlanType');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [PlanType];
GO

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'WallThicknessNegative');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [WallThicknessNegative];
GO

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'WallThicknessPositive');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [WallThicknessPositive];
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505054634_RemovePurchaseOrderPlanFields', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SupplierProfile] ADD [MaterialCategory] nvarchar(100) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505063411_AddSupplierMaterialCategory', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [UK_Supplier_Code] ON [SupplierProfile];
GO

DROP INDEX [UK_OrderItem_Sequence_Active] ON [OrderItem];
GO

DROP INDEX [UK_Material_Code] ON [Material];
GO

DROP INDEX [UK_Material_Combo] ON [Material];
GO

DROP INDEX [IX_InventoryBatch_RemainingWeight] ON [InventoryBatch];
GO

DECLARE @var9 sysname;
SELECT @var9 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'IsDeleted');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var9 + '];');
ALTER TABLE [WorkOrder] DROP COLUMN [IsDeleted];
GO

DECLARE @var10 sysname;
SELECT @var10 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Warehouse]') AND [c].[name] = N'IsDeleted');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Warehouse] DROP CONSTRAINT [' + @var10 + '];');
ALTER TABLE [Warehouse] DROP COLUMN [IsDeleted];
GO

DECLARE @var11 sysname;
SELECT @var11 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierProfile]') AND [c].[name] = N'IsDeleted');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [SupplierProfile] DROP CONSTRAINT [' + @var11 + '];');
ALTER TABLE [SupplierProfile] DROP COLUMN [IsDeleted];
GO

DECLARE @var12 sysname;
SELECT @var12 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractReturnItem]') AND [c].[name] = N'IsDeleted');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractReturnItem] DROP CONSTRAINT [' + @var12 + '];');
ALTER TABLE [SubcontractReturnItem] DROP COLUMN [IsDeleted];
GO

DECLARE @var13 sysname;
SELECT @var13 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractOrder]') AND [c].[name] = N'IsDeleted');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @var13 + '];');
ALTER TABLE [SubcontractOrder] DROP COLUMN [IsDeleted];
GO

DECLARE @var14 sysname;
SELECT @var14 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StandardGradeMapping]') AND [c].[name] = N'IsDeleted');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [StandardGradeMapping] DROP CONSTRAINT [' + @var14 + '];');
ALTER TABLE [StandardGradeMapping] DROP COLUMN [IsDeleted];
GO

DECLARE @var15 sysname;
SELECT @var15 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesOrder]') AND [c].[name] = N'IsDeleted');
IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [SalesOrder] DROP CONSTRAINT [' + @var15 + '];');
ALTER TABLE [SalesOrder] DROP COLUMN [IsDeleted];
GO

DECLARE @var16 sysname;
SELECT @var16 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RefreshToken]') AND [c].[name] = N'IsDeleted');
IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [RefreshToken] DROP CONSTRAINT [' + @var16 + '];');
ALTER TABLE [RefreshToken] DROP COLUMN [IsDeleted];
GO

DECLARE @var17 sysname;
SELECT @var17 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'IsDeleted');
IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var17 + '];');
ALTER TABLE [PurchaseSemiPlan] DROP COLUMN [IsDeleted];
GO

DECLARE @var18 sysname;
SELECT @var18 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'IsDeleted');
IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var18 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [IsDeleted];
GO

DECLARE @var19 sysname;
SELECT @var19 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseFinishedPlan]') AND [c].[name] = N'IsDeleted');
IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseFinishedPlan] DROP CONSTRAINT [' + @var19 + '];');
ALTER TABLE [PurchaseFinishedPlan] DROP COLUMN [IsDeleted];
GO

DECLARE @var20 sysname;
SELECT @var20 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductRequirement]') AND [c].[name] = N'IsDeleted');
IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [ProductRequirement] DROP CONSTRAINT [' + @var20 + '];');
ALTER TABLE [ProductRequirement] DROP COLUMN [IsDeleted];
GO

DECLARE @var21 sysname;
SELECT @var21 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionStandard]') AND [c].[name] = N'IsDeleted');
IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [ProductionStandard] DROP CONSTRAINT [' + @var21 + '];');
ALTER TABLE [ProductionStandard] DROP COLUMN [IsDeleted];
GO

DECLARE @var22 sysname;
SELECT @var22 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItem]') AND [c].[name] = N'IsDeleted');
IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [OrderItem] DROP CONSTRAINT [' + @var22 + '];');
ALTER TABLE [OrderItem] DROP COLUMN [IsDeleted];
GO

DECLARE @var23 sysname;
SELECT @var23 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderChangeNotification]') AND [c].[name] = N'IsDeleted');
IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [OrderChangeNotification] DROP CONSTRAINT [' + @var23 + '];');
ALTER TABLE [OrderChangeNotification] DROP COLUMN [IsDeleted];
GO

DECLARE @var24 sysname;
SELECT @var24 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Material]') AND [c].[name] = N'IsDeleted');
IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [Material] DROP CONSTRAINT [' + @var24 + '];');
ALTER TABLE [Material] DROP COLUMN [IsDeleted];
GO

DECLARE @var25 sysname;
SELECT @var25 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryPlan]') AND [c].[name] = N'IsDeleted');
IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [InventoryPlan] DROP CONSTRAINT [' + @var25 + '];');
ALTER TABLE [InventoryPlan] DROP COLUMN [IsDeleted];
GO

DECLARE @var26 sysname;
SELECT @var26 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'IsDeleted');
IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var26 + '];');
ALTER TABLE [InventoryBatch] DROP COLUMN [IsDeleted];
GO

DECLARE @var27 sysname;
SELECT @var27 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfile]') AND [c].[name] = N'IsDeleted');
IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfile] DROP CONSTRAINT [' + @var27 + '];');
ALTER TABLE [CustomerProfile] DROP COLUMN [IsDeleted];
GO

CREATE UNIQUE INDEX [UK_Supplier_Code] ON [SupplierProfile] ([SupplierCode]);
GO

CREATE UNIQUE INDEX [UK_OrderItem_Sequence_Active] ON [OrderItem] ([SalesOrderId], [Sequence]);
GO

CREATE UNIQUE INDEX [UK_Material_Code] ON [Material] ([MaterialCode]);
GO

CREATE UNIQUE INDEX [UK_Material_Combo] ON [Material] ([MaterialCategory], [PlantGrade], [Specification]);
GO

CREATE INDEX [IX_InventoryBatch_RemainingWeight] ON [InventoryBatch] ([RemainingWeight]) WHERE [RemainingWeight] > 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505125529_RemoveIsDeletedColumn', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

                DECLARE @cn NVARCHAR(200);
                SELECT @cn = d.name FROM sys.default_constraints d
                JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
                WHERE d.parent_object_id = OBJECT_ID(N'[SubcontractOrder]') AND c.name = 'SourceWorkOrderNo';
                IF @cn IS NOT NULL EXEC('ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @cn + ']');
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractOrder]') AND name = 'SourceWorkOrderNo')
                    ALTER TABLE [SubcontractOrder] DROP COLUMN [SourceWorkOrderNo];
GO

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractReturnItem]') AND name = 'SourceWorkOrderNo')
                BEGIN
                    ALTER TABLE [SubcontractReturnItem] ADD [SourceWorkOrderNo] nvarchar(50) NULL;
                END
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505151003_UpdateSubcontractOrderFields', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SubcontractReturnItem] ADD [PlantGrade] nvarchar(50) NULL;
GO

ALTER TABLE [SubcontractReturnItem] ADD [Remark] nvarchar(500) NULL;
GO

ALTER TABLE [SubcontractReturnItem] ADD [RequiredQuantity] int NULL;
GO

ALTER TABLE [SubcontractReturnItem] ADD [RequiredWeight] decimal(18,4) NULL;
GO

ALTER TABLE [SubcontractReturnItem] ADD [UnitWeight] decimal(18,4) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505161934_AddSubcontractReturnItemMissingFields', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractOrder]') AND name = 'FurnaceNumber')
                BEGIN
                    ALTER TABLE [SubcontractOrder] ADD [FurnaceNumber] nvarchar(50) NULL;
                END
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505164611_AddFurnaceNumberToSubcontractOrder', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var28 sysname;
SELECT @var28 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractReturnItem]') AND [c].[name] = N'ProcessType');
IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractReturnItem] DROP CONSTRAINT [' + @var28 + '];');
ALTER TABLE [SubcontractReturnItem] DROP COLUMN [ProcessType];
GO

ALTER TABLE [SubcontractOrder] ADD [ProcessType] nvarchar(30) NOT NULL DEFAULT N'';
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260505173016_MoveProcessTypeToSubcontractOrder', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var29 sysname;
SELECT @var29 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutboundRecord]') AND [c].[name] = N'Operator');
IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [OutboundRecord] DROP CONSTRAINT [' + @var29 + '];');
ALTER TABLE [OutboundRecord] DROP COLUMN [Operator];
GO

ALTER TABLE [OutboundRecord] ADD [SourceOrderNo] nvarchar(50) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260506072912_AddOutboundSourceOrderNo', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var30 sysname;
SELECT @var30 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractOrder]') AND [c].[name] = N'ManualStatus');
IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @var30 + '];');
ALTER TABLE [SubcontractOrder] DROP COLUMN [ManualStatus];
GO

DECLARE @var31 sysname;
SELECT @var31 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'ManualStatus');
IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var31 + '];');
ALTER TABLE [PurchaseOrder] DROP COLUMN [ManualStatus];
GO

ALTER TABLE [SubcontractOrder] ADD [IsForceCompleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [PurchaseOrder] ADD [IsForceCompleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260506211607_RenameManualStatusToIsForceCompleted', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ProductionBatch] (
    [Id] int NOT NULL IDENTITY,
    [BatchNo] nvarchar(50) NOT NULL,
    [Status] nvarchar(20) NOT NULL DEFAULT N'None',
    [TagNo] nvarchar(50) NULL,
    [IsForceCompleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [QualityRemark] nvarchar(500) NULL,
    [SolutionParams] nvarchar(500) NULL,
    [CurrentExecDate] datetime NULL,
    [CurrentGroupName] nvarchar(50) NULL,
    [CurrentSectionName] nvarchar(50) NULL,
    [CurrentEquipmentName] nvarchar(100) NULL,
    [CurrentOutsource] nvarchar(200) NULL,
    [NextSectionName] nvarchar(50) NULL,
    [Remark] nvarchar(500) NULL,
    [RowVersion] rowversion NOT NULL,
    [WorkOrderNo] nvarchar(50) NOT NULL,
    [SalesOrderNo] nvarchar(50) NOT NULL,
    [ProductionMainNo] nvarchar(50) NOT NULL,
    [ProductionSubNo] nvarchar(50) NULL,
    [OrderItemIds] nvarchar(500) NOT NULL,
    [SignDate] datetime NOT NULL,
    [Salesman] nvarchar(50) NOT NULL,
    [EndCustomer] nvarchar(200) NULL,
    [DeliveryDate] datetime NOT NULL,
    [DelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
    [MaterialName] nvarchar(20) NOT NULL,
    [SettlementMethod] nvarchar(20) NOT NULL,
    [StandardCode] nvarchar(50) NOT NULL,
    [DeliveryState] nvarchar(50) NOT NULL,
    [PlantGrade] nvarchar(50) NOT NULL,
    [Specification] nvarchar(100) NOT NULL,
    [OuterDiameterNegative] decimal(18,3) NOT NULL DEFAULT 0.0,
    [OuterDiameterPositive] decimal(18,3) NOT NULL DEFAULT 0.0,
    [WallThicknessNegative] decimal(18,3) NOT NULL DEFAULT 0.0,
    [WallThicknessPositive] decimal(18,3) NOT NULL DEFAULT 0.0,
    [LengthStatus] nvarchar(20) NOT NULL,
    [MinLength] decimal(18,2) NULL,
    [MaxLength] decimal(18,2) NULL,
    [TotalQuantity] int NOT NULL DEFAULT 0,
    [TotalMeters] decimal(18,2) NOT NULL DEFAULT 0.0,
    [TotalWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [TotalItemCount] int NOT NULL DEFAULT 0,
    [ItemDetails] nvarchar(max) NULL,
    [TechnicalRequirements] nvarchar(20) NOT NULL,
    [SourceBatchNo] nvarchar(50) NULL,
    [WarehouseId] int NULL,
    [SourceMaterialType] nvarchar(30) NULL,
    [InboundSource] nvarchar(20) NULL,
    [SourceName] nvarchar(200) NULL,
    [InboundDate] datetime NULL,
    [SourceHeatNo] nvarchar(50) NULL,
    [InputQuantity] int NULL,
    [InputWeight] decimal(18,3) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ProductionBatch] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ProcessGroup] (
    [Id] int NOT NULL IDENTITY,
    [ProductionBatchId] int NOT NULL,
    [SequenceNumber] int NOT NULL,
    [ProcessName] nvarchar(50) NOT NULL,
    [ManufacturingSpec] nvarchar(100) NULL,
    [ManufacturingLength] nvarchar(100) NULL,
    [CuttingTreatment] nvarchar(200) NULL,
    [Remark] nvarchar(500) NULL,
    [ColdRollDraw] int NULL,
    [OilPipeCut] int NULL,
    [Degrease] int NULL,
    [Solution] int NULL,
    [Straighten] int NULL,
    [Cut] int NULL,
    [ThicknessMeasure] int NULL,
    [Pickle] int NULL,
    [OuterPolish] int NULL,
    [InnerGrinding] int NULL,
    [OuterSpotGrinding] int NULL,
    [Inspection] int NULL,
    [WeldingHead] int NULL,
    [Lubrication] int NULL,
    [Warehouse] int NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ProcessGroup] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProcessGroup_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_ProcessGroup_BatchId] ON [ProcessGroup] ([ProductionBatchId]);
GO

CREATE UNIQUE INDEX [UK_ProcessGroup_Seq] ON [ProcessGroup] ([ProductionBatchId], [SequenceNumber]);
GO

CREATE INDEX [IX_ProductionBatch_SalesOrderNo] ON [ProductionBatch] ([SalesOrderNo]);
GO

CREATE INDEX [IX_ProductionBatch_Status] ON [ProductionBatch] ([Status]);
GO

CREATE INDEX [IX_ProductionBatch_TagNo] ON [ProductionBatch] ([TagNo]);
GO

CREATE INDEX [IX_ProductionBatch_WorkOrderNo] ON [ProductionBatch] ([WorkOrderNo]);
GO

CREATE UNIQUE INDEX [UK_ProductionBatch_BatchNo] ON [ProductionBatch] ([BatchNo]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260507153100_AddBatchContext', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProductionBatch] ADD [ProductionRatio] int NULL;
GO

ALTER TABLE [ProductionBatch] ADD [ProductionType] nvarchar(20) NULL;
GO

ALTER TABLE [ProcessGroup] ADD [OuterDiameterTolerance] nvarchar(50) NULL;
GO

ALTER TABLE [ProcessGroup] ADD [WallThicknessTolerance] nvarchar(50) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260507204240_AddProductionTypeAndRatioToBatch', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProductionBatch] ADD [SourceLengthStatus] nvarchar(20) NULL;
GO

ALTER TABLE [ProductionBatch] ADD [SourcePlantGrade] nvarchar(50) NULL;
GO

ALTER TABLE [ProductionBatch] ADD [SourceSpecification] nvarchar(100) NULL;
GO

ALTER TABLE [ProductionBatch] ADD [SourceUnitWeight] decimal(18,3) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260507234454_AddSourceWarehouseFieldsToBatch', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var32 sysname;
SELECT @var32 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'SignDate');
IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var32 + '];');
ALTER TABLE [WorkOrder] ALTER COLUMN [SignDate] datetime2 NOT NULL;
GO

DROP INDEX [IX_WorkOrder_DeliveryDate] ON [WorkOrder];
DECLARE @var33 sysname;
SELECT @var33 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'DeliveryDate');
IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var33 + '];');
ALTER TABLE [WorkOrder] ALTER COLUMN [DeliveryDate] datetime2 NOT NULL;
CREATE INDEX [IX_WorkOrder_DeliveryDate] ON [WorkOrder] ([DeliveryDate]);
GO

DROP INDEX [IX_SalesOrder_SignDate] ON [SalesOrder];
DECLARE @var34 sysname;
SELECT @var34 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesOrder]') AND [c].[name] = N'SignDate');
IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [SalesOrder] DROP CONSTRAINT [' + @var34 + '];');
ALTER TABLE [SalesOrder] ALTER COLUMN [SignDate] datetime2 NOT NULL;
CREATE INDEX [IX_SalesOrder_SignDate] ON [SalesOrder] ([SignDate]);
GO

DECLARE @var35 sysname;
SELECT @var35 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'SignDate');
IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var35 + '];');
ALTER TABLE [ProductionBatch] ALTER COLUMN [SignDate] datetime2 NOT NULL;
GO

DECLARE @var36 sysname;
SELECT @var36 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'InboundDate');
IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var36 + '];');
ALTER TABLE [ProductionBatch] ALTER COLUMN [InboundDate] datetime2 NULL;
GO

DECLARE @var37 sysname;
SELECT @var37 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'DeliveryDate');
IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var37 + '];');
ALTER TABLE [ProductionBatch] ALTER COLUMN [DeliveryDate] datetime2 NOT NULL;
GO

DECLARE @var38 sysname;
SELECT @var38 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'CurrentExecDate');
IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var38 + '];');
ALTER TABLE [ProductionBatch] ALTER COLUMN [CurrentExecDate] datetime2 NULL;
GO

DROP INDEX [IX_OutboundRecord_OutboundDate] ON [OutboundRecord];
DECLARE @var39 sysname;
SELECT @var39 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutboundRecord]') AND [c].[name] = N'OutboundDate');
IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [OutboundRecord] DROP CONSTRAINT [' + @var39 + '];');
ALTER TABLE [OutboundRecord] ALTER COLUMN [OutboundDate] datetime2 NOT NULL;
CREATE INDEX [IX_OutboundRecord_OutboundDate] ON [OutboundRecord] ([OutboundDate]);
GO

DECLARE @var40 sysname;
SELECT @var40 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItem]') AND [c].[name] = N'DeliveryDate');
IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [OrderItem] DROP CONSTRAINT [' + @var40 + '];');
ALTER TABLE [OrderItem] ALTER COLUMN [DeliveryDate] datetime2 NOT NULL;
GO

DECLARE @var41 sysname;
SELECT @var41 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatchDeleteLog]') AND [c].[name] = N'DeletedTime');
IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatchDeleteLog] DROP CONSTRAINT [' + @var41 + '];');
ALTER TABLE [InventoryBatchDeleteLog] ALTER COLUMN [DeletedTime] datetime2 NOT NULL;
GO

DECLARE @var42 sysname;
SELECT @var42 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'InboundDate');
IF @var42 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var42 + '];');
ALTER TABLE [InventoryBatch] ALTER COLUMN [InboundDate] datetime2 NOT NULL;
GO

CREATE TABLE [MaterialReceiveCheck] (
    [Id] int NOT NULL IDENTITY,
    [ProductionBatchId] int NOT NULL,
    [ReceiveDate] datetime2 NOT NULL,
    [ReceivedQuantity] int NULL,
    [ReceivedWeight] decimal(18,3) NULL,
    [Shift] nvarchar(10) NULL,
    [Checker] nvarchar(50) NULL,
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_MaterialReceiveCheck] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaterialReceiveCheck_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProductionRecord] (
    [Id] int NOT NULL IDENTITY,
    [ProductionBatchId] int NOT NULL,
    [ProcessGroupId] int NOT NULL,
    [ProcessName] nvarchar(50) NOT NULL,
    [ManufacturingSpec] nvarchar(100) NULL,
    [SectionName] nvarchar(50) NOT NULL,
    [SequenceNumber] int NOT NULL,
    [ExecDate] datetime2 NOT NULL,
    [EquipmentName] nvarchar(100) NULL,
    [Operator] nvarchar(50) NULL,
    [Shift] nvarchar(10) NULL,
    [Quantity] int NULL,
    [Weight] decimal(18,3) NULL,
    [DefectQuantity] int NULL,
    [DefectWeight] decimal(18,3) NULL,
    [IsFinished] bit NOT NULL DEFAULT CAST(0 AS bit),
    [CuttingRate] decimal(5,2) NULL,
    [FinishedCutLength] decimal(18,2) NULL,
    [PostCutQuantity] int NULL,
    [TagNo] nvarchar(50) NULL,
    [PlantGrade] nvarchar(50) NULL,
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ProductionRecord] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductionRecord_ProcessGroup_ProcessGroupId] FOREIGN KEY ([ProcessGroupId]) REFERENCES [ProcessGroup] ([Id]),
    CONSTRAINT [FK_ProductionRecord_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SectionOutsource] (
    [Id] int NOT NULL IDENTITY,
    [ProductionBatchId] int NOT NULL,
    [ProcessGroupId] int NOT NULL,
    [ProcessName] nvarchar(50) NOT NULL,
    [ManufacturingSpec] nvarchar(100) NULL,
    [SectionName] nvarchar(50) NOT NULL,
    [SequenceNumber] int NOT NULL,
    [OutsourceVendor] nvarchar(100) NOT NULL,
    [SendOutDate] datetime2 NOT NULL,
    [SendQuantity] int NULL,
    [SendWeight] decimal(18,3) NULL,
    [Status] nvarchar(20) NOT NULL DEFAULT N'待回收',
    [TagNo] nvarchar(50) NULL,
    [PlantGrade] nvarchar(50) NULL,
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_SectionOutsource] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SectionOutsource_ProcessGroup_ProcessGroupId] FOREIGN KEY ([ProcessGroupId]) REFERENCES [ProcessGroup] ([Id]),
    CONSTRAINT [FK_SectionOutsource_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [OutsourceRecovery] (
    [Id] int NOT NULL IDENTITY,
    [SectionOutsourceId] int NOT NULL,
    [RecoveryDate] datetime2 NOT NULL,
    [RecoveryQuantity] int NULL,
    [RecoveryWeight] decimal(18,3) NULL,
    [IsQualified] bit NOT NULL DEFAULT CAST(1 AS bit),
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_OutsourceRecovery] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OutsourceRecovery_SectionOutsource_SectionOutsourceId] FOREIGN KEY ([SectionOutsourceId]) REFERENCES [SectionOutsource] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [UK_MaterialReceiveCheck_BatchId] ON [MaterialReceiveCheck] ([ProductionBatchId]);
GO

CREATE INDEX [IX_OutsourceRecovery_OutsourceId] ON [OutsourceRecovery] ([SectionOutsourceId]);
GO

CREATE INDEX [IX_ProductionRecord_BatchId] ON [ProductionRecord] ([ProductionBatchId]);
GO

CREATE INDEX [IX_ProductionRecord_ProcessGroupId] ON [ProductionRecord] ([ProcessGroupId]);
GO

CREATE UNIQUE INDEX [UK_ProductionRecord_Section] ON [ProductionRecord] ([ProductionBatchId], [ProcessGroupId], [SectionName]);
GO

CREATE INDEX [IX_SectionOutsource_BatchId] ON [SectionOutsource] ([ProductionBatchId]);
GO

CREATE INDEX [IX_SectionOutsource_ProcessGroupId] ON [SectionOutsource] ([ProcessGroupId]);
GO

CREATE UNIQUE INDEX [UK_SectionOutsource_Section] ON [SectionOutsource] ([ProductionBatchId], [ProcessGroupId], [SectionName]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260508182550_AddProductionRecordContext', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

EXEC sp_rename N'[ProductionRecord].[CuttingRate]', N'CuttingMultiple', N'COLUMN';
GO

ALTER TABLE [OutsourceRecovery] ADD [UnprocessedQuantity] int NULL;
GO

ALTER TABLE [OutsourceRecovery] ADD [UnprocessedWeight] decimal(18,3) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260508210655_AddCuttingMultipleAndUnprocessedFields', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

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
GO

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
GO

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
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260509200158_FixOrderItemIdsUseSequence', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SectionOutsource] ADD [ExpectedReturnDate] datetime2 NULL;
GO

ALTER TABLE [SectionOutsource] ADD [IsUrgent] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [SectionOutsource] ADD [OutsourceSpec] nvarchar(100) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260510155012_AddOutsourceSpecReturnDateIsUrgent', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProductionBatch] ADD [CorrespondingSpec] nvarchar(max) NULL;
GO

ALTER TABLE [ProductionBatch] ADD [CurrentSpec] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260510191220_AddCurrentSpecAndCorrespondingSpec', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

UPDATE pr
SET pr.SequenceNumber =
    CASE pr.SectionName
        WHEN N'冷轧拔' THEN ISNULL(pg.ColdRollDraw, 0)
        WHEN N'油管断' THEN ISNULL(pg.OilPipeCut, 0)
        WHEN N'去油'   THEN ISNULL(pg.Degrease, 0)
        WHEN N'固溶'   THEN ISNULL(pg.Solution, 0)
        WHEN N'矫直'   THEN ISNULL(pg.Straighten, 0)
        WHEN N'断切'   THEN ISNULL(pg.Cut, 0)
        WHEN N'测壁厚' THEN ISNULL(pg.ThicknessMeasure, 0)
        WHEN N'酸洗'   THEN ISNULL(pg.Pickle, 0)
        WHEN N'外抛光' THEN ISNULL(pg.OuterPolish, 0)
        WHEN N'内修磨' THEN ISNULL(pg.InnerGrinding, 0)
        WHEN N'外点磨' THEN ISNULL(pg.OuterSpotGrinding, 0)
        WHEN N'检验'   THEN ISNULL(pg.Inspection, 0)
        WHEN N'打焊头' THEN ISNULL(pg.WeldingHead, 0)
        WHEN N'润滑'   THEN ISNULL(pg.Lubrication, 0)
        WHEN N'入库'   THEN ISNULL(pg.Warehouse, 0)
        ELSE 0
    END
FROM [ProductionRecord] pr
INNER JOIN [ProcessGroup] pg ON pr.ProcessGroupId = pg.Id;
GO

UPDATE so
SET so.SequenceNumber =
    CASE so.SectionName
        WHEN N'冷轧拔' THEN ISNULL(pg.ColdRollDraw, 0)
        WHEN N'油管断' THEN ISNULL(pg.OilPipeCut, 0)
        WHEN N'去油'   THEN ISNULL(pg.Degrease, 0)
        WHEN N'固溶'   THEN ISNULL(pg.Solution, 0)
        WHEN N'矫直'   THEN ISNULL(pg.Straighten, 0)
        WHEN N'断切'   THEN ISNULL(pg.Cut, 0)
        WHEN N'测壁厚' THEN ISNULL(pg.ThicknessMeasure, 0)
        WHEN N'酸洗'   THEN ISNULL(pg.Pickle, 0)
        WHEN N'外抛光' THEN ISNULL(pg.OuterPolish, 0)
        WHEN N'内修磨' THEN ISNULL(pg.InnerGrinding, 0)
        WHEN N'外点磨' THEN ISNULL(pg.OuterSpotGrinding, 0)
        WHEN N'检验'   THEN ISNULL(pg.Inspection, 0)
        WHEN N'打焊头' THEN ISNULL(pg.WeldingHead, 0)
        WHEN N'润滑'   THEN ISNULL(pg.Lubrication, 0)
        WHEN N'入库'   THEN ISNULL(pg.Warehouse, 0)
        ELSE 0
    END
FROM [SectionOutsource] so
INNER JOIN [ProcessGroup] pg ON so.ProcessGroupId = pg.Id;
GO

DECLARE @var43 sysname;
SELECT @var43 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutsourceRecovery]') AND [c].[name] = N'IsQualified');
IF @var43 IS NOT NULL EXEC(N'ALTER TABLE [OutsourceRecovery] DROP CONSTRAINT [' + @var43 + '];');
ALTER TABLE [OutsourceRecovery] DROP COLUMN [IsQualified];
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260511171630_FixProductionRecordAndSectionOutsourceSequenceNumber', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var44 sysname;
SELECT @var44 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'UpdatedBy');
IF @var44 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var44 + '];');
ALTER TABLE [WorkOrder] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var45 sysname;
SELECT @var45 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'CreatedBy');
IF @var45 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var45 + '];');
ALTER TABLE [WorkOrder] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var46 sysname;
SELECT @var46 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Warehouse]') AND [c].[name] = N'UpdatedBy');
IF @var46 IS NOT NULL EXEC(N'ALTER TABLE [Warehouse] DROP CONSTRAINT [' + @var46 + '];');
ALTER TABLE [Warehouse] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var47 sysname;
SELECT @var47 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Warehouse]') AND [c].[name] = N'CreatedBy');
IF @var47 IS NOT NULL EXEC(N'ALTER TABLE [Warehouse] DROP CONSTRAINT [' + @var47 + '];');
ALTER TABLE [Warehouse] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var48 sysname;
SELECT @var48 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierProfile]') AND [c].[name] = N'UpdatedBy');
IF @var48 IS NOT NULL EXEC(N'ALTER TABLE [SupplierProfile] DROP CONSTRAINT [' + @var48 + '];');
ALTER TABLE [SupplierProfile] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var49 sysname;
SELECT @var49 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierProfile]') AND [c].[name] = N'CreatedBy');
IF @var49 IS NOT NULL EXEC(N'ALTER TABLE [SupplierProfile] DROP CONSTRAINT [' + @var49 + '];');
ALTER TABLE [SupplierProfile] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var50 sysname;
SELECT @var50 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractReturnItem]') AND [c].[name] = N'UpdatedBy');
IF @var50 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractReturnItem] DROP CONSTRAINT [' + @var50 + '];');
ALTER TABLE [SubcontractReturnItem] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var51 sysname;
SELECT @var51 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractReturnItem]') AND [c].[name] = N'CreatedBy');
IF @var51 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractReturnItem] DROP CONSTRAINT [' + @var51 + '];');
ALTER TABLE [SubcontractReturnItem] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var52 sysname;
SELECT @var52 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractOrder]') AND [c].[name] = N'UpdatedBy');
IF @var52 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @var52 + '];');
ALTER TABLE [SubcontractOrder] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var53 sysname;
SELECT @var53 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractOrder]') AND [c].[name] = N'CreatedBy');
IF @var53 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @var53 + '];');
ALTER TABLE [SubcontractOrder] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var54 sysname;
SELECT @var54 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StandardGradeMapping]') AND [c].[name] = N'UpdatedBy');
IF @var54 IS NOT NULL EXEC(N'ALTER TABLE [StandardGradeMapping] DROP CONSTRAINT [' + @var54 + '];');
ALTER TABLE [StandardGradeMapping] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var55 sysname;
SELECT @var55 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StandardGradeMapping]') AND [c].[name] = N'CreatedBy');
IF @var55 IS NOT NULL EXEC(N'ALTER TABLE [StandardGradeMapping] DROP CONSTRAINT [' + @var55 + '];');
ALTER TABLE [StandardGradeMapping] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var56 sysname;
SELECT @var56 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SectionOutsource]') AND [c].[name] = N'UpdatedBy');
IF @var56 IS NOT NULL EXEC(N'ALTER TABLE [SectionOutsource] DROP CONSTRAINT [' + @var56 + '];');
ALTER TABLE [SectionOutsource] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var57 sysname;
SELECT @var57 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SectionOutsource]') AND [c].[name] = N'Status');
IF @var57 IS NOT NULL EXEC(N'ALTER TABLE [SectionOutsource] DROP CONSTRAINT [' + @var57 + '];');
ALTER TABLE [SectionOutsource] ADD DEFAULT N'PendingRecovery' FOR [Status];
GO

DECLARE @var58 sysname;
SELECT @var58 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SectionOutsource]') AND [c].[name] = N'CreatedBy');
IF @var58 IS NOT NULL EXEC(N'ALTER TABLE [SectionOutsource] DROP CONSTRAINT [' + @var58 + '];');
ALTER TABLE [SectionOutsource] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var59 sysname;
SELECT @var59 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesOrder]') AND [c].[name] = N'UpdatedBy');
IF @var59 IS NOT NULL EXEC(N'ALTER TABLE [SalesOrder] DROP CONSTRAINT [' + @var59 + '];');
ALTER TABLE [SalesOrder] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var60 sysname;
SELECT @var60 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesOrder]') AND [c].[name] = N'CreatedBy');
IF @var60 IS NOT NULL EXEC(N'ALTER TABLE [SalesOrder] DROP CONSTRAINT [' + @var60 + '];');
ALTER TABLE [SalesOrder] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var61 sysname;
SELECT @var61 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RefreshToken]') AND [c].[name] = N'UpdatedBy');
IF @var61 IS NOT NULL EXEC(N'ALTER TABLE [RefreshToken] DROP CONSTRAINT [' + @var61 + '];');
ALTER TABLE [RefreshToken] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var62 sysname;
SELECT @var62 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RefreshToken]') AND [c].[name] = N'CreatedBy');
IF @var62 IS NOT NULL EXEC(N'ALTER TABLE [RefreshToken] DROP CONSTRAINT [' + @var62 + '];');
ALTER TABLE [RefreshToken] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var63 sysname;
SELECT @var63 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'UpdatedBy');
IF @var63 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var63 + '];');
ALTER TABLE [PurchaseSemiPlan] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var64 sysname;
SELECT @var64 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'CreatedBy');
IF @var64 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var64 + '];');
ALTER TABLE [PurchaseSemiPlan] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var65 sysname;
SELECT @var65 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'UpdatedBy');
IF @var65 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var65 + '];');
ALTER TABLE [PurchaseOrder] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var66 sysname;
SELECT @var66 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'CreatedBy');
IF @var66 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var66 + '];');
ALTER TABLE [PurchaseOrder] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var67 sysname;
SELECT @var67 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseFinishedPlan]') AND [c].[name] = N'UpdatedBy');
IF @var67 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseFinishedPlan] DROP CONSTRAINT [' + @var67 + '];');
ALTER TABLE [PurchaseFinishedPlan] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var68 sysname;
SELECT @var68 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseFinishedPlan]') AND [c].[name] = N'CreatedBy');
IF @var68 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseFinishedPlan] DROP CONSTRAINT [' + @var68 + '];');
ALTER TABLE [PurchaseFinishedPlan] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var69 sysname;
SELECT @var69 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductRequirement]') AND [c].[name] = N'UpdatedBy');
IF @var69 IS NOT NULL EXEC(N'ALTER TABLE [ProductRequirement] DROP CONSTRAINT [' + @var69 + '];');
ALTER TABLE [ProductRequirement] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var70 sysname;
SELECT @var70 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductRequirement]') AND [c].[name] = N'CreatedBy');
IF @var70 IS NOT NULL EXEC(N'ALTER TABLE [ProductRequirement] DROP CONSTRAINT [' + @var70 + '];');
ALTER TABLE [ProductRequirement] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var71 sysname;
SELECT @var71 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionStandard]') AND [c].[name] = N'UpdatedBy');
IF @var71 IS NOT NULL EXEC(N'ALTER TABLE [ProductionStandard] DROP CONSTRAINT [' + @var71 + '];');
ALTER TABLE [ProductionStandard] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var72 sysname;
SELECT @var72 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionStandard]') AND [c].[name] = N'CreatedBy');
IF @var72 IS NOT NULL EXEC(N'ALTER TABLE [ProductionStandard] DROP CONSTRAINT [' + @var72 + '];');
ALTER TABLE [ProductionStandard] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var73 sysname;
SELECT @var73 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionRecord]') AND [c].[name] = N'UpdatedBy');
IF @var73 IS NOT NULL EXEC(N'ALTER TABLE [ProductionRecord] DROP CONSTRAINT [' + @var73 + '];');
ALTER TABLE [ProductionRecord] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var74 sysname;
SELECT @var74 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionRecord]') AND [c].[name] = N'CreatedBy');
IF @var74 IS NOT NULL EXEC(N'ALTER TABLE [ProductionRecord] DROP CONSTRAINT [' + @var74 + '];');
ALTER TABLE [ProductionRecord] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var75 sysname;
SELECT @var75 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'UpdatedBy');
IF @var75 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var75 + '];');
ALTER TABLE [ProductionBatch] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var76 sysname;
SELECT @var76 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'CreatedBy');
IF @var76 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var76 + '];');
ALTER TABLE [ProductionBatch] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var77 sysname;
SELECT @var77 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessGroup]') AND [c].[name] = N'UpdatedBy');
IF @var77 IS NOT NULL EXEC(N'ALTER TABLE [ProcessGroup] DROP CONSTRAINT [' + @var77 + '];');
ALTER TABLE [ProcessGroup] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var78 sysname;
SELECT @var78 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessGroup]') AND [c].[name] = N'CreatedBy');
IF @var78 IS NOT NULL EXEC(N'ALTER TABLE [ProcessGroup] DROP CONSTRAINT [' + @var78 + '];');
ALTER TABLE [ProcessGroup] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var79 sysname;
SELECT @var79 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutsourceRecovery]') AND [c].[name] = N'UpdatedBy');
IF @var79 IS NOT NULL EXEC(N'ALTER TABLE [OutsourceRecovery] DROP CONSTRAINT [' + @var79 + '];');
ALTER TABLE [OutsourceRecovery] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var80 sysname;
SELECT @var80 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutsourceRecovery]') AND [c].[name] = N'CreatedBy');
IF @var80 IS NOT NULL EXEC(N'ALTER TABLE [OutsourceRecovery] DROP CONSTRAINT [' + @var80 + '];');
ALTER TABLE [OutsourceRecovery] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var81 sysname;
SELECT @var81 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItem]') AND [c].[name] = N'UpdatedBy');
IF @var81 IS NOT NULL EXEC(N'ALTER TABLE [OrderItem] DROP CONSTRAINT [' + @var81 + '];');
ALTER TABLE [OrderItem] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var82 sysname;
SELECT @var82 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItem]') AND [c].[name] = N'CreatedBy');
IF @var82 IS NOT NULL EXEC(N'ALTER TABLE [OrderItem] DROP CONSTRAINT [' + @var82 + '];');
ALTER TABLE [OrderItem] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var83 sysname;
SELECT @var83 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderChangeNotification]') AND [c].[name] = N'UpdatedBy');
IF @var83 IS NOT NULL EXEC(N'ALTER TABLE [OrderChangeNotification] DROP CONSTRAINT [' + @var83 + '];');
ALTER TABLE [OrderChangeNotification] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var84 sysname;
SELECT @var84 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderChangeNotification]') AND [c].[name] = N'CreatedBy');
IF @var84 IS NOT NULL EXEC(N'ALTER TABLE [OrderChangeNotification] DROP CONSTRAINT [' + @var84 + '];');
ALTER TABLE [OrderChangeNotification] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var85 sysname;
SELECT @var85 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaterialReceiveCheck]') AND [c].[name] = N'UpdatedBy');
IF @var85 IS NOT NULL EXEC(N'ALTER TABLE [MaterialReceiveCheck] DROP CONSTRAINT [' + @var85 + '];');
ALTER TABLE [MaterialReceiveCheck] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var86 sysname;
SELECT @var86 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaterialReceiveCheck]') AND [c].[name] = N'CreatedBy');
IF @var86 IS NOT NULL EXEC(N'ALTER TABLE [MaterialReceiveCheck] DROP CONSTRAINT [' + @var86 + '];');
ALTER TABLE [MaterialReceiveCheck] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var87 sysname;
SELECT @var87 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Material]') AND [c].[name] = N'UpdatedBy');
IF @var87 IS NOT NULL EXEC(N'ALTER TABLE [Material] DROP CONSTRAINT [' + @var87 + '];');
ALTER TABLE [Material] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var88 sysname;
SELECT @var88 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Material]') AND [c].[name] = N'CreatedBy');
IF @var88 IS NOT NULL EXEC(N'ALTER TABLE [Material] DROP CONSTRAINT [' + @var88 + '];');
ALTER TABLE [Material] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var89 sysname;
SELECT @var89 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryPlan]') AND [c].[name] = N'UpdatedBy');
IF @var89 IS NOT NULL EXEC(N'ALTER TABLE [InventoryPlan] DROP CONSTRAINT [' + @var89 + '];');
ALTER TABLE [InventoryPlan] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var90 sysname;
SELECT @var90 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryPlan]') AND [c].[name] = N'CreatedBy');
IF @var90 IS NOT NULL EXEC(N'ALTER TABLE [InventoryPlan] DROP CONSTRAINT [' + @var90 + '];');
ALTER TABLE [InventoryPlan] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var91 sysname;
SELECT @var91 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'UpdatedBy');
IF @var91 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var91 + '];');
ALTER TABLE [InventoryBatch] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var92 sysname;
SELECT @var92 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'CreatedBy');
IF @var92 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var92 + '];');
ALTER TABLE [InventoryBatch] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var93 sysname;
SELECT @var93 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfile]') AND [c].[name] = N'UpdatedBy');
IF @var93 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfile] DROP CONSTRAINT [' + @var93 + '];');
ALTER TABLE [CustomerProfile] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
GO

DECLARE @var94 sysname;
SELECT @var94 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfile]') AND [c].[name] = N'CreatedBy');
IF @var94 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfile] DROP CONSTRAINT [' + @var94 + '];');
ALTER TABLE [CustomerProfile] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
GO

ALTER TABLE [InventoryBatch] ADD CONSTRAINT [FK_InventoryBatch_Warehouse_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouse] ([Id]);
GO

ALTER TABLE [OutboundRecord] ADD CONSTRAINT [FK_OutboundRecord_InventoryBatch_InventoryBatchId] FOREIGN KEY ([InventoryBatchId]) REFERENCES [InventoryBatch] ([Id]);
GO

DELETE FROM PurchaseSemiPlan WHERE WorkOrderId NOT IN (SELECT Id FROM WorkOrder)
GO

DELETE FROM PurchaseFinishedPlan WHERE WorkOrderId NOT IN (SELECT Id FROM WorkOrder)
GO

ALTER TABLE [PurchaseFinishedPlan] ADD CONSTRAINT [FK_PurchaseFinishedPlan_WorkOrder_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrder] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [PurchaseOrder] ADD CONSTRAINT [FK_PurchaseOrder_SupplierProfile_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [SupplierProfile] ([Id]);
GO

ALTER TABLE [PurchaseSemiPlan] ADD CONSTRAINT [FK_PurchaseSemiPlan_WorkOrder_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrder] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [SubcontractOrder] ADD CONSTRAINT [FK_SubcontractOrder_SupplierProfile_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [SupplierProfile] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260511192139_AddMissingForeignKeys', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

UPDATE SectionOutsource SET Status = 'InProgress' WHERE Status = N'在轧'
GO

UPDATE SectionOutsource SET Status = 'PendingRecovery' WHERE Status = N'待回收'
GO

UPDATE SectionOutsource SET Status = 'Recovered' WHERE Status = N'已回收'
GO

UPDATE SectionOutsource SET Status = 'PendingRecovery' WHERE Status NOT IN ('PendingRecovery', 'Recovered', 'InProgress')
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260511195143_FixSectionOutsourceStatusInProgress', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [BatchOperationLog] (
    [Id] int NOT NULL IDENTITY,
    [ProductionBatchId] int NOT NULL,
    [OperationType] nvarchar(20) NOT NULL,
    [Detail] nvarchar(2000) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_BatchOperationLog] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BatchOperationLog_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_BatchOperationLog_BatchId] ON [BatchOperationLog] ([ProductionBatchId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260512171128_AddBatchOperationLog', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProductionBatch] ADD [CurrentValidQty] int NULL;
GO

ALTER TABLE [ProductionBatch] ADD [CurrentValidWeight] decimal(18,3) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260513174640_AddCurrentValidFieldsAndCancelledStatus', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ProcessInspection] (
    [Id] int NOT NULL IDENTITY,
    [ProductionBatchId] int NOT NULL,
    [ProcessGroupId] int NOT NULL,
    [ProcessName] nvarchar(50) NOT NULL,
    [ManufacturingSpec] nvarchar(100) NULL,
    [SectionName] nvarchar(50) NOT NULL,
    [SequenceNumber] int NOT NULL,
    [InspectionDate] datetime2 NOT NULL,
    [EquipmentName] nvarchar(100) NULL,
    [Inspector] nvarchar(50) NULL,
    [Shift] nvarchar(10) NULL,
    [Quantity] int NULL,
    [Weight] decimal(18,3) NULL,
    [InspectionItem] nvarchar(100) NULL,
    [QualifiedQuantity] int NULL,
    [QualifiedWeight] decimal(18,3) NULL,
    [DefectReworkQuantity] int NULL,
    [DefectWarehouseQuantity] int NULL,
    [DefectScrapQuantity] int NULL,
    [DefectDescription] nvarchar(500) NULL,
    [SourceUnit] nvarchar(200) NULL,
    [TagNo] nvarchar(50) NULL,
    [PlantGrade] nvarchar(50) NULL,
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_ProcessInspection] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProcessInspection_ProcessGroup_ProcessGroupId] FOREIGN KEY ([ProcessGroupId]) REFERENCES [ProcessGroup] ([Id]),
    CONSTRAINT [FK_ProcessInspection_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_ProcessInspection_BatchId] ON [ProcessInspection] ([ProductionBatchId]);
GO

CREATE INDEX [IX_ProcessInspection_ProcessGroupId] ON [ProcessInspection] ([ProcessGroupId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260513201920_AddProcessInspection', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var95 sysname;
SELECT @var95 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionRecord]') AND [c].[name] = N'DefectQuantity');
IF @var95 IS NOT NULL EXEC(N'ALTER TABLE [ProductionRecord] DROP CONSTRAINT [' + @var95 + '];');
ALTER TABLE [ProductionRecord] DROP COLUMN [DefectQuantity];
GO

DECLARE @var96 sysname;
SELECT @var96 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionRecord]') AND [c].[name] = N'DefectWeight');
IF @var96 IS NOT NULL EXEC(N'ALTER TABLE [ProductionRecord] DROP CONSTRAINT [' + @var96 + '];');
ALTER TABLE [ProductionRecord] DROP COLUMN [DefectWeight];
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260513211449_RemoveDefectQuantityDefectWeight', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ChemicalComposition] (
    [Id] int NOT NULL IDENTITY,
    [PlantGrade] nvarchar(50) NOT NULL,
    [Carbon] nvarchar(100) NULL,
    [Silicon] nvarchar(100) NULL,
    [Manganese] nvarchar(100) NULL,
    [Phosphorus] nvarchar(100) NULL,
    [Sulfur] nvarchar(100) NULL,
    [Nickel] nvarchar(100) NULL,
    [Chromium] nvarchar(100) NULL,
    [Molybdenum] nvarchar(100) NULL,
    [Copper] nvarchar(100) NULL,
    [Nitrogen] nvarchar(100) NULL,
    [Niobium] nvarchar(100) NULL,
    [Titanium] nvarchar(100) NULL,
    [Iron] nvarchar(100) NULL,
    [Aluminum] nvarchar(100) NULL,
    [Tungsten] nvarchar(100) NULL,
    [PREN] nvarchar(100) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_ChemicalComposition] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [UK_ChemicalComposition_PlantGrade] ON [ChemicalComposition] ([PlantGrade]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260514084213_AddChemicalComposition', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [FurnaceRegistration] (
    [Id] int NOT NULL IDENTITY,
    [IncomingDate] date NOT NULL,
    [RawMaterialUnit] nvarchar(100) NOT NULL,
    [RawMaterialType] nvarchar(50) NOT NULL,
    [RegisteredGrade] nvarchar(100) NOT NULL,
    [RelatedPlantGrade] nvarchar(100) NULL,
    [FurnaceNumber] nvarchar(100) NOT NULL,
    [Specification] nvarchar(100) NULL,
    [Quantity] int NULL,
    [Weight] decimal(18,3) NULL,
    [Carbon] decimal(18,6) NULL,
    [Silicon] decimal(18,6) NULL,
    [Manganese] decimal(18,6) NULL,
    [Phosphorus] decimal(18,6) NULL,
    [Sulfur] decimal(18,6) NULL,
    [Nickel] decimal(18,6) NULL,
    [Chromium] decimal(18,6) NULL,
    [Molybdenum] decimal(18,6) NULL,
    [Copper] decimal(18,6) NULL,
    [Nitrogen] decimal(18,6) NULL,
    [Niobium] decimal(18,6) NULL,
    [Titanium] decimal(18,6) NULL,
    [Iron] decimal(18,6) NULL,
    [Aluminum] decimal(18,6) NULL,
    [Tungsten] decimal(18,6) NULL,
    [PREN] decimal(18,6) NULL,
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_FurnaceRegistration] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [UK_FurnaceRegistration_FurnaceNumber] ON [FurnaceRegistration] ([FurnaceNumber]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260514104815_AddFurnaceRegistration', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var97 sysname;
SELECT @var97 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Tungsten');
IF @var97 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var97 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Tungsten] decimal(18,3) NULL;
GO

DECLARE @var98 sysname;
SELECT @var98 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Titanium');
IF @var98 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var98 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Titanium] decimal(18,3) NULL;
GO

DECLARE @var99 sysname;
SELECT @var99 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Sulfur');
IF @var99 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var99 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Sulfur] decimal(18,3) NULL;
GO

DECLARE @var100 sysname;
SELECT @var100 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Silicon');
IF @var100 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var100 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Silicon] decimal(18,3) NULL;
GO

DECLARE @var101 sysname;
SELECT @var101 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Phosphorus');
IF @var101 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var101 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Phosphorus] decimal(18,3) NULL;
GO

DECLARE @var102 sysname;
SELECT @var102 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Nitrogen');
IF @var102 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var102 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Nitrogen] decimal(18,3) NULL;
GO

DECLARE @var103 sysname;
SELECT @var103 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Niobium');
IF @var103 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var103 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Niobium] decimal(18,3) NULL;
GO

DECLARE @var104 sysname;
SELECT @var104 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Nickel');
IF @var104 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var104 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Nickel] decimal(18,3) NULL;
GO

DECLARE @var105 sysname;
SELECT @var105 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Molybdenum');
IF @var105 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var105 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Molybdenum] decimal(18,3) NULL;
GO

DECLARE @var106 sysname;
SELECT @var106 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Manganese');
IF @var106 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var106 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Manganese] decimal(18,3) NULL;
GO

DECLARE @var107 sysname;
SELECT @var107 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Iron');
IF @var107 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var107 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Iron] decimal(18,3) NULL;
GO

DECLARE @var108 sysname;
SELECT @var108 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Copper');
IF @var108 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var108 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Copper] decimal(18,3) NULL;
GO

DECLARE @var109 sysname;
SELECT @var109 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Chromium');
IF @var109 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var109 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Chromium] decimal(18,3) NULL;
GO

DECLARE @var110 sysname;
SELECT @var110 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Carbon');
IF @var110 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var110 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Carbon] decimal(18,3) NULL;
GO

DECLARE @var111 sysname;
SELECT @var111 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Aluminum');
IF @var111 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var111 + '];');
ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Aluminum] decimal(18,3) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260514111304_ChangeChemicalElementPrecisionTo3', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [UK_FurnaceRegistration_FurnaceNumber] ON [FurnaceRegistration];
GO

CREATE INDEX [IX_FurnaceRegistration_FurnaceNumber] ON [FurnaceRegistration] ([FurnaceNumber]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260514112147_RemoveFurnaceNumberUniqueIndex', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ChemicalValidationRule] (
    [Id] int NOT NULL IDENTITY,
    [PlantGrade] nvarchar(100) NOT NULL,
    [CMin] nvarchar(50) NULL,
    [CMax] nvarchar(50) NULL,
    [SiMin] nvarchar(50) NULL,
    [SiMax] nvarchar(50) NULL,
    [MnMin] nvarchar(50) NULL,
    [MnMax] nvarchar(50) NULL,
    [PMin] nvarchar(50) NULL,
    [PMax] nvarchar(50) NULL,
    [SMin] nvarchar(50) NULL,
    [SMax] nvarchar(50) NULL,
    [NiMin] nvarchar(50) NULL,
    [NiMax] nvarchar(50) NULL,
    [CrMin] nvarchar(50) NULL,
    [CrMax] nvarchar(50) NULL,
    [MoMin] nvarchar(50) NULL,
    [MoMax] nvarchar(50) NULL,
    [CuMin] nvarchar(50) NULL,
    [CuMax] nvarchar(50) NULL,
    [NMin] nvarchar(50) NULL,
    [NMax] nvarchar(50) NULL,
    [NbMin] nvarchar(50) NULL,
    [NbMax] nvarchar(50) NULL,
    [TiMin] nvarchar(50) NULL,
    [TiMax] nvarchar(50) NULL,
    [FeMin] nvarchar(50) NULL,
    [FeMax] nvarchar(50) NULL,
    [AlMin] nvarchar(50) NULL,
    [AlMax] nvarchar(50) NULL,
    [WMin] nvarchar(50) NULL,
    [WMax] nvarchar(50) NULL,
    [PRENMin] nvarchar(50) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_ChemicalValidationRule] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [UK_ChemicalValidationRule_PlantGrade] ON [ChemicalValidationRule] ([PlantGrade]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260514120447_AddChemicalValidationRule', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [FinalInspection] (
    [Id] int NOT NULL IDENTITY,
    [InspectionItem] nvarchar(20) NOT NULL,
    [InspectionDate] datetime2 NOT NULL,
    [BatchNo] nvarchar(50) NOT NULL,
    [ProductionBatchId] int NOT NULL,
    [MaterialName] nvarchar(50) NULL,
    [TagNo] nvarchar(50) NULL,
    [WorkOrderNo] nvarchar(50) NULL,
    [SalesOrderNo] nvarchar(50) NULL,
    [SourceUnit] nvarchar(200) NULL,
    [FurnaceNo] nvarchar(50) NULL,
    [PlantGrade] nvarchar(50) NULL,
    [Specification] nvarchar(100) NULL,
    [FixedLength] nvarchar(50) NULL,
    [EquipmentName] nvarchar(100) NULL,
    [Shift] nvarchar(10) NULL,
    [Operator] nvarchar(50) NULL,
    [Quantity] int NULL,
    [Weight] decimal(18,3) NULL,
    [QualifiedQuantity] int NULL,
    [QualifiedWeight] decimal(18,3) NULL,
    [DefectReworkQuantity] int NULL,
    [DefectWarehouseQuantity] int NULL,
    [DefectScrapQuantity] int NULL,
    [DefectDescription] nvarchar(500) NULL,
    [OuterDiameterRange] nvarchar(100) NULL,
    [WallThicknessRange] nvarchar(100) NULL,
    [LengthAllowanceRange] nvarchar(100) NULL,
    [Pressure] decimal(18,3) NULL,
    [HoldTime] int NULL,
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_FinalInspection] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FinalInspection_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_FinalInspection_BatchNo] ON [FinalInspection] ([BatchNo]);
GO

CREATE INDEX [IX_FinalInspection_InspectionDate] ON [FinalInspection] ([InspectionDate]);
GO

CREATE INDEX [IX_FinalInspection_InspectionItem] ON [FinalInspection] ([InspectionItem]);
GO

CREATE INDEX [IX_FinalInspection_ProductionBatchId] ON [FinalInspection] ([ProductionBatchId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260514163558_AddFinalInspection', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [RoundBarPiercingPlan] (
    [Id] int NOT NULL IDENTITY,
    [WorkOrderId] int NOT NULL,
    [PlanDate] date NOT NULL,
    [AdjustedWallThickness] decimal(18,3) NOT NULL,
    [YieldRate] decimal(5,2) NOT NULL,
    [InputMultiple] int NOT NULL DEFAULT 1,
    [QualifiedRate] decimal(5,2) NOT NULL,
    [Density] decimal(18,4) NULL,
    [UnitWeight] decimal(18,3) NULL,
    [RawUnitWeight] decimal(18,3) NULL,
    [PlantGrade] nvarchar(100) NOT NULL,
    [RawMaterialType] nvarchar(20) NOT NULL,
    [RoundBarSpec] nvarchar(100) NOT NULL,
    [PiercingSpec] nvarchar(100) NOT NULL,
    [RequiredUnitWeight] decimal(18,3) NULL,
    [RequiredPieces] int NULL,
    [RequiredWeight] decimal(18,3) NOT NULL,
    [RequiredDate] date NOT NULL,
    [ProcessPlan] nvarchar(max) NULL,
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_RoundBarPiercingPlan] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoundBarPiercingPlan_WorkOrder_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrder] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_RoundBarPiercingPlan_WorkOrderId] ON [RoundBarPiercingPlan] ([WorkOrderId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260514201533_AddRoundBarPiercingPlan', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Equipment] (
    [Id] int NOT NULL IDENTITY,
    [EquipmentCode] nvarchar(50) NOT NULL,
    [EquipmentName] nvarchar(200) NOT NULL,
    [ModelNumber] nvarchar(100) NULL,
    [TechnicalParams] nvarchar(500) NULL,
    [Manufacturer] nvarchar(200) NULL,
    [InstallationDate] date NULL,
    [Remark] nvarchar(500) NULL,
    [Location] nvarchar(100) NULL,
    [RelatedSection] nvarchar(100) NULL,
    [NeedInspection] bit NOT NULL DEFAULT CAST(0 AS bit),
    [InspectionPerson] nvarchar(50) NULL,
    [InspectionCycleDays] int NOT NULL DEFAULT 7,
    [LastInspectionDate] date NULL,
    [NextInspectionDate] date NULL,
    [InspectionStatus] nvarchar(20) NOT NULL DEFAULT N'Normal',
    [NeedMaintenance] bit NOT NULL DEFAULT CAST(0 AS bit),
    [MaintPerson] nvarchar(50) NULL,
    [MaintCycleDays] int NOT NULL DEFAULT 30,
    [LastMaintDate] date NULL,
    [NextMaintDate] date NULL,
    [MaintStatus] nvarchar(20) NOT NULL DEFAULT N'Normal',
    [LifecycleStatus] nvarchar(20) NOT NULL DEFAULT N'Active',
    [UsageType] nvarchar(20) NOT NULL DEFAULT N'Primary',
    [RunningStatus] nvarchar(20) NOT NULL DEFAULT N'Normal',
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Equipment] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [InspectionRecord] (
    [Id] int NOT NULL IDENTITY,
    [RecordNo] nvarchar(50) NOT NULL,
    [EquipmentId] int NOT NULL,
    [ScheduledDate] date NOT NULL,
    [ActualDate] date NULL,
    [Inspector] nvarchar(50) NULL,
    [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
    [ChecklistResults] nvarchar(max) NULL,
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_InspectionRecord] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionRecord_Equipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceOrder] (
    [Id] int NOT NULL IDENTITY,
    [MaintOrderNo] nvarchar(50) NOT NULL,
    [EquipmentId] int NOT NULL,
    [MaintType] nvarchar(20) NOT NULL DEFAULT N'Monthly',
    [ScheduledDate] date NOT NULL,
    [ActualDate] date NULL,
    [Executor] nvarchar(50) NULL,
    [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
    [ChecklistResults] nvarchar(max) NULL,
    [Remark] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_MaintenanceOrder] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceOrder_Equipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [RepairOrder] (
    [Id] int NOT NULL IDENTITY,
    [RepairOrderNo] nvarchar(50) NOT NULL,
    [EquipmentId] int NOT NULL,
    [FaultDescription] nvarchar(500) NOT NULL,
    [FaultType] nvarchar(50) NULL,
    [Priority] nvarchar(20) NOT NULL DEFAULT N'Normal',
    [RepairStatus] nvarchar(20) NOT NULL DEFAULT N'Pending',
    [ReportPerson] nvarchar(50) NOT NULL,
    [ReportTime] datetime2 NOT NULL,
    [RepairPerson] nvarchar(50) NULL,
    [RepairStartTime] datetime2 NULL,
    [RepairEndTime] datetime2 NULL,
    [RepairContent] nvarchar(1000) NULL,
    [SparePartUsed] nvarchar(500) NULL,
    [DowntimeHours] decimal(18,2) NULL,
    [VerifyPerson] nvarchar(50) NULL,
    [VerifyTime] datetime2 NULL,
    [VerifyComment] nvarchar(500) NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_RepairOrder] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RepairOrder_Equipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_Equipment_InspectionStatus] ON [Equipment] ([InspectionStatus]);
GO

CREATE INDEX [IX_Equipment_LifecycleStatus] ON [Equipment] ([LifecycleStatus]);
GO

CREATE INDEX [IX_Equipment_Location] ON [Equipment] ([Location]);
GO

CREATE INDEX [IX_Equipment_MaintStatus] ON [Equipment] ([MaintStatus]);
GO

CREATE INDEX [IX_Equipment_Name] ON [Equipment] ([EquipmentName]);
GO

CREATE INDEX [IX_Equipment_NeedInspection] ON [Equipment] ([NeedInspection]);
GO

CREATE INDEX [IX_Equipment_NeedMaintenance] ON [Equipment] ([NeedMaintenance]);
GO

CREATE INDEX [IX_Equipment_RelatedSection] ON [Equipment] ([RelatedSection]);
GO

CREATE INDEX [IX_Equipment_RunningStatus] ON [Equipment] ([RunningStatus]);
GO

CREATE UNIQUE INDEX [UK_Equipment_Code] ON [Equipment] ([EquipmentCode]);
GO

CREATE INDEX [IX_InspectionRecord_EquipmentId] ON [InspectionRecord] ([EquipmentId]);
GO

CREATE INDEX [IX_InspectionRecord_ScheduledDate] ON [InspectionRecord] ([ScheduledDate]);
GO

CREATE INDEX [IX_InspectionRecord_Status] ON [InspectionRecord] ([Status]);
GO

CREATE UNIQUE INDEX [UK_InspectionRecord_No] ON [InspectionRecord] ([RecordNo]);
GO

CREATE INDEX [IX_MaintenanceOrder_EquipmentId] ON [MaintenanceOrder] ([EquipmentId]);
GO

CREATE INDEX [IX_MaintenanceOrder_ScheduledDate] ON [MaintenanceOrder] ([ScheduledDate]);
GO

CREATE INDEX [IX_MaintenanceOrder_Status] ON [MaintenanceOrder] ([Status]);
GO

CREATE UNIQUE INDEX [UK_MaintenanceOrder_No] ON [MaintenanceOrder] ([MaintOrderNo]);
GO

CREATE INDEX [IX_RepairOrder_EquipmentId] ON [RepairOrder] ([EquipmentId]);
GO

CREATE INDEX [IX_RepairOrder_ReportTime] ON [RepairOrder] ([ReportTime]);
GO

CREATE INDEX [IX_RepairOrder_Status] ON [RepairOrder] ([RepairStatus]);
GO

CREATE UNIQUE INDEX [UK_RepairOrder_No] ON [RepairOrder] ([RepairOrderNo]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260515190813_AddEquipmentContext', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [IX_Equipment_InspectionStatus] ON [Equipment];
GO

DROP INDEX [IX_Equipment_MaintStatus] ON [Equipment];
GO

DROP INDEX [IX_Equipment_RunningStatus] ON [Equipment];
GO

DECLARE @var112 sysname;
SELECT @var112 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Equipment]') AND [c].[name] = N'InspectionStatus');
IF @var112 IS NOT NULL EXEC(N'ALTER TABLE [Equipment] DROP CONSTRAINT [' + @var112 + '];');
ALTER TABLE [Equipment] DROP COLUMN [InspectionStatus];
GO

DECLARE @var113 sysname;
SELECT @var113 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Equipment]') AND [c].[name] = N'MaintStatus');
IF @var113 IS NOT NULL EXEC(N'ALTER TABLE [Equipment] DROP CONSTRAINT [' + @var113 + '];');
ALTER TABLE [Equipment] DROP COLUMN [MaintStatus];
GO

DECLARE @var114 sysname;
SELECT @var114 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Equipment]') AND [c].[name] = N'RunningStatus');
IF @var114 IS NOT NULL EXEC(N'ALTER TABLE [Equipment] DROP CONSTRAINT [' + @var114 + '];');
ALTER TABLE [Equipment] DROP COLUMN [RunningStatus];
GO

EXEC sp_rename N'[Equipment].[NextMaintDate]', N'CurrentMaintStartDate', N'COLUMN';
GO

EXEC sp_rename N'[Equipment].[NextInspectionDate]', N'CurrentInspectionStartDate', N'COLUMN';
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260515221728_RefactorEquipmentContext', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [IX_MaintenanceOrder_ScheduledDate] ON [MaintenanceOrder];
GO

DROP INDEX [IX_MaintenanceOrder_Status] ON [MaintenanceOrder];
GO

DROP INDEX [IX_InspectionRecord_ScheduledDate] ON [InspectionRecord];
GO

DROP INDEX [IX_InspectionRecord_Status] ON [InspectionRecord];
GO

DECLARE @var115 sysname;
SELECT @var115 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RepairOrder]') AND [c].[name] = N'DowntimeHours');
IF @var115 IS NOT NULL EXEC(N'ALTER TABLE [RepairOrder] DROP CONSTRAINT [' + @var115 + '];');
ALTER TABLE [RepairOrder] DROP COLUMN [DowntimeHours];
GO

DECLARE @var116 sysname;
SELECT @var116 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RepairOrder]') AND [c].[name] = N'VerifyComment');
IF @var116 IS NOT NULL EXEC(N'ALTER TABLE [RepairOrder] DROP CONSTRAINT [' + @var116 + '];');
ALTER TABLE [RepairOrder] DROP COLUMN [VerifyComment];
GO

DECLARE @var117 sysname;
SELECT @var117 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RepairOrder]') AND [c].[name] = N'VerifyPerson');
IF @var117 IS NOT NULL EXEC(N'ALTER TABLE [RepairOrder] DROP CONSTRAINT [' + @var117 + '];');
ALTER TABLE [RepairOrder] DROP COLUMN [VerifyPerson];
GO

DECLARE @var118 sysname;
SELECT @var118 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RepairOrder]') AND [c].[name] = N'VerifyTime');
IF @var118 IS NOT NULL EXEC(N'ALTER TABLE [RepairOrder] DROP CONSTRAINT [' + @var118 + '];');
ALTER TABLE [RepairOrder] DROP COLUMN [VerifyTime];
GO

DECLARE @var119 sysname;
SELECT @var119 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceOrder]') AND [c].[name] = N'ChecklistResults');
IF @var119 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceOrder] DROP CONSTRAINT [' + @var119 + '];');
ALTER TABLE [MaintenanceOrder] DROP COLUMN [ChecklistResults];
GO

DECLARE @var120 sysname;
SELECT @var120 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceOrder]') AND [c].[name] = N'MaintType');
IF @var120 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceOrder] DROP CONSTRAINT [' + @var120 + '];');
ALTER TABLE [MaintenanceOrder] DROP COLUMN [MaintType];
GO

DECLARE @var121 sysname;
SELECT @var121 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceOrder]') AND [c].[name] = N'ScheduledDate');
IF @var121 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceOrder] DROP CONSTRAINT [' + @var121 + '];');
ALTER TABLE [MaintenanceOrder] DROP COLUMN [ScheduledDate];
GO

DECLARE @var122 sysname;
SELECT @var122 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceOrder]') AND [c].[name] = N'Status');
IF @var122 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceOrder] DROP CONSTRAINT [' + @var122 + '];');
ALTER TABLE [MaintenanceOrder] DROP COLUMN [Status];
GO

DECLARE @var123 sysname;
SELECT @var123 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InspectionRecord]') AND [c].[name] = N'ChecklistResults');
IF @var123 IS NOT NULL EXEC(N'ALTER TABLE [InspectionRecord] DROP CONSTRAINT [' + @var123 + '];');
ALTER TABLE [InspectionRecord] DROP COLUMN [ChecklistResults];
GO

DECLARE @var124 sysname;
SELECT @var124 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InspectionRecord]') AND [c].[name] = N'ScheduledDate');
IF @var124 IS NOT NULL EXEC(N'ALTER TABLE [InspectionRecord] DROP CONSTRAINT [' + @var124 + '];');
ALTER TABLE [InspectionRecord] DROP COLUMN [ScheduledDate];
GO

DECLARE @var125 sysname;
SELECT @var125 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InspectionRecord]') AND [c].[name] = N'Status');
IF @var125 IS NOT NULL EXEC(N'ALTER TABLE [InspectionRecord] DROP CONSTRAINT [' + @var125 + '];');
ALTER TABLE [InspectionRecord] DROP COLUMN [Status];
GO

ALTER TABLE [Equipment] ADD [LastRepairDate] date NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260515230645_SimplifyEquipmentContext', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [MaintenanceOrder] ADD [ExecutionSummary] nvarchar(500) NULL;
GO

ALTER TABLE [InspectionRecord] ADD [ExecutionSummary] nvarchar(500) NULL;
GO

DROP INDEX [IX_Equipment_Location] ON [Equipment];
DECLARE @var126 sysname;
SELECT @var126 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Equipment]') AND [c].[name] = N'Location');
IF @var126 IS NOT NULL EXEC(N'ALTER TABLE [Equipment] DROP CONSTRAINT [' + @var126 + '];');
UPDATE [Equipment] SET [Location] = N'' WHERE [Location] IS NULL;
ALTER TABLE [Equipment] ALTER COLUMN [Location] nvarchar(100) NOT NULL;
ALTER TABLE [Equipment] ADD DEFAULT N'' FOR [Location];
CREATE INDEX [IX_Equipment_Location] ON [Equipment] ([Location]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260515235429_AddExecutionSummaryFields', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [WorkOrderExecutionSummary] (
    [Id] int NOT NULL IDENTITY,
    [WorkOrderId] int NOT NULL,
    [WorkOrderNo] nvarchar(50) NOT NULL,
    [Salesman] nvarchar(50) NOT NULL,
    [CustomerName] nvarchar(200) NOT NULL,
    [SignDate] datetime2 NOT NULL,
    [DeliveryDate] datetime2 NOT NULL,
    [DelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
    [SettlementMethod] nvarchar(20) NOT NULL,
    [SalesOrderNo] nvarchar(50) NOT NULL,
    [ProductionMainNo] nvarchar(50) NOT NULL,
    [ProductionSubNo] nvarchar(50) NULL,
    [MaterialName] nvarchar(50) NOT NULL,
    [DeliveryState] nvarchar(50) NOT NULL,
    [PlantGrade] nvarchar(50) NOT NULL,
    [Specification] nvarchar(100) NOT NULL,
    [LengthStatus] nvarchar(20) NOT NULL,
    [MinLength] decimal(18,2) NULL,
    [MaxLength] decimal(18,2) NULL,
    [TotalItemCount] int NOT NULL DEFAULT 0,
    [TotalQuantity] int NOT NULL DEFAULT 0,
    [TotalMeters] decimal(18,2) NOT NULL DEFAULT 0.0,
    [TotalWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [LatestPlanDate] date NULL,
    [MaterialPlanRate] decimal(5,2) NOT NULL DEFAULT 0.0,
    [MaterialPlanStatus] int NOT NULL DEFAULT 0,
    [MainNoMaterialPlanRate] decimal(5,2) NOT NULL DEFAULT 0.0,
    [MainNoMaterialPlanStatus] int NOT NULL DEFAULT 0,
    [InputStartDate] date NULL,
    [InputEndDate] date NULL,
    [TotalBatchCount] int NOT NULL DEFAULT 0,
    [InputQuantity] int NOT NULL DEFAULT 0,
    [InputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [TheoreticalOutputQty] decimal(18,3) NOT NULL DEFAULT 0.0,
    [TheoreticalOutputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [InputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
    [InputStatus] int NOT NULL DEFAULT 0,
    [MainNoInputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
    [MainNoInputStatus] int NOT NULL DEFAULT 0,
    [ValidBatchCount] int NOT NULL DEFAULT 0,
    [ValidInputQuantity] int NOT NULL DEFAULT 0,
    [ValidInputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [ValidOutputQty] decimal(18,3) NOT NULL DEFAULT 0.0,
    [ValidOutputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [ValidInputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
    [ValidInputStatus] int NOT NULL DEFAULT 0,
    [LastRefreshTime] datetime2 NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_WorkOrderExecutionSummary] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_WES_InputStatus] ON [WorkOrderExecutionSummary] ([InputStatus]);
GO

CREATE INDEX [IX_WES_ProductionMainNo] ON [WorkOrderExecutionSummary] ([ProductionMainNo]);
GO

CREATE INDEX [IX_WES_SalesOrderNo] ON [WorkOrderExecutionSummary] ([SalesOrderNo]);
GO

CREATE INDEX [IX_WES_WorkOrderNo] ON [WorkOrderExecutionSummary] ([WorkOrderNo]);
GO

CREATE UNIQUE INDEX [UK_WES_WorkOrderId] ON [WorkOrderExecutionSummary] ([WorkOrderId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260516211553_AddWorkOrderExecutionSummary', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProductionBatch] ADD [ManufacturingItem] nvarchar(30) NOT NULL DEFAULT N'';
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260516223231_AddManufacturingItemToProductionBatch', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProductionRecord] ADD [DataSource] nvarchar(10) NULL DEFAULT N'MANUAL';
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260517011900_AddProductionRecordDataSource', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [WorkOrderExecutionSummary] ADD [MainNoValidInputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [WorkOrderExecutionSummary] ADD [MainNoValidInputStatus] int NOT NULL DEFAULT 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260517144122_AddMainNoValidInputOutputRatio', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProcessGroup] ADD [ManufacturingMultiple] int NOT NULL DEFAULT 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260517172335_AddProcessGroupManufacturingMultiple', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

UPDATE [ProductionBatch] SET [ProductionRatio] = 0 WHERE [ProductionRatio] IS NULL
GO

DECLARE @var127 sysname;
SELECT @var127 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'ProductionRatio');
IF @var127 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var127 + '];');
UPDATE [ProductionBatch] SET [ProductionRatio] = 0 WHERE [ProductionRatio] IS NULL;
ALTER TABLE [ProductionBatch] ALTER COLUMN [ProductionRatio] int NOT NULL;
ALTER TABLE [ProductionBatch] ADD DEFAULT 0 FOR [ProductionRatio];
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260517183955_MakeProductionRatioNonNullable', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [UK_ProductionRecord_Section] ON [ProductionRecord];
GO

CREATE INDEX [IX_ProductionRecord_Section] ON [ProductionRecord] ([ProductionBatchId], [ProcessGroupId], [SectionName]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260519195606_DropProductionRecordUK', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [UK_SectionOutsource_Section] ON [SectionOutsource];
GO

CREATE INDEX [IX_SectionOutsource_Section] ON [SectionOutsource] ([ProductionBatchId], [ProcessGroupId], [SectionName]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260519202738_DropSectionOutsourceUK', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProcessInspection] ADD [ConcessionRemark] nvarchar(max) NULL;
GO

ALTER TABLE [ProcessInspection] ADD [QualifiedConcessionQuantity] int NULL;
GO

ALTER TABLE [FinalInspection] ADD [ConcessionRemark] nvarchar(max) NULL;
GO

ALTER TABLE [FinalInspection] ADD [QualifiedConcessionQuantity] int NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260519234641_AddQualifiedConcessionFields', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [OrderListSummary] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [OrderNumber] nvarchar(50) NOT NULL,
    [SignDate] datetime2 NOT NULL,
    [CustomerName] nvarchar(200) NOT NULL,
    [Salesman] nvarchar(50) NOT NULL,
    [EndCustomer] nvarchar(200) NULL,
    [DeliveryStart] date NULL,
    [DeliveryEnd] date NULL,
    [HasDelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
    [TotalContractWeight] int NOT NULL DEFAULT 0,
    [ItemCount] int NOT NULL DEFAULT 0,
    [HasTechReqCount] int NOT NULL DEFAULT 0,
    [Status] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [LastChangeDate] datetime2 NULL,
    [FirstOrderItemId] int NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_OrderListSummary] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_OLS_CustomerName] ON [OrderListSummary] ([CustomerName]);
GO

CREATE INDEX [IX_OLS_DeliveryEnd] ON [OrderListSummary] ([DeliveryEnd]);
GO

CREATE INDEX [IX_OLS_OrderNumber] ON [OrderListSummary] ([OrderNumber]);
GO

CREATE INDEX [IX_OLS_SignDate] ON [OrderListSummary] ([SignDate]);
GO

CREATE INDEX [IX_OLS_Status] ON [OrderListSummary] ([Status]);
GO

CREATE UNIQUE INDEX [UK_OLS_OrderId] ON [OrderListSummary] ([OrderId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260521022534_AddOrderListSummary', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [WorkOrderListSummary] (
    [Id] int NOT NULL IDENTITY,
    [WorkOrderId] int NOT NULL,
    [WorkOrderNo] nvarchar(50) NOT NULL,
    [SalesOrderNo] nvarchar(50) NOT NULL,
    [ProductionMainNo] nvarchar(50) NOT NULL,
    [ProductionSubNo] nvarchar(50) NULL,
    [OrderItemIds] nvarchar(500) NULL,
    [SignDate] datetime2 NOT NULL,
    [Salesman] nvarchar(50) NOT NULL,
    [EndCustomer] nvarchar(200) NULL,
    [DeliveryDate] datetime2 NOT NULL,
    [DelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
    [SettlementMethod] nvarchar(20) NOT NULL,
    [MaterialName] nvarchar(20) NOT NULL,
    [StandardCode] nvarchar(100) NULL,
    [DeliveryState] nvarchar(50) NOT NULL,
    [PlantGrade] nvarchar(100) NOT NULL,
    [Specification] nvarchar(100) NOT NULL,
    [OuterDiameterNegative] decimal(18,2) NULL,
    [OuterDiameterPositive] decimal(18,2) NULL,
    [WallThicknessNegative] decimal(18,2) NULL,
    [WallThicknessPositive] decimal(18,2) NULL,
    [LengthStatus] nvarchar(20) NOT NULL,
    [MinLength] decimal(18,2) NULL,
    [MaxLength] decimal(18,2) NULL,
    [TotalQuantity] int NOT NULL DEFAULT 0,
    [TotalMeters] decimal(18,2) NOT NULL DEFAULT 0.0,
    [TotalWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [TotalItemCount] int NOT NULL DEFAULT 0,
    [ItemDetails] nvarchar(max) NULL,
    [TechnicalRequirements] nvarchar(20) NOT NULL DEFAULT N'Normal',
    [Status] int NOT NULL DEFAULT 0,
    [CreatedTime] datetimeoffset NOT NULL,
    [LatestPlanDate] date NULL,
    [MaterialPlanRate] decimal(5,2) NOT NULL DEFAULT 0.0,
    [MaterialPlanStatus] int NOT NULL DEFAULT 0,
    [SemiPlanTotalWeight] decimal(18,3) NULL,
    [SemiPlanTotalPieces] int NULL,
    [FinishedPlanTotalWeight] decimal(18,3) NULL,
    [FinishedPlanTotalPieces] int NULL,
    [InventoryPlanTotalWeight] decimal(18,3) NULL,
    [InventoryPlanTotalPieces] int NULL,
    [ReworkPlanTotalWeight] decimal(18,3) NULL,
    [ReworkPlanTotalPieces] int NULL,
    [PiercingPlanTotalWeight] decimal(18,3) NULL,
    [PiercingPlanTotalPieces] int NULL,
    [MainNoMaterialPlanRate] decimal(5,2) NOT NULL DEFAULT 0.0,
    [MainNoMaterialPlanStatus] int NOT NULL DEFAULT 0,
    [OrderMaterialPlanStatus] int NOT NULL DEFAULT 0,
    [RowVersion] rowversion NULL,
    [LastRefreshTime] datetime2 NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_WorkOrderListSummary] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [WorkOrderStatusSummary] (
    [Id] int NOT NULL IDENTITY,
    [SalesOrderId] int NOT NULL,
    [OrderNumber] nvarchar(50) NOT NULL,
    [SignDate] datetime2 NOT NULL,
    [CustomerName] nvarchar(200) NOT NULL,
    [Salesman] nvarchar(50) NOT NULL,
    [EndCustomer] nvarchar(200) NULL,
    [DeliveryStart] date NULL,
    [DeliveryEnd] date NULL,
    [HasDelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
    [TotalContractWeight] int NOT NULL DEFAULT 0,
    [ItemCount] int NOT NULL DEFAULT 0,
    [WorkOrderCount] int NOT NULL DEFAULT 0,
    [WorkOrderStatus] nvarchar(20) NOT NULL DEFAULT N'NotGenerated',
    [HasWorkOrder] bit NOT NULL DEFAULT CAST(0 AS bit),
    [WorkOrderId] int NULL,
    [RowVersion] rowversion NULL,
    [LastChangeDate] datetime2 NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_WorkOrderStatusSummary] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_WOLS_LatestPlanDate] ON [WorkOrderListSummary] ([LatestPlanDate]);
GO

CREATE INDEX [IX_WOLS_MainNoMaterialPlanStatus] ON [WorkOrderListSummary] ([MainNoMaterialPlanStatus]);
GO

CREATE INDEX [IX_WOLS_MaterialPlanStatus] ON [WorkOrderListSummary] ([MaterialPlanStatus]);
GO

CREATE INDEX [IX_WOLS_OrderMaterialPlanStatus] ON [WorkOrderListSummary] ([OrderMaterialPlanStatus]);
GO

CREATE INDEX [IX_WOLS_ProductionMainNo] ON [WorkOrderListSummary] ([ProductionMainNo]);
GO

CREATE INDEX [IX_WOLS_SalesOrderNo] ON [WorkOrderListSummary] ([SalesOrderNo]);
GO

CREATE INDEX [IX_WOLS_Status] ON [WorkOrderListSummary] ([Status]);
GO

CREATE INDEX [IX_WOLS_WorkOrderNo] ON [WorkOrderListSummary] ([WorkOrderNo]);
GO

CREATE UNIQUE INDEX [UK_WOLS_WorkOrderId] ON [WorkOrderListSummary] ([WorkOrderId]);
GO

CREATE INDEX [IX_WOSS_CustomerName] ON [WorkOrderStatusSummary] ([CustomerName]);
GO

CREATE INDEX [IX_WOSS_OrderNumber] ON [WorkOrderStatusSummary] ([OrderNumber]);
GO

CREATE INDEX [IX_WOSS_SignDate] ON [WorkOrderStatusSummary] ([SignDate]);
GO

CREATE INDEX [IX_WOSS_WorkOrderStatus] ON [WorkOrderStatusSummary] ([WorkOrderStatus]);
GO

CREATE UNIQUE INDEX [UK_WOSS_SalesOrderId] ON [WorkOrderStatusSummary] ([SalesOrderId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260521104953_AddWorkOrderReadModels', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var128 sysname;
SELECT @var128 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderListSummary]') AND [c].[name] = N'MaterialPlanRate');
IF @var128 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderListSummary] DROP CONSTRAINT [' + @var128 + '];');
ALTER TABLE [WorkOrderListSummary] ALTER COLUMN [MaterialPlanRate] decimal(7,2) NOT NULL;
ALTER TABLE [WorkOrderListSummary] ADD DEFAULT 0.0 FOR [MaterialPlanRate];
GO

DECLARE @var129 sysname;
SELECT @var129 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderListSummary]') AND [c].[name] = N'MainNoMaterialPlanRate');
IF @var129 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderListSummary] DROP CONSTRAINT [' + @var129 + '];');
ALTER TABLE [WorkOrderListSummary] ALTER COLUMN [MainNoMaterialPlanRate] decimal(7,2) NOT NULL;
ALTER TABLE [WorkOrderListSummary] ADD DEFAULT 0.0 FOR [MainNoMaterialPlanRate];
GO

DECLARE @var130 sysname;
SELECT @var130 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderExecutionSummary]') AND [c].[name] = N'MaterialPlanRate');
IF @var130 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderExecutionSummary] DROP CONSTRAINT [' + @var130 + '];');
ALTER TABLE [WorkOrderExecutionSummary] ALTER COLUMN [MaterialPlanRate] decimal(7,2) NOT NULL;
ALTER TABLE [WorkOrderExecutionSummary] ADD DEFAULT 0.0 FOR [MaterialPlanRate];
GO

DECLARE @var131 sysname;
SELECT @var131 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderExecutionSummary]') AND [c].[name] = N'MainNoMaterialPlanRate');
IF @var131 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderExecutionSummary] DROP CONSTRAINT [' + @var131 + '];');
ALTER TABLE [WorkOrderExecutionSummary] ALTER COLUMN [MainNoMaterialPlanRate] decimal(7,2) NOT NULL;
ALTER TABLE [WorkOrderExecutionSummary] ADD DEFAULT 0.0 FOR [MainNoMaterialPlanRate];
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260521112129_FixPlanRatePrecision', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProductionBatch] ADD [ValidInputQuestion] bit NULL;
GO

UPDATE [ProductionBatch] SET [ValidInputQuestion] = 0
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260522202809_AddValidInputQuestionToBatch', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SectionOutsource] ADD [DataSource] nvarchar(max) NULL;
GO

ALTER TABLE [ProcessInspection] ADD [DataSource] nvarchar(max) NULL;
GO

ALTER TABLE [OutsourceRecovery] ADD [DataSource] nvarchar(max) NULL;
GO

ALTER TABLE [MaterialReceiveCheck] ADD [DataSource] nvarchar(max) NULL;
GO

ALTER TABLE [FinalInspection] ADD [DataSource] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260523162153_AddDataSourceToAllModules', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ProductionBatch] ADD [CurrentSectionCompleted] bit NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260523190936_AddCurrentSectionCompletedToBatch', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [PurchaseOrder] ADD [InputMultiple] int NULL;
GO

ALTER TABLE [PurchaseFinishedPlan] ADD [InputMultiple] int NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260524220441_AddPurchaseFinishedPlanInputMultiple', N'8.0.0');
GO

COMMIT;
GO

