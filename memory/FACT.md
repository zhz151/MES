# MES 项目持久知识

## 枚举合并：MaterialCategory / RawMaterialType / ManufacturingItem → MaterialType
- 3 个旧枚举已完全删除，统一使用 MaterialType（15 个值）
- DTO 类型改为 MaterialType，Entity string 字段不变
- Service 层使用 `Enum.Parse<MaterialType>()` / `.ToString()` 转换

## PipeCategory → MaterialType 合并（已完成）
- PipeCategory.cs 已删除，Ncr 实体改用 MaterialType
- DB 列名仍为 `PipeCategory`，值通过 SQL 迁移到 MaterialType 枚举名
- 映射：TubeBlank→RoughTube, SurplusInventory→Surplus, PreparedFinished→Finished, SpecialDelivery→SpecialDeliveryStatus

## SubcontractProcessStatus → SubcontractOrderStatus 合并（已完成）
- SubcontractProcessStatus.cs 已删除
- SubcontractReturnItem.ProcessStatus 类型从 SubcontractProcessStatus → SubcontractOrderStatus
- 映射：Pending → Sent, PartialReturned → PartialReturned, Completed → Completed

## MaterialType 完整枚举（15 个值）
Finished（备料成品）、OrderFinished（订单成品）、CriticalFinished（临界成品）、Surplus（余库料）、SemiFinished（半成品）、DefectSemi（次品半成品）、DefectFinished（次品成品）、RoughTube（荒管）、RoundBar（圆棒）、DefectRoundBar（次品圆棒）、DefectRoughTube（次品荒管）、Scrap（报废品）、SpecialDeliveryStatus（特定交态成品）、WorkInProgress（在制品）、DefectWIP（次品在制）
