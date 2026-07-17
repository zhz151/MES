-- ============================================================
-- 20260717 PipeCategory → MaterialType 迁移
--
-- PipeCategory 枚举已合并到 MaterialType，Ncr 表的旧值需要映射：
--   TubeBlank          → RoughTube
--   WorkInProgress     → WorkInProgress （不变）
--   SurplusInventory   → Surplus
--   CriticalFinished   → CriticalFinished （不变）
--   OrderFinished      → OrderFinished （不变）
--   PreparedFinished   → Finished
--   SpecialDelivery    → SpecialDeliveryStatus
-- ============================================================

UPDATE dbo.Ncrs SET PipeCategory = 'RoughTube'             WHERE PipeCategory = 'TubeBlank';
UPDATE dbo.Ncrs SET PipeCategory = 'Surplus'               WHERE PipeCategory = 'SurplusInventory';
UPDATE dbo.Ncrs SET PipeCategory = 'Finished'              WHERE PipeCategory = 'PreparedFinished';
UPDATE dbo.Ncrs SET PipeCategory = 'SpecialDeliveryStatus' WHERE PipeCategory = 'SpecialDelivery';
-- WorkInProgress, CriticalFinished, OrderFinished 无需变更（枚举名相同）

GO
