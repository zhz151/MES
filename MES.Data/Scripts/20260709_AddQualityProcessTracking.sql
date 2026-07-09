BEGIN TRANSACTION;
GO

ALTER TABLE [SalesOrder] ADD [CustomerName] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [SalesOrder] ADD [EndCustomer] nvarchar(max) NULL;
GO

ALTER TABLE [SalesOrder] ADD [Salesman] nvarchar(max) NOT NULL DEFAULT N'';
GO

CREATE TABLE [QualityProcessTracking] (
    [Id] int NOT NULL IDENTITY,
    [MaterialReceiveCheckId] int NOT NULL,
    [ProductionBatchId] int NOT NULL,
    [BatchNo] nvarchar(50) NULL,
    [ManufacturingItem] nvarchar(50) NULL,
    [TagNo] nvarchar(100) NULL,
    [WorkOrderNo] nvarchar(50) NULL,
    [SalesOrderNo] nvarchar(50) NULL,
    [SourceUnit] nvarchar(100) NULL,
    [FurnaceNo] nvarchar(50) NULL,
    [PlantGrade] nvarchar(100) NULL,
    [Specification] nvarchar(100) NULL,
    [ProductionType] nvarchar(20) NULL,
    [LengthStatus] nvarchar(20) NULL,
    [ProductionWeight] decimal(18,3) NULL,
    [ReceiveDate] date NOT NULL,
    [Shift] nvarchar(20) NULL,
    [Checker] nvarchar(50) NULL,
    [Salesman] nvarchar(50) NULL,
    [DeliveryState] nvarchar(50) NULL,
    [IsForceCompleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [PbBatchNo] nvarchar(50) NULL,
    [PmiDate] date NULL,
    [VisualDate] date NULL,
    [DimensionDate] date NULL,
    [EndoscopyDate] date NULL,
    [HydroDate] date NULL,
    [UnderwaterPneumaticDate] date NULL,
    [EddyCurrentDate] date NULL,
    [UltrasonicDate] date NULL,
    [PortColoringDate] date NULL,
    [InspectionCount] int NOT NULL DEFAULT 0,
    [ProductionCutQuantity] int NOT NULL DEFAULT 0,
    [TotalQuantity] int NOT NULL DEFAULT 0,
    [QualifiedQuantity] int NOT NULL DEFAULT 0,
    [DefectReworkQuantity] int NOT NULL DEFAULT 0,
    [DefectWarehouseQuantity] int NOT NULL DEFAULT 0,
    [DefectScrapQuantity] int NOT NULL DEFAULT 0,
    [MaxInspectionDate] date NULL,
    [InboundQuantity] int NOT NULL DEFAULT 0,
    [InboundWeight] decimal(18,3) NULL,
    [InboundDate] date NULL,
    [QualityStatus] nvarchar(20) NOT NULL DEFAULT N'待检验',
    [LastRefreshTime] datetime2 NULL,
    [CreatedTime] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(50) NOT NULL,
    [UpdatedTime] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_QualityProcessTracking] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_QPT_BatchNo] ON [QualityProcessTracking] ([BatchNo]);
GO

CREATE INDEX [IX_QPT_ProductionBatchId] ON [QualityProcessTracking] ([ProductionBatchId]);
GO

CREATE INDEX [IX_QPT_QualityStatus] ON [QualityProcessTracking] ([QualityStatus]);
GO

CREATE INDEX [IX_QPT_ReceiveDate] ON [QualityProcessTracking] ([ReceiveDate]);
GO

CREATE INDEX [IX_QPT_SalesOrderNo] ON [QualityProcessTracking] ([SalesOrderNo]);
GO

CREATE INDEX [IX_QPT_WorkOrderNo] ON [QualityProcessTracking] ([WorkOrderNo]);
GO

CREATE UNIQUE INDEX [UK_QPT_MaterialReceiveCheckId] ON [QualityProcessTracking] ([MaterialReceiveCheckId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260709040114_AddQualityProcessTracking', N'8.0.0');
GO

COMMIT;
GO

