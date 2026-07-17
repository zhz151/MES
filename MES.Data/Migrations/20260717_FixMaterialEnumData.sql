-- ============================================================
-- 数据迁移：修复 Material 表枚举值变更导致的数据兼容问题
-- 问题：Material.MaterialCategory 字段中遗留了旧枚举值，
--   "defectsenmiproduct"、"preparedfinished"、"semiproduct"
--   无法被解析为 MaterialType 枚举成员，导致物料档案页面
--   物料分类筛选下拉框显示英文而非中文。
-- ============================================================

BEGIN TRANSACTION;

-- ============================================================
-- 1. MaterialCategory：旧枚举名 → MaterialType 枚举名
--    defectsenmiproduct/DefectSemiProduct → DefectSemi（次品半成品）
--    preparedfinished/PreparedFinished → Finished（备料成品）
--    semiproduct → SemiFinished（半成品）
-- ============================================================
UPDATE Material SET MaterialCategory = 'DefectSemi'   WHERE MaterialCategory IN ('defectsenmiproduct', 'DefectSemiProduct');
UPDATE Material SET MaterialCategory = 'Finished'     WHERE MaterialCategory IN ('preparedfinished', 'PreparedFinished');
UPDATE Material SET MaterialCategory = 'SemiFinished' WHERE MaterialCategory = 'semiproduct';

-- ============================================================
-- 2. 处理中文值（如果之前存储了中文显示名）
-- ============================================================
UPDATE Material SET MaterialCategory = 'Finished'              WHERE MaterialCategory = '备料成品';
UPDATE Material SET MaterialCategory = 'OrderFinished'         WHERE MaterialCategory = '订单成品';
UPDATE Material SET MaterialCategory = 'CriticalFinished'      WHERE MaterialCategory = '临界成品';
UPDATE Material SET MaterialCategory = 'Surplus'               WHERE MaterialCategory = '余库料';
UPDATE Material SET MaterialCategory = 'SemiFinished'          WHERE MaterialCategory = '半成品';
UPDATE Material SET MaterialCategory = 'DefectSemi'            WHERE MaterialCategory = '次品半成品';
UPDATE Material SET MaterialCategory = 'DefectFinished'        WHERE MaterialCategory = '次品成品';
UPDATE Material SET MaterialCategory = 'RoughTube'             WHERE MaterialCategory = '荒管';
UPDATE Material SET MaterialCategory = 'RoundBar'              WHERE MaterialCategory = '圆棒';
UPDATE Material SET MaterialCategory = 'DefectRoundBar'        WHERE MaterialCategory = '次品圆棒';
UPDATE Material SET MaterialCategory = 'DefectRoughTube'       WHERE MaterialCategory = '次品荒管';
UPDATE Material SET MaterialCategory = 'Scrap'                 WHERE MaterialCategory = '报废品';
UPDATE Material SET MaterialCategory = 'SpecialDeliveryStatus' WHERE MaterialCategory = '特定交态成品';
UPDATE Material SET MaterialCategory = 'WorkInProgress'        WHERE MaterialCategory = '在制品';
UPDATE Material SET MaterialCategory = 'DefectWIP'             WHERE MaterialCategory = '次品在制';

-- ============================================================
-- 3. 兼容旧 PipeCategory 枚举值（Ncr 表迁移时已处理，
--    但 Material 表可能也有这些旧值）
-- ============================================================
UPDATE Material SET MaterialCategory = 'RoughTube' WHERE MaterialCategory = 'TubeBlank';
UPDATE Material SET MaterialCategory = 'Surplus'   WHERE MaterialCategory = 'SurplusInventory';
UPDATE Material SET MaterialCategory = 'Finished'  WHERE MaterialCategory = 'PreparedFinished';

COMMIT;

-- 打印受影响行数
SELECT 'Material.MaterialCategory 旧值修复: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO
SELECT 'Material.MaterialCategory 中文值修复: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO
SELECT 'Material.MaterialCategory 旧PipeCategory值修复: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO
