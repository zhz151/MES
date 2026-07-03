# MES 项目持久知识

## DisplayConverter 统一显示转换模式（§6.34）
...

## FlowLevel 1A/1B 等级拆分（V5.13）
- **拆分规则**：FlowLevel==1 拆分为 1A（FlowCRType == MainNoAttentionProcess）和 1B（FlowCRType != MainNoAttentionProcess）
- **G7 新增属性**：`IsFlowLevel1A`、`IsFlowLevel1B`、`FlowLevelDisplay`（BatchPlanDto.cs）
- **G13 新增属性**：`PlanFlowLevelDisplay`（BatchPlanDto.cs，PlanFlowLevel==1 时按 PlanFlowCRType == MainNoAttentionProcess 判断 1A/1B）
- **排程汇总三档**：特急A档（1A）| 特急B档（1B）| 非特急（等级2/3/4）
- **ColdRollScheduleSummaryDto**：ProdKeyWeight/ProdLevel1BWeight/ProdNonKeyWeight + WaitKeyWeight/WaitLevel1BWeight/WaitNonKeyWeight 六字段
- **排程汇总前端列名**：在轧特急A档/在轧特急B档/在轧非特急/待轧特急A档/待轧特急B档/待轧非特急
- **排程汇总数据源**：从 PlanIsFlow（G13持久化）改为 IsFlow（G7实时值）

## G13 执行序修正（V5.13）
- PlanAllAsync ExecutionSequence 改为始终取 CurrentGroupName/CurrentSectionName（与 G7 一致）
- 不再因 CurrentSectionCompleted==true 而取 NextProcess/NextSectionName

## 冷轧计划主表新增字段（V5.13）
- **近日在轧组**：WeightProdUrgent(特急管)、WeightProdUrgentOther(急管)
- **近日待轧组**：WeightWaitNear(合计)、WeightWaitNearUrgent(特急管)、WeightWaitNearBackUrgent(后特急)、WeightWaitNearOtherUrgent(其它急管)
- **待轧三路分类**：特急管=冷轧类+序号优先 | 后特急=非冷轧关注(荒管/在制修检/收尾-成检) | 其它急管=冷轧类但序号不够优先
- **按钮名**："全部"→"全部(近日)"、"近3天"→"近2天"、"近5天"→"近4天"（maxDiff 同步 3→2, 5→4）
- **打印**：新增 CSS 隐藏 MudDrawer + MudAppbar

## StandardRegister（标准号）模块
...

## 命名空间冲突解决模式
...

## Blazor 服务端筛选模式（ExcelFilter）
...

## 详情页 MudGrid 布局模式
...

## MudSelect 中文值绑定模式
...