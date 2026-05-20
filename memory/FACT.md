# MES 项目持久知识

## 行为规则
- **失败自愈上限**：连续失败 2 次后必须停下来问用户，不得继续自行重试
  - "失败"定义：编译错误、测试失败、命令非预期退出
  - 第 1 次失败：正常分析修复
  - 第 2 次连续失败：停下来问用户"已连续失败 2 次，是否换思路/跳过/手动介入？"
  - 用户确认继续后，重置计数器再试
- 所有业务和代码规则见 `docs/04_开发规范.md`

## 重要文件路径
- **工单执行状况服务**: `MES.Services/WorkOrderExecutionService.cs`
- **用料计划满足率计算**: `MES.Services/PlanRateCalculator.cs`（internal static 共享工具类）
- **工单执行状况实体**: `MES.Data.Entities/WorkOrderExecutionSummary`（物化读模型）
- **工单执行状况 DTO**: `MES.Core.DTOs/WorkOrderExecutionSummaryDto`
- **工单服务**: `MES.Services/WorkOrderService.cs`

## 关键决策记录
### 用料计划满足率计算（2026-05-20）
- 不从 WorkOrder 预计算字段读取 MaterialPlanRate
- 改为从 5 种计划数据实时计算（PurchaseSemiPlan/PurchaseFinishedPlan/InventoryPlan/RoundBarPiercingPlan）
- 主号级用料计划率取加权平均（定尺按 TotalQuantity、非定尺按 TotalWeight 加权）
- 使用 PlanRateCalculator 共享工具类，供 WorkOrderExecutionService 和 WorkOrderService 共用
- 库存计划排除 Cancelled 状态

### 投料起止日（2026-05-20）
- 使用批次 CreatedTime（创建时间），非仓库入库日期 InboundDate

### LastRefreshTime（2026-05-20）
- 使用 DateTime.Now（本地时间），非 DateTime.UtcNow

## 关键实体关系
- Batch ↔ WorkOrder：通过 WorkOrderNo（string）关联
- WorkOrderExecutionSummary：一个工单一条记录，只读，通过 RefreshAllAsync 全量刷新
