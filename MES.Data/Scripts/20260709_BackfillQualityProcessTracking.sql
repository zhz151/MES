-- =====================================================
-- 质量过程跟踪物化表（QualityProcessTracking）全量数据回填
-- 上线前执行：INSERT INTO ... SELECT 从源表计算后写入
-- 执行方式：SQL Server Management Studio 或 sqlcmd
-- 注意：先执行 20260709_AddQualityProcessTracking.sql 建表
-- =====================================================

SET NOCOUNT ON;

PRINT N'开始回填质量过程跟踪数据...';

INSERT INTO [dbo].[QualityProcessTracking] (
    [MaterialReceiveCheckId],
    [ProductionBatchId],
    [BatchNo],
    [ManufacturingItem],
    [TagNo],
    [WorkOrderNo],
    [SalesOrderNo],
    [SourceUnit],
    [FurnaceNo],
    [PlantGrade],
    [Specification],
    [ProductionType],
    [LengthStatus],
    [ProductionWeight],
    [ReceiveDate],
    [Shift],
    [Checker],
    [Salesman],
    [DeliveryState],
    [IsForceCompleted],
    [PbBatchNo],
    [PmiDate],
    [VisualDate],
    [DimensionDate],
    [EndoscopyDate],
    [HydroDate],
    [UnderwaterPneumaticDate],
    [EddyCurrentDate],
    [UltrasonicDate],
    [PortColoringDate],
    [InspectionCount],
    [ProductionCutQuantity],
    [TotalQuantity],
    [QualifiedQuantity],
    [DefectReworkQuantity],
    [DefectWarehouseQuantity],
    [DefectScrapQuantity],
    [MaxInspectionDate],
    [InboundQuantity],
    [InboundWeight],
    [InboundDate],
    [QualityStatus],
    [LastRefreshTime],
    [CreatedTime],
    [CreatedBy],
    [UpdatedTime],
    [UpdatedBy]
)
SELECT
    -- 关联标识
    rc.[Id],
    rc.[ProductionBatchId],

    -- G1: 批次信息
    COALESCE(rc.[BatchNo], pb.[BatchNo]),
    rc.[ManufacturingItem],
    rc.[TagNo],
    rc.[WorkOrderNo],
    rc.[SalesOrderNo],
    rc.[SourceUnit],
    rc.[FurnaceNo],
    rc.[PlantGrade],
    rc.[Specification],
    pb.[ProductionType],
    rc.[LengthStatus],
    rc.[ProductionWeight],
    rc.[ReceiveDate],
    rc.[Shift],
    rc.[Checker],
    COALESCE(rc.[Salesman], pb.[Salesman]),
    COALESCE(rc.[DeliveryState], pb.[DeliveryState]),
    rc.[IsForceCompleted],
    pb.[BatchNo],

    -- G2: 各检验项日期（取最大值）
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId] AND fi.[InspectionItem] = N'PMIInspection'),
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId] AND fi.[InspectionItem] = N'VisualInspection'),
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId] AND fi.[InspectionItem] = N'Dimension'),
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId] AND fi.[InspectionItem] = N'Endoscopy'),
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId] AND fi.[InspectionItem] = N'HydrostaticPressure'),
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId] AND fi.[InspectionItem] = N'UnderwaterPneumatic'),
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId] AND fi.[InspectionItem] = N'EddyCurrent'),
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId] AND fi.[InspectionItem] = N'Ultrasonic'),
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId] AND fi.[InspectionItem] = N'PortColoring'),

    -- InspectionCount: 已检项目数（去重）
    ISNULL((SELECT COUNT(DISTINCT fi.[InspectionItem]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId]), 0),

    -- G3: 检验汇总
    -- ProductionCutQuantity: 断切工段已完成记录的 PostCutQuantity 合计
    ISNULL((SELECT SUM(ISNULL(pr.[PostCutQuantity], 0)) FROM [dbo].[ProductionRecord] pr
     WHERE pr.[ProductionBatchId] = rc.[ProductionBatchId]
       AND pr.[SectionName] = N'断切' AND pr.[IsFinished] = 1), 0),

    -- TotalQuantity: 检验支数（各检验项 Quantity 的最大值）
    ISNULL((SELECT MAX(ISNULL(fi.[Quantity], 0)) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId]), 0),

    -- QualifiedQuantity: 合格支数（各检验项 QualifiedQuantity 的最小值）
    ISNULL((SELECT MIN(ISNULL(fi.[QualifiedQuantity], 0)) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId]), 0),

    -- DefectReworkQuantity: 返整支数合计
    ISNULL((SELECT SUM(ISNULL(fi.[DefectReworkQuantity], 0)) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId]), 0),

    -- DefectWarehouseQuantity: 不合格入库支数合计
    ISNULL((SELECT SUM(ISNULL(fi.[DefectWarehouseQuantity], 0)) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId]), 0),

    -- DefectScrapQuantity: 报废支数合计
    ISNULL((SELECT SUM(ISNULL(fi.[DefectScrapQuantity], 0)) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId]), 0),

    -- MaxInspectionDate: 最晚检验日期
    (SELECT MAX(fi.[InspectionDate]) FROM [dbo].[FinalInspection] fi
     WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId]),

    -- G4: 成品入库
    -- InboundQuantity: 入库支数合计（按批次号关联库存批次）
    ISNULL((SELECT SUM(ISNULL(ib.[InitialQuantity], 0)) FROM [dbo].[InventoryBatch] ib
     WHERE ib.[ProductionBatchNo] = pb.[BatchNo]), 0),

    -- InboundWeight: 入库重量合计
    (SELECT SUM(ib.[InitialWeight]) FROM [dbo].[InventoryBatch] ib
     WHERE ib.[ProductionBatchNo] = pb.[BatchNo]),

    -- InboundDate: 最晚入库日期
    (SELECT MAX(ib.[InboundDate]) FROM [dbo].[InventoryBatch] ib
     WHERE ib.[ProductionBatchNo] = pb.[BatchNo]),

    -- G5: 质量状态
    CASE
        WHEN EXISTS (SELECT 1 FROM [dbo].[FinalInspection] fi WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId])
        THEN
            CASE
                WHEN EXISTS (SELECT 1 FROM [dbo].[InventoryBatch] ib WHERE ib.[ProductionBatchNo] = pb.[BatchNo])
                THEN N'完成检验'
                ELSE N'检验中'
            END
        ELSE N'待检验'
    END,

    -- 刷新追踪
    SYSDATETIME(),

    -- BaseEntity 审计字段
    SYSDATETIMEOFFSET(),
    N'Backfill',
    SYSDATETIMEOFFSET(),
    N'Backfill'

FROM [dbo].[MaterialReceiveCheck] rc
INNER JOIN [dbo].[ProductionBatch] pb ON rc.[ProductionBatchId] = pb.[Id];

PRINT N'质量过程跟踪数据回填完成。';
GO
