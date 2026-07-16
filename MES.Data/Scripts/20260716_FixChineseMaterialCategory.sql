-- ============================================================
-- 20260716 修复 MaterialCategory 中文名→枚举名
--
-- 问题：数据库中各表的 MaterialCategory 列存储了中文显示名
-- （如"圆棒""荒管"），而非枚举名称（如"RoundBar""RoughTube"），
-- 导致 Enum.Parse<MaterialCategory>() 抛出 ArgumentException。
-- ============================================================

BEGIN TRANSACTION;

-- MaterialCategory 中文→枚举名映射
-- RoundBar            = 0
-- RoughTube           = 1
-- SemiProduct         = 2
-- OrderFinished       = 3
-- PreparedFinished    = 4
-- CriticalFinished    = 5
-- DefectRoundBar      = 6
-- DefectRoughTube     = 7
-- DefectSemiProduct   = 8
-- DefectFinished      = 9
-- Scrap               = 10
-- Surplus             = 11
-- SpecialDeliveryFinished = 12
-- DefectWIP           = 13

-- ========== 1. SubcontractOrder.OutMaterialCategory ==========
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = 'RoundBar'    WHERE [OutMaterialCategory] = N'圆棒';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = 'RoughTube'   WHERE [OutMaterialCategory] = N'荒管';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = 'SemiProduct' WHERE [OutMaterialCategory] = N'半成品';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'OrderFinished' WHERE [OutMaterialCategory] = N'订单成品';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'PreparedFinished' WHERE [OutMaterialCategory] = N'备料成品';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'CriticalFinished' WHERE [OutMaterialCategory] = N'临界成品';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'DefectRoundBar' WHERE [OutMaterialCategory] = N'次品圆棒';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'DefectRoughTube' WHERE [OutMaterialCategory] = N'次品荒管';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'DefectSemiProduct' WHERE [OutMaterialCategory] = N'次品半成品';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'DefectFinished' WHERE [OutMaterialCategory] = N'次品成品';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'Scrap' WHERE [OutMaterialCategory] = N'报废品';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'Surplus' WHERE [OutMaterialCategory] = N'余库料';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'SpecialDeliveryFinished' WHERE [OutMaterialCategory] = N'特定交态成品';
UPDATE [SubcontractOrder] SET [OutMaterialCategory] = N'DefectWIP' WHERE [OutMaterialCategory] = N'次品在制';

-- ========== 2. SubcontractReturnItem.MaterialCategory ==========
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = 'RoundBar'    WHERE [MaterialCategory] = N'圆棒';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = 'RoughTube'   WHERE [MaterialCategory] = N'荒管';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = 'SemiProduct' WHERE [MaterialCategory] = N'半成品';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'OrderFinished' WHERE [MaterialCategory] = N'订单成品';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'PreparedFinished' WHERE [MaterialCategory] = N'备料成品';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'CriticalFinished' WHERE [MaterialCategory] = N'临界成品';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'DefectRoundBar' WHERE [MaterialCategory] = N'次品圆棒';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'DefectRoughTube' WHERE [MaterialCategory] = N'次品荒管';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'DefectSemiProduct' WHERE [MaterialCategory] = N'次品半成品';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'DefectFinished' WHERE [MaterialCategory] = N'次品成品';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'Scrap' WHERE [MaterialCategory] = N'报废品';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'Surplus' WHERE [MaterialCategory] = N'余库料';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'SpecialDeliveryFinished' WHERE [MaterialCategory] = N'特定交态成品';
UPDATE [SubcontractReturnItem] SET [MaterialCategory] = N'DefectWIP' WHERE [MaterialCategory] = N'次品在制';

