# MES 前端页面结构参考

> 版本：V71（2026-09-09；生成 2026-08-19）
> 用途/状态：Quick Reference - 按导航菜单分组的前端页面结构参考，§1 上下文总览 / §2 各上下文页面块 / §3 列表页全量清单。
> 上次实质变更（V71）：**质量管理 15 张列表页默认列显隐收敛**（2026-09-09，成品检验/过程检验已于 V69 收敛，其余 13 页对齐；隐藏列均可经列选择器开启）：
> ① 过程检验 `/quality/process-inspection`：隐 工单号/设备名称/班次（默认显 挂牌号/订单号/主号/执行序号/执行日期/检验项目/检验结果/不合格处理等）；列偏好键 v1→**v2**（`col_prefs_process-inspection_v2`）。
> ② 成检到料 `/quality/material-receive-check`：隐 执行序/班次/数据来源/更新时间；该页此前未并列偏好版本（Save/Load 第 2 参传 null），本次首并 `ColumnPrefsVersion="v1"`（`col_prefs_material-receive-checks_v1`）。
> ③ 成品检验 `/quality/final-inspection`：隐 设备名称/班次/工单号（更新时间已于 V69 在 G8 默认隐藏）；列偏好键 v1→**v2**（`col_prefs_final-inspection_v2`）。
> ④ 成检追踪 `/quality/quality-process-tracking`：隐 工单号/最终用户/班次/更新日期；首并 `ColumnPrefsVersion="v1"`（`col_prefs_quality-process-tracking_v1`）。
> ⑤ 不合格报告 `/quality/ncrs`：更新日期 默认隐藏；首并 `ColumnPrefsVersion="v1"`（`col_prefs_ncrs_v1`）。
> ⑥ 来料炉号登记 `/quality/furnace-registration`：更新日期 默认隐藏；首并 `ColumnPrefsVersion="v1"`（`col_prefs_furnace-registration_v1`）。
> ⑦ 理化检测 9 张列表页（化学分析/扩口/压扁/晶粒度/硬度/晶间腐蚀/金相/点蚀/拉伸，`/quality/chemical-analysis` 等）：各页 更新日期 默认隐藏；9 页均首并 `ColumnPrefsVersion="v1"`（`col_prefs_chemical-analysis_v1`、`col_prefs_flaring-test_v1`、`col_prefs_flattening-test_v1`、`col_prefs_grain-size-test_v1`、`col_prefs_hardness-test_v1`、`col_prefs_intergranular-corrosion-test_v1`、`col_prefs_metallographic-test_v1`、`col_prefs_pitting-corrosion-test_v1`、`col_prefs_tensile-test_v1`）。
> 验证 `dotnet build MES.Blazor` 0 错误 0 警告 + 质量组件定向单测 6 通过。
> 历史变更（V70）：**批次管理 5 张列表页默认列显隐收敛 + 工艺卡打印列序调整**（2026-09-09，对齐生产批次/生产记录收敛，隐藏列均可在列选择器开启）：
> ① 去油/酸洗入缸记录 `/pickling-in-records`：G1去油/酸洗信息 增隐 工单号/设备名称/班次/操作人（默认显 登记日期/生产编号/挂牌号/订单号/主号/工序名称/工段名称/工厂牌号/制造规格/生产支数/生产重量/产类）；**G2完工信息 状态/完工日期 恢复默认显示、完工班次/完工操作人 默认隐藏**（V69 曾整组隐，本次按用户决策仅保留两操作信息列隐藏）；列偏好键 v1→**v2**（`col_prefs_pickling-in-records_v2`）。
> ② 去油/酸洗完工记录 `/pickling-out-records`：入缸信息 隐 设备名称（完工日期/班次/操作人保持默认显示）；列偏好键 v1→**v2**（`col_prefs_pickling-out-records_v2`）。
> ③ 工段委外 `/section-outsources`：委外信息 隐 要求收回日期/紧急；回收信息 隐 非正常回收(支)/非正常回收(重)/回收备注；列偏好键 v1→**v2**（`col_prefs_section-outsources_v2`）。
> ④ 委外回收 `/outsource-recoveries`：回收信息 隐 数据来源/更新时间；该页此前未并列偏好版本（Save/Load 第 2 参传 null），本次首并 `ColumnPrefsVersion="v1"`（`col_prefs_outsource-recoveries_v1`）。
> ⑤ 工艺卡流转卡打印 `/process-card-print`：批次选择列表「当前规格」列移至「当前工序」列后（对齐生产批次列表同组列序）；列偏好键 v1→**v2**（LoadAsync 按保存顺序恢复列序，升版强制新序）。
> 验证 `dotnet build MES.Blazor` 0 错误 0 警告 + Pickling/SectionOutsource/OutsourceRecover/ProcessCardPrint/Batches 定向单测 125 通过。
> 历史变更（V69）：**成检记录 / 过程检验 / 去油入缸 / 去油完工 4 张内联编辑列表页 默认列显隐收敛 + 去 table-min-width**（2026-09-09，对齐生产批次/生产记录）——①根因=这几页此前**全列默认显示**且表格带 `table-min-width` class + `--table-min-width:{总宽}px` Style（`.mud-table-root` min-width 被锁成全部可见列宽总和，内联编辑页数十列全显时被撑到极宽）；本次各页将次要/追溯/明细列改 `Visible=false` 默认隐藏（列选择器可开启），并移除两处强制最小宽（列宽由 `th` 宽提示自然撑开）。各页默认显隐：
> ① 成检记录 `/quality/final-inspection`（63列8组→默认显 **33 列**）：G1检验执行 隐 资格等级（显 检验项目/检验日期/设备/班次/操作员/成检类型/是否交付态/生产编号 8 列）；G2生产批次 隐 生产类型/制造物品/制造状态/交货状态/最终用户/来料单位/长度状态（显 挂牌号/工单号/订单号/主号/业务员/炉号/工厂牌号/规格/生产支数/生产重量 10 列）；G3检验结果 隐 非定尺长度范围；G4不合格处理 7 列全显；**G5尺寸值/G6压力值/G7涡流超声波(13)/G8辅助信息(检验备注/数据来源/更新日期) 四组整组默认隐藏**；列偏好键并入 `ColumnPrefsVersion="v1"`（`col_prefs_final-inspection_v1`）。
> ② 过程检验 `/quality/process-inspection`（33列5组→默认显 **24 列**）：G1生产批次 隐 执行序号；G4不合格处理 隐 理论返整重/理论入库重/理论报废重/次品情况描述；**G5辅助信息（来料单位/备注/数据来源/更新日期）整组默认隐藏**；列偏好键 `col_prefs_process-inspection_v1`。
> ③ 去油/酸洗入缸记录 `/pickling-in-records`（24列2组→默认显 G1 核心列）：G1去油/酸洗信息 隐 执行序号/备注/数据来源/更新时间；**G2完工信息（状态/完工日期/完工班次/完工操作人）整组默认隐藏**（在产浸泡批次仍经行操作「完工」按钮按工件确认，完工后置信息可经列选择器开启）；列偏好键 `col_prefs_pickling-in-records_v1`。
> ④ 去油/酸洗完工记录 `/pickling-out-records`（16列→隐 备注/数据来源/更新时间）：入缸信息 + 完工日期/班次/操作人 默认显示；列偏好键 `col_prefs_pickling-out-records_v1`。
> ⑤ 清理 `app.css` 中已无引用的 `.compact-table.table-min-width .mud-table-root` 规则及其说明注释（全站已无页使用 table-min-width 标识）。验证 `dotnet build MES.Blazor` 0 警告 + FinalInspections/ProcessInspections/Pickling/ProductionRecords 定向单测 233 通过。
> 历史变更（V68）：**生产批次列表（/batches）列组重排 + 默认显隐收敛**（2026-09-09）——① **组合并**：原「产品要求」「质量要求」（固溶参数/质量备注）并入「工单信息」组、原「生产执行」并入「现执行状态」组（截止执行日/当前工序/工段/工段完工/设备/委外/规格/下一工段/下一工序等归入）、**原「原始投料信息」12 字段（投料类型/来源批次号/源生产编号/原料类型/来源牌号/来料单位/炉号/来源规格/来源长度状态/来源单支重/领料支数/领料重量）并入「批次基本信息」组、自「挂牌号」字段后按原序插入**（全部默认隐藏）；② **整组后移**：「关联工单状态」「流转判定」「有效投料变更」「成品切割跟踪」四核查组移为**末尾 4 组**；③ 最终 **7 组布局** = 批次基本信息(含原始投料) → 工单信息(含原产品要求+质量要求) → 现执行状态(含生产执行) → 关联工单状态 → 流转判定 → 有效投料变更 → 成品切割跟踪（**2026-09-09 二次调整：「现执行状态」与「工单信息」两组对调，工单信息前置**），**默认仅显前三组**（后四核查组整组默认隐藏）——核查相关派生列（执行匹配/流转判定/需调整/成切存疑）默认隐藏后仍可在列选择器手动开启再列头筛选（错疑核查职责已由生产执行核查页承接，批次列表页保留纯列表职责）；**2026-09-09 三调（字段级默认显隐收敛）**：批次基本信息仅显 生产编号/挂牌号/原料类型/来源牌号/来源规格/领料支数/领料重量/制造物品；工单信息仅显 订单号/主号/业务员/交货状态/工厂牌号/规格；现执行状态仅显 状态/截止执行日/当前工序/当前规格/当前工段/当前委外/下一工段（组内列序调：当前规格→当前工序后、当前设备→当前委外后、对应规格→下一工序后），同组余列与后四核查组均默认隐藏（列选择器可开启）；列偏好键并入 `ColumnPrefsVersion="v2"`（`col_prefs_batches_v2`，原第 2 参传 null）→ **2026-09-09 三调再升 `ColumnPrefsVersion="v3"`（`col_prefs_batches_v3`）** 使旧持久化失效一次、新默认立即生效。验证 `dotnet build MES.Blazor` 0 警告 + Batches 定向单测 33 通过。**同日生产记录列表（/production-records）默认显隐收敛 + 列宽修复**：G1 执行信息仅显 执行日期/生产编号/挂牌号/订单号/主号/工序名称/工段名称/工厂牌号/制造规格（工单号/执行序号 隐）；G2 产出数据仅显 加工支数/加工重量/产类/平头数/断切倍数/预成切/长度状态/成品长度/符合工单长度/切后支数（设备名称/班次/操作人 隐）；G3 工艺参数/G4 追溯信息 整组隐；列偏好键并入 `ColumnPrefsVersion="v1"`（`col_prefs_production-records_v1`）强制新默认；**列宽根因**=原表格带 `table-min-width` class + `--table-min-width` Style 把 `.mud-table-root` min-width 锁到全部可见列宽总和（此前 4 组全显约 27 列 >2900px 撑宽），生产批次无此设置故正常 → 已移除两处对齐生产批次，列宽由 `th` 宽提示自然撑开。
> 历史变更（V67）：**全仓日期范围搜索「至」端统一含当天**（跨 批次/生产记录/质量/订单/工单/仓库/设备 服务层，2026-09-09）——凡日期范围 `XxxDateFrom/To`、`EndDate` 及 Controller 绑定参数 `signDateTo/deliveryDateTo/deliveryDateEnd` 的「至」端按日期(00:00)比较的站点，一律改半开区间 `< Xxx.Value.AddDays(1)`（含「至」日全天；date 列等价、datetime/datetimeoffset 列修复「至日当天被排除」bug）：批次 `BatchService` 登记日期（CreatedTime，生产批次页共享通道受益）、`ProductionRecordService` 执行日期；质量各试验/成检/到料/化学/炉号/NCR/质量过程追踪（InspectionDate/AnalysisDate/IncomingDate/ReceiveDate/ReportDate）；订单 签订日期+交期起止（OrderService 列头日期 From/To）、工单 需求调整/执行状况 交货/签订日期（WorkOrderService/OrderDemandAdjustmentService）；仓库 入库/出库历史（InboundDate/OutboundDate）；设备 报修 ReportTime。验证：`dotnet build MES.Api` 0 警告 + 受影响模块定向单测 718 通过。生产执行核查页顶栏已于 V66 移除搜索，本次为共享层修复，生产批次页日期筛选与其余各页受益。
> 历史变更（V66）：生产执行核查 `/production-execution-check`（批次）**顶部搜索栏整行删除**——① 原「模糊搜索 - 批次号/工单号/销售单号/挂牌号/规格」+「登记日期从/至」三输入框移除（宽搜实际命中共享 `api/batch/list` 约 43 字段、远大于提示文案；日期按 CreatedTime 且"至"仅到当日 00:00 边界，两页共享服务层）；② 页面从错疑卡/联动提示条直入列表更清爽，定位改依赖 错疑卡联动 + 列头 ExcelFilter + 排序 + 分页；PageState 去 Keyword/dateFrom/dateTo（仍存排序与 `_columnFilters`）；③ 后端 `api/batch/list` 通道与生产批次页宽搜/日期**均不动**；测试改为 `Render_NoTopSearchBars`（断言不再渲染搜索/日期框）锁定。
> 历史变更（V65）：生产执行核查 `/production-execution-check`（批次）**错疑聚合卡改名「错疑-生产批次执行」+ 默认折叠**——① 折叠按钮/卡内标题「批次-错疑执行」→**「错疑-生产批次执行」**（页面级改名同步，消除「存疑执行」与"执行存疑"歧义，新名点明批次+执行双重核查视角）；② 页面加载时聚合卡**默认折叠**（不再首屏展开），展开时才懒加载 `/api/batch/doubt-execution-summary`（4 类错疑批次数+领料重量合计，BatchCount>0 可点选联动筛下列表，可取消筛选）；列表/其余无变化。
> 历史变更（V64）：生产执行核查 `/production-execution-check`（批次）**取消「过程检成重」列**——① G3 删 `ProcessInspectionQualifiedWeight` 列（过程检合格重量，此前作重量侧对照参考；投料需调整/成切存疑判灯不依赖，成因与重量折算源详情/批次页可见），组内收束为六列：过程检理论成支/现理论成支/理论成品重/成切需求/成切执行/成切支数；② 页底合计同步去「过程检成重」；列偏好键 batchExecutionCheck_v5→**v6** 强制新默认。
> 历史变更（V63）：生产执行核查 `/production-execution-check`（批次）**G3 列名精简 + 过程检两列前移 + 组名贴合实际**——① 组名「投料与有效量」→**「理论产出对照」**（裁领料/现有效原料/缺陷六列后，组内实为理论产出折算/过程检/成切产出对照，组名改名点题）；② **列序**：过程检理论成支、过程检成重 **前移到 现理论成支 之前**（过程检侧在前、现有效侧在后）；③ **列名**：过程检理论成品支→**过程检理论成支**、过程检合格量→**过程检成重**（明确重量口径，其值即过程检侧成品重量折算源）、理论成品支→**现理论成支**（强调来自现有效 CurrentValidQty×制几率；理论成品重列名保持不改）；组内现为 过程检理论成支/过程检成重/现理论成支/理论成品重/成切需求/成切执行/成切支数；列偏好键 batchExecutionCheck_v4→**v5** 强制新默认。
> 历史变更（V62）：生产执行核查 `/production-execution-check`（批次）**投料与有效量组收束为错疑缘由数据对照组**——① **成切存疑移回执行核查组**（该组回 4 灯：匹配工单/工段流转/投料需调整/成切存疑），成切需求/执行/支数留投料与有效量组作灯下数值对照；② **投料与有效量组裁剪** 领料支数/领料重量/现有效原料支数/现有效原料重量/缺陷-返整量/缺陷-纯次品量 六列彻底删除，收束为：理论成品支（现理论成支）/理论成品重（现理论成重）/过程检理论成品支/**过程检合格量**（由隐藏转默认可见，其值即过程检侧成品重量口径折算源）/成切需求/成切执行/成切支数——即「投料需调整」（=|过程检理论成品支−理论成品支|/理论成品支>3%）与「成切存疑」（=理论成品支 vs 成切支数 偏差>5%）两灯的行级缘由数据对照；列偏好键 batchExecutionCheck_v3→**v4** 强制新默认。
> 历史变更（V61）：生产执行核查 `/production-execution-check`（批次）**列裁剪/重组**——①「批次与执行」+「关联工单」合并为「批次与工单」（生产编号/状态/工单号/工单关注 + 挂牌号/次号隐藏）；② 执行核查组精简为 3 灯并重命名：执行匹配→**匹配工单**、流转判定→**工段流转**、需调整→**投料需调整**（成切存疑移出该组）；③ **成切全组（成切需求/成切执行/成切支数/成切存疑）并入「投料与有效量」组**；④ 裁剪 6 列彻底删除（当前工序/当前工段/截止执行日/工段完工、订单号/主号）；错疑聚合卡及卡片↔列表联动不变；列偏好键 batchExecutionCheck_v2→**v3** 强制新默认。
> 历史变更（V60）：生产执行核查 `/production-execution-check`（批次）**G4 投料与有效量默认补理论产出三列**（字段后端零改动，DTO 已带）——理论成品支（新列）/理论成品重（原隐藏列改默认显示）/过程检理论成品支（新列）默认全显并参与页底合计，排 现有效原料重量 之后构成「领料→现有效→理论产出」链条，为 4 类错疑提供行级对照量（成品切割疑单=理论成品支 vs 成切支数 G3、有效投料疑单=过程检理论成品支 vs 理论成品支 偏差>3%）；列偏好键 batchExecutionCheck_v1→**v2** 强制新默认。
> 历史变更（V59）：批次管理菜单**拆分两入口**（对齐工单管理「用料计划+用料投料核查」范式，数据层/读模型零改动）——菜单「批次首页」改名「生产批次」，其下方新增「生产执行核查」`/production-execution-check`（新列表页）；原批次首页顶部的「批次-错疑执行」折叠卡片（4 类错疑聚合，点选联动筛选）整体迁出至新页，新页=聚合卡（默认展开）+ 页内自足精简批次列表（只读，仅查看详情跳批次详情），批次列表页职责收敛（通知/新建/工艺卡打印/列头手动筛选错疑保留）；批次上下文 14→15 页、列表页 6→7，§3 清单补 #87。
> 历史变更（V58）：计划排程上下文**默认列显隐/行序收敛**（2026-09-08）——① 工单排程 `/scheduling-plans`（列表）「最终客户」默认隐藏（列偏好键并入 `ColumnPrefsVersion="v1"` → `col_prefs_workorderschedules_v1`，升版使已持久化旧布局失效一次）。② 负载总览「订单负荷总量」=报表「现订单负荷总量」同一 `WorkOrderLoadOverview` 组件表，**行序与序号**（取消 2026-08-23「订单交期负荷置顶」改按自然构建序输出）：原料 1-1/1-2/1-3+汇总 → 投料-在产 2-*+汇总 → 投料-成检 3-1+汇总 → 整体完工预计 **4-0** → 订单交期负荷 **5-1/5-2/5-3**（订单延期-原料/在产/成检，日期桶仅显副值），两页共享后端同步生效。③ 批次计划 `/batch-plans`（列表，批次基础信息组）「交货状态」改「制造状态」**换源显示**：默认显示批次真实制造状态 ManufacturingStatus 列、原交货状态 DeliveryState 列默认隐藏；长度状态默认隐藏；重量(kg)→重量；列偏好键 v2→v3（`col_prefs_batchplans_v3`）。列显隐均可经列选择器切回。
> 历史变更（V57）：工单上下文三页**默认列显隐收敛**（2026-09-08，各页版本键升 v1 使已持久化旧布局失效一次、新默认立即生效）——① 工单生成 `/workorders`（列表）：最终客户 默认隐藏（次号、钢管制造 随默认定义同步默认隐藏，与执行状况 G1 对齐）；列偏好 ColumnPrefs 键并入 `ColumnPrefsVersion="v1"`（`col_prefs_workorders_v1`，原版本传 null）。② 工单需求调整 `/workorders-demand-adjustment`（列表）：实时关注「主号-预计完成日」默认隐藏（此前本页已默认隐藏 订单日期/最终客户/主号-原锁备注）；整页 PageState 存储键并入 `ColumnLayoutVersion="v1"`（`page_state_workorders-demand-adjustment-v1`）。③ 工单执行状况 `/workorder-execution`（列表）：基础数据「最终客户」「订单日期」默认隐藏；整页 PageState 存储键并入 `ColumnLayoutVersion="v1"`（`page_state_workorderexecution-v1`）。列显隐均可经列选择器切回。
> 历史变更（V56）：成检计划 `/final-inspection-plan` **默认列显隐再收敛 + 列标签简化**（2026-09-08，存储键并入 `ColumnLayoutVersion="v3"` 升 v3 使旧持久化失效）——① 批次信息组 炉号 改默认隐藏，默认仅显 生产编号/生产类型/制造状态/工厂牌号/规格/长度状态/支数/重量/订单号/主号/业务员；生产支数→支数、生产重量(kg)→重量；② 技术要求检验项组标签简化：必检项数→必检项、PMI检验→PMI、水下气压→气压、端口着色→着色；③ **G5 各项检验的日期 + G6 检验的数量信息 两组整组默认隐藏**（可经列选择器打开）。
> 历史变更（V55）：成检计划 `/final-inspection-plan` **默认列显隐收敛**（2026-09-08，列定义 Visible 默认值）——批次信息组默认仅显 生产编号/生产类型/制造状态/工厂牌号/规格/长度状态/生产支数/生产重量(kg)/炉号/订单号/主号/业务员（成检类型/是否交付态/制造物品/交货状态/来料单位/工单号/最终用户 默认隐藏）；成检状态组默认仅显 成检阶段/到料日期（最晚检验 默认隐藏）；其余组默认全显；「重置」按钮恢复代码默认显隐（非全显）。⚠️ 页面状态一体存于 PageState，存储键并入 `ColumnLayoutVersion="v2"`（`page_state_final-inspection-plan-v2`）使旧持久化失效一次。
> 历史变更（V54）：批次计划 `/batch-plans` **列标签精简 + 默认显隐调整**（列偏好键升 v2 强制新默认，2026-09-08）——状态跟踪组「待在产执行工段」→「待在产工段」；执行反馈组「原工量差→原差 / 现工量差→现差 / 是否执行→执行」（Key/公式不变）；批次基础信息组 挂牌号/关联工单号/交货日期 默认隐藏、订单号+主号 默认显示；批次计划组 暂停/抢单 默认隐藏（列显隐均可切回）。
> 历史变更（V53）：定尺工单定尺 `/fixed-length-work-order-view` G6主号数据及现况分析 **8 个字段标签精简改名**（Key/排序/公式不变）：需求计划总→需求总支、理论成品总→理论可产支、免切理论支→免切、待切理论支→理论待切、已切理论支→理论已切、切割偏差判定→切割偏差、预计损耗支→预计损耗、当前理论产出→现有效产支。
> 历史变更（V52）：定尺工单定尺 `/fixed-length-work-order-view` **默认列显隐收敛**——G3成品切割/G4成检数据/G5成品入库三组默认整组隐藏，基础数据「往来单位·订单日期」默认隐藏，G6主号数据及现况分析各字段标签去「主号-」前缀（需求计划总/理论成品总/免切理论支/待切理论支/已切理论支/实切支数/切割偏差判定/预计损耗支/当前理论产出/次品支数/盈亏支数/盈亏状态）；本页无列偏好持久化，加载即默认。
> 历史变更（V51）：原「用料计划」页**一分为二**（计划归计划、核查归核查，数据层/读模型零改动）——① 用料计划 `/material-plan-overview`（改造现页，规划工作台）：实时关注收窄为仅 3 字段（主号-关注/原锁备注/计划性），两错疑卡（错疑-用料投料不一致、错误-用料计划及其执行）与联动迁出，列偏好 key→v5；**随后默认列显隐收敛为 20 项**（默认仅显 工单号/订单号/主号/业务员/交货日期/交货状态/工厂牌号/规格/长度状态/总支数/总重量 + 主号-关注·原锁备注·计划性 + 工单用料计划/工单满足率/计划日期/分类用料占比/要求到货日/理论截止投料日，最终用户/最大长度/主号-关联用料态 等默认隐藏），**页面加载默认按 主号-关注=原料锁定（档位 2）过滤**（未持久化该列筛选时套用），列偏好 key 再升 v6；② 用料投料核查 `/material-input-consistency`（新增）：实时关注列组 + 两错疑卡联动，无汇总卡/无计划类型勾选/无用料计划列组。工单上下文页面 17→18、列表页 5→6，§3 清单补 #86。
> 历史变更（V50）：销售订单确认单**打印双模式**——详情页「打印」与列表页「打印选中订单」由单一按钮改 **MudMenu 下拉（含金额确认单 / 不含金额确认单）**，请求体 `OrderPrintBatchRequest.IncludeAmounts` 控制（默认含金额）：含金额显示 结算方式→计价单位→单价→总价 并汇总**订单总价**；不含金额仅保留结算方式、隐藏三金额列；两模式打印列均把「结算方式」移至**理算重量之后**（确认单尾列=…理算重量→结算方式→[计价单位/单价/总价]→备注），重量/米数打印收敛 1 位小数；存量数据由迁移 `20260907122038_BackfillOrderItemAmountsRound1`（已 apply 真库）round1 收敛并按计价单位取量重算总价。前端列与打印列联动无需新列偏好键。
> 历史变更（V49）：订单模块页面收敛 + 项次计价金额——① 订单列表页把「基本信息+合同交付」两列组合并为单组「基本信息」（B23 列分组现为 3 组），默认仅显示订单号/签订日期/业务员/客户名称/交期截止/订单总重量/含项次数，余默认隐藏；② 订单成品(实时库存)默认隐藏 11 列（工单号/最终客户/产品标准/工厂牌号/最小长度/最大长度/仓库批次/来源/来料单位/剩余米数/物料类型）；两页列偏好键均升 v2 强制新默认；③ **订单项次新增 3 字段（计价单位 元/Kg·元/米·元/支/单价/总价）**：新建/编辑/查看三态「结算方式」自第 4 列移至理算重量之后，尾列顺序=结算方式→计价单位→单价→总价→备注，新建页新增备注输入框，详情页备注列默认隐藏（项次列偏好键 v2）；总价=单价×取量（计价单位决定：合同重量/米数/支数）自动计算且**可手动覆盖**（恢复自动按钮）；小数收敛（前后端一致）——米数/合同重量/理算重量直接四舍五入保留 1 位（前端输入即 round1 + 服务层写库兜底）、单价/总价 2 位；销售订单确认单打印追加 4 列（共 24 列：…理算重量→计价单位/单价/总价/备注），数据工具 OrderItem 实体追加 3 列。
> 历史变更（V48）：全站「计件类别/工单/订单」用户可见命名整理——① 工资结算菜单与页面把「生产计件类别/成检计件类别」改名「生产计件标准/成检计件标准」（主菜单、列表/编辑页 h5、返回按钮；域内「类别 = 主表+维档」措辞与「新增类别」等操作按钮不变）；② 工单管理菜单二级分组「工单操作[工单生成,用料计划,工单需求调整]·工单查询[工单执行状况,定尺工单定尺]」，叶子改名 工单首页→工单生成、用料计划总览→用料计划、定尺工单定尺数据→定尺工单定尺，各页 h5/返回/打印标题同步；③ 订单列表页 h5 「订单管理」→「订单列表」，折叠卡标题「订单接单-出库及现负荷汇总」→「完成预估及延期风险」且**删除**卡内「订单接单·出库及现负荷汇总」5指标×12月小表（仅保留订单(整单)完成预估/风险-已延期两张交期预估表；后端 GetInOutSummaryAsync 与报表总览的相同表不受影响，仍显示）。
> 历史变更（V47）：订单管理新增**订单进度树**只读查询页（`OrderProgress.razor` `/orders/progress?salesOrderNo=`，由订单列表行「进度树」图标进入）——一级订单号、二级订单号+主号（**含完结主号**，灰显「已完结」）、三级四阶段分支（原料锁定 A–D 单叶 / 生产执行 8 节点 / 成品检验 3 档 / 成品入库 3 档）、四级叶重量(kg)；完结主号强制仅「成品入库」分支。**不新增任何口径**，全部复用既有计算：原料锁定/生产执行取 `WorkOrderExecutionSummary` 快照（工单级字段组内 SUM），成品检验取成检看板前 3 档（按生产批去重取首行），成品入库取 `InventoryBatch(OrderFinished)` + `SalesOut` 实时；页面树根展示订单头要素、主号节点可折叠（规格要素 + 执行关注/紧急/预计完成两行头）。
> 历史变更（V46）：全站移动端「手机查看」前两环——① **首页手机看板**：`MobileLayout` 以 `<CascadingValue Name="IsMobile">` 下传，首页 Index 手机分支仿「质量检验」白底小卡（`mh-*`，细节见看板上下文详细设计）；② **横屏提示条**：策略「除首页+扫码流外一律横屏查看」，竖屏进入宽表页顶部 `LandscapeHintBanner` 提示「需横屏查看（旋转手机自动切换）」（可关闭本地持久；夸克/微信锁竖屏浏览器特制文案），宽/窄页判定 `LandscapeHintRule`（宽页 = AppMenu 全部叶子 − 首页 − 两扫码窄叶，见 §6）。菜单结构/页面归属本身未变。
> 历史变更（V45）：前端导航菜单**单源树化**——菜单统一由 `MES.Blazor/Shared/AppMenu.cs`（`AppMenu.Root`）驱动，桌面/手机共用一棵树（见 §6），根治手机菜单漂移（订单组残留「牌号对照」等）；手机横屏大宽（landscape 且 innerWidth≥700）自动切桌面布局（`ResponsiveLayout`）。
> 历史变更（V44）：生产计件类别 #76 模拟测算搜索与金额口径三项增强——工段/工序中文子串反查、元/头折算接线、断切率同源。

---

## 1. 上下文定义

本项目中"上下文"按导航菜单分组定义，共 **11 大业务上下文** + 工具/管理/首页：

| 上下文 | 导航标签 | RBAC 角色 | 页面数 | 列表页数 |
|-------|---------|----------|-------|---------|
| 首页 | 首页 | 所有 | 1 | 0 |
| 订单 | 订单管理 | OrderViewer/Editor/Full + Admin | 8 | 3 |
| 工单 | 工单管理 | WorkOrderViewer/Editor/Full + Admin | 18 | 6 |
| 计划排程 | 计划排程 | SchedulingViewer/Editor/Full + Admin | 6 | 5 |
| 批次 | 批次管理 | BatchViewer/Editor/Full + Admin | 15 | 7 |
| 质量 | 质量管理 | QualityViewer/Editor/Full + Admin | 32 | 16 |
| 物料 | 物料管理 | MaterialViewer/Editor/Full + Admin | 9 | 4 |
| 仓库 | 仓库管理 | WarehouseViewer/Editor/Full + Admin | 7 | 4 |
| 设备 | 设备管理 | EquipmentViewer/Editor/Full + Admin | 8 | 4 |
| 生产标准 | 生产标准 | StandardViewer/Editor/Full + Admin | 18 | 9 |
| 报表系统 | (已并入仓库管理) | ReportViewer/Editor/Full + Admin | 0 | 0 |
| 数据工具 | (独立按钮) | DataToolViewer/Editor/Full + Admin | 2 | 0 |
| 扫码报工 | (独立按钮) | 所有（仅登录） | 1 | 0 |
| 设备扫码 | (独立按钮) | 所有（仅登录） | 1 | 0 |
| 配置 | 参数表 | ConfigurationViewer/Editor/Full + Admin | 13 | 13 |
| 工资结算 | 工资结算 | SalaryViewer/Editor/Full + Admin | 11 | 10 |
| 用户管理 | (Admin按钮) | UserViewer/Editor/Full + Admin | 1 | 0 |

---

## 2. 各上下文详细页面清单

### 2.1 订单上下文

```
路由前缀: /orders, /customers, /orders/progress, /orders/pending-delivery
菜单: 订单管理 → [订单列表, 客户管理, 订单成品(实时库存)]

┌─ 订单管理 ───────────────────────────────────────────────┐
│                                                           │
│  Orders.razor          /orders              [列表页+内联编辑]│
│  OrderProgress.razor   /orders/progress     [进度树子页,行内进入]│
│  OrderCreate.razor     /orders/create       [创建页]       │
│  OrderDetail.razor     /orders/{Id:int}     [详情页]       │
│  ProductRequirement.razor /orders/{orderId:int}/requirements [子页] │
│                                                           │
│  Customers.razor       /customers           [列表页]       │
│  CustomerCreate.razor  /customers/create    [创建页]       │
│                                                           │
│  PendingDelivery.razor  /orders/pending-delivery [列表页]  │
│                                                           │
│  列表页: Orders, Customers, PendingDelivery               │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

### 2.2 工单上下文

```
路由前缀: /workorders, /material-plan-overview, /material-input-consistency,
         /workorders-demand-adjustment, /workorder-execution, /fixed-length-work-order-view
菜单: 工单管理 → 工单操作[工单生成 /workorders, 用料计划 /material-plan-overview, 用料投料核查 /material-input-consistency,
          工单需求调整 /workorders-demand-adjustment]
           · 工单查询[工单执行状况 /workorder-execution, 定尺工单定尺 /fixed-length-work-order-view]

┌─ 工单管理 ───────────────────────────────────────────────┐
│                                                           │
│  WorkOrders.razor          /workorders         [列表页+内联编辑]│
│  WorkOrderGenerate.razor   /workorders/generate [功能页]   │
│  WorkOrderRelation.razor   /workorders/relation [功能页]   │
│  WorkOrderDetail.razor     /workorders/{Id:int} [详情页]   │
│                                                           │
│  子页（嵌入在WorkOrderDetail中导航）:                        │
│  WorkOrderMaterialPlan.razor     /workorders/{id}/material-plan │
│  WorkOrderMaterialPlanCreate.razor /workorders/{id}/material-plan/create │
│  WorkOrderPiercingPlanCreate.razor /workorders/{id}/piercing-plan/create │
│  WorkOrderPiercingPlanEdit.razor   /workorders/{id}/piercing-plan/edit/{Id:int} │
│  WorkOrderInventoryPlanCreate.razor /workorders/{id}/inventory-plan/create │
│  WorkOrderFinishPlanCreate.razor   /workorders/{id}/finish-plan/create │
│  WorkOrderReworkPlanCreate.razor   /workorders/{id}/rework-plan/create │
│  WorkOrderInProcessReworkPlanCreate.razor /workorders/{id}/in-process-rework-plan/create │
│  WorkOrderInMainWorkOrderPlanCreate.razor /workorders/{id}/in-main-work-order-plan/create │
│                                                           │
│  MaterialPlanOverview.razor /material-plan-overview [列表页]│
│  MaterialInputConsistency.razor /material-input-consistency [列表页]│
│                                                           │
│  WorkOrderExecution.razor         /workorder-execution            [列表页]  │
│                                                           │
│  OrderDemandAdjustment.razor      /workorders-demand-adjustment        [列表页]  │
│                                                           │
│  FixedLengthWorkOrderView.razor   /fixed-length-work-order-view  [列表页]  │
│                                                           │
│  列表页: WorkOrders, MaterialPlanOverview, MaterialInputConsistency,     │
│          WorkOrderExecution, OrderDemandAdjustment, FixedLengthWorkOrderView  │
│  ※ 页面文件在 Pages/WorkOrders/ 目录                        │
└───────────────────────────────────────────────────────────┘
```

### 2.3 计划排程上下文

```
路由前缀: /plan-overview, /raw-material-lock-plan, /scheduling-plans, /cold-roll-plans, /batch-plans, /final-inspection-plan
菜单: 计划排程 → [负载总览, 原锁计划, 工单排程, 冷轧排程, 批次计划, 成检计划]

┌─ 计划排程 ─────────────────────────────────────────────┐
│                                                           │
│  PlanOverview.razor                  /plan-overview                    [只读聚合] │
│  RawMaterialLockPlanAndExecution.razor /raw-material-lock-plan      [列表页]     │
│  WorkOrderSchedules.razor           /scheduling-plans                [列表页]     │
│  ColdRollPlans.razor                /cold-roll-plans                [列表页]     │
│  BatchPlans.razor                   /batch-plans                    [列表页]     │
│  FinalInspectionPlan.razor          /final-inspection-plan          [列表页]     │
│                                                           │
│  列表页: RawMaterialLockPlanAndExecution,                 │
│          WorkOrderSchedules, ColdRollPlans, BatchPlans,   │
│          FinalInspectionPlan                              │
│  只读聚合: PlanOverview（MudTable 客户端模式，无分页/排序/筛选）│
│  ※ 已删除独立页面（数据改经批次计划页内嵌折叠与报表总览消费， │
│     后端接口保留）：                                       │
│     SectionProductionStatus, SectionParagraphFlowAnalysis │
└───────────────────────────────────────────────────────────┘
```

### 2.4 批次上下文

```
路由前缀: /batches, /production-execution-check, /production-records, /section-outsources,
         /outsource-recoveries, /pickling-in-records, /pickling-out-records, /process-card-print
菜单: 批次管理 → [生产批次, 生产执行核查, 生产记录, 去油酸洗, 工段委外, 工艺卡打印]

┌─ 批次管理 ───────────────────────────────────────────────┐
│                                                           │
│  Batches.razor              /batches           [列表页]     │
│  BatchCreate.razor          /batches/create    [创建页]     │
│  BatchDetail.razor          /batches/{Id:int}  [详情页]     │
│  BatchEdit.razor            /batches/{Id:int}/edit [编辑页] │
│  ProductionExecutionCheck.razor /production-execution-check [列表页]│
│                                                           │
│  ProductionRecords.razor    /production-records [列表页]     │
│  ProductionRecordCreate.razor /production-records/create [创建页]│
│                                                           │
│  SectionOutsources.razor    /section-outsources [列表页]     │
│  SectionOutsourceCreate.razor /section-outsources/create [创建页]│
│  OutsourceRecoveryCreate.razor /section-outsources/create-recovery [创建页]│
│                                                           │
│  OutsourceRecoveries.razor  /outsource-recoveries [列表页]   │
│                                                           │
│  PicklingInRecords.razor    /pickling-in-records [列表页]                 │
│  PicklingInRecordCreate.razor /pickling-in-records/create [创建页]        │
│  PicklingOutRecords.razor   /pickling-out-records [列表页]                │
│                                                           │
│  ProcessCardPrint.razor     /process-card-print [功能页]     │
│                                                           │
│  列表页: Batches, ProductionExecutionCheck, ProductionRecords, │
│          SectionOutsources, OutsourceRecoveries,             │
│          PicklingInRecords, PicklingOutRecords               │
│  ※ Batches.razor 实现了通知轮询（StartNotificationPollingAsync），│
│    每30秒检查工单变更通知；原顶部「批次-错疑执行」折叠卡片已迁出至 │
│    生产执行核查页（本页 4 错疑派生列自 V68 默认隐藏，列选择器   │
│    开启后仍可列头手动筛选）                                      │
│  ※ ProductionExecutionCheck.razor 承载「错疑-生产批次执行」聚合卡  │
│    （即原「批次-错疑执行」，2026-09-09 改名；4 类错疑批次数+领料重量 │
│    合计，默认折叠，点开才懒加载，BatchCount>0 点选联动筛列表，可取消 │
│    筛选）+ 只读精简批次列表（服务端分页，列组=批次与工单（批次与执行 │
│    及关联工单合并，2026-09-08 裁剪 当前工序/当前工段/截止执行日/工段 │
│    完工/订单号/主号；挂牌号/次号隐藏）/ 执行核查(4 灯：匹配工单/工段 │
│    流转/投料需调整/成切存疑 必显)/ 理论产出对照（错疑缘由数据对照：  │
│    过程检理论成支/现理论成支/理论成品重/成切需求/成切执行/成切支数； │
│    V62 裁 领料支/重、现有效原料支/重、缺陷-返整量、缺陷-纯次品量，  │
│    V64 再删 过程检成重 列），分组标题栏，仅查看详情跳批次详情，无删 │
│    除/编辑、无通知轮询，顶部无模糊搜索/日期框（2026-09-09 删））    │
│  ※ ProductionRecords.razor 生产记录列表 4 组默认显隐收敛（2026-09-09）：│
│    G1 执行信息 默认仅显 执行日期/生产编号/挂牌号/订单号/主号/工序名称/工段│
│    名称/工厂牌号/制造规格（工单号/执行序号 默认隐藏）；G2 产出数据 默认仅 │
│    显 加工支数/加工重量/产类/平头数/断切倍数/预成切/长度状态/成品长度/符合│
│    工单长度/切后支数（设备名称/班次/操作人 默认隐藏）；G3 工艺参数（固溶温│
│    度/保温时间）与 G4 追溯信息（备注/数据来源/更新日期）整组默认隐藏；列偏 │
│    好键并入 ColumnPrefsVersion="v1"（col_prefs_production-records_v1）强制 │
│    新默认；表格移除 table-min-width+--table-min-width 强制最小宽（原将所有│
│    可见列宽总和锁为表根 min-width 致列过宽），对齐生产批次表格由内容撑开    │
│  ※ PicklingInRecords.razor 去油/酸洗入缸记录（入缸报工入口）2026-09-09│
│    默认显隐收敛（V70 v2）：G1去油/酸洗信息 默认仅显 登记日期/生产编号/ │
│    挂牌号/订单号/主号/工序名称/工段名称/工厂牌号/制造规格/生产支数/生产 │
│    重量/产类（工单号/执行序号/设备名称/班次/操作人/备注/数据来源/更新时 │
│    间 隐）；G2完工信息 状态/完工日期 默认显示、完工班次/完工操作人 默认 │
│    隐藏（V70 按用户决策由整组隐改回两操作信息隐）；列偏好键 v2          │
│    （col_prefs_pickling-in-records_v2）；表格无 table-min-width       │
│  ※ PicklingOutRecords.razor 去油/酸洗完工记录 2026-09-09 默认显隐收敛（V70 │
│    v2）：入缸信息 隐 设备名称（登记日期/生产编号/挂牌号/订单号/主号/工序 │
│    名称/工段名称/工厂牌号/制造规格/生产支数/生产重量 显）+ 完工日期/班次 │
│    /操作人 显；备注/数据来源/更新时间 隐；列偏好键                      │
│    col_prefs_pickling-out-records_v2                                 │
│  ※ SectionOutsources.razor 工段委外 2026-09-09 默认显隐收敛（V70 v2）： │
│    委外信息 隐 要求收回日期/紧急；回收信息 隐 非正常回收(支)/非正常回收  │
│    (重)/回收备注；列偏好键 col_prefs_section-outsources_v2            │
│  ※ OutsourceRecoveries.razor 委外回收 2026-09-09 默认显隐收敛（V70 首并 │
│    v1）：回收信息 隐 数据来源/更新时间；列偏好键                       │
│    col_prefs_outsource-recoveries_v1                                 │
│  ※ ProcessCardPrint.razor /process-card-print 批次选择列表列序 2026-09-09 │
│    （V70 v2）：「当前规格」列移至「当前工序」后（对齐生产批次现执行状态 │
│    组列序）；列偏好键 col_prefs_process-card-print_list_v2            │
│  ※ BatchDetail.razor 区块三"批次执行进度"：工序组横排卡最右端追加│
│    "成品检验"9项组（InspectionItem×9，必检/预角标/日期/检验员/4值，│
│     正式成检为主）；工段卡明细多行化（入缸-出缸/发出-回收/检验4值等）│
└───────────────────────────────────────────────────────────┘
```

### 2.5 质量上下文

```
路由前缀: /quality/furnace, /quality/process-inspection, /quality/material-receive-checks, /quality/final-inspection,
         /quality/process-tracking, /quality/ncr,
         /quality/chemical-analysis, /quality/hardness-test, /quality/grain-size-test,
         /quality/pitting-corrosion-test, /quality/intergranular-corrosion-test,
         /quality/tensile-test, /quality/metallographic-test,
         /quality/flattening-test, /quality/flaring-test,
         /quality/lab-testing, /quality/certificates
菜单: 质量管理 → [过程检验, 成检到料, 成品检验, 成检追踪, 不合格报告, 炉号/化学(子组), 理化检测, 质量证明书]
      炉号/化学子组: [炉号登记]

┌─ 质量管理 ───────────────────────────────────────────────┐
│                                                           │
│  FurnaceRegistrations.razor       /quality/furnace              [列表页]│
│  FurnaceRegistrationCreate.razor  /quality/furnace/create       [创建页]│
│                                                           │
│  ProcessInspections.razor         /quality/process-inspection   [列表页]│
│  ProcessInspectionCreate.razor    /quality/process-inspection/create [创建页]│
│                                                           │
│  MaterialReceiveChecks.razor        /quality/material-receive-checks      [列表页]│
│  MaterialReceiveCheckCreate.razor   /quality/material-receive-checks/create [创建页]│
│                                                           │
│  FinalInspections.razor           /quality/final-inspection     [列表页]│
│  FinalInspectionCreate.razor      /quality/final-inspection/create [创建页]│
│                                                           │
│  QualityProcessTracking.razor     /quality/process-tracking    [列表页]│
│                                                           │
│  Ncrs.razor                      /quality/ncr                      [列表页+分页汇总]│
│  NcrForm.razor                   /quality/ncr/create               [创建页]       │
│  NcrForm.razor                   /quality/ncr/{Id:int}             [详情页]       │
│                                                           │
│  --- 理化检测模块 ---                                      │
│  ChemicalAnalyses.razor          /quality/chemical-analysis    [列表页]      │
│  ChemicalAnalysisCreate.razor    /quality/chemical-analysis/create [创建页]  │
│  HardnessTests.razor             /quality/hardness-test        [列表页]      │
│  HardnessTestCreate.razor        /quality/hardness-test/create [创建页]      │
│  GrainSizeTests.razor            /quality/grain-size-test      [列表页]      │
│  GrainSizeTestCreate.razor       /quality/grain-size-test/create [创建页]    │
│  PittingCorrosionTests.razor     /quality/pitting-corrosion-test [列表页]    │
│  PittingCorrosionTestCreate.razor /quality/pitting-corrosion-test/create [创建页]│
│  IntergranularCorrosionTests.razor /quality/intergranular-corrosion-test [列表页]│
│  IntergranularCorrosionTestCreate.razor /quality/intergranular-corrosion-test/create [创建页]│
│  TensileTests.razor              /quality/tensile-test         [列表页]      │
│  TensileTestCreate.razor         /quality/tensile-test/create  [创建页]      │
│  MetallographicTests.razor       /quality/metallographic-test  [列表页]      │
│  MetallographicTestCreate.razor  /quality/metallographic-test/create [创建页] │
│  FlatteningTests.razor           /quality/flattening-test      [列表页]      │
│  FlatteningTestCreate.razor      /quality/flattening-test/create [创建页]    │
│  FlaringTests.razor              /quality/flaring-test         [列表页]      │
│  FlaringTestCreate.razor         /quality/flaring-test/create  [创建页]      │
│                                                           │
│  --- 质量证明书模块 ---                                      │
│  Certificates.razor              /quality/certificates        [列表页]      │
│  CertificateCreate.razor         /quality/certificates/create [创建页]      │
│  CertificateDetail.razor         /quality/certificates/{Id:int} [详情页]    │
│  CertificatePrintSettingsDialog.razor  [打印设置对话框，无路由]│
│                                                           │
│  列表页: FurnaceRegistrations, ProcessInspections,           │
│          MaterialReceiveChecks, FinalInspections,            │
│          QualityProcessTracking, Ncrs,                       │
│          ChemicalAnalyses, HardnessTests, GrainSizeTests,    │
│          PittingCorrosionTests, IntergranularCorrosionTests, │
│          TensileTests, MetallographicTests,                  │
│          FlatteningTests, FlaringTests, Certificates         │
└───────────────────────────────────────────────────────────┘
```

### 2.6 物料上下文

```
路由前缀: /purchase-orders, /subcontract-orders, /subcontract-return-items, /suppliers
菜单: 物料管理 → [采购订单, 圆棒穿孔(子项), 子项查询, 供应商管理]

┌─ 物料管理 ───────────────────────────────────────────────┐
│                                                           │
│  PurchaseOrders.razor        /purchase-orders      [列表页+内联编辑]│
│  PurchaseOrderCreate.razor   /purchase-orders/create [创建页]│
│  PurchaseOrderDetail.razor   /purchase-orders/{id:int} [详情页]│
│                                                           │
│  SubcontractOrders.razor     /subcontract-orders   [列表页+内联编辑]│
│  SubcontractOrderCreate.razor /subcontract-orders/create [创建页]│
│  SubcontractOrderDetail.razor /subcontract-orders/{id:int} [详情页]│
│                                                           │
│  SubcontractReturnItems.razor /subcontract-return-items [列表页] │
│                                                           │
│  Suppliers.razor             /suppliers           [列表页]   │
│  SupplierCreate.razor        /suppliers/create    [创建页]   │
│                                                           │
│  列表页: PurchaseOrders, SubcontractOrders, SubcontractReturnItems,│
│          Suppliers                                        │
└───────────────────────────────────────────────────────────┘
```
（物料档案 /materials 主档已删除，2026-08-18）

### 2.7 仓库上下文

```
路由前缀: /warehouse, /warehouse/{Code}, /warehouse/inbound, /warehouse/outbound,
         /warehouse/inbound-history, /warehouse/outbound-history
菜单: 仓库管理 → [原料库, 成品库, 在制品库, 次品库, 物料进出存报表]
      （报表系统已删除并入仓库管理；原「待发货项」已迁至订单管理上下文，更名「订单成品(实时库存)」）

┌─ 仓库管理 ───────────────────────────────────────────────┐
│                                                           │
│  WarehouseInventory.razor     /warehouse          [列表页]   │
│  WarehouseInventory.razor     /warehouse/{Code}   [列表页(复用)]│
│  仓库类型Code: raw(原料库) / fg(成品库) / defect(次品库) / wip(在制品库)  │
│                                                           │
│  WarehouseInbound.razor       /warehouse/inbound  [功能页]   │
│  WarehouseInbound.razor       /warehouse/inbound/{Code} [功能页]│
│                                                           │
│  WarehouseOutbound.razor      /warehouse/outbound [功能页]   │
│                                                           │
│  InboundHistory.razor         /warehouse/inbound-history      [列表页]│
│  InboundHistory.razor         /warehouse/inbound-history/{Code}[列表页(复用)]│
│                                                           │
│  OutboundHistory.razor        /warehouse/outbound-history      [列表页]│
│  OutboundHistory.razor        /warehouse/outbound-history/{Code}[列表页(复用)]│
│  MonthlyStock.razor          /warehouse/monthly-stock          [报表页]   │
│  报表页: MonthlyStock（原生 table，4 报表切换：入库/出库/库存/物料进出存；入库按来源展开、出库按类型展开（含物料汇总合并列）；行=库房×物料类型，库房+物料类型双层合并单元格，无合计行；当前月之后月份单元格留空，「实时结存/实时数据」=截至当前月合计，三值格「入/出,[结]」如 80/15,[65]；打印横向 A4 撑满页宽）│
│  ※ API: InventoryController (api/inventory/monthly-stock-summary) │
│                                                           │
│  列表页: WarehouseInventory, InboundHistory, OutboundHistory  │
│  注: Code参数路由复用同一页面文件，仅查询时区分仓库类型       │
└───────────────────────────────────────────────────────────┘
```

### 2.8 设备上下文

```
路由前缀: /equipment, /repair-orders, /maintenance-orders, /inspection-records
菜单: 设备管理 → [设备台账, 维修工单, 保养工单, 点检记录]

┌─ 设备管理 ───────────────────────────────────────────────┐
│                                                           │
│  Equipments.razor            /equipment          [列表页]   │
│  EquipmentCreate.razor       /equipment/create   [创建页]   │
│                                                           │
│  RepairOrders.razor          /repair-orders      [列表页]   │
│  RepairOrderCreate.razor     /repair-orders/create [创建页] │
│                                                           │
│  MaintenanceOrders.razor     /maintenance-orders [列表页]   │
│  MaintenanceOrderCreate.razor /maintenance-orders/create [创建页]│
│                                                           │
│  InspectionRecords.razor     /inspection-records [列表页]   │
│  InspectionRecordCreate.razor /inspection-records/create [创建页]│
│                                                           │
│  列表页: Equipments, RepairOrders, MaintenanceOrders,      │
│          InspectionRecords                                  │
└───────────────────────────────────────────────────────────┘
```

### 2.9 生产标准上下文

```
路由前缀: /standard-registers, /grade-mappings, /grade-chemical-compositions, /grade-physical-properties, /sub-standard-quick-views, /standard-inspection-requirements, /factory-inspection-requirements, /chemical-composition, /chemical-validate
菜单: 生产标准 → [标准号列表, 标准号检验项要求, 工厂检验项要求, 牌号对照, 标准牌号化学成分, 工厂牌号化学成分, 工厂牌号化分验证, 牌号物理性能, 子标准速览]

┌─ 生产标准 ───────────────────────────────────────────────┐
│                                                           │
│  StandardRegisters.razor          /standard-registers          [列表页]     │
│  StandardRegisterDetail.razor     /standard-registers/create   [创建页]    │
│  StandardRegisterDetail.razor     /standard-registers/{Id:int} [详情页]    │
│                                                           │
│  GradeMappings.razor              /grade-mappings              [列表页+内联编辑]│
│  GradeMappingCreate.razor         /grade-mappings/create       [创建页]    │
│                                                           │
│  GradeChemicalCompositions.razor     /grade-chemical-compositions     [列表页+内联编辑]│
│  GradeChemicalCompositionCreate.razor /grade-chemical-compositions/create [创建页]│
│                                                           │
│  GradePhysicalProperties.razor       /grade-physical-properties     [列表页+内联编辑]│
│  GradePhysicalPropertyCreate.razor   /grade-physical-properties/create [创建页]│
│                                                           │
│  SubStandardQuickViews.razor         /sub-standard-quick-views     [列表页]     │
│  SubStandardQuickViewCreate.razor    /sub-standard-quick-views/create [创建页]  │
│                                                           │
│  StandardInspectionRequirements.razor  /standard-inspection-requirements [列表页]  │
│  StandardInspectionRequirementCreate.razor /standard-inspection-requirements/create [创建页]│
│                                                           │
│  FactoryInspectionRequirements.razor   /factory-inspection-requirements [列表页+内联编辑]│
│  FactoryInspectionRequirementCreate.razor /factory-inspection-requirements/create [创建页]│
│                                                           │
│  ChemicalCompositions.razor       /chemical-composition  [列表页]  │
│  ChemicalCompositionCreate.razor  /chemical-composition/create [创建页]  │
│                                                           │
│  ChemicalValidationRules.razor    /chemical-validate     [列表页]  │
│  ChemicalValidationRuleCreate.razor /chemical-validate/create [创建页]  │
│                                                           │
│  列表页: StandardRegisters, StandardInspectionRequirements, │
│          FactoryInspectionRequirements, GradeMappings,      │
│          GradeChemicalCompositions, GradePhysicalProperties,│
│          SubStandardQuickViews, ChemicalCompositions,      │
│          ChemicalValidationRules                           │
│  ※ StandardRegisterDetail 双模式：Id=0 创建，Id>0 查看/编辑│
│  ※ 详情页含子项目内联表格（StandardRegisterItem）           │
│  ※ StandardRegister Save/SaveItem 返回 int（Id），防子项 SeqNo 重复创建 │
│  ※ GradeMappings 原属订单上下文，2026-06-21 迁移至此      │
│  ※ GradeChemicalCompositions/GradePhysicalProperties 为     │
│     2026-06-21 新增，按 StandardGrade+Category 纯逻辑关联   │
│  ※ ChemicalCompositions/ChemicalValidationRules 原属质量上下文，│
│     已迁移至生产标准上下文，路由同步更新为生产标准前缀        │
│  ※ StandardInspectionRequirements/SubStandardQuickViews      │
│     2026-07-15 全列筛选支持（23 列 ExcelFilter）             │
│  ※ GradeChemicalCompositions/GradePhysicalProperties         │
│     2026-07-15 全列筛选支持（17列/12列 ExcelFilter）         │
│  ※ FactoryInspectionRequirements 2026-08-15 新增，29 检验字段 │
│     内联编辑 + 打印；作为订单技术要求默认值数据源            │
└───────────────────────────────────────────────────────────┘
```

### 2.10 配置上下文

```
路由前缀: /section-paragraph-config-settings, /daily-production-capacities, /daily-output-estimates, /standard-work-days, /standard-work-day-delivery-states, /process-definitions, /enum-display-definitions, /dict-value-definitions, /config-parameters, /workstations, /employees, /cold-roll-capacities, /cold-roll-machine-configs, /cold-roll-machine-group-configs
菜单: 扫码管理 → [扫码报工, 设备扫码, 工位管理, 员工管理]；参数表 → [工序组定义(批次/工艺), 工段工量天数(排程/用料), 交货状态附加天数(排程/用料), 规格日产预估(工单执行), 冷轧产能档案(冷轧排程), 冷轧机台数配置(冷轧排程), 冷轧机台组配置(冷轧排程), 重点工段日产(生产总览), 段落日产配置(段落流转), 枚举显示配置(全局显示), 字典显示配置(全局显示), 系统参数(全局参数)]

┌─ 系统配置 ───────────────────────────────────────────────┐
│                                                           │
│  SectionParagraphConfigSettings.razor /section-paragraph-config-settings [3类配置驱动自动生成+Tab筛选+仅参数可编辑]│
│  DailyProductionCapacities.razor    /daily-production-capacities      [列表页+内联编辑]│
│  DailyOutputEstimates.razor         /daily-output-estimates           [列表页+内联编辑]│
│  StandardWorkDays.razor              /standard-work-days                [列表页+内联编辑]│
│  StandardWorkDayDeliveryStates.razor /standard-work-day-delivery-states [列表页+内联编辑]│
│  ProcessDefinitions.razor            /process-definitions              [列表页+内联编辑]│
│  ColdRollCapacities.razor            /cold-roll-capacities             [列表页+内联编辑]│
│  ColdRollMachineConfigs.razor       /cold-roll-machine-configs        [列表页+内联编辑]│
│  ColdRollMachineGroupConfigs.razor  /cold-roll-machine-group-configs  [列表页+内联编辑]│
│  EnumDisplayDefinitions.razor        /enum-display-definitions         [列表页+内联编辑]│
│  DictValueDefinitions.razor          /dict-value-definitions           [列表页+内联编辑]│
│  ConfigParameters.razor             /config-parameters                [列表页+内联编辑]│
│  Workstations.razor                 /workstations                     [列表页+内联编辑]│
│  Employees.razor                    /employees                        [列表页+内联编辑]│
│                                                           │
│  列表页: SectionParagraphConfigSettings, DailyProductionCapacities,   │
│          DailyOutputEstimates, StandardWorkDays,            │
│          StandardWorkDayDeliveryStates, ProcessDefinitions, │
│          ColdRollCapacities, ColdRollMachineConfigs,        │
│          EnumDisplayDefinitions, DictValueDefinitions,      │
│          ConfigParameters, Workstations, Employees           │
│  注: AdminOnly，所有业务模块引用其参数参与工量/业务计算          │
└───────────────────────────────────────────────────────────┘
```

### 2.11 工资结算上下文

```
路由前缀: /payroll
菜单: 工资结算 → [生产计件标准, 成检计件标准, 考勤表, 杂辅工记录, 集体计件评分, 津贴与处罚, 非计件工资, 个人计件工资, 集体计件月结, 靠工计件月结, 月工资津贴汇总]
     （独立主菜单，位于扫码管理下方、参数表上方）

┌─ 工资结算 ───────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                                                                                              │
│ Attendance.razor                /payroll/attendance                 [考勤表月视图网格页]                     │
│ MonthlyWages.razor              /payroll/wages/non-piece            [非计件工资月视图网格页]          │
│                                /payroll/wages/piece                 [个人计件工资月视图网格页（单组件双路由）] │
│ CollectiveScores.razor          /payroll/collective-scores          [集体计件评分页]                  │
│ CollectiveMonthly.razor         /payroll/collective-monthly         [集体计件月结页]                  │
│ PieceAttendanceMonthly.razor    /payroll/attendance-monthly         [靠工计件月结页]                  │
│ MiscWorkMonthly.razor           /payroll/misc-work                  [杂辅工记录台账页]                │
│ AllowanceMonthly.razor          /payroll/allowance                  [津贴与处罚月度网格页]            │
│ MonthlySummary.razor            /payroll/monthly-summary            [月工资津贴汇总页]                    │
│ PieceRateProductionCategories.razor  /payroll/piece-rate-categories  [生产计件标准列表页]                    │
│ PieceRateProductionCategoryEdit.razor                                                                        │
│         /payroll/piece-rate-categories/create        [创建页]                                                │
│         /payroll/piece-rate-categories/edit/{Id:int}  [编辑页]                                               │
│ FinalInspectionCategories.razor   /payroll/final-inspection-categories  [成检计件标准列表页]                  │
│ FinalInspectionCategoryEdit.razor /payroll/final-inspection-categories/create、/edit/{Id:int} [成检计件标准编辑页]│
│                                                                                                              │
│ 列表页: Attendance, PieceRateProductionCategories                                                            │
│  计件类别体系 = 「类别主表 + 维档子表」两表模型：类别 = 工段×工序/产类/阶段约束（空=全选；生产计件）或成检项目（成检单键）+ 基准价 + 结算单位 + 启停；
│  结算单价 = 基准价 × 命中维档系数连乘（不配某维 = 系数 1）；同覆盖仅允许一个启用类别；
│  编辑页 = 上区类别定义 + 下区同页整组编辑维档（区间/等值维），同维重叠/重复本地标红、跨类别覆盖冲突服务端权威校验，保存整类一次落库（删除级联删档）。
│  各页特性明细（模拟测算按记录点选计价、每日工资引擎带出/保存快照、集体/靠工月结重算、津贴整元规约、月汇总整表打印等）见 §3 #76-#85；考勤月视图网格见本块首行 Attendance。
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.12 其他页

```
┌─ 其他 ────────────────────────────────────────────────────┐
│                                                           │
│  Index.razor               /                  [首页看板（严格 2×2：工单执行/批次生产/质量检验/设备维修）]│
│  Login.razor               /login             [登录页]       │
│  DataExchange.razor        /data-exchange     [数据工具]     │
│  ScanExecute.razor         /mobile-report     [扫码报工]     │
│  EquipmentRepair.razor     /equipment-repair  [扫码报修]     │
│  RepairExecute.razor       /repair-execute    [扫码维修]     │
│  EquipmentScan.razor       /equipment-scan    [设备扫码入口]   │
│  Admin/Users.razor         /admin/users         [用户管理]    │
│                                                           │
│  导航栏：左侧 MudNavMenu 树形导航（200px 宽）            │
│          首页                                          │
│          ▸ 订单管理 / ▸ 工单管理 / ▸ 计划排程            │
│          ▸ 批次管理                                     │
│          ▾ 质量管理（2 级嵌套）                          │
│            过程检验 / 成检到料 / 成品检验 / 成检追踪      │
│            不合格报告                                   │
│            ▾ 炉号/化学 → 炉号登记                    │
│            理化检测 / 质量证明书（质量证明书移至理化检测下方）│
│          ▸ 物料管理 / ▸ 仓库管理 / ▸ 设备管理            │
│          ▸ 生产标准 → 标准号列表 / 标准号检验项要求 /   │
          │            牌号对照 / 标准牌号化学成分 /            │
          │            工厂牌号化学成分 / 工厂牌号化分验证 /   │
          │            牌号物理性能 / 子标准速览             │
│          数据工具                                        │
│          ▸ 扫码管理 → 扫码报工 / 设备扫码 / 工位管理 / 员工管理 │
│          ▸ 工资结算 → 生产计件标准 / 成检计件标准 / 考勤表 / 杂辅工记录 / │
│                       集体计件评分 / 津贴与处罚 / 非计件工资 / 个人计件工资 / │
│                       集体计件月结 / 靠工计件月结 / 月工资津贴汇总 │
│          ▸ 参数表 → 工序组定义(批次/工艺) / 工段工量天数(排程/用料) │
│                    交货状态附加天数(排程/用料) / 规格日产预估(工单执行) │
│                    冷轧产能档案(冷轧排程) / 冷轧机台数配置(冷轧排程) │
│                    冷轧机台组配置(冷轧排程)               │
│                    重点工段日产(生产总览) / 段落日产配置(段落流转) │
│                    枚举显示配置(全局显示) / 字典显示配置(全局显示) │
│                    系统参数(全局参数)                    │
│          用户管理                                        │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

---

## 3. 列表页完整清单（需检查加载/排序/筛选）

共 **87 个列表页**，采用 `ServerData` + `ExcelFilter` 模式：

| # | 页面文件 | 路由 | 上下文 | 内联编辑 | 备注 |
|---|---------|------|-------|---------|------|
| 1 | Orders.razor | /orders | 订单 | ✅ | B23 列分组3组(基本信息/订单确认/订单执行)；「基本信息」=原基本信息+合同交付合并，默认仅显示订单号/签订日期/业务员/客户名称/交期截止/订单总重量/含项次数，余(最终客户/交期起始/延期罚款等)默认隐藏 + ExcelFilter + B23分组标题栏 + 搜索栏3组(模糊搜索+签订日期+交货日期) + 工具栏「完成预估及延期风险」折叠卡片（仅两张交期预估小表：订单(整单)完成预估 / 风险-已延期订单(整单)，逐格联动筛选订单列表、各表可打印；原「接单-出库及现负荷」5指标×12月小表已删除，页面标题=订单列表） |
| 2 | Customers.razor | /customers | 订单 | ✅(①组档案列内联) | B23 列分组 2 组 + 底部合计 + 完整打印：① 基本信息（默认仅显 业务员/最终用户/状态，客户编码/客户单位/联系人/电话/地址/备注默认隐藏，内联编辑）；② 往来信息（8 业务统计只读列，服务层按「业务员+最终用户」聚合实时注入，仅 GetPagedAsync 回填；8 列均按「X单/吨/万」三位一体显示（单数=落入该状态桶的订单张数，可跨阶段并列），吨/万保留 1 位小数；金额按结算分治：过磅=实际公斤不封顶、理算/过磅-负=封顶合同额发货→库存→在产阶梯认领；待在产两桶仅统计主号未完成(阶段≠1)订单（已完成单欠产/未入库不计在产）；统计列不可排序/筛选（SortKey=null 标记）；底部合计仿订单=②组数值列页内合计；打印选中=Mode A 列表 PDF 按可见列含统计列完整打印 → print-list-file；ColumnPrefsVersion=v2） |
| 3 | GradeMappings.razor | /grade-mappings | 生产标准 | | |
| 4 | WorkOrders.razor | /workorders | 工单 | ✅ | 2026-09-08 默认隐藏 次号/最终客户/钢管制造（列偏好键升 col_prefs_workorders_v1） |
| 5 | MaterialPlanOverview.razor | /material-plan-overview | 工单 | | |
| 6 | **Batches.razor** | /batches | 批次 | | ✅ 已过规范检查 |
| 7 | **ProductionRecords.razor** | /production-records | 批次 | ✅ | 列分组4组（G1执行信息/G2产出数据/G3工艺参数/G4追溯信息）+ ExcelFilter + 内联编辑 + 分组标题栏；**2026-09-09 默认显隐收敛**：G1仅显 执行日期/生产编号/挂牌号/订单号/主号/工序名称/工段名称/工厂牌号/制造规格（工单号/执行序号 隐），G2仅显 加工支数/加工重量/产类/平头数/断切倍数/预成切/长度状态/成品长度/符合工单长度/切后支数（设备名称/班次/操作人 隐），G3工艺参数/G4追溯信息 整组隐；列偏好键 `ColumnPrefsVersion="v1"`（`col_prefs_production-records_v1`）；**列宽修复**：表格移除 `table-min-width`+`--table-min-width`（原把表根 min-width 锁到全部可见列宽总和致过宽），与生产批次表格设置对齐 |
| 8 | **SectionOutsources.razor** | /section-outsources | 批次 | | 2026-09-09 默认列显隐收敛（列偏好键 col_prefs_section-outsources_v2）：委外信息 隐 要求收回日期/紧急；回收信息 隐 非正常回收(支)/非正常回收(重)/回收备注 |
| 9 | **OutsourceRecoveries.razor** | /outsource-recoveries | 批次 | | 2026-09-09 默认列显隐收敛（首并列偏好键 col_prefs_outsource-recoveries_v1）：回收信息 隐 数据来源/更新时间 |
| 10 | PicklingInRecords.razor | /pickling-in-records | 批次 | | 去油/酸洗入缸记录（入缸报工）；2026-09-09 默认列显隐收敛 + 移除 table-min-width，2026-09-09 二调（列偏好键 v1→v2）：G1 默认仅显 登记日期/生产编号/挂牌号/订单号/主号/工序名称/工段名称/工厂牌号/制造规格/生产支数/生产重量/产类（工单号/执行序号/设备名称/班次/操作人/备注/数据来源/更新时间 隐）；G2 状态/完工日期 默认显示、完工班次/完工操作人 默认隐藏（由整组隐改回，col_prefs_pickling-in-records_v2） |
| 11 | PicklingOutRecords.razor | /pickling-out-records | 批次 | | 去油/酸洗完工记录；2026-09-09 默认隐 备注/数据来源/更新时间 + 移除 table-min-width，2026-09-09 二调（列偏好键 v1→v2）：入缸信息 增隐 设备名称（col_prefs_pickling-out-records_v2） |
| 12 | FurnaceRegistrations.razor | /quality/furnace | 质量 | | 2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_furnace-registration_v1） |
| 13 | ChemicalCompositions.razor | /chemical-composition | 生产标准 | | 原属质量上下文，已迁移 |
| 14 | ChemicalValidationRules.razor | /chemical-validate | 生产标准 | | 原属质量上下文，已迁移 |
| 15 | ProcessInspections.razor | /quality/process-inspection | 质量 | | 2026-09-09 默认列显隐收敛（默认显24列）：G1 隐 执行序号、G4 隐 理论返整重/理论入库重/理论报废重/次品情况描述、G5辅助信息整组隐 + 移除 table-min-width（列偏好键 col_prefs_process-inspection_v1）；2026-09-09 二调（列偏好键 v1→v2）：隐 工单号/设备名称/班次（col_prefs_process-inspection_v2） |
| 16 | MaterialReceiveChecks.razor | /quality/material-receive-checks | 质量 | | 2026-09-09 默认列显隐收敛（首并列偏好键 col_prefs_material-receive-checks_v1）：隐 执行序/班次/数据来源/更新时间 |
| 17 | FinalInspections.razor | /quality/final-inspection | 质量 | | 2026-09-09 默认列显隐收敛（63列默认显33列）：G1 隐 资格等级、G2 隐 生产类型/制造物品/制造状态/交货状态/最终用户/来料单位/长度状态、G3 隐 非定尺长度范围、G4不合格处理全显；G5尺寸值/G6压力值/G7涡流超声波/G8辅助信息整组隐 + 移除 table-min-width（列偏好键 col_prefs_final-inspection_v1）；2026-09-09 二调（列偏好键 v1→v2）：隐 设备名称/班次/工单号（更新时间已于 G8 默认隐）（col_prefs_final-inspection_v2） |
| 18 | Equipments.razor | /equipment | 设备 | | |
| 19 | RepairOrders.razor | /repair-orders | 设备 | | |
| 20 | MaintenanceOrders.razor | /maintenance-orders | 设备 | | |
| 21 | InspectionRecords.razor | /inspection-records | 设备 | | |
| 22 | PurchaseOrders.razor | /purchase-orders | 物料 | ✅ | |
| 23 | SubcontractOrders.razor | /subcontract-orders | 物料 | ✅ | |
| 24 | Suppliers.razor | /suppliers | 物料 | | |
| 25 | WarehouseInventory.razor | /warehouse | 仓库 | | Code复用 |
| 27 | InboundHistory.razor | /warehouse/inbound-history | 仓库 | | Code复用 |
| 28 | OutboundHistory.razor | /warehouse/outbound-history | 仓库 | | Code复用 |
| 29 | WorkOrderExecution.razor | /workorder-execution | 工单 | | ✅ 已过规范检查。列分组14组(G1-G14) + 复选框选择列 + 打印选中+打印全部 + 分组标题栏 + 底部聚合行 + 2026-09-08 基础数据 最终客户/订单日期 默认隐藏（PageState 键升 page_state_workorderexecution-v1） |
| 30 | QualityProcessTracking.razor | /quality/process-tracking | 质量 | | 只读列表；2026-09-09 默认列显隐收敛（首并列偏好键 col_prefs_quality-process-tracking_v1）：隐 工单号/最终用户/班次/更新日期 |
| 31 | Ncrs.razor | /quality/ncr | 质量 | | 列表页+分页汇总；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_ncrs_v1） |
| 32 | OrderDemandAdjustment.razor | /workorders-demand-adjustment | 工单 | ✅ | 内联编辑催单/分批/暂停开关及调整备注（2026-09-08 默认隐藏 订单日期/最终客户/主号-预计完成日/主号-原锁备注；PageState 键升 page_state_workorders-demand-adjustment-v1） |
| 33 | RawMaterialLockPlanAndExecution.razor | /raw-material-lock-plan | 计划排程 | ✅ | G15 预执行 MudSwitch 内联编辑 + BudgetInputDate 日期输入（LEFT JOIN 实时查询，无计划安排按钮）；右上角「待投料量汇总」按钮展开汇总卡片（待投料+成购两矩阵表 + 理论待投料截日，可打印） |
| 34 | StandardWorkDays.razor | /standard-work-days | 配置 | ✅ | 查改一体表 |
| 35 | StandardWorkDayDeliveryStates.razor | /standard-work-day-delivery-states | 配置 | ✅ | 查改一体表 |
| 36 | ConfigParameters.razor | /config-parameters | 配置 | ✅ | 查改一体表 |
| 40 | WorkOrderSchedules.razor | /scheduling-plans | 计划排程 | | LEFT JOIN 实时查询模式（WorkOrderExecutionSummary + WorkOrderPlan 薄表），G15 内联编辑 + 计划安排按钮 + 2026-09-08 最终客户默认隐藏（列偏好键 col_prefs_workorderschedules_v1） |
| 41 | DailyOutputEstimates.razor | /daily-output-estimates | 配置 | ✅ | 查改一体表 |
| 42 | Workstations.razor | /workstations | 配置 | ✅ | 查改一体表 |
| 43 | Employees.razor | /employees | 配置 | ✅ | 查改一体表（列偏好 v5，2026-09-03 靠工计件六期：「靠工系数」前新增**靠工岗位**多选列 AttendancePositions，候选=计件活岗 GET api/employee/piece-positions，保存岗位英文 Key 逗号串，显示逐项中文） |
| 44 | ColdRollPlans.razor | /cold-roll-plans | 计划排程 | | 冷轧按规格维度聚合时间桶分布计划 + 简化/明细视图切换 + 打印功能 + 排程编辑模式（在轧要求/待轧要求/待轧序/待轧设备号/单机单日量）+ **右上角排机估算折叠表（4行×5列，懒加载，可打印）** + **排程建议折叠卡片（半自动：三步决策 特急锁定→流转保底→产能平衡，组级+行级明细表，「一键采用建议」走 save-all 全量同步）** + 搜索栏+ExcelFilter列筛选 |
| 45 | BatchPlans.razor | /batch-plans | 计划排程 | | 全量加载 Items 模式 + **工段筛选 Tab 配置驱动**（`GET api/batch-plan/section-tab-options`：冷轧/冷拔=工序组定义启用冷轧拔工序逐工序、普通工段=工段工量天数启用工段扣除冷轧拔/检验/入库且内抛/内修磨独立、末尾固定荒管检/在制检，2026-08-30 起新增工序自动出现）+ 列分组标题栏 + 列显隐（永久隐藏 22 列：冷轧排程 5 组 + 工单需求调整 + 批次基础信息多余字段）+ 客户端排序/筛选 + 6 项 Tab 汇总（批次数/总重量/计划流转批次/重量/计划重点批次/重量，重点按 PlanFlowLevel==1 急+）+ 汇总重量单位吨(t) + G13 批次计划组只读（仅抢单/计划备注内联编辑） + 2026-09-08 批次基础信息组默认显隐（v2→v3）：制造状态换源默认显示、交货状态/长度状态默认隐藏、重量(kg)→重量 |
| 46 | FinalInspectionPlan.razor | /final-inspection-plan | 计划排程 | | 全量加载 Items 模式 + 五档Tab(全部/待到料/待检验/检验中/完成检验待入库) + 待检批支重汇总卡片（行=检验项，列=检验项/待到料/待检验+检验中/汇总数据，0值显"-"，可打印）+ 客户排序/筛选 + 列分组 G1-G6（G1批次/G2排程/G3成检状态/G4技术要求检验项/G5各项检验日期/G6数量）+ 紧急程度 MudChip 颜色渲染；默认列显隐收敛（V56）：G1 批次仅显 生产编号/生产类型/制造状态/工厂牌号/规格/长度状态/支数/重量/订单号/主号/业务员，G3 成检状态仅显 成检阶段/到料日期，G5 各项检验日期+G6 数量整组默认隐藏，余默认隐藏（可经列选择器打开） |
| 47 | StandardRegisters.razor | /standard-registers | 生产标准 | | ExcelFilter 列筛选 + RenderCell 模板 + FooterContent 分页汇总 + 导航至详情页；Save/SaveItem 返回 Id 防 SeqNo 重复 |
| 48 | GradeChemicalCompositions.razor | /grade-chemical-compositions | 生产标准 | ✅ | 15元素内联编辑 + 全列ExcelFilter(17列) + 列显隐 |
| 49 | GradePhysicalProperties.razor | /grade-physical-properties | 生产标准 | ✅ | 12物理性能字段内联编辑 + 全列ExcelFilter(12列) + 列显隐 |
| 50 | SubStandardQuickViews.razor | /sub-standard-quick-views | 生产标准 | | 全列ExcelFilter(23列)，按标准号快速查看24项检验项目引用标准 |
| 51 | StandardInspectionRequirements.razor | /standard-inspection-requirements | 生产标准 | ✅ | 全列ExcelFilter(23列)，标准号检验项要求+内联编辑 |
| 52 | FactoryInspectionRequirements.razor | /factory-inspection-requirements | 生产标准 | ✅ | 工厂检验项要求，全列ExcelFilter(30列)，29检验字段内联编辑 + 打印（选中+全部） |
| 53 | ChemicalAnalyses.razor | /quality/chemical-analysis | 质量 | | 理化检测-化学分析；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_chemical-analysis_v1） |
| 54 | HardnessTests.razor | /quality/hardness-test | 质量 | | 理化检测-硬度检验；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_hardness-test_v1） |
| 55 | GrainSizeTests.razor | /quality/grain-size-test | 质量 | | 理化检测-晶粒度检验；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_grain-size-test_v1） |
| 56 | PittingCorrosionTests.razor | /quality/pitting-corrosion-test | 质量 | | 理化检测-点腐蚀检验；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_pitting-corrosion-test_v1） |
| 57 | IntergranularCorrosionTests.razor | /quality/intergranular-corrosion-test | 质量 | | 理化检测-晶间腐蚀检验；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_intergranular-corrosion-test_v1） |
| 58 | TensileTests.razor | /quality/tensile-test | 质量 | | 理化检测-室温拉伸检验；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_tensile-test_v1） |
| 59 | MetallographicTests.razor | /quality/metallographic-test | 质量 | | 理化检测-金相检验；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_metallographic-test_v1） |
| 60 | FlatteningTests.razor | /quality/flattening-test | 质量 | | 理化检测-压扁检验；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_flattening-test_v1） |
| 61 | FlaringTests.razor | /quality/flaring-test | 质量 | | 理化检测-扩口检验；2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_flaring-test_v1） |
| 62 | DailyProductionCapacities.razor | /daily-production-capacities | 配置 | ✅ | 查改一体表，仿ConfigParameters模式；行键=荒管抛光固定 Polish + 冷轧机台组 GroupKey（2026-08-30 起配置表驱动下拉） |
| 64 | Certificates.razor | /quality/certificates | 质量 | | 质量证明书列表页（打印选中/打印全部 + 打印设置对话框：打印版式/字段布局） |
| 65 | PendingDelivery.razor | /orders/pending-delivery | 订单 | | 订单成品(实时库存)列表页（原仓库「待发货项」，2026-08-26 迁入订单上下文；订单关联组含「工单关注」列：取工单执行状况读模型主号-关注档位，按工单号关联）；默认隐藏列：工单号/最终客户/产品标准/工厂牌号/最小长度/最大长度/仓库批次/来源/来料单位/剩余米数/物料类型 |
| 66 | SubcontractReturnItems.razor | /subcontract-return-items | 物料 | | 委外子项查询—列表页+复选框选择列+打印选中+ExcelFilter全列筛选；字段两组分组（一、委外信息12列含下单日期/要求到货日/委外备注、二、执行状态6列含退货量/属强制完成）；执行状态4档（已发出/部分收回/已完成/超量到货，MudChip与采购订单一致） |
| 67 | FixedLengthWorkOrderView.razor | /fixed-length-work-order-view | 工单 | | 定尺工单联通视图，主号级按长度实时聚合 + 分组标题栏 + 分页汇总（可汇总列：G1需求支数/G3切后支数/G4到料·成切·非成切·次品·合格·合格盈缺/G5入库·入库盈缺，G6主号级聚合不参与求和）；默认隐藏：G3成品切割/G4成检数据/G5成品入库三组 + 基础数据「往来单位·订单日期」（2026-09-08，组头/列显隐可打开） |
| 68 | SectionParagraphConfigSettings.razor | /section-paragraph-config-settings | 配置 | ✅ | 段落日产配置（3类配置驱动自动生成：冷轧拔/普通工段/检验，段落仅参数可编辑），Tab 筛选 |
| 70 | ProcessDefinitions.razor | /process-definitions | 配置 | ✅ | 工序组定义（含默认工段 DefaultSections） |
| 71 | EnumDisplayDefinitions.razor | /enum-display-definitions | 配置 | ✅ | 枚举显示配置（display-map/options-map） |
| 72 | DictValueDefinitions.razor | /dict-value-definitions | 配置 | ✅ | 字典显示配置（display-map/enabled-values） |
| 73 | ColdRollCapacities.razor | /cold-roll-capacities | 配置 | | 冷轧产能档案（四维 ProcessType/BilletSpec/RollingSpec/IsFinished 唯一），查改一体表；排程建议产能平衡输入 |
| 74 | ColdRollMachineConfigs.razor | /cold-roll-machine-configs | 配置 | | 冷轧机台数配置（ProcessType 唯一），查改一体表；排程建议产能平衡输入（方式A兜底 daily） |
| 75 | ColdRollMachineGroupConfigs.razor | /cold-roll-machine-group-configs | 配置 | | 冷轧机台组配置（GroupKey 唯一），归组配置表驱动；工序多选（仅启用的冷轧/冷拔工序，显示走 GetProcessNameText 中文）+供给目标组列（供需链显式化，组角色字段已移除；链合法性校验：凡配目标则目标存在+无环，允许多链/多级链，2030→冷拔(None) 末端合法）；保存/删除失效三引擎缓存键；工序禁用时自动从组内移除（ProcessDefinitionService） |
| 76 | PieceRateProductionCategories.razor | /payroll/piece-rate-categories | 工资结算 | | 生产计件标准列表页（页面标题「生产计件标准（基准价 × 维档系数）」；两表模型：PieceRateProductionCategory + PieceRateProductionTier 维档子表；类别 = 工段×工序/产类/阶段约束 + 基准价 + 维档系数，结算单价 = 类别基准价 × 命中维档系数连乘，不配某维=系数1，工段×工序×产类×阶段同覆盖仅允许一个启用类别）；列：自动组合名/工段中文/基准价(G29)/单位/维档数/是否启用/备注/更新时间/创建时间 + 列显隐/排序；顶部工段下拉 + 启停下拉 + 模糊搜索（自动组合名/工段/备注）；行操作：编辑走独立页 `/payroll/piece-rate-categories/edit/{Id}`、删除弹 ConfirmDialog（级联删维档），新增走 `/payroll/piece-rate-categories/create` |
| 77 | FinalInspectionCategories.razor | /payroll/final-inspection-categories | 工资结算 | | 成检计件标准列表页（页面标题「成检计件标准（基准价 × 维档系数）」）：主表 PieceRateFinalInspectionCategory = 成检项目 InspectionItem 单键 + 基准价 + 单位 + 启停（同项目启用唯一）+ 子表 8 维档（区间 外径/壁厚/长度/检验支数整数闭带 + 等值 长度状态/特殊牌号/特殊制造状态/特殊设备号）；列 + 行内展开「模拟测算」按**成检记录点选计价**：候选=全局任意跨期成检记录，顶部成检项目下拉 + 关键字(生产编号/设备/操作人)，服务端分页记录小表，行「试算」按 Id 计价 → 命中类别 基准价×总系数=单价 + 整行计件额(与月结同口径、未按人头均分)/缺数量灰字提示/未定价提示；手动填维度试算表单已删，match-price 端点保留）+ 专用批量导出/导入弹窗 |
| 78 | FinalInspectionCategoryEdit.razor | /payroll/final-inspection-categories/create、/edit/{Id:int} | 工资结算 | | 成检计件标准编辑页：定义（成检项目单选 + 基准价/单位/启停/备注）+ 8 维档同页整组编辑，保存整类落库（档行整组替换） |
| 79 | MonthlyWages.razor | /payroll/wages/non-piece、/payroll/wages/piece | 工资结算 | | 每日工资两表月视图网格页（单组件双路由）：仿考勤网格（attendance-scroll/grid + enableAttendanceKeyNav）单元格=每日工资额（原生 input 失焦提交），引擎自动带出 + 常编辑 + 显式「引擎重算」（ConfirmDialog 覆盖网格）+「保存本月」落库（Amount>0 存/空删，SalaryMode 归口快照）；非计件=Hourly 小时×时薪 / Daily 日薪×min(出勤,8)/8，个人计件=PieceIndividual 当月产量+成检按现行单价逐行折算（成检合作行按人数均分、Range/NonFixed 定尺 6000mm 兜底、PerTon/PerPiece/PerKm 换算）；顶部 年/月/«»/工号姓名搜索/岗位类别/岗位筛选 + 表头排序；员工集=归口∈组启用员工 ∪ 当月历史快照（换归口历史月仍显示）；写操作 SalaryEdit 门控 |
| 80 | CollectiveScores.razor | /payroll/collective-scores | 工资结算 | | 集体计件评分页：年月选择 → 员工按岗位分组卡片（工号/姓名/岗位/分值输入 1–10 一位小数如 8.5 + 已评分/新录入/未评分备注）→「保存评分」整月 upsert；只显示在册集体成员 + 当月已有评分历史员工补集；写操作 SalaryEdit 门控 |
| 81 | CollectiveMonthly.razor | /payroll/collective-monthly | 工资结算 | | 集体计件月结页：年月选择 → 每岗位结算卡片（成员行 出勤/分值/权重只读 + 实得金额整元可改，默认 已存?Saved:引擎草稿；卡标题=岗位中文+岗位池+Σw）；顶部「引擎重算」(ConfirmDialog 覆盖在册集体成员)/「全量重算(清历史)」(双重确认 PayrollFullRecalcDialogs 清历史快照成员)/「保存本月」；写操作 SalaryEdit 门控 |
| 82 | PieceAttendanceMonthly.razor | /payroll/attendance-monthly | 工资结算 | | 靠工计件月结页：年月选择 → 单张 auto-table 员工行（靠工无岗位池不分组）：工号/姓名/靠工岗位(中文)/出勤/靠工系数/平均时薪(G29 只读)/实得金额(整元可改，默认 已存?Saved:RoundYuan引擎草稿)/备注(历史快照·未配岗·无计件参照·无出勤)；员工集=在册靠工 ∪ 当月快照员工（停用/换模式历史月仍显示）；顶部「引擎重算」/「全量重算(清历史)」双重确认/「保存本月」；写操作 SalaryEdit 门控 |
| 83 | MiscWorkMonthly.razor | /payroll/misc-work | 工资结算 | ✅ | 杂辅工记录台账页：杂项辅助手工登记（完整月工资 = 各类工资 + 杂辅）。MudTable 台账列表页，行=一条杂辅任务（日期/工号/姓名/内容/小时/金额 G29/备注），金额=手工录入源头保留小数不取整、同人同日可多条；行内编辑（编辑不改员工归属）+ 新增面板（员工下拉=全量启用员工）+ ConfirmDialog 删除；顶部 MudPaper 描述文字 + 月份导航（Chevron + 年月 MudSelect）+ 当月合计 chips（N 条 · Σ小时 · Σ金额，整月口径）+ 页内关键词筛选（工号/姓名/内容，客户端，合计不变）；日期 MudTextField string yyyy-MM-dd（禁 MudDatePicker）；写操作 SalaryEdit 门控 |
| 84 | AllowanceMonthly.razor | /payroll/allowance | 工资结算 | ✅ | 津贴与处罚月度网格页：月度金额录入，行=员工、列=固定 9 金额项目（满勤奖/工龄奖/夜班津贴/岗位补贴/高温费/工伤补贴/带班费/处罚/代缴社保，参考 Excel《津贴与处罚.xlsx》），宽表每人每月一行（EmployeeId+Year+Month 唯一）；金额强制整元（RoundYuan AwayFromZero、空/0=null、禁负数，OnCellChanged 即时规约与后端 NormalizeAmount 同口径）；员工月历 = IsActive 在册 ∪ 当月已有记录（停用员工当月行浅灰回显可改）；考勤同款 attendance-grid 宽表（attendance-scroll/grid 类 + 原生 input 每格失焦提交 + enableAttendanceKeyNav 方向键导航复用；sticky-left 工号/姓名/岗位类别/岗位列，岗位中文经 DictValueDisplayHelper）+ tfoot 各列整月合计 + 页内关键词筛选（工号/姓名/岗位，客户端，合计不变）+ @foreach 渲染防闭包 + OverrideMap 事件订阅重渲染；顶部「清空本月」(ConfirmDialog Error，提交空 Rows)「保存本月」(Snackbar 计数) SalaryEdit 门控 |
| 85 | MonthlySummary.razor | /payroll/monthly-summary | 工资结算 | ✅ | 月工资津贴汇总页：员工某结算月「完整应发/实发」汇总表（参考 Excel《工资条及打印.xlsx》17 列：工号/姓名/月份常量/出勤天数/本月基础工资/本月杂辅工资 + 岗位补贴·工龄奖·满勤奖·带班费·夜班津贴·高温费·工伤补贴 7 正津贴 + 处罚·代缴社保(存负)/应发/实发）；基础工资按各子页「已保存金额」归口（Fixed=Employee.MonthlyWage、PieceCollective→集体月结快照、PieceAttendance→靠工月结快照、Hourly/Daily/PieceIndividual→每日工资当月Σ），出勤天数=当月考勤去重日期数；应发=基础+杂辅+7 正津贴，实发=应发+处罚+代缴（后两列存负）；行集=IsActive 在册 ∪ 当月任一来源有行（停用行灰显），工号升序 + 页内关键词（工号/姓名）+ 金额 0 网格留空 + tfoot 列合计；顶部年/月导航 + 已保存/未保存徽标 +「保存本月」(SalaryEdit，整月重算替换快照 PayrollMonthlySummaryRecord 每人每月一行 UK)+「全部打印」A4 横向整表 +「个人打印」每员工一条带表头工资条（两打印读已保存快照、未保存禁用提示「先保存本月」）；写操作 SalaryEdit 门控 |
| 86 | MaterialInputConsistency.razor | /material-input-consistency | 工单 | | 用料投料核查页（2026-09-08 拆分两页之一，独立终态视图）：列组=基础数据 / 实时关注（3 字段主号级）/ 用料及投料（7 列 2026-09-08 拆出：分类用料/分类到料/原料未至/到料未投/生产投料量/投料比/投料状态，除投料状态外不支持排序筛选），默认隐藏 最终用户/最大长度，投料状态超量=chip-dark 深底白字区分满足绿色；**无**待投料汇总卡、**无**计划类型勾选、**无**用料计划列组；两张异常卡「错疑-用料投料不一致」(ErrorDoubt，原料锁定档位) +「错误-用料计划及其执行」(InProductionInspection，主号完成/生产执行/成品检验 三档已过投料期，标题旁附注「以下执行状态无需再投料」) 卡↔主表 ScheduleStage/待料联动筛选（自原用料计划页迁入）；错疑卡重量三列表头=工单重量/计划投料重量/到料重量（到货量口径）；错误卡聚合行首列表头=工单执行状态，聚合列=原料未至/到料未投；仅显示投影，数据层/读模型零改动；列偏好 key `materialInputConsistency_v4`、页面状态 key `materialInputConsistency` |
| 87 | ProductionExecutionCheck.razor | /production-execution-check | 批次 | | 生产执行核查页（2026-09-08 批次管理拆分两入口之一）：承载「错疑-生产批次执行」聚合卡（即原「批次-错疑执行」，2026-09-09 改名；4 类错疑 匹配工单/工段流转/有效投料/成品切割 批次数+领料重量合计，**默认折叠、点开才懒加载**，BatchCount>0 可点选联动筛下列表，可取消筛选；列头错疑列手动筛选同样可用）+ 页内自足精简批次列表（服务端分页，列组=批次与工单（批次与执行及关联工单合并，生产编号/状态/工单号/工单关注 + 挂牌号/次号隐藏；V61 裁剪 当前工序/当前工段/截止执行日/工段完工/订单号/主号 六列）/ 执行核查（**4 灯** 匹配工单/工段流转/投料需调整/成切存疑 必显；V62 成切存疑由投料组回归）/ 理论产出对照（**错疑缘由数据对照**：过程检理论成支·现理论成支/理论成品重·成切需求/执行/支数；V64 取消 过程检成重 列；V63 组名「投料与有效量」改名点题 + 过程检列前移到 现理论成支 前 + 列名精简（过程检理论成品支→过程检理论成支、理论成品支→现理论成支，理论成品重列名保持）；V62 裁 领料支数/领料重量/现有效原料支数/现有效原料重量/缺陷-返整量/缺陷-纯次品量 六列，页底合计）；只读仅「查看详情」跳 /batches/{id}，无删除/编辑，无通知轮询/无打印全部）；**顶部无搜索栏**（2026-09-09 模糊搜索+登记日期整行删除，定位靠错疑卡联动+列头筛选））；列偏好 key `batchExecutionCheck_v6`、页面状态 key `batchExecutionCheck`（排序/列筛选） |

---

## 4. 代码分离状态

所有 `*.razor.cs` code-behind 文件为 **已提交**（committed），列表页从单体 `.razor` 向分离模式迁移已完成。

---

## 5. 规范检查覆盖记录

| 上下文 | 列表页 | 加载 | 排序 | 筛选 | 检查日期 |
|-------|-------|------|------|------|---------|
| 批次 | Batches | ✅ | ✅ | ✅ | 2026-05-22 |
| 批次 | ProductionRecords | ✅ | ✅ | ✅ | 2026-07-08 列分组重构 |
| 批次 | SectionOutsources | ✅ | ✅ | ✅ | 2026-05-23 |
| 批次 | OutsourceRecoveries ⚠️ | ✅ | ✅ | ✅ | 2026-05-23 |
| 工单 | WorkOrderExecution | ✅ | ✅ | ✅ | 2026-07-09 新增选择列+打印 |
| 其他 | ... | ❌ | ❌ | ❌ | 未检查 |

---

## 6. 关键文件路径

| 类别 | 路径 |
|------|------|
| 页面文件 | `MES.Blazor/Pages/{Subdir}/*.razor`（按模块划分子目录） |
| Code-behind | `MES.Blazor/Pages/{Subdir}/*.razor.cs` |
| 前端 Service | `MES.Blazor/Services/*Service.cs` |
| 后端 Service | `MES.Services/{Module}/*Service.cs` |
| API 控制器 | `MES.Api/Controllers/{Module}/*Controller.cs` |
| DTO | `MES.Core/DTOs/*QueryParams.cs` |
| 实体 | `MES.Data/Entities/*.cs` |
| 接口 | `MES.Core/Interfaces/I*Service.cs` |
| 筛选扩展 | `MES.Services/Helpers/QueryableExtensions.cs` |
| 枚举映射 | `MES.Blazor/Helpers/DisplayHelper.cs` |
| ExcelFilter | `MES.Blazor/Components/ExcelFilter.razor` |
| 开发规范 | `docs/04_开发规范.md` |
| 菜单树（单一数据源） | `MES.Blazor/Shared/AppMenu.cs` + `AppMenuNode.cs`（桌面/手机共用 `AppMenu.Root`；**改动菜单只许改这里**，回归断言 `MES.Tests/Components/AppMenuTests.cs`） |
| 布局外壳 / 菜单渲染 | `ResponsiveLayout.razor`（移动判定 + 横屏切桌面）→ `MainLayout.razor`（桌面）/ `MobileLayout.razor`（手机）；菜单渲染 `DesktopMenuNode.razor` / `MobileMenuNode.razor` |
| 移动横屏提示条 | `LandscapeHintBanner.razor`（MobileLayout 顶部壳级组件：竖屏宽表页提示横屏、localStorage 关闭持久、锁竖屏浏览器特制文案）+ `LandscapeHintRule.cs`（宽/窄页判定：宽页 = AppMenu.AllLeaves() − `/` − `/mobile-report`·`/equipment-scan` 窄叶，排除登录/进出货/维修点选窄页与 `/create`·`/edit` 表单路径；防漂移单测 `MES.Tests/Components/LandscapeHintRuleTests.cs`） |

---

> 使用方式：询问关于页面结构、上下文归属、列表页检查范围等问题时，可引用此文档作为参考基础。