-- ========== 3. PurchaseOrder.MaterialCategory ==========
UPDATE [PurchaseOrder] SET [MaterialCategory] = 'RoundBar'    WHERE [MaterialCategory] = N'圆棒';
UPDATE [PurchaseOrder] SET [MaterialCategory] = 'RoughTube'   WHERE [MaterialCategory] = N'荒管';
UPDATE [PurchaseOrder] SET [MaterialCategory] = 'SemiProduct' WHERE [MaterialCategory] = N'半成品';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'OrderFinished' WHERE [MaterialCategory] = N'订单成品';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'PreparedFinished' WHERE [MaterialCategory] = N'备料成品';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'CriticalFinished' WHERE [MaterialCategory] = N'临界成品';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'DefectRoundBar' WHERE [MaterialCategory] = N'次品圆棒';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'DefectRoughTube' WHERE [MaterialCategory] = N'次品荒管';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'DefectSemiProduct' WHERE [MaterialCategory] = N'次品半成品';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'DefectFinished' WHERE [MaterialCategory] = N'次品成品';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'Scrap' WHERE [MaterialCategory] = N'报废品';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'Surplus' WHERE [MaterialCategory] = N'余库料';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'SpecialDeliveryFinished' WHERE [MaterialCategory] = N'特定交态成品';
UPDATE [PurchaseOrder] SET [MaterialCategory] = N'DefectWIP' WHERE [MaterialCategory] = N'次品在制';

-- ========== 4. Material.MaterialCategory ==========
UPDATE [Material] SET [MaterialCategory] = 'RoundBar'    WHERE [MaterialCategory] = N'圆棒';
UPDATE [Material] SET [MaterialCategory] = 'RoughTube'   WHERE [MaterialCategory] = N'荒管';
UPDATE [Material] SET [MaterialCategory] = 'SemiProduct' WHERE [MaterialCategory] = N'半成品';
UPDATE [Material] SET [MaterialCategory] = N'OrderFinished' WHERE [MaterialCategory] = N'订单成品';
UPDATE [Material] SET [MaterialCategory] = N'PreparedFinished' WHERE [MaterialCategory] = N'备料成品';
UPDATE [Material] SET [MaterialCategory] = N'CriticalFinished' WHERE [MaterialCategory] = N'临界成品';
UPDATE [Material] SET [MaterialCategory] = N'DefectRoundBar' WHERE [MaterialCategory] = N'次品圆棒';
UPDATE [Material] SET [MaterialCategory] = N'DefectRoughTube' WHERE [MaterialCategory] = N'次品荒管';
UPDATE [Material] SET [MaterialCategory] = N'DefectSemiProduct' WHERE [MaterialCategory] = N'次品半成品';
UPDATE [Material] SET [MaterialCategory] = N'DefectFinished' WHERE [MaterialCategory] = N'次品成品';
UPDATE [Material] SET [MaterialCategory] = N'Scrap' WHERE [MaterialCategory] = N'报废品';
UPDATE [Material] SET [MaterialCategory] = N'Surplus' WHERE [MaterialCategory] = N'余库料';
UPDATE [Material] SET [MaterialCategory] = N'SpecialDeliveryFinished' WHERE [MaterialCategory] = N'特定交态成品';
UPDATE [Material] SET [MaterialCategory] = N'DefectWIP' WHERE [MaterialCategory] = N'次品在制';

-- ========== 5. SupplierProfile.MaterialCategory ==========
UPDATE [SupplierProfile] SET [MaterialCategory] = 'RoundBar'    WHERE [MaterialCategory] = N'圆棒';
UPDATE [SupplierProfile] SET [MaterialCategory] = 'RoughTube'   WHERE [MaterialCategory] = N'荒管';
UPDATE [SupplierProfile] SET [MaterialCategory] = 'SemiProduct' WHERE [MaterialCategory] = N'半成品';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'OrderFinished' WHERE [MaterialCategory] = N'订单成品';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'PreparedFinished' WHERE [MaterialCategory] = N'备料成品';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'CriticalFinished' WHERE [MaterialCategory] = N'临界成品';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'DefectRoundBar' WHERE [MaterialCategory] = N'次品圆棒';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'DefectRoughTube' WHERE [MaterialCategory] = N'次品荒管';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'DefectSemiProduct' WHERE [MaterialCategory] = N'次品半成品';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'DefectFinished' WHERE [MaterialCategory] = N'次品成品';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'Scrap' WHERE [MaterialCategory] = N'报废品';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'Surplus' WHERE [MaterialCategory] = N'余库料';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'SpecialDeliveryFinished' WHERE [MaterialCategory] = N'特定交态成品';
UPDATE [SupplierProfile] SET [MaterialCategory] = N'DefectWIP' WHERE [MaterialCategory] = N'次品在制';

COMMIT TRANSACTION;
