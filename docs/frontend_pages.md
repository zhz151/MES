# MES 前端页面结构参考

> 版本：V147（2026-09-15；生成 2026-08-19）
> 用途/状态：Quick Reference - 按导航菜单分组的前端页面结构参考，§1 上下文总览 / §2 各上下文页面块 / §3 列表页全量清单 / §7 生产编号链接台账。
> 上次实质变更（V147）：**两处菜单正名 —— ① 「计划排程 → 批次计划」更名「生产计划」；② 一级菜单「生产标准」更名「产品标准」**（2026-09-15，**纯命名；后端端点 / 路由 / 实体 / DTO / DB / 迁移 / 权限零改动**）：
> ① **改名（用户拍板）**——`Shared/AppMenu.cs` 计划排程组第 4 项 `Label`「批次计划」→ **「生产计划」**（`Href` 仍 `/batch-plans`、`Policy` 仍继承组级 `SchedulingMenu`）。**理由**：与一级「生产执行」不再两处都叫「批次」，且与「成检计划」并列成「生产计划 → 成检计划」的工序先后关系。
> ② **改名（用户拍板）**——`Shared/AppMenu.cs` 一级分组 `Label`「生产标准」→ **「产品标准」**（`Policy` 仍 `Roles.Policies.StandardView` 不变，组内 9 项叶子不动）。**理由**：该组维护的是**产品（牌号 / 化学成分 / 物理性能 / 标准号）本身的标准数据**，而非「生产过程的作业标准」（后者是「参数表」的工序组定义 / 工段工量天数 / 日产配置等），正名后二者边界清晰。
> ③ **同步改（用户管理 UI）**——`Helpers/UserRoleDisplayHelper.cs` 的 `MenuTiers`：`new("Standard", "生产标准")` → **`new("Standard", "产品标准")`**（用户管理「角色分配」弹窗档位名）。`Prefix` 仍是 `Standard`，15 项与顺序不变。
> ④ **⚠️ 有意不改（关键）**——**角色代码 `StandardViewer` / `StandardEditor` / `StandardFull` 保持原样**（`MES.Shared/Constants/Roles.cs`）：角色名存于 `AspNetRoles` 且逐次写进 JWT，改名须改库 + 全员重新登录，换来的可见差异为零。`Roles.cs` 两处注释已注明「菜单更名、角色代码不改」。**同理**：`BatchPlanSchedule` 实体 / `BatchPlanSchedules` 表 / `api/batch-plan*` 路由 / `/batch-plans` 页面路由 / `MES.Services.Scheduling.BatchPlanService` 一律不变。
> ⑤ **页内文案（用户拍板「一并全改」）**——「批次计划」作为**页面文案**一并改为「生产计划」：G13 列组名 `GroupName`「批次计划」→「**生产计划**」（`Pages/Scheduling/BatchPlans.razor.cs`）、打印标题默认值 `BatchPlanPrintRequest.Title`、PDF 文件名（`BatchPlanController.PrintFile` → `生产计划.pdf`）、页内脚注与 `FormatOutsourceCell` 注释文案；DataExchange 实体显示名「批次-批次计划」→「**批次-生产计划**」（`DataExchangeRegistry.cs`，⚠️ **实体 Key 与表头列名不变**，导入识别不受影响）。**页面标题**（`BatchPlans.razor` 页头）与**首页常用入口**（`AppShortcuts`）同步改「生产计划」。
> ⑥ **⚠️ 领域概念不改名（勿扩大化）**——代码与文档中「**批次计划薄表（BatchPlanSchedule）**」「**批次计划等级**（`PlanFlowLevel` / `ScheduleTier` 档位）」「批次计划流转」等**领域概念**保留原名：它们描述的是**批次粒度的计划安排数据本身**，与「菜单 / 页面叫什么」是两回事；全局替换会污染实体名、路由、DTO 与历史文档。
> ⑦ **⚠️ 文档取舍（防误读）**——与本文件 V146 同规：**以「历史变更（Vxx）:」/「版本变更（Vxx …）:」开头的历史条目保留当时的旧名原文**（历史不可改写），**当前状态类描述**（菜单树、上下文↔角色表、§2 / §3 页面台账括注）**已全部更新**。三份上下文文档同步更名：`模块设计/产品标准上下文详细设计.md`（V1.10）/ `接口设计/产品标准上下文接口设计.md`（V2.1）/ `数据库设计/产品标准上下文数据库设计.md`（V1.9），引用它们的链接同步更新（`01_设计总纲领.md`、`订单上下文数据库设计.md`、`质量管理上下文数据库设计.md`、`订单模块详细设计.md`）。
> ⑧ **测试**——`MES.Tests/Components/AppMenuTests.cs`：`牌号对照_仅存在于生产标准组` → **`牌号对照_仅存在于产品标准组`**（`Node("生产标准")` → `Node("产品标准")`）；`根级顺序_与电脑版历史一致` 第 11 位改 `"产品标准"`；**新增 `计划排程_五项并列二级_生产计划取代批次计划`**（逐项校验 Label + Href 顺序 + `AppMenu.Find("批次计划")` 为 null）。`AppShortcutsTests` 断言常用入口 Label 必须与菜单叶子完全一致 → `AppShortcuts` 已同步改。
> ⑨ **零影响面**——**无需抬 `?v=` 串**（纯菜单标签 / 页面标题 / 打印标题文案，**无 CSS 变更**，`app.css?v=13` 与 `MES.Blazor.styles.css?v=6` 均保持）；**零 EF 迁移 / 零数据变更 / 零权限变更 / 零新端点**。
> 历史变更（V146）：**一级菜单「批次管理」更名「生产执行」**（2026-09-15，**纯 WASM；后端 / API / DTO / DB / 迁移 / 权限 / 路由零改动**）：
> ① **改名（用户拍板，二选一后选定「生产执行」）**——`Shared/AppMenu.cs` 一级分组 `Label`「批次管理」→ **「生产执行」**（`Href` 无、`Policy` 仍 `Roles.Policies.BatchMenu` 不变）。**理由**：该组 7 项叶子（生产批次 / 生产执行核查 / 生产记录 / 去油酸洗 / 工段委外 / 委外单位管理 / 工艺卡打印）**全属生产执行动作**，「批次」只是承载对象；且与「计划排程」构成 **计划 → 执行** 的先后关系；另避免与「计划排程 → 批次计划」两处都叫「批次」。**未选「生产管理」**：一级已有 工单管理 / 计划排程 / 生产标准，该名是其公共上位词，易被误读为「生产总入口」。
> ② **同步改（用户管理 UI）**——`Helpers/UserRoleDisplayHelper.cs` 的 `MenuTiers`：`new("Batch", "批次管理")` → **`new("Batch", "生产执行")`**（用户管理「角色分配」弹窗里的档位名）。`Prefix` 仍是 `Batch`。
> ③ **⚠️ 有意不改（关键）**——**角色代码 `BatchViewer` / `BatchEditor` / `BatchFull` 保持原样**（`MES.Shared/Constants/Roles.cs`）：46 个角色存于 `AspNetRoles` 且逐次写进 JWT，改名须改库 + 全员重新登录，换来的可见差异为零（用户只看到 ② 那个中文档位名）。`Roles.cs` 两处注释已注明「菜单更名、角色代码不改」。
> ④ **测试**——`MES.Tests/Components/AppMenuTests.cs`：`批次管理组_生产批次与生产执行核查并列` → **`生产执行组_生产批次与生产执行核查并列`**，`Node("批次管理")` → `Node("生产执行")`；`根级顺序_与电脑版历史一致` 第 6 位由 `"批次管理"` 改 `"生产执行"`。`UserRoleDisplayHelperTests` 只断言 `Prefix` / 条数 / 顺序、**不断言中文名 → 不受影响**。
> ⑤ **⚠️ 文档取舍（防误读）**——本文件与各模块文档中，**以「历史变更（Vxx）:」/「版本变更（Vxx …）:」开头的历史条目保留当时的「批次管理」原文**（历史不可改写）；**当前状态类描述**（菜单树 §2.4 / §6.1、上下文↔角色表 §4、页面清单括注、入口描述）**已全部更新为「生产执行」**。⚠️ 另注：`docs/01_设计总纲领.md` / `02_架构设计.md` / `12_商业MES差距分析.md` 中的「批次管理」指**仓库上下文的库存批次管理**（另一回事），**本次未动**。
> ⑥ **无需抬 `?v=` 串**——纯菜单标签 + 用户管理弹窗文案，**无 CSS 变更**（`app.css?v=13` / `MES.Blazor.styles.css?v=6` 均保持）。**零 EF 迁移 / 零数据变更 / 零权限变更**。
> 历史变更（V145）：**「订单负荷总量」页：交期截止负荷量列组默认折叠 + 日期表头两行强化层次**（2026-09-15，**纯 WASM（`Pages/Scheduling/WorkOrderLoadOverview.razor(.cs)`，样式写在页内 `<style>`）；后端 / DTO / DB / 迁移 / 权限 / 路由零改动**）：
> ① **列组折叠（用户拍板「默认不显示」）**——`col-g101` 交期截止负荷量（7 个日期桶列）**默认折叠**，页头右侧「打印」旁新增切换按钮（`ExpandMore`/`ExpandLess`，文案「展开/收起交期截止负荷量」）；折叠后表格 8 列、不再横向滚动。**这是「表不上眼」的根源**。
> ② **⚠️ 三处必须成对增减**（缺一整组错位，已写入代码注释）——① 分组标题栏的 `col-g101` 徽章 item、② `HeaderContent` 的 7 个 `MudTh`、③ `RowTemplate` 的 7 个 `MudTd`：`wwwroot/js/table-nav.js` 的 `initGroupHeaders` **按 th 的 `col-gN` 顺序、按索引**给 `headerBar.querySelectorAll('.col-group-header-item')[i]` 写宽度（`table-nav.js:264-287`），只藏一边会整体串位。折叠/展开后 `OnAfterRenderAsync` 每次渲染都调 `initGroupHeaders`（非首次走 `requestAnimationFrame(syncGroupWidths)` 分支）+ thead 的 `MutationObserver` 亦会触发重测，无需额外 JS。
> ③ **打印=所见即所得（用户拍板）**——`PrintTable` 直接抓 `#workorder-overview-table` 的 DOM，**不做自动展开**：折叠状态下打印即少 7 列。分组标题栏本就不打印（既有决策），故本次对打印件零影响。
> ④ **日期表头两行 + 结束日为主信息（用户拍板，经两轮修正定稿）**——`FormatBucketHeader` 最终形态：区间桶 **首行=完整起日 `yy/M/d`（浅色弱化）**、**次行=`～ 止日`（深色加粗，主信息；跨年补年份）**；`≤`/`≥` **单端桶下移到次行**、首行返回 `\u00A0` 占位 → 与区间桶的「～ 结束日」同排对齐且 7 个表头**等高、基线齐平**（原实现单端桶占首行、与双行桶参差）。样式由页内 `<style>` 的 `.db-start`（0.8em/400/`#90A4AE`）与 `.db-end`（0.95em/700/**中性深灰 `#37474F`**）承担，**原 `MudTh` 的内联 `font-size:0.9em` 已移除**。⚠️ 中途曾误做成「起止日合成一行」，已按用户示例回退为两行（**勿再合并**）。
> ⑤ **⚠️ 无需抬 `?v=` 串**——本页样式为**页内 `<style>` 元素**（非 `app.css` / `MES.Blazor.styles.css`），随组件渲染下发，`css/app.css?v=13` 与 `MES.Blazor.styles.css?v=6` 均保持不动。
> ⑥ **影响面**——`WorkOrderLoadOverview` 为共享组件，**两处入口同时生效**：`/plan-overview`（计划排程 → 订单负荷总量）与**报表总览 Tab2「现订单负荷总量」**。**无单测**（该页此前即无组件测试）；`dotnet build MES.Blazor` 0 错 0 警。
> 历史变更（V144）：**工单管理菜单拍平 + 6 项改名重排（纯 WASM 菜单树与页面标题，后端 / API / DB / 迁移 / 权限零改动）**（2026-09-15，**用户拍板**）：
> ① **结构：原「工单操作 / 工单查询」两个三级分组取消**，其下 6 项**上提为二级菜单**直接挂在「工单管理」之下（`Shared/AppMenu.cs`）。
> ② **改名**（**菜单名 ↔ 页面标题同步改**）：工单生成（不变）/ 用料计划 → **工单用料** / 用料投料核查 → **用投料核查** / 工单需求调整 → **需求调整** / 工单执行状况 → **查询工单执行** / 定尺工单定尺 → **查询定尺工单**。
> ③ **重排**（新顺序）：**工单生成 → 需求调整 → 工单用料 → 用投料核查 → 查询工单执行 → 查询定尺工单**（操作在前、查询在后，与用户给定顺序逐字一致）。
> ④ **同步改动**——`Shared/AppShortcuts.cs` 首页常用入口 Label「用料计划」→「**工单用料**」（`AppShortcutsTests` 断言常用入口 Label / 有效策略必须与菜单叶子完全一致，不改即挂）；5 个页面标题（`OrderDemandAdjustment` / `MaterialPlanOverview` / `MaterialInputConsistency` / `WorkOrderExecution` / `FixedLengthWorkOrderView`）同步新名。**路由与权限零变更**（6 项 Policy 仍继承分组 `WorkOrderMenu`）。**未改**：`WorkOrderMaterialPlan.razor` 独立详情页「工单用料计划」（另一页，非菜单项）及其跳转图标提示 `Title="工单用料计划"`。
> ⑤ **测试**——`MES.Tests/Components/AppMenuTests.cs` 断言由「二级分组_操作与查询」改为「**六项并列二级_无子分组**」（逐项校验 Label + Href 顺序 + `OnlyContain(IsLeaf)` + `AppMenu.Find("工单操作")/("工单查询")` 为 null 防漂移）；`AppMenuTests` + `AppShortcutsTests` **14 例全过**。**无 CSS 变更 → 不抬 `index.html` 版本串**。
> 历史变更（V143）：**订单进度树叶子下沉「生产批次名单」（在产 / 在途分段 + 默认折叠 + 点击批号弹「批次执行进度」）**（2026-09-15，**全栈：`OrderProgressTree.razor(.cs/.css)` + `index.html` 抬串 + 后端 5 文件 + DTO 3 类；无 EF 迁移 / 无数据变更 / 无权限变更 / 零新端点**）：
> ① **来由（用户拍板）**——用户用「订单进度树」做**查询**，但树原为 4 层纯重量结构（订单 → 主号 → 阶段分支 → 重量叶），**叶上没有任何批次身份**：看到「荒管处理 19281 kg」不知是哪些批次；且**首页查询是两段式的（① 订单 → ② 生产编号）**，而第 ① 步的进度树不显示生产编号 → **查询中断**。用户核对中还发现真实口径偏差：**【待产】标签下的批次实际多是「在产」而非「待产」** —— 逐批手算确证 G17【待产】= 「**还没完成该节点**」的剩余工作量 = **在产**（批次已在本节点工序组内、目标工段未完成）+ **在途**（还没做到本节点工序组）两部分。故名单必须**分段呈现**，否则数字与名单对不上。
> ② **用户拍板 5 条（不再变更）**——a. 生产执行分支名单**按「在产 / 在途」两段拆**；b. 名单**默认折叠**（否则页面过于啰嗦）；c. 节点标签**【待产】不改**；d. **首页查询卡也要**（打通两段式查询），批号**可点击 → 弹「批次执行进度」**；e. 生产执行分支的重量数字**改为实时重算**，与名单同源自洽。
> ③ **口径定义**——沿用 G17 既有三分支，把被计入的批次按 `batchCurrentSeq` vs `targetSeq` 切两段：`== targetSeq` → **在产** `InProgress`；`< targetSeq` → **在途** `InTransit`；`> targetSeq` / 该批无此工序组 / 该工序组无目标工段 / `Status ∈ {Completed, InFinalInspection}` → **不计入**（与今一致）。**安全绳不变式**：`Σ在产 + Σ在途 == 原 PendingSection* 数值`（分段只是对同一集合分区，不增不减）。**成品检验分支只有一段**（每批恰好落一个档，无「在途」概念，段 Label = 该档中文阶段名）。
> ④ **前端呈现**（`Shared/OrderProgressTree.razor` 叶块）——叶行（3 个 span 原样）之下追加名单块：**折叠态**一行短文本 `【在产 14 批 · 在途 41 批】`（检验叶为 `【待到料 3 批】`，计数本身即有用且不撑行）；**展开态**每段一行「标签 `@seg.Label @seg.BatchCount 批：` + 批发号胶囊列」。用 `<span @onclick>` 而非 `MudButton`（密集列表用按钮会带 padding / ripple 撑高，与「密集网格用原生控件」取向一致）。**一个叶子一个开关**（不按段分开关）。段内批号 `@onclick="() => OpenBatchProgressAsync(b.BatchId, b.BatchNo)"` + `@onclick:stopPropagation="true"`（`.op-main-head` / `ToggleMain` **不是**该块祖先，双击保险）。**展开态存于 `_expandedLeaves`（「已展开集合」，默认空集 = 全折叠）**，key = `"{主号}|{分支Key}|{叶Key}"`（分隔符沿用项目既有记录键习惯）；**清空时机复用既有 `_collapsedInitializedFor` 的 `ReferenceEquals` 引用守卫**（新 `Tree` 实例才清，父组件重渲染不重置用户展开，换单 / 重查回到全折叠）；**不持久化**（与 `_collapsedMains` 一致）。弹窗参数**逐行照抄** `Pages/Scheduling/FinalInspectionPlan.razor.cs`：`MaxWidth.Large` + `CloseOnEscapeKey`，**不开 `FullWidth`**（6.19 `ExtraLarge=1920px` + `FullWidth` 会满屏留白）。**无需权限降级**——`api/batch/{id}/tracking` 与 `api/batch/by-batch-no` 已于 2026-09-14 放宽为 `[Authorize]` 仅需登录（首页放开），点批号不会 403。⚠️ **前提锁定（已写入组件头注释）**：`OrderProgressTree` 现渲染于 `MudPaper` / `MudCard` 内（首页卡、`/orders/progress` 页），**不在任何 `MudDialog` 内** → 弹根级对话框安全；将来若塞进弹窗需重新评估。
> ⑤ **后端**（详见订单接口设计 V2.20 / 订单模块详细设计 V1.36 / 工单模块详细设计 V7.6）——抽公共计算器 `MES.Services/Helpers/ProductionPendingNodeHelper.cs`（单源 8 节点表 + 批次粒度在产 / 在途计算器，纯内存、不依赖 DbContext）替换原 G17 内联块与 `OrderProgressQueryService.ProductionNodeDefs` **两份重复定义**；订单进度树「生产执行」分支**由快照改实时重算**（`ProductionBatches.Include(ProcessGroups)` 按 `workOrderNos` 收窄后按主号分组 `Compute`，主号级 = 该主号全部工单批次并集一次算完）；`MainProgressLeafDto` 新增 `BatchSegments`，新增 `LeafBatchSegmentDto`（`Key`/`Label`/`WeightKg`/`BatchCount`/`Batches`）与 `LeafBatchItemDto`（`BatchId`/`BatchNo`/`WeightKg`）——**英文 Key + 服务端拼中文 Label**，前端只拼接、不做中文判断。成品检验分支名单与重量**取自同一行**（`GroupBy(ProductionBatchId).First()` 去重铁律不动）。
> ⑥ **⚠️ 口径分叉（必须在文档写明）**——生产节点叶重由快照改实时后，与「工单执行状况」页（未点「即时更新」时仍是旧快照）**可能不一致**；`DeformedProcessCompleted` / `ProductionAttentionProcess` 仍是快照 → 会长期分叉。**这是本次最大行为变更，不是 bug**。
> ⑦ **⚠️ 静态资源版本串（本批共抬 3 次）**——`index.html`：`MES.Blazor.styles.css?v=3` → **`?v=4`**（名单块新增）；**随后按用户反馈调字号两轮**（用户原话「在产在途的字体放大，现在太小了……包括生产编号」→ 再「15px，叶子正文提到16」）：第一轮 `.op-batch-toggle` / `.op-batch-seg-label` / `.op-batch-no` 三处 13px → 14px（`?v=5`）；第二轮 **叶子正文 `.op-leaf-row` / `.op-leaf-kg` / `.op-leaf-return` 15px → 16px，名单三处 14px → 15px**（`?v=6`，最终值）。**层级关系保持：叶子 16px > 名单 15px**（均为硬编码 px、非继承，桌面与手机壳共用同值、不额外分档）。⚠️ **字号硬编码 → 改了必须抬串才下发**。`css/app.css` **本批未改、保持 `?v=13`**。
> ⑧ **验证**——`dotnet build MES.Api` / `MES.Blazor` **各 0 错 0 警**；定向单测 `ProductionPendingNodeHelperTests`（新建 16 例）+ `OrderProgressQueryServiceTests` **21 过** + `WorkOrderExecutionServiceTests` / `ProductionPendingNodeHelperTests` 合计 **102 过**；**真库 SQL 快路径核验**（订单 `D26Z2159001` / 主号 `X03`）：荒管处理 **19281.000（14 批，名单逐字吻合 2603-302/326/327/328/329/330/332/333/335/337/338/342/343/387）**、50冷轧 **37433.000**、30冷轧 **98973.000**，与 2026-09-15 快照值一致。⚠️ **本批尚未打包上线**（登记见 `docs/本机与线上发布差异待办.md`）；**浏览器人工验证待用户执行**（首页手机 / 桌面两分支 + `/orders/progress`：默认折叠、展开名单换行不溢出、点批号弹「批次执行进度」）。
> 历史变更（V142）：**手机端首页复查：订单进度树防横向溢出 4 组 5 条 `.mh-shell` 覆盖**（2026-09-15，**纯 WASM（`Shared/OrderProgressTree.razor.css` + `index.html`）；后端 / API / DB / 迁移 / 权限零改动**）：
> ① **来由（用户点名复查）**——「复查一下，手机端的首页，提示信息、卡片等。是否存在超出屏幕，宽度超出的问题。另外如果存在类似之前的竖排显示的问题，也需要一并解决。请你先全面检查，再输出解决方案。」复查覆盖首页整棵渲染树（`Index.razor` 手机+桌面两分支 / `MobileLayout.razor` / `ResponsiveLayout.razor` / 两张查询卡 / `OrderProgressTree` / `BatchProgressCard` / `AppShortcuts` 11 条 / `app.css` 6 段 + MudBlazor 6.19 实际取值）。
> ② **竖排问题：首页不存在（已排除）**——Grep 全首页渲染树 `MudTable` / `MudDataGrid` / `MudSimpleTable` / `Breakpoint=` **0 命中**。竖排根因是 `MudTable` 默认 `Breakpoint=Xs` 在 <600px 隐藏表头并把每条记录拆成 N 行「列名—值」，首页两块结果区均为 `div`+flex 自绘结构，不具触发条件；`MudAlert` 提示条是 flex 行内布局，亦不竖排。
> ③ **宽度溢出：3 处真实风险，全在订单进度树内**（均为「不可断行」内容）。手机 360px 逐层实算：视口 360 − `.mh-shell .mud-container` 左右 12px = 336 − `.mh-card.pa-3` 12px = **卡内容 312px**；`.op-root` 左右 padding 14px 并减图标 24 + gap 10 → **`.op-root-meta` ≈ 250px**；叶子列再扣缩进 44px → **≈ 256px**。风险点：a. `.op-kv{white-space:nowrap}`（树根行2「订单总重量 / 客户名称」）—— 父 `.op-root-line2` 虽 `flex-wrap` 但**只在 token 边界换行**、token 自身不可断，「订单总重量 123456.78 kg」≈ **266px > 250px**；b. `.op-leaf-row` **无 `flex-wrap`** —— 次品入库叶「文本 + 入库量 + 退货量」三段同排 ≈ **365px > 256px**；c. `.op-spec-token{white-space:nowrap;height:23px}` —— 固定行高 + 单 token 不可折，长规格 / 长标准号单 token 越界。⚠️ **越界后果不是被裁掉**：`.home-query-result{overflow-y:auto}` 按 CSS 规范把 `overflow-x` 计算为 `auto` → 结果区自动成为**横向滚动容器**（观感即「宽度超出卡片」，且纵向滑列表易被横向滚动劫持）；`.mh-card{overflow:hidden}` + `html,body{overflow-x:hidden}` 只保证整页无滚动条，不解决卡内越界。
> ④ **修复（4 组 5 条，全部 `.mh-shell` 前缀，写在 `Shared/OrderProgressTree.razor.css` 末尾）**——`op-kv{flex-wrap:wrap;white-space:normal;max-width:100%}` / `op-kv .op-kv-v{min-width:0;overflow-wrap:anywhere}` / `op-spec-token{max-width:100%;white-space:normal;overflow-wrap:anywhere;height:auto;line-height:1.35}` / `op-leaf-row{flex-wrap:wrap;row-gap:2px}` / `op-stage-children{margin-left:24px}`（34 → 24px，给 nowrap 重量值多留 10px）。⚠️ **钩子用 `.mh-shell`（移动壳自身类）而非 `.mh-container` 祖先** —— 沿用 2026-09-14 真机教训（`.mh-container` 后代选择器在真机未命中会整批静默失效）。⚠️ **scoped CSS 命中已实测**：Blazor 只给选择器**最后一级**加 scope 属性 → 编译产物为 `.mh-shell .op-kv[b-c9vd7ch8hx]`（`.mh-shell` 不带 `[b-]`），已从 `obj/.../scopedcss/bundle/MES.Blazor.styles.css` 第 401/407/412/420/426 行逐条核对。特异性各高一级（0,3,0 > 0,2,0），无需 `!important`。**桌面 `/orders/progress` 页规则不命中 → 行为零变化**。
> ⑤ **已确认非问题 4 点（有意未改）**——a. `.detail-progress-track` 固定 200px（在 `flex-wrap` 行内可整行换行且 200 < 312，不溢出）；b. `.detail-section-scroll{overflow-x:auto}` + `.detail-group-card{min-width:150px}`（工序组流程图**设计即横向滚动**）；c. `MudAlert`「未找到…」（中文可任意断行、≈310px 临界但不溢出）；d. 常用入口磁贴 `flex:1 1 28%`（11 条时前三行 3 列、**末行 2 条被 `flex-grow` 拉成半屏宽** —— 观感不齐但非溢出）。
> ⑥ **⚠️ 静态资源版本串（同提交抬串，务必）**——`index.html`：`MES.Blazor.styles.css?v=2` → **`?v=3`**（`.razor.css` 变更）；`css/app.css` **本批未改、保持 `?v=13`**。
> ⑦ **验证与上线**——`dotnet build MES.Blazor` **0 错 0 警** + 编译产物含 5 条新规则（产物级复核）。**已随 `mes-deploy-20260915_1400.zip` 于 2026-09-15 上线**；上线核验（本机只读 curl）：首页 `http=200`、下发 `MES.Blazor.styles.css?v=3`、线上 `styles.css` 内含本批 4 组 `.mh-shell .op-*` 规则。**真机复核已通过（2026-09-15，用户确认「均正常了」）**。⚠️ 今后同类改动仍不可只靠 F12 模拟下结论（模拟与真机走的分支不同）。
> 历史变更（V141）：**手机端可读性两项修正——① V137 批改漏网 8 处补 `Breakpoint="Breakpoint.None"`；② 首页两张查询卡「清除」按钮弱化为纯文字**（2026-09-15，**纯 WASM（`.razor` 组件属性 / 按钮变体）；⚠️ 无 CSS 变更故该批自身不抬 `?v=` 版本串（`app.css` 结束时为 `?v=13`、`MES.Blazor.styles.css` 结束时为 `?v=2`，随后由同批次的 V142 抬到 `?v=3`）；后端 / API / DB / 迁移 / 权限零改动**）：
> ① **V137 批改漏网 8 处补齐（用户报障触发）**——用户报「生产标准上下文『标准号列表』→ 打开某个标准号，**手机端的查看不符合要求**（列表查看）」。根因＝MudBlazor 6.19 `MudTable` 的 `Breakpoint` **默认值即 `Breakpoint.Xs`** → 窄于 600px 时外层 div 带 `mud-xs-table`，`MudBlazor.min.css` 的 `@media(max-width:600px)` 内**隐藏表头** + 每格 `display:flex` + `:before{content:attr(data-label)}` → 一条记录被拆成 N 行「列名—值」竖排。2026-09-14 已批量补 127 处 / 82 文件，**本批补漏网 8 处 / 5 文件**：`Pages/StandardRegister/StandardRegisterDetail.razor:209`（子项目，即用户报障页）、`Pages/Quality/CertificateDetail.razor:142/262/295/320`（质保书明细 / 化学成分 / 成品检验 / 理化检测）、`Pages/WorkOrders/WorkOrderMaterialPlan.razor:258`（库存计划）、`Pages/Batches/OutsourceRecoveryCreate.razor:42`（委外回收明细）、`Pages/WorkOrders/WorkOrderGenerate.razor:111`（工单生成-订单项次）。**漏网成因**＝V137 脚本按「`ReadOnly="true"` 字面量」与「`edit-table`」两类归档，**混合态 `ReadOnly="@(!_isEditMode …)"`** 与**只读但无 `ReadOnly` 字面量、`Class` 仅 `auto-table`** 的表两边都不命中。**全仓复查结论（无遗漏）**：`MudTable` 共 190 张，未加 `Breakpoint` 的 60 张**全属录入 / 操作页**（57 张 `*Create.razor` / `*Edit.razor` + `WarehouseInbound.razor:108` + `WarehouseOutbound.razor:61` + `InspectionPatrolForm.razor:163`），**有意保留竖排属预期**；全仓无 `MudDataGrid`、无 `Breakpoint` 取 `None` 以外的值、`Shared/` 与 `Components/` 下无遗漏表。⚠️ **判据陷阱**：**不能**用「表内是否含 `MudTextField` / `MudNumericField`」判录入表——大量录入表行内控件由 `.razor.cs` 的 `RenderCell(...)` 以 `RenderFragment` 产出，扫描会报 `inputs=0` 全是**假阴性**；可靠判据＝**文件名（`*Create` / `*Edit`）+ `Class` 是否含 `edit-table`**。
> ② **首页两张查询卡「清除」按钮弱化**（用户反馈「手机端不好看」）——`Shared/OrderProgressQueryCard.razor` 与 `Shared/BatchProgressQueryCard.razor` 两处同款：`Variant="Variant.Outlined"` → **`Variant="Variant.Text"`**、删除 `StartIcon="@Icons.Material.Filled.Clear"`（`Color="Color.Default"` 与 `Disabled="_loading || !CanClear"` 保持）。理由（已写入源码注释）：手机端与实心主按钮「查询」并列时，「**灰色空心框 + 叉号**」与实心蓝两种风格冲突，且 **✕ 易被误读为「关闭」**。**零行为变更**——`Clear()`（清空输入 + 结果 + `_searched`）与 `CanClear`（`_searched || 输入非空`）均未动；`HomeQueryCardsTests` 5 例按 `FindAll("button")[0]`=查询 / `[1]`=清除 定位，**不看 `Variant` / `StartIcon` → 不受影响**。验证：`dotnet build MES.Blazor` 0 错 0 警 + `HomeQueryCardsTests` **5 过**。
> ③ **发布与验收**——纯 `.razor` 属性改动，**无 CSS 变更 → 本批自身不抬 `index.html` 版本串**；**无 EF 迁移 / 无数据变更 / 无权限变更**。⚠️ 属 V135–V141 手机壳体系整批（含 V142），**已随 `mes-deploy-20260915_1400.zip` 于 2026-09-15 上线**；但 **手机端样式与 `Breakpoint` 行为仍须在真机验证**（F12 模拟 `mud-xs-table` 分支有时会走另一条路径、看不出问题）。
> 历史变更（V140）：**删除「手机横屏切电脑版」例外 + 删除「请横屏」提示条机制——手机/触屏设备一律走手机壳**（2026-09-14，**WASM（`ResponsiveLayout.razor` / `MobileLayout.razor` / `app.css` / `index.html`）+ 删除 2 个组件文件与 1 个测试类；后端 / API / DB / 迁移零改动**）：
> ① **来由**——用户 2026-09-14 追问：「我们原来设置的，手机横向查看时，是完全按『电脑』样式的；现在是否已经没必要了，可以全按手机版了。」分析结论：该例外的原始理由（宽表竖屏不可读）**已被同日 V135–V139 四批改动推翻**，且例外本身带来 3 项代价（横屏反而丢掉全部 `.mh-shell` 收敛规则 / 扫码流窄页被误切桌面版 / 旋转屏幕换壳重建子树丢页面状态）。用户拍板三项：**a. 干脆全删例外；b. 横屏筛选网格放宽为每行 2 列（采纳自主建议）；c. 提示条机制一并删除**。
> ② **删掉的例外**（`Shared/ResponsiveLayout.razor`）——原判定表达式尾部为 `&&!(matchMedia('(orientation: landscape)').matches && window.innerWidth>=700)`，即「命中移动条件后，若横屏且 ≥700px 就反悔、切桌面版 `MainLayout`」（2026-09-05 拍板，当时唯一的宽表可读出路）。**现已整条删除** → 手机/触屏设备**不再按横竖屏区分**，一律 `MobileLayout`（带 `.mh-shell`）。**收益**：a) 手机横屏不再丢页头换行 / MudGrid 堆叠 / 宽表粘性表头与限高滚动区；b) 扫码流等「窄页」（`/mobile-report`、`/equipment-scan`、`/mobile-quality/*`）不再被误切成桌面版；c) 旋转屏幕不再跨阈值换壳 → **不再重建整棵页面子树，已查询的列表 / 未提交的表单状态得以保留**。**⚠️ 已知代价（用户知情接受）**：判据中含 UA / `pointer:coarse` / `maxTouchPoints` → **触屏笔记本与大平板横屏也会走移动布局**（大屏上被「手机化」）；**纯鼠标桌面机不受影响**（宽≥960 且无任何触屏特征 → 仍走 `MainLayout`）。
> ③ **横屏筛选网格每行 2 列**（`app.css` 手机壳段第 2 组之后新增）——删例外后手机横屏（`innerWidth` 700~932）也走手机壳，而横屏可用高度常不足 400px，筛选网格若仍逐行全宽会占满整屏 → 横屏时把**含输入控件**的网格项放宽为每行 2 列：`@media (orientation: landscape) { .mh-shell .mud-grid > .mud-grid-item:has(.mud-input-control) { flex-basis:50% !important; max-width:50% !important } }`（800px 屏 → 每项约 400px，够放一个输入框）。⚠️ **只匹配 `:has(.mud-input-control)`**——全仓 454 个 `<MudItem>` 中 426 个是 `xs="12"` 的**内容块**（表格 / 卡片 / 进度树），一律改 50% 会把它们压成半宽，故仅让表单字段配对、内容块保持整宽。特异性 (0,4,0)（`:has()` 计入内部选择器）压过第 2 组的 (0,3,0)；不支持 `:has()` 的旧 WebView 会**整条丢弃本规则**→ 静默回落「逐行全宽」（即第 2 组行为），不报错。
> ④ **删除「请横屏」提示条机制（组件 + 规则 + 单测）**——`Shared/LandscapeHintBanner.razor`、`Shared/LandscapeHintRule.cs`、`MES.Tests/Components/LandscapeHintRuleTests.cs`（8 例，含 2 条「菜单新增窄叶漏接」防漂移断言）**整文件删除**；`MobileLayout.razor` 原注释「组件与规则均保留不删、恢复只需加回一行」改为「已连同单测一并删除，不再保留」。⚠️ 该机制自 V135 起已不渲染（提示条已撤销，规则只被自己的单测引用）→ 属**死代码清理**；随之失效的窄页判定（`NarrowMenuLeafHrefs` 等）不再有任何消费方。
> ⑤ **⚠️ 静态资源版本串（同提交抬串，务必）**——`index.html`：`css/app.css?v=12` → **`?v=13`**（`MES.Blazor.styles.css` 无 `.razor.css` 改动，保持 `?v=2`）。
> ⑥ **验证** `dotnet build MES.Tests --no-incremental` **8 项目 0 错 0 警** + 定向单测 **14 过**（`AppMenuTests` / `HomeQueryCardsTests`；`LandscapeHintRuleTests` 已随类删除）。⚠️ 仍属未打包未上线批次（批三十一），真机验证需等下次发布。
> 历史变更（V139）：**工资结算 4 张密集网格页在手机端同样「不冻结任何列」+「月工资津贴汇总」页手机端放开横向滚动**（2026-09-14，**纯 WASM（`app.css` / `index.html`）；后端 / API / DB / 迁移 / 页面本体零改动**）：
> ① **用户诉求**——V138 汇报时提出「考勤表（`/payroll/attendance`）在页面级显式冻结了岗位列 + 月份合计列，本次未动，如需一并去掉请说」，用户答复**「一并去掉」**。
> ② **涉及 4 页**（共用同一套 `.sticky-left` / `.sticky-right` 类）——`Payroll/Attendance.razor`（考勤表）、`Payroll/AllowanceMonthly.razor`（津贴与处罚月表）、`Payroll/MonthlyWages.razor`（月工资表）、`Payroll/MonthlySummary.razor`（月工资津贴汇总）。冻结列 = 左侧「工号 / 姓名 / 岗位类别 / 岗位」＋右侧「出勤天数 / 总小时（本月合计）」。
> ③ **做法（⚠️ 只在小屏取消、桌面保留）**——`app.css`「全站手机壳」段新增**第 6 组**规则：`.mh-shell .attendance-grid .sticky-left, .mh-shell .attendance-grid .sticky-right { position: static; left: auto; right: auto; z-index: auto }`。特异性 (0,3,0) 压过原 `.attendance-grid .sticky-left/.sticky-right` (0,2,0)；`thead th.sticky-left/right`(0,3,1) 只声明 `z-index`，`static` 后不适用、无需覆盖；表头 `thead th{position:sticky;top:0}`（**纵向**冻结）不受影响、仍保留。**桌面 `MainLayout` 不带 `.mh-shell` → 桌面行为不变**（桌面 1920px 下「岗位 4 列 + 31 天 + 合计 2 列」约 1900px 仍会横向滚动，冻结仍有价值）。
> ④ **「月工资津贴汇总」（`MonthlySummary`）手机端放开横向滚动（同日追加）**——汇报 ③ 时用户追问「手机端的横向，也是一样的？」，核查发现该页与其它 3 页**表现不一致**：它额外带 `.monthly-summary-grid`，把基类 `.attendance-grid { min-width:max-content }` **覆盖成 `width:100%; min-width:0; table-layout:fixed`**（原设计目标＝桌面 14 金额列 ×88px + 4 标识列 ≈1478px，在 1900px 容器内正好放得下、整表免横滚）。但**手机 360px 下 `fixed` + `100%` 会把 18 列等比压到屏宽**（每列约 20px），既看不清也**不会横滚**（`overflow:auto` 无内容可滚）。→ 用户拍板**「手机端放开横滚」**：手机壳内补 `.mh-shell .attendance-grid.monthly-summary-grid { width:auto; min-width:max-content; table-layout:auto }`（特异性 (0,3,0) 压过原 (0,2,0)）→ 手机端列按内容撑开 + 横向滚动，与其它 3 页口径一致；**桌面无 `.mh-shell` → 维持「免横滚」原状**（该页桌面仍不横滚）。
> ⑤ **⚠️ 静态资源版本串（同提交抬串，务必）**——`index.html`：`css/app.css?v=10` → **`?v=11`**（③ 首版）→ **`?v=12`**（本 ④ 追加；两项同属 V139、同提交，抬一次到 12 即可）。
> ⑥ **验证** `dotnet build MES.Blazor --no-incremental` **3 项目 0 错 0 警**（纯 CSS / HTML）。⚠️ 仍属未打包未上线批次（批三十一）。
> 历史变更（V138）：**手机端表格「不冻结任何列」——删除 `.mh-shell` 的粘性首列规则（`left:0`）与勾选框场景「粘第 2 列」规则（`left:40px`）及配套 z-index 三条，只保留粘性表头**（2026-09-14，**纯 WASM（`app.css` / `index.html`）；后端 / API / DB / 迁移 / 页面本体零改动**）：
> ① **用户诉求（原话）**——「移动时没必要冻住字段。例如：仓库的列表中，现在是仓库的批次号冻住的。其它的所有列表，在手机端也是如此，没必要冻住任何一列或几列。」即：**手机端横向拖动时列应随内容一起走，不冻结任何列**。
> ② **删除的规则（`app.css`「全站手机壳」第 4 组，`@media screen` 内）**——a. `.mh-shell .mud-table-head tr:last-child th:first-child, .mh-shell .mud-table-body td:first-child { position:sticky; left:0; background-color:…; box-shadow:1px 0 0 #ECEFF1 }`（V135 新增的**粘性首列**）；b. `.mh-shell .mud-table-head tr:last-child th:first-child:has(.mud-checkbox) + th, .mh-shell .mud-table-body td:first-child:has(.mud-checkbox) + td { position:sticky; left:40px; … }`（V136 新增的**勾选框场景粘第 2 列**）；c. 配套的 z-index 三条（左上交叉格 6 / 勾选场景第 2 列 5 / 首列数据格 4）。原位留注释说明删除原因与「将来若恢复需同步确认 `border-collapse:separate`」。
> ③ **保留**——**粘性表头**（`.mh-shell .mud-table-head tr:last-child th { position:sticky; top:0; z-index:5 }`）与 `border-collapse: separate`（表头 sticky 所必需，与列冻结无关）、`.mud-table-container` 的 `overflow-x/y:auto` + `max-height:max(260px, calc(100dvh - 140px))` + `overscroll-behavior:contain`、`.list-toolbar` 换行等**均不动**（冻结表头是「纵向滚动时知道看的是哪一列」，与本次诉求「左右移动不冻字段」不冲突）。
> ④ **不动的两处**——**`edit-table` 编辑录入表**（手机端保持「列名—值」竖排）与**`WarehouseOutbound.razor` 出库操作表**（V137 同口径）；**考勤表页面级的 `.sticky-left` / `.sticky-right`**（`Attendance.razor` 密集网格刻意设计的「岗位列 + 月份合计列」冻结）在 V138 时未动 —— **⚠️ 已由 V139「一并去掉」（手机端）**。
> ⑤ **⚠️ 静态资源版本串（同提交抬串，务必）**——`index.html`：`css/app.css?v=9` → **`?v=10`**（`MES.Blazor.styles.css` 无 `.razor.css` 改动，保持 `?v=2`）。
> ⑥ **验证** `dotnet build MES.Blazor --no-incremental` **3 项目 0 错 0 警**（纯 CSS / HTML 改动，无单测影响）。⚠️ **本批仍属未打包未上线批次**（批三十一），真机验证需等下次发布。
> 历史变更（V137）：**⚠️ 修正 V136 的根因判断——手机端列表「每条记录竖向显示」的真根因 = MudTable 默认 `Breakpoint=Xs`；各列表页 `<MudTable>` 显式加 `Breakpoint="Breakpoint.None"`（**共 127 处 / 82 个文件**），手机竖屏由「列名—值」竖排恢复为真正的横向宽表 + 横向滚动**（2026-09-14，**纯 WASM（`.razor` 页面 + `app.css` / `index.html`）；后端 / API / DB / 迁移零改动**）：
> ① **用户诉求（原话）**——「举个例子，仓库的列表数据，在手机端，现在的每条记录都是纵向显示的。我的意思还是直接横向吧，就是向右移动的范围会比较大。」即：**手机端列表要直接横向（接受大量左右移动），不要把一条记录拆成竖排的「列名—值」多行**。
> ② **⚠️⚠️ 真根因（推翻 V136「横向滚动本身并没坏、只需加粘性表头首列」的判断）**——`MudTable` 的 `Breakpoint` **默认值 = `Breakpoint.Xs`**（MudBlazor 6.19 `MudTableBase.cs`：`public Breakpoint Breakpoint { get; set; } = Breakpoint.Xs;`，xml 文档亦写明「the default behavior is breaking on Xs」）。窄于 Xs 断点（手机竖屏 < 600px）时外层 div 带上 `mud-xs-table` 类，MudBlazor.min.css 的 `@media(max-width:600px)` 内有三条规则：`.mud-xs-table .mud-table-root .mud-table-head{display:none}`（**表头整条隐藏**）、`.mud-xs-table .mud-table-row{display:revert}`、`.mud-xs-table .mud-table-cell{display:flex; justify-content:space-between; align-items:center; border:none; padding:14px 16px; text-align:start!important}` + `.mud-table-cell:before{content:attr(data-label); …}`（**每格前面补列名**）→ **一条记录被拆成 N 行「列名 — 值」竖排**（仓库库存 20 列 = 每条记录 20 行），表头消失。**故手机端从来就没有横向滚动，而是 MudBlazor 自带的移动布局在接管**；V136 那批 `.mh-shell .mud-table-head …` 粘性规则在真机上因「无 thead 可粘」全部落空（桌面 / 横屏 >600px 不堆叠，故此前只在真机竖屏暴露）。
> ③ **修法**——各列表页 `<MudTable>` **显式加参数 `Breakpoint="Breakpoint.None"`**（先例：`MiscWorkMonthly.razor` 早已如此）。`Breakpoint.None` 表示「任何断点都不切换移动布局」→ >600px 本就不堆叠，**对桌面 / 横屏零影响**；关闭后走 `table-layout:auto` + `.mud-table-cell{white-space:nowrap!important}` → 表格总宽 = Σ 列 min-content（典型 1200~2400px）> 手机容器宽 → `.mud-table-container{overflow-x:auto!important}` 的横向滚动自然生效（即 §6.29「列表页横向滚动」的原始设计意图）。**无需**给表格再加 `width:max-content`（nowrap 已保证不被压缩）。`MudGlobal` 无表格相关配置项 → 无法全局设默认值，只能逐页加参数。
> ④ **改动范围（共 127 处 / 82 个文件）**——a. 含 `ServerData=` 或 `<MudTablePager>` 的**主列表页**（72 文件，多为单表）按「单表文件整文件插入」批改（69 处）；b. 其余**只读展示型 `MudTable`**（主表之外的附属展示表，如详情页明细、报表 Tab 内的数据表）按「块级扫描 + 跳过含输入控件的块」批改（58 处）。⚠️ **有意保留手机端竖排的两类**：**编辑型表**（`Class="edit-table"` 的录入表，如批次/订单创建页、报工录入表）——手机端「列名—值」竖排反而利于录入；**出库操作表** `WarehouseOutbound.razor`（操作型、不接受宽表横拖）。⚠️ 其中 3 处落在未跟踪（`??`）文件内（`InspectionPatrolViewDialog.razor` / `InspectionPatrols.razor` / `NonconformingFeedbacks.razor`），故 `git diff` 只看到 124 处，差额 3 即此。
> ⑤ **配套（同提交）**——`app.css` 第 4 组「宽表」注释块补写本次真根因（MudTable 默认 `Breakpoint=Xs` → `@media(max-width:600px)` 的 `mud-xs-table` 行为 → 修法为逐页 `Breakpoint="Breakpoint.None"`），并修正原先「逐页零改动」的表述（本批确实动了页面，但仅加参数、不改结构）。
> ⑥ **⚠️ 静态资源版本串（同提交抬串，务必）**——`index.html`：`css/app.css?v=8` → **`?v=9`**（`MES.Blazor.styles.css` 无 `.razor.css` 改动，保持 `?v=2`）。
> ⑦ **验证** `dotnet build MES.Blazor` **3 项目 0 错 0 警**；⚠️ **本批改动尚未打包 / 未上线**，真机验证需等下次发布（发布后建议在手机竖屏复核：仓库库存查询、现订单负荷总量、NCR 列表三类宽表）。
> 历史变更（V136）：**宽表手机端可用性收敛——粘性表头 + 粘性首列 + 勾选列场景粘第 2 列 + 滚动区高度放宽 + 工具栏换行**（2026-09-14，**纯 WASM（`app.css` / `index.html`）；后端与 API 零改动，逐页零改动**）：
> ① **背景与拍板**——用户 2026-09-14 追加诉求：手机竖屏看「现订单负荷总量」报表（**15 列**：序号/类别/负荷节点 + 未编制计划/待落实量/待产量/预计天数/预计完成 + 7 个「交期截止负荷量」日期桶；日期桶内联 `min-width:90px` 且表头两行日期 `nowrap` 缩不下去 → 全表约 **1260px**，约 3.2 屏）与**仓库各列表**「根本无法查看」。**拍板：保留全部列、不隐藏，接受大量左右移动**（明确否决「手机端默认收起日期桶」与「逐行卡片化」两个方案），只解决「左右拖动时迷路」。范围 = **各类列表统一收敛**（全仓 `MudTable` **141 文件 / 274 张表**；带独立分组标题栏 `.col-group-header-bar` 的 **42 页**；用 `.list-toolbar` 的 **56 页**）。
> ② **⚠️ 先厘清「横向滚动本身并没坏」**——`table-layout` 在 MudBlazor 6.19 全文**未声明**；app.css 第 137 行 `.mud-table { width:100%; table-layout:fixed }` 作用于 MudBlazor 的**外层 div**（该 div 声明了 `background-color`/`border-radius`，是容器而不是 `<table>`）→ **是一条死规则**；实际 `<table class="mud-table-root">` 走 `table-layout:auto` + `width:100%`（`border-collapse` 全仓无任何规则声明，浏览器默认即 `separate`）。故「列被压缩 / 文字全变省略号」不会发生，`.mud-table-container { overflow-x:auto }` 的横向滚动是**既定且有效**的机制。**真正的问题是「宽表左右拖动时列名与行标识全部滚出屏幕、无法定位」**，而不是滚不动。
> ③ **本次四项增强**（全部挂 `.mh-shell`，桌面 `MainLayout` 零影响）——**a. 滚动区高度放宽**：`max-height: max(260px, calc(100vh - 220px))` → **`calc(100vh - 140px)`**，并补一行 `100dvh` 版本（移动端动态工具栏下 `100vh` 大于可见高度，不支持的浏览器自动沿用前一行）→ 一屏多显示若干行；**b. `overscroll-behavior: contain`**：横滑到边界不再带动页面 / 浏览器返回手势；**c. ⚠️ 首列是批量勾选框时粘住第 2 列**——`th:first-child:has(.mud-checkbox) + th` / `td:first-child:has(.mud-checkbox) + td` 设 `position:sticky; left:40px`。仓库库存查询等页首列是 40px 复选框，`left:0` 粘住复选框对「认行」毫无帮助，粘住第 2 列（批次号/单号）才有意义；`left:40px` 与勾选列既有约定宽（`MudTh Style="width:40px"`）同值。⚠️ `:has()` 在旧 WebView 上不支持 → **整条规则静默忽略、回落「只粘首列」**，不报错；**d. `.list-toolbar` 窄屏换行**（56 页共用；右侧按钮组靠 `margin-left:auto` 顶行尾，不许换行会被挤扁 / 溢出）。
> ④ **不改动**——分组标题栏 `.col-group-header-scroll`（`overflow:hidden` + 固定像素宽）与表格的横向同步**已由 `table-nav.js: initGroupHeaders` 用 `transform: translateX(-scrollLeft)` 实现**（表格容器 scroll 事件 + ResizeObserver + MutationObserver），手机端无需改；横向滚动所需的 `overflow-x:auto` 已在 `.mud-table-container` 基线样式中（见 §6.29），本次**未新增**。
> ⑤ **有意不做的两件事**——**不隐藏任何列**（用户拍板保留全列）；**不给 `WorkOrderLoadOverview` 加 `IsMobile` 分支 / 默认收起日期桶 / 逐行卡片化**（用户明确否决，见 ①）。
> ⑥ **⚠️ 静态资源版本串（同提交抬串，务必）**——`index.html`：`css/app.css?v=7` → **`?v=8`**（`MES.Blazor.styles.css` 无 `.razor.css` 改动，保持 `?v=2`）。
> ⑦ **验证** `dotnet build MES.Blazor` **3 项目 0 错 0 警**（纯 CSS / HTML 改动，无 `.razor` / `.cs` 变更、无单测影响）。⚠️ **本批改动尚未上线**，真机验证需等下次发布。
> 历史变更（V135）：**手机竖屏全站收敛——新增「全站手机壳」`.mh-shell` 钩子（除首页外所有页面的竖屏统一样式）+ 撤销自动弹「请横屏」提示条**（2026-09-14，**纯 WASM（`MobileLayout.razor` / `app.css` / `index.html`）；后端与 API 零改动**）：
> ① **背景与拍板**——全仓 160+ 页面在手机上渲染的都是同一套桌面布局（**只有首页有 `IsMobile` 手机分支**），竖屏表现为「页头按钮挤成一团 / 筛选工具栏控件平铺溢出 / 宽表只能左右拖 / 内容贴边」。用户 2026-09-14 拍板**「竖屏也要可用」**，放宽 2026-09-06「除首页 + 扫码流外一律横屏查看」的策略；实现方式为**一层全局 CSS 统一收敛，不改 160+ 个页面本体**。
> ② **⚠️ 钩子 = `MobileLayout.razor` 里真实渲染的 `<div class="mh-shell">`**（包住 `<CascadingValue IsMobile>` 与 `@ChildContent`）——**不依赖「祖先容器 + 后代选择器」的推断链**。这是 V134 教训的直接落实：上一次真机样式失效的根因正是钩子挂 `.mh-container` 在真机未命中（F12 模拟命中、真机不命中，整批规则静默回落桌面值）。桌面 `MainLayout` 不加此类 → **桌面版零影响**。
> ③ **全局收敛四项**（`app.css` 新增「全站手机壳」段，全部挂 `.mh-shell`）——**a. 页头按钮组**：`.mud-card-header` 允许换行、`.mud-card-header-actions` 独占一行（`width:100%` + `margin:0` + `align-self:auto`，覆盖 MudBlazor 默认的 `flex:0 0 auto` 与 `-8px` 负边距）、按钮 `flex:0 0 auto` 横向换行左对齐；**b. 筛选工具栏 / 表单**：`MudGrid` 是 **flex 容器**（不是 CSS grid），列宽由 `.mud-grid-item-{断点}-{n}` 类的 `flex-basis/max-width` 百分比决定 → `.mh-shell .mud-grid > .mud-grid-item { flex-basis:100% !important; max-width:100% !important }` 即逐行全宽堆叠；行内工具栏 `.d-flex` 允许换行；**c. 留白与密度**：`.mud-container` 左右 `12px`、`MudCardContent` `10px 12px`、`MudCardActions` `6px 12px`；**d. 宽表**：粘性表头 + 粘性首列（详见 ④）。
> ④ **宽表「粘性表头 + 粘性首列」实现要点**——表头 sticky 需要「有界高度 + 纵向可滚」的容器，故 `.mh-shell .mud-table-container { overflow-y:auto; max-height: max(260px, calc(100vh - 220px)) }`；**只让表头最后一行 sticky**（`.mud-table-head tr:last-child th { position:sticky; top:0 }`）—— 两行分组表头（`col-g1`/`col-g2`）时保留带列名的那一行，且避免两行都钉 `top:0` 互相重叠；首列 `.mud-table-head tr:last-child th:first-child` 与 `.mud-table-body td:first-child` 设 `position:sticky; left:0` + **不透明背景**（否则横向滚动时后面的列会从它下面透出来）+ 右侧 `1px` 分隔线；z-index 分层：表头 5 / 首列数据格 4 / 左上交叉格 6。**配套**：`border-collapse` 由 `collapse` 改 `separate` + `border-spacing:0`（`collapse` 下粘性单元格自己的底边框不随固定元素绘制、表头底线会消失；MudBlazor 自身的 `StickyHeader` 实现亦用 `separate`；全仓表格单元格只设 `border-bottom`、无相邻竖向边框，故观感不变）。⚠️ 本页表格不使用行级底色（全仓 `RowClass=` **0 处**），故首列固定白底不会盖掉行色；**将来若新增带底色的行，需给该行首格单独指定 `background`**。四条表格规则包在 `@media screen` 内，避免影响打印。
> ⑤ **撤销自动弹「请横屏」提示条（用户拍板）**——`MobileLayout.razor` 移除 `<LandscapeHintBanner />` 调用（**原位保留注释与「恢复只需把这一行加回」说明**）；`LandscapeHintBanner.razor` 组件与 `LandscapeHintRule.cs` 判定规则**均保留不删**、`LandscapeHintRuleTests` 8 例照旧通过（只测规则不测渲染）；`ResponsiveLayout` 的**「横屏 ≥700 自动切桌面宽表」保持有效**（横屏看宽表这条路仍在）。
> ⑥ **⚠️ 静态资源版本串（同提交抬串，务必）**——`index.html`：`css/app.css?v=6` → **`?v=7`**（`MES.Blazor.styles.css` 无 `.razor.css` 改动，保持 `?v=2`）。不抬串则真机（已缓存 `?v=6`）拿不到本次全部竖屏收敛。
> ⑦ **验证** `dotnet build MES.Blazor --no-incremental` **3 项目 0 错 0 警** + 定向单测 **22 过**（`LandscapeHintRuleTests` / `AppMenuTests` / `HomeQueryCardsTests`）。⚠️ 因 `MES.Api` 进程与 Visual Studio 锁定 `MES.Api\bin`，本次用 `dotnet test --no-build` 跑既有测试程序集（本次测试改动仅 XML 注释，无行为变化）。
> 历史变更（V134）：**首页常用入口磁贴桌面端字号放大 1 号 + 手机端（真机）磁贴/查询卡样式钩子改挂 `.mh-card` + 窄屏半屏卡兜底 + 清 8 条死 CSS**（2026-09-14，**纯 WASM（app.css / index.html）；后端与 API 零改动**）：
> ① **桌面端常用入口磁贴字号放大 1 号（用户指定「字体请放大 1 号」，经确认仅桌面端）**——`app.css` 的 `.shortcut-tile { font-size: 14px }` → **`16px`**；手机端字号不随之放大，仍由下方 `.mh-card .shortcut-tile` 规则收敛为 `12px`（级联优先级更高）。
> ② **⚠️ 手机端与 F12 模拟「不一致」根因 = 手机端样式钩子用了 `.mh-container` 后代选择器**——原手机端规则写作 `.mh-container .shortcut-grid` / `.mh-container .shortcut-tile`（及 `.mh-container .home-query-*`），**依赖 `.mh-container` 在真机 DOM 中命中**；F12 设备模拟下命中了 3 列布局，真机却未命中 → 全部手机端规则整体失效、回落桌面值（`flex: 1 1 160px` → 窄屏挤成 2 列），表现为**「真机比 F12 看到的更拥挤、每行不是 3 个」**。**修复**：钩子由 `.mh-container`（祖先）**改为卡自身 `.mh-card`**（`MobileLayout` 每张卡都带 `mh-card`，命中最稳），涉及 `.mh-card .shortcut-grid` / `.mh-card .shortcut-tile` / `.mh-card .shortcut-tile span` / `.mh-card .home-query-bar` / `.mh-card .home-query-actions` / `.mh-card .home-query-result` 六条；`.mh-container { padding-top:0; padding-bottom:16px }` 保留不变。
> ③ **手机端磁贴改「上下结构」（icon 在上、文字在下）**——`.mh-card .shortcut-tile { flex: 1 1 28%; flex-direction: column; gap: 2px; padding: 8px 2px; font-size: 12px; line-height: 1.2 }` + `span { text-align: center; word-break: keep-all }`：三列并排（`flex-basis: 28%`）+ 竖排防长名（如「不合格反馈扫码」6 字）在窄格内被压成多行而显得拥挤；`gap: 6px` 收窄格间距。
> ④ **窄屏「半屏卡」兜底**——新增 `@media (max-width: 959px) { .home-stack-card { width: 100% } }`：`home-stack-card` 桌面端为半屏宽居中（两卡并排），若**真机被判进桌面布局**（JS 移动判定未命中）则卡片只占半屏、右侧留白 → 窄屏一律撑满整宽兜底。
> ⑤ **删除 8 条死 CSS（V133 删卡后遗留）**——`.stage-pill` / `.stat-count` / `.stat-weight` / `.stat-unit` / `.stat-sep` / `.stat-muted` / `.stat-caption` / `.mh-stage-card` 全仓 grep 零引用（原「订单负荷实时状况」卡专用，V133 已从首页删除），一并清除。
> ⑥ **⚠️ 静态资源版本串（同提交抬串，务必）**——`app.css` 内容变更 → `index.html`：`css/app.css?v=5` → **`?v=6`**（`MES.Blazor.styles.css` 本次无 `.razor.css` 改动，保持 `?v=2`）。**前者不可省**：nginx 对 `location /css/` 下发 `max-age=31536000, immutable`，真机（可能已缓存 `?v=5`）不抬串即拿不到新样式 —— 本轮真机问题即含此风险。
> 验证 `dotnet build MES.Blazor --no-incremental` **3 项目 0 错 0 警**（纯前端改动，无 `.razor` / 后端 / 单测影响）。
> 历史变更（V133）：**首页删除「订单负荷实时状况」卡片 + 两张查询卡补「清除」（取消查询）按钮**（2026-09-14，**纯 WASM + `Index.razor.cs` 瘦身 + app.css / index.html；后端与 API 零改动**）：
> ① **删除首页「订单负荷实时状况」卡片（用户决策）**——`Pages/Index.razor` 桌面分支（`MudTable` 三阶段表 + 合计 Footer）与手机分支（`.mh-stage-card` 阶段色条卡）**两处整块移除**；`Pages/Index.razor.cs` 随之瘦身为**仅剩 `[CascadingParameter(Name="IsMobile")]`**，删除 `WorkOrderStageRow` / `BuildCrossTable` / `Tons` / `_rows` / 四个 footer 字段 / `OnInitializedAsync` 与 `@inject WorkOrderExecutionService`（**首页自 V133 起无任何常驻取数**，数据一律由子卡按用户查询触发）。桌面端 `.home-stack` 由「常用入口 + 订单负荷」两卡变为「常用入口 + 订单进度查询 + 生产批次进度查询」三卡，手机端同序纵向堆叠。
> ② **⚠️ 只下线首页入口，不清理链路**——后端 `GET api/workorder-execution/dashboard-summary` 端点、`IWorkOrderExecutionService.GetDashboardSummaryAsync`（`MES.Services`）、前端 `MES.Blazor.Services.WorkOrderExecutionService.GetDashboardSummaryAsync` **全部保留**（本次仅移除首页唯一调用点，`MES.Api` / `MES.Services` 零改动）；聚合口径留档见《看板上下文详细设计》V3.16「订单负荷实时状况（V3.16 已从首页删除）」节。如需连同后端一并删除请另行确认。
> ③ **两张查询卡新增「清除」按钮 = 取消查询（用户反馈「不知道怎么取消」）**——`Shared/OrderProgressQueryCard` 与 `Shared/BatchProgressQueryCard` 的查询按钮右侧各加 `Variant.Outlined` / `Color.Default` / `StartIcon=Icons.Material.Filled.Clear` 的「清除」按钮，`OnClick="Clear"`、`Disabled="_loading || !CanClear"`（`CanClear = _searched || 输入非空`，空态下不常亮）。`Clear()` 清空输入框 + 结果 + `_searched`，**回到未查询空态**（批次卡另清 `_resolvedBatchNo` / `_batchId`）；**纯前端复位，不触后端、不影响 `/orders/progress` 与批次详情页**。此前只能刷新页面才能取消。
> ④ **样式**——`app.css`「首页查询卡」段新增 `.home-query-input`（`flex:1 1 auto; min-width:0`，输入框吃满剩余宽度）与 `.home-query-actions`（查询/清除成组，手机端 `justify-content:flex-end` 不被逐个撑满整行）；`app.css` 抬串 **`?v=4` → `?v=5`**（`MES.Blazor.styles.css` 本次无 `.razor.css` 改动，保持 `?v=2`）。
> ⑤ **验证** `dotnet build MES.sln --no-incremental` **9 项目 0 错 0 警** + 定向单测 **9 过**（`HomeQueryCardsTests` 由 3 → **5** 例：新增「订单卡点清除 → 结果清空、且再点查询不发起请求」「批次卡点清除 → 提示消失、进度卡不渲染」；`HomeQueryEndpointAuthorizationTests` 4 例不变）。
> 历史变更（V132）：**首页新增两张「卡内查询」卡（订单进度 / 生产批次进度）+ 三个数据源端点放宽为仅登录 + 订单进度树抽为共享组件**（2026-09-14，**后端 3 端点授权 + 前端；无 EF 迁移 / 无数据变更**）：
> ① **需求（用户拍板）**——首页建立 2 个查询：「订单进度」的查询结果＝**订单进度树**、「生产批次进度」的查询结果＝**批次执行进度卡片**，两者都**只在首页就地渲染、不是链接**（不跳 `/orders/progress`、不跳批次详情页）；取数口径＝**卡内查询框**（输入订单号 / 生产编号，回车或点「查询」）；订单进度树在首页走**折叠态摘要**（**全部主号默认折叠**，点主号才展开分支与重量叶）。
> ② **权限（用户拍板「首页人人可查」）**——首页 `Index.razor` 只有 `[Authorize]`（仅登录），故三个数据源端点**去掉业务角色档、改为仅需登录**：`GET api/order/progress`（原 `OrderView`）、`GET api/batch/by-batch-no/{batchNo}`（原 `BatchView`）、`GET api/batch/{id}/tracking`（原 `BatchView`）。⚠️ **副作用**：`by-batch-no` 返回 `ProductionBatchDetailDto` 全量，放宽后全体登录用户可读批次主数据（菜单门控不变——「生产执行」整页仍按 `BatchMenu` 隐藏）；⚠️ **按 Id 取详情 `GET api/batch/{id}` 保持 `BatchView` 档**（仍属「生产执行」页职责，未被顺手放开，有测试护栏）。
> ③ **组件抽取（⚠️ Blazor scoped CSS 不能跨组件）**——`Pages/Orders/OrderProgress.razor(.cs)` 的树本体抽为 **`Shared/OrderProgressTree.razor` + `.razor.cs` + `.razor.css`**（64 条 `.op-*` 规则**整文件 `git mv`** 自 `Pages/Orders/OrderProgress.razor.css`；页面级打印 `<style>` 块留在原页 —— `.op-main-node` / `.op-print-footer` / `.op-print-hide` 是普通类选择器、不带 scoped 属性，仍能命中）。组件新增 `[Parameter] bool Compact`：`false`（`/orders/progress`）＝**原行为**（非完结主号默认展开、完结主号默认折叠）；`true`（首页卡）＝**全部主号默认折叠**。默认折叠态初始化由 `OnInitializedAsync` 移到 `OnParametersSet`，并以**树实例引用比较**（`_collapsedInitializedFor`）守卫 —— 否则父组件每次重渲染都会把用户手动展开的主号重置回默认折叠。`/orders/progress` 页行为零变化（改为渲染 `<OrderProgressTree Tree="_tree" />`，打印 / 返回 / 页脚 / 打印样式块全部保留）。
> ④ **新增两卡**——`Shared/OrderProgressQueryCard.razor(.cs)`（订单号 → `OrderProgressService.GetAsync` → `<OrderProgressTree Tree Compact="true" />`）与 `Shared/BatchProgressQueryCard.razor(.cs)`（生产编号 → `BatchService.GetByBatchNoAsync` 取 `Id` → 内嵌既有 `Shared/BatchProgressCard`，即「批次执行进度」组件）。两卡自带 `[CascadingParameter(Name="IsMobile")]` → 手机端外壳 `mh-card pa-3` / 桌面端 `home-stack-card`（半屏宽居中，与首页其它卡一致），故 `Index.razor` 桌面 / 手机**两分支各只写两行组件标签**（不复制标记）。结果区 `.home-query-result` 限高 `420px` 纵向滚动（手机端不限高、随页面滚动），查询条 `.home-query-bar`（手机端改纵向堆叠）。查不到时给 `MudAlert` 业务提示且不渲染结果。
> ⑤ **静态资源版本串（同提交抬串，见 V131 教训）**——`app.css` 新增 `.home-query-bar` / `.home-query-result` / `.mh-container .home-query-*` 规则、`MES.Blazor.styles.css` 因 scoped CSS 迁移而变动 → `index.html`：`css/app.css?v=3` → **`?v=4`**、`MES.Blazor.styles.css?v=1` → **`?v=2`**。
> ⑥ **验证** `dotnet build --no-incremental` **9 项目 0 错 0 警** + 定向单测 **99 过**（新增 `MES.Tests/Components/HomeQueryCardsTests` 3 例：就地渲染树 + Compact 全折叠 + 点主号才展开、订单查不到给提示；`MES.Tests/Controllers/HomeQueryEndpointAuthorizationTests` 4 例：三端点无角色档且仍保留 `[Authorize]`、按 Id 取批次详情仍带 `BatchView`）。
> 历史变更（V131）：**两个源列表页补齐「往来信息」表头强调色 + 静态资源版本串纪律修复**（2026-09-14，**纯 WASM + index.html，后端 / DB / 端点零改动**）：
> ① **强调色同步到源列表页（用户指定，承接 V129 遗留的「源列表页本次未同步」）**——供应商 `/suppliers` 的「**待收货**」列加**暖橙**（同客户往来「待发货」范式）、委外 `/outsource-vendors` 的「**委外未回收**」列加**冷紫**（同「待在产」范式），与报表总览 Tab4/Tab5 两卡视觉对齐。实现照抄 V123 客户管理范式：两页 `.razor.cs` 各新增 `private static string GetTradeAccentCss(string key) => key switch { "Pending" => " th-accent-stock" /* 委外页为 th-accent-wip */, _ => "" }`，`.razor` 表头改 `var _headerClass = GetHeaderGroupCss(col.GroupKey, _isGroupStart) + GetTradeAccentCss(col.Key);`。⚠️ 两页「待收货 / 委外未回收」列 `Key` 同为 `"Pending"`、`GroupKey=2`（② 往来信息）；样式归口 app.css 既有 `th.th-accent-stock` / `th.th-accent-wip`（带 `!important`，**必须置于 `.col-g2` 之后**，该处注释已同步枚举四处入口）。至此「待发货 / 待收货 / 待在产 / 委外未回收」四处口径的**三张报表卡 + 三个源列表页**（`/customers` V123、`/suppliers`、`/outsource-vendors`）全部同源同色。
> ② **静态资源版本串纪律修复（部署级缺陷，同日排查）**——`index.html` 的 `css/app.css?v=2` 自 **2026-09-04**（commit `ede83097`）起未再抬串，而 app.css 其后至少修改 3 次（含本次把「执行进度」卡片的 20 个 `.detail-*` 规则由 scoped 的 `BatchDetail.razor.css` **上提至 app.css** —— HEAD 版 app.css 含 `detail-` **0** 处、工作树 **20** 处，与 `BatchProgressCard.razor` 用到的类名逐一精确吻合）；而 env nginx（`scripts/deploy-windows/nginx-mes.conf.template`）对 `location /css/` 下发 `Cache-Control: public, max-age=31536000, immutable` → **浏览器取过一次 `app.css?v=2` 后一年内不再重取** → 新上提的 `.detail-*` 全部拿不到，「执行进度」卡片表现为**「原来设定的显示样式没有了」**（`MES.Blazor.styles.css` 亦无版本串，同一机制会命中完全依赖 scoped CSS 的订单进度树页）。**修复**：`css/app.css?v=2` → **`?v=3`**、`MES.Blazor.styles.css` → **`?v=1`**（对齐 `print.js?v=14` / `table-nav.js?v=12` / `scanner.js?v=5` 既有约定），并在 index.html 该两行上方补**版本串纪律注释**（内容一改必须同步抬串）。
> 验证 `dotnet build MES.Blazor` 0 错 0 警（纯前端改动，无后端 / 单测影响）。
> 历史变更（V130）：**「投料产出总况」完成日期范围搜索口径修正（两处入口同步 + 脚注按模式切换）**（2026-09-14，**后端 1 处 + 前端文案；无 EF 迁移 / 无数据变更 / 无权限变更**）：
> ① **问题**——V126 引入的区间判定服务端按「**完成月月首**」锚定（`new DateTime(doneAt.Year, doneAt.Month, 1)` 参与 [起, 止] 比较），导致**起日非月初 → 整个起始月被整月剔除**、**止日非月末 → 整个截止月被整月纳入**，用户按「完成日期」设定范围时数据对不上。真库实证（全部口径，全库完成订单 17 单）：区间 `2026-01-15 ~ 2026-02-20` 旧逻辑命中 **5 单**（1 月因月首 `01-01 < 01-15` 整月丢失），按真实完成日应为 **9 单**。
> ② **修正（用户拍板方案 A 最小改动）**——`MES.Services/Order/OrderThroughputQueryService.cs` 区间判定改为 `doneAt.Value.Date` 直比闭区间（**起止当日均含**）；整区间仍聚合为 1 行、默认 12 个月窗口行为零变化；接口 / Controller XML 注释口径同步。
> ③ **脚注按模式切换**——两处入口卡片（`Orders.razor` `#order-throughput-table` 与 `ReportOverview.razor` `#report-throughput-table`）脚注由新增的 `ThroughputFootnote()` 输出：**区间模式显式声明「按订单真实完成日落入区间过滤、整区间聚合为 1 行」+ 各列为命中订单的全生命周期合计（与完成日不相关）——生产投料 = 各批次工艺卡领料重之和、入库列 = 各批次入库量之和，区间之外发生的投料/入库量也会计入本行**（`ProductionBatch` 无投料日期字段，逐列按事件日期切分不可行，故不改口径、只补说明）；报表卡头说明改为「按完成日落入所选区间，整区间聚合为 1 行」。
> ④ 验证 `dotnet build MES.Api` + `dotnet build MES.Blazor` 0 错 0 警 + 定向单测 `OrderThroughputQueryServiceTests` **19 过**（新增 1 例「边界按真实完成日直比含起止当日」；改写 1 例原「月首不入区间的订单被排除」为「按真实完成日纳入」）。
> 历史变更（V129）：**报表总览「业务总况」Tab 卡片顺序调整 + 「订单接单」卡标题行结构统一**（2026-09-14，**纯 WASM，后端 / DB / 端点零改动**）：① **卡序调整（用户指定）**——「业务总况」Tab 三张独立卡由「客户往来数据 → 订单接单·出库及现负荷汇总 → 投料产出总况」改为「**订单接单·出库及现负荷汇总（含订单完成预估 / 延期交货订单预估 2 张子表）→ 投料产出总况 → 客户往来数据**」，即「客户往来数据」移到「投料产出总况」**下方**（`ReportOverview.razor` 仅物理顺序调整，卡内容 / 折叠键 `report:*-trade` / 打印 id 全不变）。② **「订单接单」卡标题行结构统一**——原为 `div.d-flex.justify-space-between.align-center.mb-2` + 内层 `div.d-flex.align-center style="gap:8px"`，内层 8px gap 使标题色条 / 标题文字比另两卡**右移 8px**（用户反馈「没有与其它 2 个左对齐」）→ 改为与「客户往来数据」「投料产出总况」完全同款：`div.d-flex.align-center.mb-2` + `MudIconButton` + 标题（`MudText subtitle2 Class="ml-1"`）+ 说明（`MudText caption Class="text-hint ml-2"`）+ 打印按钮包在 `<div class="ml-auto">` 内。验证 `dotnet build MES.Blazor --no-incremental` 0 错 0 警。③ **「订单接单·出库及现负荷汇总」表结构重构（方案 B，用户指定）**——原表 5 行 × 12 月矩阵把 **3 个当前时点存量**（成品库存(完工)/成品库存(未完工)/订单负荷量(实时)）挤在**当月列**（`RenderCurrentOnly` 仅 `_currentMonthIndex` 有值、其余 `-`）→ 语义错位（月份列头放存量）+ 潜在跨年错位（`_currentMonthIndex` 与表格年份无关，切年会把本年存量标进他年同月）：**① 12 月矩阵只留流量行**（接单量按签订月 / 出库量按出库月）；**② 存量 3 行拆为表下独立小表「当前负荷快照」**（`指标 | 当前值` 两列，同卡内、同属打印 id `#report-inout-summary-table` 故打印一次全含，无新 id / 无新打印按钮）；**③ 「12月」后新增「汇总」列**（表头浅蓝底 `#e8f0fe`、单元格 `#fafcff`；口径=**本年 12 个月合计**，接单量/出库量两行均给；前端 `Sum()` 直算，新助手 `RenderYearTotalCell(weightKgByMonth, amountYuanByMonth)`，复用 `OrderOverviewFormatter.RenderInOutCell`）；**④ 删除** `ReportOverview.razor.cs` 的 `RenderCurrentOnly` 与 `_currentMonthIndex`（二者为该处唯一用途，无死代码残留；`MonthlyStock.razor.cs` 的同名私有字段属另一页，未动）；**⑤ 卡头说明文案**同步。④ **报表总览 5 项样式统一（2026-09-14，纯 WASM）**——**a. Tab2「现订单负荷总量」全表居中（⚠️ 此项即用户原话「现订单负荷总量，表头字段与数据值均居中显示」所指 —— 该 Tab 页签名 = 「现订单负荷总量」，嵌入 `Pages/Scheduling/WorkOrderLoadOverview.razor` 组件，`/plan-overview` 页复用同一组件故两处同步生效）**——`未编制计划(吨)` / `待落实量(吨)` / `待产量(吨)` / `预计天数` 4 列表头由 `Class="text-right col-g100"` → `Class="text-center col-g100"`（原表头右对齐而数据格 `text-center`，左右不一致），`类别` / `负荷节点` 的表头与数据格（`CellClass(row, "")` → `CellClass(row, "text-center")`）一并居中；⚠️ `app.css` 的 `.auto-table th { text-align:left !important }` 会压掉 th 上的 `text-right`（**只有 `text-center` 有对应 th 规则见效**），汇总行分类底色类 `summary-*` / `row-overall` 保留。另 Tab1「订单接单·出库及现负荷汇总」卡两表（流量矩阵 + 当前负荷快照）的「指标」列也一并居中（同批，使该卡整表居中）；**b. Tab4 供应商往来「待收货」表头加暖橙强调色**（`background:#fff3e0; color:#E65100; border-bottom:2px solid #ffb74d; font-weight:700`，与「客户往来数据」待发货同范式）；**c. Tab5 委外单位往来「委外未回收」表头加冷紫强调色**（`background:#f3e5f5; color:#6A1B9A; border-bottom:2px solid #ce93d8`，与「客户往来数据」待在产同范式）——三处均走**报表卡内联样式**（打印天然带上），⚠️ 源列表页 `/suppliers`、`/outsource-vendors` 对应列**本次未同步**；**d. Tab5「段落流转分析」由 `MudTable Class="auto-table"`（无格线）改为带纵横格线的原生 HTML 表格**，样式对齐同 Tab 的「冷轧拔近日排程」（`border-collapse: collapse` + 每格 `border:1px solid #e0e0e0` + 表头 `background:#f5f5f5`），数值列居中、`生产段落` 列居左加粗，页脚沿用 `col-footer-cell` / `col-footer-sum` 且首格补「合计」标签；**e. 全页数值列表头去右对齐**——`MudTh Class="text-right"` 3 处（Tab4 半成品待购 / 成品待购「待购量(kg)」、委外待穿孔「缺少量(kg)」）→ `Class="text-center"`（原先表头右对齐而数据格 `text-center`，且受 `app.css` `.auto-table th { text-align:left !important }` 干扰，实际渲染与数据格不一致）。验证 `dotnet build MES.Blazor --no-incremental` 0 错 0 警。
> 历史变更（V128）：**「供应商往来数据」与「委外单位往来数据」两卡补齐「日期区间」（双区间，各两处入口同步）**（2026-09-14，**后端 + 前端；无 EF 迁移 / 无数据变更 / 无权限变更**，对齐 V125「客户往来数据」范式）：
> ① **区间形态 = 双区间（互相独立、可叠加）**——**供应商**：`出单区间`（`SupplierOrderDateFrom/To`，驱动「本年出单」→「**区间出单**」，取采购/委外单 `OrderDate`）+ `到货区间`（`SupplierArrivalDateFrom/To`，驱动「本年到货[扣除退货]」→「**区间到货**」、「本年退货」→「**区间退货**」，取入厂批 `InventoryBatch.InboundDate` / 退货 `OutboundRecord.OutboundDate`；两列同源同一窗口）。**委外单位**：`发出区间`（`VendorSendDateFrom/To`，驱动「本年委外」→「**区间委外**」，取工段委外单 `SendOutDate`）+ `回收区间`（`VendorRecoveryDateFrom/To`，驱动「本年回收[扣除退回]」→「**区间回收**」、「本年退回」→「**区间退回**」，取委外回收 `RecoveryDate`）。**存量列不参与区间**（待收货 / 委外未回收）与**累计列不参与**（累计出单 / 累计委外）。
> ② **后端**——`QueryParams` 新增 8 个属性（`SupplierOrderDateFrom/To`、`SupplierArrivalDateFrom/To`、`VendorSendDateFrom/To`、`VendorRecoveryDateFrom/To`）；`SupplierService.BuildTradeStatsAsync(ctx, year, rows, orderFrom, orderTo, arrivalFrom, arrivalTo)`（原 `yearArrivalByOrder` 改名 `arrivalByOrder`、局部函数 `InArrivalWindow`）与 `OutsourceVendorProfileService.BuildOutsourceStatsAsync(ctx, year, rows, sendFrom, sendTo, recoveryFrom, recoveryTo)`（局部函数 `InSendWindow` / `InRecoveryWindow`）各拆两套独立窗口（结束日按项目惯例 `< To.AddDays(1)` 闭区间含当天）；两个 `list` 端点各加 4 个 `[FromQuery] DateTime?`；Blazor 客户端 `SupplierService` / `OutsourceVendorService` URL 拼 `orderDateFrom/orderDateTo/arrivalDateFrom/arrivalDateTo`、`sendDateFrom/sendDateTo/recoveryDateFrom/recoveryDateTo`（`yyyy-MM-dd`）。**区间模式下服务端一并做行过滤**（`GetPagedInRangeModeAsync`：先按「激活列有数据」过滤档案行再分页，`TotalCount` 返回过滤后条数；档案量级小，内存过滤可接受），`ActiveStatsColumns` / `HasDataInActiveColumns` 与前端列激活**键名须同步**（供应商 `YearOrder` / `Arrived` / `YearReturn`；委外 `YearOrder` / `YearRecovered` / `YearReturn`；⚠️ **报表卡键名不带 -ing，源列表页键名带 -ing** `YearOrdering` —— 与客户往来同款两套，勿混）。
> ③ **报表卡**（`ReportOverview.razor(.cs)`）——「供应商往来数据」（Tab4 物料执行）与「委外单位往来数据」（Tab5 生产执行）两卡工具栏改 **V127 组合范式**：外层 `gap:16px` 组间隔离 + 组内 `gap:6px` 紧凑、日期框 `140px`、`MudDivider` 竖分隔；新增 `_supplierTradeOrder/ArrivalFrom/To`、`_outsourceTradeSend/RecoveryFrom/To` 等状态与 `XxxTradeOrderRangeMode`/`ArrivalRangeMode`/`AnyRangeMode`、`ActiveTradeColumns()`、`TradeColumnActive(col)`、`TradeHeader(col)`、`RangeHint()`（三分支）、`OnXxxChangedAsync()`、`ClearXxxRangeAsync()`；`SupplierTradeCell`/`SupplierTradeSummary`/`OutsourceTradeCell`/`OutsourceTradeSummary` **由 static 改实例方法**（须判区间），未激活列渲染灰色「—」；`SupplierTradeVisible()`/`OutsourceTradeVisible()` 区间生效时先剔除「激活列全为 0」的行；公共 `ParseCustomerTradeDate` **更名为 `ParseTradeDate`**（客户 4 处调用同步）。⚠️ `var supplierRows/outsourceRows = XxxTradeShown();` **上提为 `@if` 块内裸声明**（代码上下文内不得再用 `@{` 切换，否则 `RZ1010`）。
> ④ **源列表页两处同步**——「供应商管理」`/suppliers`（`Suppliers.razor(.cs)`）与「委外单位档案」`/outsource-vendors`（`OutsourceVendors.razor(.cs)`）：搜索栏 `MudGrid` 下方新增同款双区间工具栏 + 口径提示条；`TradeColumnLabel(col)` 接表头（`@col.Label` → `@TradeColumnLabel(col)`）；② 往来信息**未激活列在分页合计（`ComputePageSums`）置「—」**；`ApplyRangeAsync()` 置 `_resetToFirstPage` 后 `ReloadServerData()`。
> ⑤ 验证 `dotnet build MES.Api` + `dotnet build MES.Blazor` 0 错 0 警 + 定向单测 `SupplierServiceTests` / `OutsourceVendorProfileServiceTests` **46 过**。
> 历史变更（V127）：**「客户往来数据」工具栏分组紧凑化 + 组间隔离（两处入口同步）**（2026-09-14，**纯 WASM，后端 / DB / 端点零改动**）：原工具栏所有控件平铺于单一 `gap:8px` 容器 → 改为**外层 `gap:16px` 组间隔离 + 组内 `gap:6px` 紧凑**的组合：①「搜索业务员/最终用户」（独立）②「接单日期起/止 + 应用接单区间 + 清除」③「发货日期起/止 + 应用发货区间 + 清除」④**仅报表总览卡**另有「显示行数下拉 + 计数注释（显示 N / 共 M 条｜合计按显示行计）」；②③ 之间保留 `MudDivider` 竖分隔（`height:28px`，去掉原 `margin:0 2px`，间距由外层 gap 提供）；日期输入框宽 150 → **140px**（与「投料产出总况」日期范围组一致）；做法对齐「投料产出总况」日期范围组合范式（`d-flex align-center` 子容器 + `gap:6px`，组内视为一个整体）。**两处入口**：报表总览 `/reports/overview`「客户往来数据」卡（`ReportOverview.razor`）与源列表页「客户管理」`/customers`（`Customers.razor`，该页 `MudGrid` 4/8 布局、左格搜索框即①、右格为②③组合，无「显示行数」控件）。⚠️ `var customerRows = CustomerTradeShown();` 由原 `@{ }` 块**上提为 `@if` 块内裸声明**（代码上下文内不得再用 `@{`，否则 `RZ1010`）。验证 `dotnet build MES.Blazor --no-incremental` 0 错 0 警。
> 历史变更（V126）：**「投料产出总况」两处入口的「完成月搜索」改为「完成日期范围」查询**（2026-09-14，**后端 + 前端；无 EF 迁移 / 无数据变更 / 无权限变更**）：
> ① **动机**——原「完成月搜索」是前端本地按 `yyyy-MM` 模糊匹配，且后端恒返回近 12 个月 → **查不到 12 个月之外的历史区间**。
> ② **后端**——`IOrderThroughputQueryService.GetMonthlySummaryAsync(string? scope, DateTime? dateFrom = null, DateTime? dateTo = null)`；`GET api/order/throughput-summary` 新增 `[FromQuery] DateTime? dateFrom/dateTo`（`yyyy-MM-dd`）。任一端有值即**日期区间模式**：按「**完成月月首落入 [起, 止] 闭区间**」取数（⚠️ **V130 已修正为「按订单真实完成日落入闭区间、起止当日均含」** —— 月首锚定会在起日非月初时整月剔除、止日非月末时整月纳入），**不受 12 个月窗口限制**（可查任意历史区间），且**整个所选区间跨订单聚合为 1 行**（用户拍板：区间查询即汇总口径，非逐月清单）；两端皆空沿用默认「最近 12 个月（含当月）」并按完成月分行；单端为空 = 该端开放；起晚于止 = 空行。Blazor `OrderService.GetThroughputSummaryAsync(scope, dateFrom, dateTo)` 拼 `&dateFrom=&dateTo=`。
> ③ **两处入口同步**——订单列表页 `/orders` 卡片（`#order-throughput-table`）与报表总览 `/reports/overview`「业务总况」Tab 卡（`#report-throughput-table`）：删「完成月搜索」`MudTextField`，改 **「完成日期起 / 完成日期止」两个 `MudTextField T="string"`（`Placeholder="yyyy-MM-dd"`，**禁 `MudDatePicker`**，`Immediate="true"`，`width:140px`）+ 应用 + 清除**（`Disabled` 绑 `!_throughputRangeMode`）。`Orders.razor.cs`/`ReportOverview.razor.cs` 同步：删 `_throughputKeyword`，新增 `_throughputDateFrom/_throughputDateTo`、`_throughputRangeMode`、`ParseThroughputDate`（`TryParseExact` 严格 yyyy-MM-dd，空/非法 → null 该端不参与）、`ApplyThroughputRangeAsync`（重新取数）、`ClearThroughputRangeAsync`（清空 + 回落 12 个月）；`_throughputRows` 由「本地模糊过滤」**简化为直取服务端 `Months`**；空态文案二分支（`该日期区间内暂无完成订单` / `近 12 个月暂无完成订单`）；报表卡头说明文案 `ThroughputRangeCaption()` 随模式切换。
> ④ 验证 `dotnet build MES.Api` + `dotnet build MES.Blazor` 0 错 0 警 + 定向单测 `OrderThroughputQueryServiceTests` **18 过**（新增 5 例：整区间聚合为单行且月首不入区间的订单被排除、默认窗口仍按完成月分行、可查默认窗口外历史完成月、单端开放边界、起晚于止返空）。
> ⑤ **版式三调整（同日追加，两处入口同步）**：**全部数据单元格改居中**（原数值/比率列 `text-align:right` → `center`，表头本就是 center）；**日期范围控件靠右紧凑成组**——用 `MudSpacer` 占位把「完成日期起 / 完成日期止 / 应用 / 清除」四项包进一个 `d-flex align-center` 容器（`gap:8px`、输入框宽 140px）并推到工具栏右侧，四项视为**一个整体**（订单页把该容器置于「打印」之后）；**区间模式下首列语义临时切换**——表头「完成月」→「**选定范围**」，该行（**整个区间聚合的唯一一行**）首列显示**所选日期区间文本**（如 `2026-02-01 - 2026-09-14`，单端为空/非法端点显示「不限」；⚠️ 文本由**后端** `BuildRangeText(dateFrom, dateTo)` 生成写入 `row.Month`，前端直接渲染 `@row.Month`，**前端无 `ThroughputRangeText()`**），打印「所见即所得」同步生效。
> 历史变更（V125）：**客户往来「日期区间」拆分并补齐「发货区间」（双区间独立可叠加）+ 未激活列置「—」+ 工具栏按组重排**（2026-09-14，**后端 + 前端；无 EF 迁移 / 无数据变更**，仅报表卡加 UI，供应商/委外两卡不动）：
> ① **双区间独立**——原单区间同时驱动「接单 + 已发货」两维度（语义混淆）→ 拆为**两个互相独立、可叠加**的区间：**接单区间**（`SignDateFrom/To`，驱动「区间接单」，取 `SalesOrder.SignDate`）与**发货区间**（`ShipDateFrom/To`，驱动「区间已发货(整单/非整单)」，取 `OutboundRecord.OutboundDate`）。各自只影响自己的列，另一维度回落自然年口径。
> ② **列激活（防视觉污染）**——`CustomerTradeActiveColumns()` 返回当前有数据的列集合（未启用任何区间时返回 null = 8 列全正常）：接单区间生效 → 仅 `YearOrder`；发货区间生效 → 仅 `ShippedDone/ShippedOther`；两者叠加 → 三列。**未激活列（含「累计接单」）一律渲染灰色「—」**（`#9e9e9e`，口径为自然年/全时段/当前存量，与所选区间不同源）；表头仅在对应区间生效时改名（「本年接单」→「区间接单」、「本年已发货」→「区间已发货」）。
> ③ **行过滤**——任一区间生效时 `CustomerTradeVisible()` **只保留激活列有值的客户行**（`Count>0 || Weight>0`），防区间口径下满屏空行；未启用区间时维持原「全客户」显示。
> ④ **工具栏重排**（用户指定顺序）——业务员/最终用户搜索 → **接单日期起 / 接单日期止 / 应用接单区间 / 清除** → `MudDivider` 竖分隔 → **发货日期起 / 发货日期止 / 应用发货区间 / 清除** → **显示行数** → 计数文案 →（有区间时）「清空全部区间」；区间生效时卡片追加一行口径说明（`CustomerTradeRangeHint()` 三分支：仅接单 / 仅发货 / 双区间）。
> ⑤ **后端**——`QueryParams` 新增 `ShipDateFrom/ShipDateTo`；`CustomerService.BuildStatsAsync(ctx, year, signFrom, signTo, shipFrom, shipTo)` 拆出 `signRangeMode`/`shipRangeMode` 两套独立窗口（**出库查询用 ship 窗口、接单桶判定用 sign 窗口**）；`CustomerController.list` 加 `shipDateFrom/shipDateTo` 两个 `[FromQuery] DateTime?`；Blazor `CustomerService` URL 拼 `shipDateFrom/shipDateTo`；**区间模式下服务端一并做行过滤**（新增 `GetPagedInRangeModeAsync`：先按「激活列有数据」过滤客户行再分页，`TotalCount` 返回过滤后条数 → 「共 N 条记录」与显示行同口径；客户档案量级小，内存过滤可接受；统计字段由 `ApplyStats` 从同一份桶回填，`AttachStatsAsync` 改为复用它）。
> ⑥ **源列表页「客户管理」同步**（`/customers`，与报表卡同口径）——`MudGrid` 拆 **4/8** 两列：左「模糊搜索」、右一行按 **接单起/止 + 应用接单区间 + 清除 → `MudDivider` → 发货起/止 + 应用发货区间 + 清除 →（有区间时）清空全部区间**；区间生效时下方追加口径提示条。`Customers.razor.cs` 新增 `_signDateFrom/_To`、`_shipDateFrom/_To`、`SignRangeMode`/`ShipRangeMode`/`AnyRangeMode`、`ActiveTradeColumns()`（⚠️ 本页统计列键名为 **`YearOrdering`（带 -ing）**，与报表卡 `YearOrder` 不同）、`IsTradeColumnActive`、`TradeColumnLabel`（动态表头：`@col.Label` → `@TradeColumnLabel(col)`，`ExcelFilter Label` 与 `GetPrintColumnDefs` 同步）、`RangeHint`、`ParseRangeDate`、`ApplyRangeAsync`（置 `_resetToFirstPage` 后 `ReloadServerData`）。② 往来信息**未激活列**在单元格（`RenderCell`）、页脚合计（`ComputePageSums`）、打印（`GetCellDisplayText`）三处统一置灰色「—」；⚠️ `GetCellDisplayText` 因此**由 static 改实例方法**。
> ⑦ 验证 `dotnet build MES.Api` + `dotnet build MES.Blazor` 0 错 0 警 + 定向单测 `CustomerServiceTests` **42 过**（含行过滤 3 例：仅接单区间只留 A、双区间叠加留 A+B、分页发生在过滤之后；另有发货区间 5 例。⚠️ 受行过滤影响改写 5 处旧用例：未命中区间者由「桶为零」改为「行被过滤剔除」、存量口径两例改为区间命中签约/出库日以免行被过滤掉、原「接单区间驱动已发货」改写为「接单区间不驱动已发货窗口」）。
> 历史变更（V124）：**报表总览「客户往来数据」卡新增「接单日期区间」（区间模式）**（2026-09-14，**后端 + 前端；无 EF 迁移 / 无数据变更**，仅报表卡加 UI，供应商/委外两卡不动）：
> ① **背景**——该卡 8 列混了两种时间语义：接单/已发货是**流量**（可按日期参数化），待发货/待在产是**存量**（当前时点快照，无日期语义）。故区间**只作用于流量列**，为避免「一个选择器只影响一半列」的误导，卡头在区间生效时追加一行说明文案。
> ② **行为矩阵**（两端皆空 = 完全现状，8 列照旧）：选定区间后「本年接单」→ **「区间接单」**（`SalesOrder.SignDate` 落区间）、「本年已发货(整单/非整单)」→ **「区间已发货」**（`OutboundRecord.OutboundDate` 落区间）、**「累计接单」列渲染「—」**（灰色 #9e9e9e，其口径为全时段与区间窗口不同源，显旧值会误导；服务端该字段仍照常回填全时段值，仅供参考不被展示）；待发货×2 / 待在产×2 **不受影响**。
> ③ **前端**——卡头搜索行加「接单日期起 / 接单日期止」两个 `MudTextField T="string"`（`Placeholder="yyyy-MM-dd"`，**禁 `MudDatePicker`**）+「应用区间」按钮 +「清除」按钮（无区间时禁用）；`ReportOverview.razor.cs` 新增 `_customerTradeSignFrom/_customerTradeSignTo`、`CustomerTradeRangeMode`、`ParseCustomerTradeSignDate`（`TryParseExact` 严格 yyyy-MM-dd，非法/空返回 null 该端不参与过滤）、`OnCustomerTradeSignChangedAsync`、`CustomerTradeHeader(col)`（表头动态文本）；`CustomerTradeCell`/`CustomerTradeSummary` 由 static 改实例方法以判断区间模式。
> ④ **后端**——`QueryParams` 新增 `SignDateFrom/SignDateTo`（⚠️ 与 `WorkOrderQueryParams` 原同名属性**同语义**，已把子类重复声明删除上提至基类，消除 CS0108 遮蔽告警；工单过滤 `WorkOrder.SignDate`、客户往来过滤 `SalesOrder.SignDate`）；`CustomerService.BuildStatsAsync(ctx, year, signFrom, signTo)` 新增区间模式（结束日按项目惯例 `< To.AddDays(1)` 闭区间含当天）；`CustomerController.list` 加两个 `[FromQuery] DateTime?`；Blazor `CustomerService` URL 拼 `signDateFrom/signDateTo=yyyy-MM-dd`。验证 `dotnet build MES.Blazor --no-incremental` 0 错 0 警 + 定向单测（客户 34 过含新增 6 例 / 工单 254 过）。
> 历史变更（V123）：**客户往来数据补「累计接单」列 + 待发货/待在产表头分组强调色（两处同步）**（2026-09-14，**纯 WASM，后端 / DB 零改动**）：
> ① **补列**——「累计接单」(`CustomerProfileDto.TotalOrderCount/Weight/Amount`) 原仅存于源列表页「客户管理」，报表总览「业务总况」Tab 顶部「客户往来数据」卡缺失 → 在「本年接单」**前**补齐（`ReportOverview.razor.cs` `CustomerTradeValues` 加 `"TotalOrder"` 分支；`ReportOverview.razor` thead/tbody/tfoot 三处同步插入），单行 **7 → 8 统计列**（均 `z单/x吨/y万`）。
> ② **表头分组强调色**（同一「② 往来信息」分组内再区分两组语义，便于扫读）：**待发货（整单/非整单）= 暖橙**（底 `#FFF3E0` / 字 `#E65100` / 底边 2px `#FFB74D`）；**待在产（整单未入库/扣除部分入库）= 冷紫**（底 `#F3E5F5` / 字 `#6A1B9A` / 底边 2px `#CE93D8`）；两组均 `font-weight:700`。
> ③ **两处同步**——报表卡为纯 HTML `<table>`（强调色走**内联样式**，打印 `getTableHtml` 天然带上）；源列表页「客户管理」`/customers` 为 `MudTable`，走 `Customers.razor.cs` 新增 `GetTradeAccentCss(col.Key)` 在 `_headerClass` 上拼 `th-accent-stock` / `th-accent-wip` 类名，样式归口 `app.css`（`th.th-accent-*` **带 `!important`** 且**必须置于 `.col-g2` 规则之后**以覆盖其分组底色）。
> ④ 验证 `dotnet build MES.Blazor --no-incremental`（含 MES.Api）0 错 0 警。
> 历史变更（V122）：**报表总览「业务总况」Tab 新增「投料产出总况」卡 + 全卡可折叠化**（2026-09-14，**纯 WASM，后端 / DB 零改动**，复用订单列表页同一端点）：
> ① **新增卡片**——`ReportOverview.razor` `/reports/overview` 业务总况 Tab 第 3 张卡「投料产出总况」（折叠键 `report:throughput`，表格容器 `#report-throughput-table`，标题分色 `report-tt-3` 紫），**同源订单列表页卡片**（同 `GET api/order/throughput-summary?scope=` + 同 `OrderThroughputSummaryDto`，10 列 × 行=订单完成月、近 12 个月、重量四舍五入取整、比率 0~1 一位小数）；卡内自带**口径下拉**（`全部（荒管+在制+库存+外购）` 默认 / `纯生产` / 单一生产类型 4 档，**禁用可清除叉**）与**完成月模糊搜索**；数据随 Tab1 一并加载进 `LoadTab1Async` 的 `Task.WhenAll`（**失败不阻断主表**，保留 null 显示「暂无数据」）；打印走既有 `PrintTableAsync`（**内建「折叠时先自动展开再取 DOM」**），标题 `投料产出总况（{口径中文}）`。
> ② **业务总况全卡可折叠化**——「订单接单·出库及现负荷汇总」由 `MudPaper` 升为 `MudCard` + 折叠按钮（折叠键 `report:inout-summary`，标题分色 `report-tt-2` 绿），其打印按钮补传 cardKey 以支持折叠态自动展开；至此业务总况 5 张表**全部具备折叠能力**（客户往来数据 / 订单完成预估 / 延期交货订单预估此前已可折叠或随母卡折叠）。
> ③ **折叠默认值采用分级策略**（与物料执行 / 生产执行 / 质量管理三 Tab 一致）：`DefaultCollapsedCards` 新增 `report:throughput`（12 个月趋势历史类，默认折叠）；**订单接单·出库及现负荷汇总默认展开**（核心实时卡）。原注释「业务总况为单区域大表 Tab，不参与折叠」已失效并同步更正。
> ④ **双入口**：订单列表页 `/orders` 原「投料产出总况」折叠卡片**保留不动**（做单视角），报表总览为经营看板视角的第二入口。验证 `dotnet build MES.Blazor --no-incremental`（含 MES.Api）0 错 0 警。
> 历史变更（V121）：**首页看板桌面端两卡由左右等宽改为上下堆叠 + 各约半屏宽居中**（2026-09-14，**纯 WASM，后端 / DB 零改动**）：`Index.razor` 桌面分支去掉 `MudGrid`/`MudItem`（原 `xs=12 md=6` / `md=6`），改为 `.home-stack`（`display:flex; flex-direction:column; gap:16px`）+ 两卡 `.home-stack-card`（**`width:50%; margin:0 auto`**）；卡片 `Style="height:100%"` 移除；样式归口 `app.css`「首页看板」段。手机端本就是全宽纵向堆叠（`mh-*`），未受影响。详见 `看板上下文详细设计.md` V3.15。
> 历史变更（V120）：**首页「常用入口」补齐扫码组三项（9 → 11 项）**（2026-09-14，**纯 WASM，后端 / DB 零改动**）：`AppShortcuts.Items` 在既有「报工扫码」之后新增「**巡检扫码**」（`/mobile-quality/patrol`，`Checklist` + `#00695C`）与「**不合格反馈扫码**」（`/mobile-quality/feedback`，`ReportProblem` + `#AD1457`）——两项 `Policy` 均为 `null`（仅需登录），与侧栏「扫码管理」组整组不挂档一致（该组仅「工位/员工管理」挂 `ScanView`）；扫码组三项相邻排列便于现场操作员定位。防漂移断言 `AppShortcutsTests.常用入口_仅扫码组三项无角色策略` 同步放开（原断言「仅报工扫码无策略」）。手机端与桌面端共用同一份清单，无需改动渲染。详见 `看板上下文详细设计.md` V3.14。
> 历史变更（V119）：**订单列表页新增「投料产出总况」折叠卡片（10 列 × 完成月，口径可切换生产类型范围）**（2026-09-14，**新增 1 后端查询服务 + 1 端点 + 1 DTO，无 EF 迁移 / 无数据变更**）：① **入口**——`/orders` 卡头新增「投料产出总况」按钮（`ToggleThroughputCard`，置于「完成预估及延期风险」之后、「新建订单」之前），**默认折叠、首次展开懒加载**，卡片渲染于「完成预估及延期风险」卡片之后。② **表格**——行 = **订单完成月**（订单级「完成」= `WorkOrderExecutionSummary.ScheduleStage` 按 `0暂停>2原料锁>3生产>4成检>1完成` 归并后 == 1，完成日取该订单各主号 `WarehousingEndDate` 最大值，与订单列表「执行关注=主号完成」绿 Chip 同源），列固定 10 列：`完成月 / 订单数 / 生产投料 / 订单成品入库 / 余库料入库 / 次品入库 / 备料成品 / 投料产出率 / 产出成品比 / 退货`；**固定最近 12 个月（含当月）**、无完成订单的月不产生行；重量 kg **四舍五入取整**（不用本页既有 `FormatDecimalAsInt` 的截断口径）、比率 0~1 一位小数（分母 ≤0 显示 —）；**「订单数」= 该口径下真实相关订单数**（该完成月中在本生产类型范围内有生产批次的订单数，非该月完成订单总数；某月在该口径下无相关订单则**整行隐藏**）。③ **口径切换**（MudSelect，**禁用可清除叉**）——`全部（荒管+在制+库存+外购）`（默认）/ `纯生产（荒管+在制）` / 单一生产类型 4 档（荒管生产/在制生产/库存料生产/外购生产）；返整 / 委外生产 / 对外加工三类一律排除；**「全部」口径订单成品只计交付态 `OrderFinished`（走订单号直取）；「纯生产 / 单一类型」口径订单成品含非交付态 `SpecialDeliveryStatus`（走生产批号反查）**——U 型管常规生产只到非交付态、交付态由外购委外产出，否则该口径整列为空。④ **月度模糊搜索**（MudTextField `Immediate`，按 `yyyy-MM` 本地过滤）。⑤ **退货口径**：次品库 `ReturnOut` 出库，经生产批号反查归属订单；**已分别从「生产投料」与「次品入库」扣减，同时单列供核对**。⑥ **打印**——标题行右侧「打印」按钮（数据未加载时不渲染），走 `getTableHtml("#order-throughput-table")` + `printRawHtml`（Mode A 前端渲染，与同页交期预估小表同模式）；**所见即所得** = 当前口径 + 当前完成月搜索结果；打印标题 `投料产出总况（{口径中文}）`、页脚「打印日期」由 `print.js` `openPrintWindow` 统一输出（默认 landscape 横向）；**不新增 JS 桥接函数**。数据端点 `GET api/order/throughput-summary?scope=`（OrderViewer/OrderEditor/OrderFull/Admin）。详见 `订单模块详细设计.md` §5.14.3、§6.1。
> 历史变更（V118）：**订单进度树新增「打印」（所见即所得，保留折叠状态）**（2026-09-14，**纯前端，无接口 / 数据库变更**）：`/orders/progress` 卡头新增「打印」按钮（`OnPrintAsync` → `await JS.InvokeVoidAsync("window.print")`；「返回订单列表」按钮同加 `op-print-hide`，二者打印时隐藏），走**就地打印**——**按页面当前折叠/展开状态原样输出**（折叠主号的展开区由 `@if (!IsCollapsed(...))` 条件渲染，本就不在 DOM 中，无需额外逻辑、不重新渲染）。配套**非 scoped** `<style>@@media print`（页面末尾）：`@@page { size: landscape; margin: 8mm }`、`body { -webkit-print-color-adjust: exact }`（保留主号灰底 / 比率红字）、隐藏 `.mud-appbar` / `.mud-drawer` / `.op-print-hide`、`.mud-layout` + `.mud-main-content` 解除 flex 与滚动并置 `margin-left / padding-top: 0`（否则多页内容只印首屏、左侧留出抽屉宽度）、`.op-main-node { break-inside: avoid }`；纸面末尾另加**打印页脚** `.op-print-footer`（屏幕 `display:none` / 仅 `@@media print` 显示，上边框 + `font-size:14px`）输出 `打印日期：yyyy-MM-dd`——`_printDate` 在 `OnPrintAsync` 中刷新为 `DateTime.Today`，`StateHasChanged()` + `await Task.Yield()` 后再 `window.print`（保证日期先落到 DOM）。⚠️ 样式必须写在页面内 `<style>` 而非 `.razor.css`——`.mud-appbar` / `.mud-drawer` / `.mud-main-content` / `.mud-layout` 属**其它组件**的元素，scoped CSS 追加 `[b-xxx]` 属性后匹配不到。**不新增 JS 桥接函数**（`print.js` / `index.html` 版本号不动）。该模式已沉淀为 `11_打印设计.md` V2.19 §2.2「就地打印（所见即所得）约定」（并补登此前已有的 `ColdRollPlans.razor`）；详见 `订单模块详细设计.md` V1.28 §5.17.4。
> 历史变更（V117）：**订单进度树「投料产出」两项比率高亮 + 订单级「总投料产出」聚合行**（2026-09-14，**纯前端派生，无接口 / 迁移 / 数据变更**）：① **两项比率高亮** —— 主号灰条汇总行与订单级汇总行的「投料产出率」「产出成品比」改 `.op-sum-hl`（红 `#c62828` + 粗体）；为此 `OrderProgress.razor.cs` 的 `CompletionSummaryText`（原返回单串）重构为 `BuildCompletionSummary` 返回**分段列表** `List<OpSummarySegment>`（`record OpSummarySegment(string Text, bool Highlight)`，比率项 `Highlight=true`），razor 逐段 `<span>` 渲染（段间 `；`、末尾 `。`），汇总行容器改 `display:flex; flex-wrap:wrap`（避免 Razor 换行空白落进文本、按段边界折行）。② **订单级「总投料产出」行** —— `MainNos` **非空且全部 `IsCompleted`** 时，在**订单号块**（`op-root-meta`，「含项次数」下方）渲染 `.op-root-line3`：`总投料产出：生产投料500kg；订单成品入库400kg；余次备产出入库50kg；投料产出率90.0%；产出成品比88.9%。` = **各主号三项净量直接求和**（复用 `Nets(main)`；跨主号聚合故投料项**恒「生产投料」不带生产类型后缀**），口径 / 零值整行省略 / 比率不渲染规则同主号级。③ 主号级入口方法更名 `MainCompletion(main)`。详见 `订单模块详细设计.md` V1.27 §5.17.5。
> 历史变更（V116）：**订单进度树「已完结主号」分支改为「投料 + 产出」两维度**（2026-09-14，**纯读聚合改造，无 EF 迁移 / 无数据变更**）：`OrderProgress.razor`（`/orders/progress?salesOrderNo=`）中 `ScheduleStage==1` 的主号分支由原 6 支（生产投料/过程检/成品检/余料入库/备料入库/成品入库）改为 **4 专属支 + 订单成品入库**，渲染序固定 = ① **生产投料[生产类型]**（叶「投料」）→ ② **在制品入库**（叶「入库」）→ ③ **次品入库** → ④ **备料成品**（叶「入库」）→ ⑤ **订单成品入库**（入库/出库/库存 3 叶）。① **投料**改以工艺卡开卡数据为准 = Σ `ProductionBatch.InputWeight`，批次**仅排除**「返整 / 委外生产 / 对外加工」三种生产类型、**不限制造物品**（原取 `WorkOrderExecutionSummary.InputWeight` 快照的口径废弃）；标题后缀仍为该主号投料批次 `ProductionType` 去重串（如「生产投料[荒管生产+在制生产+外购]」），无类型时纯「生产投料」。② **产出**全部按仓库实收：序2 在制品库 WIP 余库料 `Surplus`、序4 成品库 FG 备料成品 `Finished`（口径同 V92，仅标题更名）；**序3 新增「次品入库」**= 次品库 DEFECT 6 类物料（次品圆棒/次品荒管/次品半成品/次品成品/次品在制/报废品）经生产批号反查订单+主号聚合。③ **次品叶新增同叶退货出库量**（DTO `MainProgressLeafDto.ReturnWeightKg`，前端 `.op-leaf-return` 红色「退货 X kg」并列在入库量右侧，如「次品荒管 500 kg　退货 400 kg」）—— 退货**不新增任何字段**，复用既有 `OutboundRecord.OutboundType == ReturnOut`（「次品库入库 → 退货出库」两表对照），仅次品库批次的退货出库计入。④ **删除**原「过程检 / 成品检」两支（`ProcessInspectionDefect` / `FinalInspectionDefect` 字段与 `BuildProcessInspectionBranch` / `BuildFinalInspectionDefectBranch` / `BuildDefectBranch` 服务方法一并删除）。⑤ 「临界成品」**不纳入**产出（其来源为外购成品，本质属投料）。⑥ **非完结主号 4 支保持原样**（原料锁定/生产执行[待产]/成品检验/订单成品入库），仅末支标题由「成品入库」更名「订单成品入库」。⑦ **已完结主号灰色条新增「投料产出」汇总行**（置于「执行关注：已完结」下方、**主号折叠时亦可见**，`.op-main-line3`；**纯前端派生**，数据全部取自同一 DTO，无接口 / 迁移变更）：`投料产出：生产投料[在制生产]500kg；订单成品入库400kg；余次备产出入库50kg；投料产出率90.0%；产出成品比88.9%。` —— **投料** = 投料叶 − 退货；**订单成品入库** = 订单成品入库分支「入库」叶；**余次备产出入库** = 在制品入库 + 次品入库 + 备料成品 三支入库叶合计**再减退货**；**投料产出率** = (订单成品入库 + 余次备产出入库) ÷ 投料；**产出成品比** = 订单成品入库 ÷ 总产出；三项重量全 0 不渲染该行、分母 ≤ 0 的比率项不渲染（`OrderProgress.razor.cs` 的 `CompletionSummaryText`，比率一位小数四舍五入）。零值叶省略 / 空分支不渲染规则不变（完结主号默认折叠不变）；序2/3/4 按规则实现但**不显式占位**——真库当前近空（WIP 5 行 / DEFECT 3 行 / FG 备料成品仅 3 行有生产批号），数据链路补全后自动生效。详见 `订单模块详细设计.md` V1.26 §5.17。
> 历史变更（V115）：**菜单重排：质量管理「不合格反馈 / 不合格报告」收为三级子组「不合格处置」+ 扫码管理「扫码报工」更名为「报工扫码」**（2026-09-13，**纯前端菜单树与页面标题，无接口 / 数据库变更**）：① **质量管理新增三级子组「不合格处置」**（`MES.Blazor/Shared/AppMenu.cs`，位置在「成检追踪」之后、「炉号/化学」之前），收纳原并列的两个叶子 `不合格反馈`（`/quality/nonconforming-feedback`）与 `不合格报告`（`/quality/ncr`）—— 二者是**同一条闭环的两端**（现场上报 → 判定处置），并列在两个三级组（`炉号/化学`、`理化检测`）之间显得层级不一致；子组**自身不设 `Policy`**，有效策略仍按「自身 ?? 最近祖先分组」回退到「质量管理」的 `QualityMenu`，**角色权限零变更**（`AppShortcutsTests` 的有效策略断言覆盖此继承链）。② **扫码管理「扫码报工」更名为「报工扫码」**（仅此一项。曾一并评估把「不合格反馈扫码」简化为「反馈扫码」以求四字对齐，**经用户复核否决、保留原名**——「不合格反馈」语义明确优先于形式对齐）：`ScanExecute.razor` 页头标题同步，保持「页面标题 = 菜单名」既有规则；`/mobile-quality/{Kind}` 路由与 `Kind` 取值（`patrol` / `feedback`）**不变**。③ **首页常用入口**（`AppShortcuts`）`Label` 与菜单叶子同名（断言强制），`扫码报工` → **`报工扫码`**；④ 回归断言 `AppMenuTests`（质量管理子组结构 + 扫码组标签）、`AppShortcutsTests` 同步。
> 历史变更（V114）：**NCR 待处理口径由「组级」收紧为「记录级」+ 建单/编辑页新增「来源照片」入口 + 待处理卡片加「检验日期」列**（2026-09-13）：① **待处理颗粒度下探到检验记录**——「超阈值遗漏」不再按「批次+工序 / 批次+成检类型+检验项目」聚合，改为**逐条检验记录**判定（`overageQty = 各流向支数合计 + 让步放行支数`，`> NcrThreshold.Count` **且** `÷ 该条记录 Quantity > NcrThreshold.Percent` 才产出），**一条记录一行**；记录定位键 5 段 → **6 段**（末尾追加 `|{检验记录Id}`，DTO 新增 `InspectionRecordId`），排重改记录级——**该条记录**已建单（含忽略档）即不列出、同维度其它记录照常列出；**存量旧 5 段键 NCR 命中时该维度全部记录不再列出**（兼容）。② **建单/编辑页「生产编号」旁新增「来源照片」入口**——来源记录**有照片时**（`NcrService.GetSourcePhotosAsync` 返回非空）渲染 `Icons.Material.Filled.PhotoLibrary` 图标按钮（`AutoFillFromPending` / `FillFromCard` / `LoadExistingAsync` 后刷新判定），点击弹**只读**弹窗 `Shared/NcrSourcePhotoDialog.razor`（按来源分派：过程检验 / 成品检验该条记录附件、不合格反馈单问题照片；`MaxWidth.Large`；**无照片则不渲染入口**）。③ **「待处理批次」卡片新增「检验日期」列**（置于「检验项目」后，`ReportDate.ToString("yyyy-MM-dd")`）。④ **删除列表页「新建」按钮**（同日追加）：`/quality/ncr` 卡头原 `AuthorizeView Roles=QualityEdit` + 「新建」按钮（→ `/quality/ncr/create`）与 `Ncrs.razor.cs` 的 `CreateNew()` **一并删除** —— 建单入口收敛为**必带来源**的两个：两张待处理表内每行的**生产编号链接** `CreateFromPending` + 「不合格反馈」页新建（**同为 `QualityEdit` 权限**，反馈单可传 4 张照片）。依据：真库 4 张 NCR 全来自这两条路径，「新建」产出 **0 张**；且无源单无照片（NCR **无自有附件表**，照片只能从来源记录带出）、不参与排重、不可追溯。⑤ **修复编辑页「来源照片」入口不显示**：`NcrForm.razor.cs` 的 `LoadExistingAsync` 漏赋 `_formData.NonconformingFeedbackId`（三条填充路径仅此一条遗漏）→ 编辑**不合格反馈来源**的 NCR 时入口恒不渲染；已补。⚠️ 存量旧 5 段键单（1008/1009/1010）因旧键不含检验记录 Id，**有意不显示**该入口。详见 `质量管理模块详细设计.md` V8.34、`质量管理上下文接口设计.md` V8.28。
> 历史变更（V113）：**NCR 两项调整（建单/编辑页「生产编号」可开「批次执行进度」卡片 + 月度汇总对齐报表总览并排除「忽略」档）**（2026-09-13）：① **建单/编辑页「生产编号」新增批次进度入口**——生产编号字段改 `d-flex` 容器（`MudTextField Class="flex-grow-1"` + 条件图标按钮），当**已匹配到批次**（`NcrLookupResultDto.ProductionBatchId > 0`）且用户具备 **`BatchView`** 权限（`Policies.BatchView` 逗号串逐项 `Split(',').Any(IsInRole)` 降级判定——本页 `[Authorize]` 只挂 `QualityView`）时渲染 `Icons.Material.Filled.Timeline` 图标按钮，点击 `OpenBatchProgressAsync` 弹出**计划排程同一弹窗** `BatchProgressDialog`（`MaxWidth.Large`、`CloseOnEscapeKey`，**不跳转批次详情页**）；批次主键四处赋值：手工输入 `OnBatchNoChanged`（未匹配清零）、卡片带入 `AutoFillFromPending` / `FillFromCard`、编辑回填 `LoadExistingAsync`（取 `NcrDto.ProductionBatchId`）。② **「不合格月度汇总」卡片对齐「报表总览」**——删「责任部门汇总 / 责任类别汇总」两列，列改为 `责任类别 / 责任部门 / 处置方式 / 12 个月 / 合计`，改**内联样式**（移除 `<style>` 块与 `.ncrs-monthly-*` / `.ncrs-sticky-*` / `.ncrs-total-col` 类），打印样式（`PrintMonthlySummaryTable`）独立不动；**统计口径排除 `NcrStatus.Ignored`**（同一服务方法 `GetMonthlySummaryAsync` 收口，报表总览 Tab6 同口径）。详见 `质量管理模块详细设计.md` V8.33、`质量管理上下文接口设计.md` V8.27。
> 历史变更（V112）：**NCR 建单/编辑页三项前端调整（G4 行序对齐 G2 + 「忽略」按钮移至 G1 标题右侧 + 反馈人去工号）**（2026-09-13，**纯前端，无接口/数据库变更**）：① **G4「责任人及处理」行序对齐 G2**——**第一行** = `责任类别 / 新增责任类型 / 添加 / 处理是否完结 / 完结日期`（5×`md=2`，与 G2 第一行同构），**第二行** = `责任部门 / 生产责任人 / 生产操作日期`（原贴第一行末尾），其后整行「对责任人的处理」；绑定字段与列表列序**不变**；② **「忽略」按钮移至「G1 问题反馈」标题右侧**——G1 标题改 `d-flex align-center` 容器（`MudText` + `MudSpacer` + 按钮右对齐），由原「G1 被动让步块之后另起一行」移入标题行；**可见性条件与行为不变**（被动来源 + 新建模式 + `QualityEdit`，`ConfirmDialog` 确认后落一张 `NcrStatus.Ignored`）；③ **反馈人去工号（页面 + 列表）**——`DisplayHelper.FormatPersonName` 由「只剥**尾部** `(工号)`」改为**逐段剥离**（正则 `\(([^()]*)\)` + 工号形态判定：≥2 位、仅 ASCII 字母/数字/`_`/`-` 且含数字或大写字母），修复多段串「张燕平(YG045)、赵路陈」整体漏剥（原 `s[^1] == ')'` 判定使末段为纯姓名时整串原样返回）；覆盖**建单/编辑页反馈人**（检验员带出 + 编辑回填）与**列表页单元格 / 导出**；「张三(夜班)」类**非工号括号原样保留**。详见 `质量管理模块详细设计.md` V8.32。
> 历史变更（V111）：**NCR「忽略」改由建单承载（按钮移至建单页 G1 之后）+ 建单页 G1 布局重排 + 待处理卡片默认折叠 + 列表页删「显示已忽略」**（2026-09-13）：① **忽略入口迁移**——原「超阈值遗漏」表行内「忽略」按钮与「显示已忽略」开关、已忽略组表格与「恢复」按钮**全部移除**；改为在**建单页 G1 之后**渲染「**忽略**」按钮（`Variant.Outlined` + `Color.Dark` + `Icons.Material.Filled.VisibilityOff`，仅**新建模式 + 被动来源 + `QualityEdit`** 可见），点击经 `ConfirmDialog` 确认后按**正常建单流程**保存一张 **状态=忽略**（`NcrStatus.Ignored`）的不合格报告；② **状态档 3 → 4**——`NcrStatus` 新增 `Ignored`（中文「忽略」，列表状态列 chip 染色 `Color.Dark`）；因排重按 `Ncr.SourceGroupKey` **不看状态**，登记后该组**永久**不再列出（**取消原「组合计增长自动复活」**）；③ **建单页 `/quality/ncr/create` 与编辑页 G1 布局重排**——第一行 = `反馈日期* / 生产编号* / 工单号 / 反馈部门 / 反馈人 / 物料类型*`（**「生产编号 + 工单号」由第二行前移至「反馈部门」之前**），第二行 = `牌号 / 规格 / 次品支数 / 次品重量` +（被动来源）`次品流向`（**「次品流向」由独立一行并入第二行末尾**），其后为整行「问题描述」与被动让步放行块（让步支数 / 让步重量 / 让步说明）；④ **建单页「待处理批次」卡片默认折叠**（`_showPending` 默认 `false`，点标题栏展开，避免遮住表单）；⑤ 端点 `POST api/ncr/pending-checks/dismiss` 与 `.../{id}/restore` **已删除**；建单接口 `POST api/ncr` 支持 `Status=Ignored`。详见 `质量管理模块详细设计.md` V8.31、`质量管理上下文接口设计.md` V8.26、`数据库设计/质量管理上下文数据库设计.md` V8.18。
> 历史变更（V110）：**NCR「待处理批次」拆「正常提交 / 超阈值遗漏」两组 + G1 新增让步放行三字段 + 超阈值遗漏组「忽略」**（2026-09-12）：① **待处理拆两组**——「待处理批次」卡片与「不合格品实时待处理」表各按 `Bucket` 分「**正常提交**」（来源 = 不合格反馈，**无条件列出**）与「**超阈值遗漏**」（来源 = 过程检验 / 成品检验，按新阈值判定）两节渲染，**两张表 / 两个折叠卡**，各自条数 chip；组聚合键 = 过程检验「批次 + 工序」、成品检验「批次 + **成检类型** + 检验项目」；② **超阈值遗漏判据（组级一条）**——组内不合格合计 = **让步放行支 + 各流向支**，**合计 > `NcrThreshold.Count`（绝对支数）且 合计 ÷ 组内总数 > `NcrThreshold.Percent`（占比）**（**均严格大于**）→ 产出**一条**被动记录；让步放行**计入分子但永不单独成行**；③ **被动行展开明细 + 次品流向**——行可展开「返整N / 入在制库N / 可入备库N / 入次品库N / 退货N」明细；「次品流向」列（只读）取组内支数最多流向（并列按 **返整 > 入在制库 > 可入备库 > 入次品库 > 退货** 定序），正常提交行该列为空；④ **G1 问题反馈组新增 3 列**——「**让步支数 / 让步重量 / 让步说明**」（仅被动行有值），列序为 `…次品支数 / 次品重量 / 问题描述 / 次品流向 / 让步支数 / 让步重量 / 让步说明`（被动行比主动行恰多 4 字段）；不合格报告列偏好 `col_prefs_ncrs_v1` → **`col_prefs_ncrs_v2`**（`ColumnPrefsVersion` `v1`→`v2`）；⑤ **「忽略」功能（仅超阈值遗漏组）**——行内「忽略」按钮 + 「显示已忽略」开关（默认关）；已忽略行展示忽略时快照合计与当前合计，行内「恢复」取消忽略；**数据变多自动复活**（忽略台账存合计快照，当前合计 **>** 快照则重新列出）；两个端点 `POST api/ncr/pending-checks/dismiss` / `POST api/ncr/pending-checks/{id}/restore`，授权 **QualityEdit**；⑥ **「反馈部门」口径 = 位置**——过程检验 / 不合格反馈 → **工段**；成品检验 → **成检项目**；⑦ **建单页** `NcrForm` 待处理选择器按两组分节，被动行带入让步放行三字段与组键，主动来源清空让步字段；⑧ **阈值配置** `NcrThreshold` 由 8 项收敛为 **`Count`（默认 5）/ `Percent`（默认 0.10）两项**。详见 `质量管理模块详细设计.md` V8.30、`质量管理上下文接口设计.md` V8.25、`数据库设计/质量管理上下文数据库设计.md` V8.17、`配置上下文详细设计.md`、`11_打印设计.md` V2.18。
> 历史变更（V109）：**不合格反馈单新增「来源类型」三分支，位置信息按来源切换**（2026-09-12）：① **来源类型**（`NonconformingFeedbackSourceType` 枚举 3 档，存英文 Key）：**生产工段** / **过程检验** / **成品检验**，表单页与扫码反馈档均新增该下拉（**巡检档无此项**）；② **位置信息分档**——**成品检验** 档填「**工序 + 检验项目**」（`InspectionItem`，**唯一使用检验项目的来源**）+ 只读「**成检类型**」（预检/终检，**由工序与批次是否含「附加成检」推导，非用户可选项**：工序=附加成检→终检；批次含附加成检且工序≠附加成检→预检；批次不含附加成检→全部终检），**工段名称与序号置空**（`SectionName` / `SequenceNumber` 改可空）；**生产工段 / 过程检验** 档填「工序 + 工段名称」（**不要求检验项目**），序号按工段推导；③ **列表页** 新增「来源类型」列（G1，紧随工单号，`FilterType="string"`）+「检验项目」列（紧随工段名称），列数 17→**19**（默认显 15→**17**），**列偏好版本 v2 → `v3`**；查看弹窗新增「来源类型」「检验项目」两字段；④ 服务层出口统一归一化 `NormalizeLocation`（成品检验→清工段、置检验项目；其余→清检验项目、保留工段），**跨分支残留字段必然被清空**；⑤ 迁移 `20260912052001_AddNonconformingFeedbackSourceType`（`SourceType` NOT NULL 默认 `ProductionSection` 回填存量、`InspectionItem` 可空、`SectionName`/`SequenceNumber` 改可空）。详见 `质量管理模块详细设计.md` V8.29、`质量管理上下文接口设计.md` V8.24、`数据库设计/质量管理上下文数据库设计.md` V8.16。
> 历史变更（V108）：**NCR「处置方式」字典化（8 档）+ 拆出「流向」列（枚举 5 档，只读带出）**（2026-09-12）：① **不合格报告列表**（`/quality/ncrs`）：G1 新增**「流向」列**（`FilterType="enum"`，`EnumOptions = DisplayHelper.GetEnumFilterOptions<FlowDirection>()`，chip 配色 5 档）并**从 G2 拆出**，G2「处置方式」列改 **`FilterType="string"`**（字典列，chips 走 `GetDisposalChipColor(string?)`，导出/打印经 `GetDisposalText` 输出字典中文）；**待处理卡片**与**不合格品实时待处理表**列名「处置方式」→「**流向**」（chip 改 `GetFlowDirectionChipColor/GetFlowDirectionText(item.FlowDirection)`），注记文本「（生产编号×处置方式×检验项目）」→「（生产编号×**流向**×检验项目）」；跳转建单页查询参数 `disposalMethod={枚举名}` → **`flowDirection={枚举名}`**；**月度汇总表行维度=处置方式**（表头文案与列不变，`DisposalMethodDisplay` 取值改由字典出）。② **不合格报告新建/编辑页**（`/quality/ncr/create`、`/quality/ncr/{id}`）：G1 新增**只读「流向」**文本（`Value="@GetFlowDirectionText(_formData.FlowDirection)" ReadOnly`），G2「处置方式」改**字典下拉**（`_disposalOptions` 由 `DictValueDefinitionService` 已启用值加载、默认 `NcrDisposalKeys.All`，旁侧「新增处置方式」输入 +「添加」按钮即时加档，Key 规则 `NcrDM_{n}`；`NcrDisposalKeys` 无档时也补展示）。③ **报表总览 Tab6**（`/reports/overview`）：不合格品实时待处理表列名改「流向」、取值改 `DisplayHelper.GetFlowDirectionText`；月度汇总表口径不变（本就是处置方式行维度）。④ 打印 `NcrPrintHelper` G1 加「流向」字段。详见 `质量管理模块详细设计.md` V8.28、`质量管理上下文接口设计.md` V8.23、`数据库设计/质量管理上下文数据库设计.md` V8.15。
> 历史变更（V107）：**照片张数上限 3 → 4（配合「每页 2 张」打印版式）**（2026-09-12）：`QualityPhotoLimits.PerRecord` 3 → **4**（过程检验 / 成品检验 / 不合格反馈每条记录），`QualityPhotoLimits.PerType` 3 → **4**（巡检单「巡检照片 / 整改验证照片」每类各 4、两类合计单个巡检单最多 **8 张**）；前端两处接线同步纠正——`ScanPhotoPicker.MaxCount` 默认值由裸 `9` 改为 `QualityPhotoLimits.PerRecord`，扫码页 `ScanQuality` 不合格反馈「问题照片」由误用巡检常量 `MaxPhotoPerType` 改为 `MaxPhotoPerRecord`（行为等价、语义纠正）；涉及页面：`ProcessInspections` / `FinalInspections`（弹窗 `InspectionPhotoDialog`）、`NonconformingFeedbacks` / `NonconformingFeedbackForm`、`InspectionPatrolForm`、`ScanExecute`（两类检验表单）、`ScanQuality`（巡检 + 反馈）。详见 `质量管理模块详细设计.md` V8.27、`质量管理上下文接口设计.md` V8.22、`08_扫码执行设计.md` V4.9。
> 历史变更（V106）：**单据式打印照片改「单列 + 每页 2 张 / 290pt」**（2026-09-12，纯后端打印层，前端无改动）：用户实测「每页 6 张偏小」，五个单据式 Helper（过程检验 / 成品检验 / 巡检单 / 不合格反馈 / NCR）照片区统一由「每行 2 张」改**单列铺排**、单张占位高 **200pt → 290pt**（巡检单 120pt → 290pt，**+142%**），每页 **2 张**超出自动续页；页数口径随之改为 **无照片 1 页 / 有照片 = 1 页字段页 + ⌈N/2⌉ 页照片**（3 张照片 → 3 页；巡检满载 6 张 → 4 页；过程检验两条记录各 3 张 → 6 页）。⚠️ 认知校正：`FitArea` 等比缩放使照片实际尺寸**只由高度决定**（宽度受高度约束），故单列/加宽列并不放大照片，放大照片的唯一手段是加大高度、而高度上限由每页张数决定。回归保护：`MES.Tests/Services/Printing/` 5 个测试类共 18 用例页数断言同步更新。详见 `docs/11_打印设计.md` V2.14、`质量管理模块详细设计.md` V8.26。
> 历史变更（V105）：**单据式打印照片统一另起第 2 页 + 巡检双照片区合并**（2026-09-12，纯后端打印层，前端无改动）：① **过程检验 / 成品检验 / 不合格反馈 / NCR** 四类单据的照片一律 `PageBreak` 另起**第 2 页**（页首归属行「编号: 前缀-{Id:D4}」+ 照片区名与张数），照片不再挤压字段页 → 页数口径 = **无照片 1 页 / 有照片 2 页**；② **过程检验 + 成品检验**字段区启用**紧凑排版档**（字号 8pt / 节标题 10pt / 行距压缩），确保字段页（含过程检 G3 明细、成检探伤 14 字段）恒为单页 —— 不合格反馈 / NCR / 巡检**不启用**；③ **巡检单两个独立照片区合并为同一张连续表格**（区内以跨列组标题行分「巡检照片」「整改验证照片」，仅涉及整改时才有第二组），统一另起第 2 页、**定高 120pt**（实测 155pt 即溢出为第 3 页）保证满载 6 张（巡检 3 + 整改验证 3）恒落同页；④ 其余照片区定高 220pt → **200pt**；⑤ 两组照片皆空则**整页省略**，不产生空白页。回归保护：新增 `MES.Tests/Services/Printing/` 5 个测试类共 18 用例（手工 PNG 素材 + PDF 字节扫描 `/Type /Page` 断页数）。详见 `docs/11_打印设计.md` V2.13、`质量管理模块详细设计.md` V8.25。
> 历史变更（V104）：**过程检验 / 成品检验补「检验照片」+ A4 竖版单据式打印，NCR 打印带关联反馈照片**（2026-09-12）：① 两页列表各新增**「照片(N)」列**（>`0` 时可点击开共用弹窗 `Shared/InspectionPhotoDialog.razor`，`Kind` 区分过程检/成检，**每条上限 3 张**、补拍/查看/删除），工具栏新增**「打印选中单据」按钮**（`POST .../print-selected-doc-file`，A4 竖版**每条记录独立成页** + 检验照片区，**单次上限 20 条**）；② **扫码报工页**（`ScanExecute`）两类检验表单内嵌 `ScanPhotoPicker` **同页直拍**（提交成功后按记录 Id 逐张补写附件，单张失败隔离不阻断）；③ **不合格报告**「打印选中报告」改为**自动嵌入关联不合格反馈单的问题照片**（端点契约不变）；④ 附件文件本体落服务器文件系统、DB 只存元数据（迁移 `20260911171626_AddInspectionAttachments`）。详见 `质量管理模块详细设计.md` V8.24。
> 历史变更（V103）：**不合格反馈去掉「是否处理」标记，改为在「不合格报告」中呈现「待处理批次」**（2026-09-11）：① **不合格反馈列表**（`/quality/nonconforming-feedback`）删除「是否处理」列与操作列 MudChip 一键切换，列数 18→**17**（默认显 16→**15**），列偏好版本 `v1`→**`v2`**；表单页、查看弹窗、扫码反馈表单同步删除「是否处理」控件；② **不合格报告**（`/quality/ncrs`）「待处理批次卡片」与「不合格品实时待处理」表新增**「不合格反馈」来源**——凡**未生成 Ncr** 的反馈单**无条件列出**（不受 NcrThreshold 约束），其「处置方式」列留空（`DisposalMethod` 可空），点击「生成待检 NCR」跳转建单页时经 `feedbackId` 回填 `Ncr.NonconformingFeedbackId`（生成后该反馈即视为已处理、自列表消失）；来源类型由新枚举 `NcrPendingSourceType` 承载（**不复用会污染工位报工的 `ReportTemplateType`**）。详见 `质量管理模块详细设计.md` V8.23。
> 历史变更（V102）：**新增「巡检扫码」/「不合格反馈扫码」两页**（2026-09-11）：① **菜单**：扫码管理 → [扫码报工, **巡检扫码**, **不合格反馈扫码**, 设备扫码, 工位管理(ScanView), 员工管理(ScanView)]（`AppMenu.cs`）；② **页面** `ScanQuality.razor(.cs)`（`/mobile-quality/{Kind}`，`Kind` = `patrol` / `feedback`）：**仅登录**（无质量域角色档），步骤 = 扫**批次码** → 选**工序 + 工段** → 填表 → 完成；**不扫工位码、不扫员工码**（操作人 = 登录账号，服务端反查员工档案姓名，只读展示）；**巡检闭环定位**（同「批次+工序组+工段」存在待整改单 → 带出第一次的明细与照片，仅补 验证结果/整改人/是否闭环 + 整改验证照片；否则新建；页面留「改选已有单 / 新建」逃生口）；反馈单录 来料/不合格支数与重量 + 问题照片；③ **共用照片选择器** `Shared/ScanPhotoPicker.razor(.cs)`（`ScanPhotoItem` 模型；巡检照片 / 整改验证照片 / 问题照片各 ≤ 9 张）；④ **横屏提示条**：两页加入 `LandscapeHintRule.NarrowMenuLeafHrefs`（竖屏点选流，不弹「请横屏」），`LandscapeHintRuleTests` 同步断言；⑤ 接口 `api/scan-quality/*`（登录即可，见 `质量管理上下文接口设计.md` §4.13）。详见 `08_扫码执行设计.md` §2.10、`质量管理模块详细设计.md` §2.41/§6.13。
> 历史变更（V101）：**巡检单「在产设备号」正名为「在产设备名」并取消设备台账下拉 + 巡检人/在产操作人改走员工档案**（2026-09-11）：① 表单页 `InspectionPatrolForm.razor` 该字段由 `MudAutocomplete`（候选=设备台账设备号）改为 **纯手输 `MudTextField`**，允许留空、**无任何候选下拉**（设备名形态不实用），委外时仍置灰；② `GET api/inspection-patrol/position-options` **不再返回设备候选**（`InspectionPatrolPositionOptionsDto.EquipmentCodes` 删除）；③ 列表页列名、查看弹窗、打印模板、搜索/排序列（`EquipmentCode` → `EquipmentName`）与数据库列同步正名（迁移 `20260911130718_RenameInspectionPatrolEquipmentCodeToName`）；④ **巡检人 / 在产操作人下拉改走员工档案**（`MudAutocomplete<EmployeeDto>` + `api/employee/list` 内存模糊过滤，修复「下拉找不到人」）、**并保留档案外手输**（`Text`/`TextChanged` 双通道，与 `PurchaseOrderCreate` 同款），`InspectionPatrolPositionOptionsDto.Operators` 删除，`position-options` 现**仅返回在产单位·车间候选**；⑤ **同款修复「不合格反馈」表单的「反馈人」下拉**（原为完全相同的未验证写法）。委外联动口径不变。详见 `质量管理模块详细设计.md` V8.20。
> 历史变更（V100）：**新增「巡检」子菜单与三个页面（独立表检验类型）**（2026-09-11）：① **菜单**：质量管理 → [**巡检**, 过程检验, 成检到料, 成品检验, 成检追踪, 不合格反馈, 不合格报告, 炉号/化学(子组), 理化检测, 质量证明书]，「巡检」为质量管理**首项**（位于「过程检验」之前，`AppMenu.cs`）；② **列表页** `InspectionPatrols.razor`（`/quality/inspection-patrol`）：标准列表页（服务端分页 / 全字段排序 / 模糊搜索 / 巡检日期区间 / ExcelFilter 列筛选 / 列显隐持久化，列偏好键 `col_prefs_quality_inspection_patrol_v1`），18 列默认显 16（隐藏 数据来源 / 更新时间），操作列 = 查看 + 编辑 + 「是否闭环」一键切换（仅涉及整改的行显示）+ 删除（ConfirmDialog）；③ **新建/编辑页** `InspectionPatrolForm.razor`（`/quality/inspection-patrol/create`、`/{Id:int}/edit`）：生产编号带出批次（工单号/工厂牌号只读）+ 工序名称→制造规格→工段名称三级联动；**巡检明细为一对多动态行表**（序号/巡检项/巡检结果/备注，至少一条）；**整改块以「涉及整改」开关条件渲染**（整改内容描述/验证结果/是否闭环，未涉及则服务端清空）；**在产单位·车间 / 在产设备号 / 在产操作人为档案驱动自动补全但允许手输**（`GET api/inspection-patrol/position-options`）；照片按**巡检照片 / 整改验证照片**两类分列存储，各自最多 **9 张**，客户端 canvas 压缩（最长边 1600px/质量 0.8，`wwwroot/js/attachment.js`）后经鉴权 API 上传，文件本体落服务器文件系统；④ **查看弹窗** `InspectionPatrolViewDialog.razor`：只读全字段 + 巡检明细表 + 两类照片缩略图（点击放大）+ 打印（`POST api/inspection-patrol/{id}/print-file`，授权 QualityView）；⑤ 权限复用质量域三档（QualityView/QualityEdit/QualityDelete），**不新增角色**。详见 `质量管理模块详细设计.md`。
> 历史变更（V99）：**不合格反馈单查看弹窗 + 打印 + 列表按钮角色门控**（2026-09-11）：① **新增查看弹窗** `NonconformingFeedbackViewDialog.razor`（页面内弹窗，`MaxWidth.Large` + `FullWidth`）：只读全字段（顺序同录入页：工序/工段/工厂牌号/制造规格）+ 问题照片缩略图（**点击放大**为全屏遮罩大图）+ **打印**按钮；② **打印**：新增 `POST api/nonconforming-feedback/{id}/print-file`（授权 **QualityView**，打印属只读动作），服务端 `NonconformingFeedbackPrintHelper`（QuestPDF A4 竖版单据）按 G1 反馈信息 / G2 位置信息 / G3 数量信息 / G4 问题信息分区排版，**问题照片原图嵌入**（每行 2 张、超高自动翻页），工序/工段名称经**配置表映射转中文**（`GetProcessNameMapAsync` / `GetSectionNameMapAsync`，缺配置兜底 `ProcessKeys`/`SectionKeys` 常量中文）；前端经既有 `openPdfFromApi(apiUrl, "{}", true)` 管线出 PDF；③ **列表页**：操作列**「编辑」按钮补 `AuthorizeView Roles=QualityEdit` 门控**（此前 QualityView-only 用户看得到点不进的死按钮），新增**「查看」按钮**（无角色门控，QualityView 即可），**「照片」列（N 张）改为可点击**（`.cell-link`）→ 打开查看弹窗；④ `app.css` 补 `.attachment-thumb` / `.attachment-remove` / `.view-thumb` 样式（此前全仓无定义，缩略图按原图尺寸裸渲染）。详见 `质量管理模块详细设计.md` V8.16。
> 历史变更（V98）：**不合格反馈单表单两处修正 + 附件 JS 加固**（2026-09-11）：① 新建/编辑页 **工序名称 / 工段名称下拉显示英文 Key** → 两个 `MudSelect` 补 `ToStringFunc="@((string v) => ProcessDisplayHelper/SectionDisplayHelper.GetXxxText(v))"`（带出/回填属程序化赋值，下拉项未注册时 MudSelect 会回退 `Value.ToString()` 直出英文；BatchCreate/BatchEdit 同款写法）；② **反馈人** `MudTextField` → `MudAutocomplete<string>`（候选=全量启用员工，姓名/工号模糊搜 `MinCharacters=0` 上限 50，`CoerceValue=true` 允许档案外手输，不预填）；③ `attachment.js` 未加载时 JS 互操作统一兜底 Snackbar，不再打崩渲染树；④ 新建/编辑页**字段顺序**调整为 **工序名称 → 工段名称 → 工厂牌号 → 制造规格**（⚠️ 仅显示顺序变，联动链仍是 工序 → 规格 → 工段）。详见 `质量管理模块详细设计.md` V8.15。
> 历史变更（V97）：**新增「不合格反馈」子菜单与两个页面**（2026-09-11）：① **菜单**：质量管理 → [过程检验, 成检到料, 成品检验, 成检追踪, **不合格反馈**, 不合格报告, 炉号/化学(子组), 理化检测, 质量证明书]，「不合格反馈」位于「不合格报告」之前（`AppMenu.cs`）；② **列表页** `NonconformingFeedbacks.razor`（`/quality/nonconforming-feedback`）：标准列表页（服务端分页 / 全字段排序 / 模糊搜索 / 反馈日期区间 / ExcelFilter 列筛选 / 列显隐持久化，列偏好键 `col_prefs_quality_nonconforming_feedback_v1`），18 列默认显 16（隐藏 数据来源 / 更新时间），操作列 = 编辑 + 「是否处理」MudChip 一键切换 + 删除（ConfirmDialog）；③ **新建/编辑页** `NonconformingFeedbackForm.razor`（`/quality/nonconforming-feedback/create`、`/{Id:int}/edit`）：生产编号带出批次（工单号/工厂牌号只读）+ 工序名称→制造规格→工段名称三级联动；**不合格重量 = 来料重量 ÷ 来料支数 × 不合格支数 四舍五入取整，可手改，清空恢复自动**；照片最多 **9 张**，客户端 canvas 压缩（最长边 1600px/质量 0.8，`wwwroot/js/attachment.js`）后经**鉴权 API** 上传，文件本体落**服务器文件系统**（`Attachment:RootPath`），登记方与处置方均可上传。定位：只登记问题不做处置（处置在「不合格报告」），本单仅带「是否处理」标记。文档分部：见 `质量管理模块详细设计.md` §2.35/§2.36/§3.6/§6.11。
> 历史变更（V96）：**成品检验不合格品去向 4 档 → 5 档 + 「不合格处理」组统一更名「不合格品去向」**（2026-09-11）：① **成品检验**新增 `DefectInProcessWarehouseQuantity/Weight`（**入在制库**），组由 4 档（返整/可入备库/入次品库/退货）改 **5 档（返整/入在制库/入次品库/退货/可入备库）**，前端列顺序严格按此排列，**列表 G4 由 9 列 → 11 列**（+ 入在制库支/理论入在制重），列偏好键 v3→**v4**（`col_prefs_final-inspection_v4`），新建页 `/quality/final-inspection/create` G4 同步 11 列；② **过程检验**不合格品去向仍 **4 档**（在制件无备库概念），列数与列序不变，仅组名「不合格处理」→**「不合格品去向」**（`GroupName` 不入持久化，**列偏好键保持 v3 不变**）；③ 扫码报工成品检不合格输入由 4→5 格（返整/入在制库/入次品库/退货/可入备库）；④ 成检追踪、成检计划列表、数据工具（`DataExchangeRegistry` 成品检 5 支数 + 5 重量列）、工单执行汇总快照（`FinalInspectionInProcessWarehouseWeight`）、订单进度树成品检分支、**不合格报告 `/quality/ncrs` 待处理卡片**（成品检每聚合组由 4 张 → **5 张**：返整/入在制库/可入备库/入次品库/退货，入在制库与可入备库共用 Warehouse 阈值；过程检仍 4 张）同步；⑤ 迁移 `20260911071723_AddFinalInspectionInProcessWarehouseTier`。
> 历史变更（V95）：**不合格品报告（NCR）「处置方式」由 4 档拆为 5 档**（2026-09-11）：`DisposalMethod` 删 `WarehouseEntry`（入库），新增 `InProcessWarehouse`（**入在制库**，来源过程检验）/ `FinishedWarehouse`（**可入备库**，来源成品检验），`Scrap` 中文显示「报废」→**入次品库**，与过程检/成品检不合格处理四档口径对齐。① **不合格报告** `/quality/ncrs`：列表「处置方式」列（枚举列 `FilterType="enum"`，`EnumOptions` 自动 5 项）与新建页 `/quality/ncr/create`「处置方式」下拉（自动 5 项）文案更新；两页 `GetDisposalChipColor` 配色扩 **5 档**（返整 Warning / 入在制库 Info / 可入备库 Primary / 入次品库 Error / 退货 Secondary）；② **待处理卡片**每来源聚合组生成 **4 张卡**（返整 / 入在制库·可入备库 / 入次品库 / 退货），「生成待检 NCR」跳转 URL 携带的 `disposalMethod` 枚举名同步；③ **月度汇总表**（`/quality/ncrs` 折叠卡 + 报表总览 Tab6）处置方式粒度行由 4 档变 5 档；④ 打印（`NcrPrintHelper`）与数据工具（`DataExchangeRegistry` 枚举列）经 `EnumHelper` 自动生效；⑤ 迁移 `20260911064754_AdjustNcrDisposalMethodTiers`（含 `EnumDisplayDefinitions` 5 档切换 + 存量 `WarehouseEntry`→`InProcessWarehouse` 防御性回写，本机真库 0 行受影响）。
> 历史变更（V94）：**质量域过程检验 / 成品检验「不合格处理」四档化**（2026-09-11）：① **过程检验**不合格处理组由 3 档（返整/入库/报废）改 **4 档（返整/入在制库/入次品库/退货）**，**列表 9 列**（返整支/入在制库支/入次品库支/退货支/理论返整重/理论入在制重/理论入次库重/理论退货重/次品情况描述），列偏好键 v2→**v3**（`col_prefs_process-inspection_v3`），新建页同步（G6 10 列）；② **成品检验**不合格处理组由 3 档（返整/入库/报废）改 **4 档（返整/可入备库/入次品库/退货）**，**列表 9 列**（返整支/可入备库支/入次品库支/退货支/理论返整重/理论可入备库重/理论入次库重/理论退货重/次品情况描述），列偏好键 v2→**v3**（`col_prefs_final-inspection_v3`），新建页同步（G4 9 列）；③ **订单进度树**「执行关注=已完结」主号的过程检/成品检分支由 2 叶（报废+次品入库）改 **3 叶**（过程检：入在制库/入次品库/退货；成品检：可入备库/入次品库/退货，均不含返整）；④ 同步扫码报工（过程检验/成品检验入缸表单）、质量过程跟踪、成检计划列表、数据工具导入导出表头、批次跟踪可视化展示口径。
> 历史变更（V93）：**订单进度树「生产投料」分支标题加生产类型后缀**（2026-09-11）：「生产投料」标题改为「生产投料[生产类型串]」，如 `生产投料[荒管生产+在制生产+外购]`。生产类型串**另查 `ProductionBatch` 实时**（快照无 `ProductionType` 字段），按主号聚合投料批次 `ProductionType` 去重后按 `ProductionTypeKeys.All` 固定序拼中文、`+` 分隔；批次集口径同 G11（`ProductionType != Rework` 且 `ManufacturingItem ∈ {OrderFinished, SpecialDeliveryStatus}`）；无生产类型时退回纯「生产投料」（不显示空方括号）。
> 历史变更（V92）：**订单进度树「执行关注=已完结」主号分支扩充**（2026-09-11）：完结主号由「仅成品入库分支」改为 **6 分支**，渲染序 = 生产投料 → 过程检 → 成品检 → 余料入库 → 备料入库 → 成品入库。① 生产投料 = 快照 `InputWeight`（**原始投料**，非有效流转）单叶「投料」；② 过程检、③ 成品检 = 快照**报废 + 次品入库** 2 叶（**不含返整**）；④ 余料入库 = **在制品库(WIP)** 入库行经 `ProductionBatchNo` 反查 `ProductionBatch` 取订单号+主号后聚合 `InitialWeight`；⑤ 备料入库 = 同款改 **成品库(FG) + 备料成品(Finished)**；⑥ 成品入库不变。零值叶省略 / 空分支不渲染规则不变（完结主号默认折叠不变）。⚠️ **真库现状**：WIP 5 行全无订单号、FG 备料成品 704 行中 700 行反查不到生产批次 → ④⑤ 当前不渲染，数据链路补全后自动生效。无 EF 迁移、无数据变更（纯读聚合新增）。
> 历史变更（V91）：**8 个试验页字段名正名 `FurnaceNo` → `BatchNo` + 批次进度弹窗宽度收敛**（2026-09-10）：
> ① **8 个试验模块（扩口/压扁/晶粒度/硬度/晶间腐蚀/金相/点腐蚀/室温拉伸）全链路改名**：实体 / DTO / Service / 列表页 `.razor.cs` + 创建页 `.razor` / 单测 / `AppDbContext.Quality.cs` 配置与索引 / `DataExchangeRegistry`（硬度·晶粒度表头由「炉批号」→「生产编号」）；**`ChemicalAnalysis.FurnaceNo`（真炉号）不动**。EF 迁移 `20260910103357_RenameQualityTestFurnaceNoToBatchNo`（纯 `RenameColumn`/`RenameIndex`）。
> ② ⚠️ **前端列表 `SortKey` 必须同步为 `batchno`**（服务端排序按属性名反射，保留 `furnaceno` 会静默回退 `CreatedTime` 排序）。
> ③ **批次执行进度弹窗宽度收敛**：批次计划 / 成检计划两页 `OpenBatchProgressAsync` 的 `DialogOptions` 由 `MaxWidth.ExtraLarge + FullWidth`（1920px 且近满屏、右侧大片留白）改为 `MaxWidth.Large`（1280px）+ **不开 `FullWidth`**，按内容自适应。
> ④ C 组口径不变（**仍仅登记台账、不加批次链接**），但字段名遗留问题已随本次改名消除。
> 上次实质变更（V90）：**补 3 页「生产编号 → 生产批次详情」链接 + 新建 §7 链接台账**（2026-09-10，批三十三）：
> ① **新增直接链接 4 处**：去油酸洗入缸记录（BatchView，无需降级）、成品检验（QualityView ⊄ BatchView → **按角色降级**）、不合格报告（无页级策略 → 按角色降级）、报表总览 NCR 待处理表（ReportView ⊆ BatchView，无需降级）。
> ② **后端改动仅在 NCR**：`NcrDto` / `NcrPendingCheckDto` 补 `ProductionBatchId`（服务端查询后按 `BatchNo` 反查 `ProductionBatch` 回填，`⚠️` 内存字典必须 `StringComparer.OrdinalIgnoreCase`）；`NcrService` 新增 `GetBatchIdMapAsync` + 两个 `FillProductionBatchIdsAsync` 重载，`GetAllAsync` / `GetByIdAsync` / `GetPendingChecksAsync` 三处回填。
> ③ **新建 §7「生产编号 → 生产批次详情」链接台账**：登记「直接链接 / 弹窗 / 不链接」选型原则、已具备链接的 14 处基线、**核查出的 4 组缺口（A 纯前端 / B 需后端补 Id / C 8 个试验页 / D 语义待定）**、以及 3 处「有意不加链接」。
> ④ **8 个试验页口径澄清**（用户拍板）：扩口/压扁/晶粒度/硬度/晶间腐蚀/金相/点腐蚀/室温拉伸 八页列标签「生产编号」**是正确的**，字段名 `FurnaceNo` 仅历史命名遗留 → 归入应链接未链接（C 组）。（**已随 V91 改名 `BatchNo`**）
> 本次**仅登记不实现**的缺口见 §7.3（用户拍板"都不补，仅登记台账"）。
> 历史变更（V89）：**批次计划 / 成检计划「生产编号」可点开「批次执行进度」弹窗**（2026-09-10，批三十二；**纯 WASM，后端/DB 零改动**）：
> ① **卡片抽取复用**：批次详情页区块三「批次执行进度」卡片抽为 `Shared/BatchProgressCard.razor`（数据源不变 `ProductionRecordService.GetTrackingVisualAsync` → `GET api/batch/{id}/tracking`），批次详情页内联引用、两计划页经新增 `Shared/BatchProgressDialog.razor` 弹窗引用；**不跳转批次详情页**（用户明确要求，弹窗内亦不提供跳转入口）。
> ② **两计划页接线**：`BatchPlans.razor(.cs)` / `FinalInspectionPlan.razor(.cs)` 的 `RenderCell` 中 `BatchNo` 分支改渲染 `<span class="cell-link">`（新增全局类，蓝字 + 悬停下划线），点击调 `OpenBatchProgressAsync(BatchId / ProductionBatchId, BatchNo)` → `DialogService.ShowAsync<BatchProgressDialog>`（`MaxWidth.ExtraLarge` + `FullWidth`）。
> ③ **按角色降级（权限不对称处理）**：两计划页页级策略 `SchedulingView`、追踪端点策略 `BatchView` → `OnInitializedAsync` 内判定可点性；⚠️ `Policies.BatchView` 是**逗号分隔多角色串**，`ClaimsPrincipal.IsInRole` 只认单角色名，必须 `Split(',').Any(user.IsInRole)` 逐角色展开，无批次查看权者「生产编号」保持纯文本、无可点样式。
> ④ **样式上提**：Blazor scoped CSS 不能跨组件复用 → 卡片所需 `.detail-*` 类（progress-track/fill、summary-bar、section-*、meta-line、final-header、outsource-chip、empty）由 `BatchDetail.razor.css` **上提至 `wwwroot/css/app.css`**，`BatchDetail.razor.css` 仅保留本页专用 6 类。
> 验证 `dotnet build MES.Blazor --no-incremental` 0 错 0 警 + `MES.Tests` Components **163 例全绿**。
> 历史变更（V88）：**手机端补「常用入口」卡 + 桌面两卡改等宽 `md=6`（同步放大磁贴）**（2026-09-10，批三十一；**纯 WASM，后端/DB 零改动**）：
> ① **手机端渲染「常用入口」卡**：`Index.razor` 的 `IsMobile` 分支原只渲染订单负荷卡（V3.9 决策"移动导航由汉堡抽屉承担"），现改为**纵向堆叠两张 `MudPaper.mh-card`：上「常用入口」→ 下「订单负荷实时状况」**；清单**与桌面同一份** `AppShortcuts.Items`（未新增第二份），同样包 `<AuthorizeView Roles="@item.Policy" @key="item.Href">` 按角色过滤。
> ② **手机磁贴紧凑化**：`.shortcut-grid` / `.shortcut-tile` 由「仅桌面」提升为**桌面/手机共用类**（app.css 段标题「首页桌面看板」→「首页看板」），手机下用 `.mh-container .shortcut-tile { flex:1 1 28% }` 收成**三列**（与订单负荷阶段卡 xs=4 同宽感）、字号 12px、内距 `9px 4px`。
> ③ **桌面两卡等宽**：`md="4"` / `md="8"` → **`md="6"` / `md="6"`**（用户要求"常用入口放大一点、两卡宽度一致"）；两卡标题字号统一 `Typo.h6`（常用入口原 `subtitle2`）。随列变宽，桌面磁贴 `flex:1 1 150px` → **`flex:1 1 160px`**、内距 `10px 8px` → `12px 10px`、字号 13px → 14px。
> ④ **文档同步**：`docs/模块设计/看板上下文详细设计.md` V3.12 → **V3.13**（布局框图重绘为等宽两栏 / 核心原则 / 布局要点 / 手机端差异条目 / 常用入口表）。
> 验证 `dotnet build MES.Blazor --no-incremental` 0 错 0 警。
> 历史变更（V87）：**首页桌面改左右布局（左入口/右负荷）+ 单数口径改「订单号」+ 数值样式统一「X单/Y吨」异色、急单去红**（2026-09-10，批三十）：
> ① **桌面改左右布局**：`Index.razor` 桌面分支改为一个 `MudGrid Spacing="3"`，**左 `MudItem xs="12" md="4"` = 常用入口磁贴卡**、**右 `MudItem xs="12" md="8"` = 订单负荷实时状况卡**，两卡 `Style="height:100%"` 等高，窄屏自动堆叠（V88 已改为两列均 `md="6"` 等宽）。
> ② **单数口径改「订单号」（后端改动）**：三阶段单数一律按 `SalesOrderNo` 去重（同订单多工单/多批次只算 1 单），急单数同理；表头「工单数/吨」→「**订单数/吨**」。`WorkOrderExecutionDashboardItem` 由「按 ScheduleStage × UrgencyLevel 分组（多行）」改为「**按 ScheduleStage 一行**」——删 `UrgencyLevel`，新增 `UrgentOrderCount`/`UrgentWeight`；服务新增私有 `StageOrderRow` + `AggregateByOrder`（`Distinct(StringComparer.OrdinalIgnoreCase)`）。⚠️ 旧多行结构下页面合计是跨紧急度组求和，同一订单横跨多档会重复计数 → 必须下沉为「阶段内先按订单号去重」。
> ③ **数值样式统一**：桌面与手机一律渲染「**X单/Y吨**」`<span>` 结构 —— 单数 `#1565C0` 蓝粗体 / 吨位 `#00838F` 青粗体，单位与「/」12px 浅灰（app.css 新增 `.stat-count` / `.stat-weight` / `.stat-unit` / `.stat-sep` / `.stat-muted` / `.stat-caption`）。页面 `WorkOrderStageRow` 删 `TotalText`/`UrgentTotalText` 拼接串，改由 4 个整数驱动；`FormatWeight`(string) → `Tons`(int)；表尾合计改 4 个整数字段。
> ④ **急单不再用红色**：删 `MudChip Color="Error"`，急单列与合计列同结构同色系；急单数 0 时显示浅灰「无急单」（原手机分行「N单 / N吨」也合并为单行「X单/Y吨」）。
> ⑤ **文档同步**：`docs/模块设计/看板上下文详细设计.md` V3.11 → **V3.12**。
> 验证 `dotnet build MES.Api --no-incremental` 0 错 0 警 + `MES.Blazor` 0 错 0 警 + `AppShortcutsTests`/`AppMenuTests` **14 例全绿**。
> 历史变更（V86）：**首页展示层收敛：卡片更名「订单负荷实时状况」+ 全卡去链接 + 数值居中 + 阶段配色；常用入口收敛为 9 项并磁贴化**（2026-09-10，批二十九；**纯 WASM，后端/DB 零改动**）：
> ① **卡片更名 + 去全部跳转**：「工单执行」→ **「订单负荷实时状况」**（手机标题 + 桌面标题）；删除标题右侧「负荷总量 →」按钮（原跳 `/plan-overview`）、三个阶段名不再渲染为可点标签块。`Index.razor` 随之删 `@inject NavigationManager`；`Index.razor.cs` 删 `WorkOrderStageRow.NavigateUrl` 与 stages 元组的 Url 列 —— **首页不再持有任何写死的详情页路径**（跳转一律走「常用入口」磁贴或侧栏）。
> ② **数值全居中**：表格 3 列表头与单元格统一 `text-center`（原阶段列左对齐、数值列右对齐）；急单 chip 由「恒红」改「有急单 `Color.Error` 红 / 无急单 `Color.Default` 灰」（与手机口径统一）。
> ③ **阶段视觉区分**：三阶段各配「强调色 + 图标」——原料锁定 `#FB8C00`+`Lock` / 生产在产 `#1976D2`+`PrecisionManufacturing` / 成品检验 `#43A047`+`Science`；桌面 = 同色药丸（`.stage-pill`，白字），手机 = 顶部同色条卡（`.mh-stage-card`，`border-top-color` + 同色图标）。色值唯一来源 = `Index.razor.cs` stages 元组 `Accent`。
> ④ **手机端同步优化**：三张阶段卡改为「顶部色条 + 同色图标 + 阶段名 + `N单` / `N吨` 分行 + 急单小字（红/灰）」，**取消整卡点击跳转**（原 `.mh-stage-tap` 锚点样式已从 app.css 删除）。
> ⑤ **常用入口收敛为 9 项并磁贴化**：删「工单执行状况 / 生产批次 / 过程检验」，保留 报表总览 / 订单列表 / 用料计划 / 批次计划 / 成检计划 / 不合格报告 / 原料库 / 成品库 / 扫码报工；`AppShortcuts` 条目类型由 `AppMenuNode` 改为专用 `AppShortcut`（Label/Href/Policy/**Icon**/**Accent**）——菜单节点无图标字段，磁贴需要图标配色，给 `AppMenuNode` 加字段会牵动侧栏与手机两套渲染；渲染由 `MudButton` 横排改为**等宽图标磁贴**（app.css 新增 `.shortcut-grid` / `.shortcut-tile`）。
> ⑥ **防漂移测试**：`AppShortcutsTests` 4 → **5 项**（新增「每条必须带图标与 `#RRGGBB` 强调色」；角色域断言去掉 `BatchMenu`）。总计 5 例全绿。
> ⑦ **文档同步**：`docs/模块设计/看板上下文详细设计.md` V3.10 → **V3.11**。
> 验证 `dotnet build MES.Blazor --no-incremental` 0 错 0 警 + `AppShortcutsTests` **5 例全绿**。
> 历史变更（V85）：**首页删除「质量检验」卡（手机 + 桌面两处）**（2026-09-10，批二十八；**纯 WASM，后端/DB 零改动**）：
> ① **首页至此只剩「常用入口 + 工单执行」**：删除「质量检验」卡（成品检验 待到料/待检验/检验中 3 档 + 不合格报告 待处理批次/处理中 2 项），手机端 `mh-card` 与桌面端右列 `MudItem md=6` 两处同删。质量状态改由 **成检计划**（`/final-inspection-plan`，成检四档看板）与 **不合格报告**（`/quality/ncr`）承载。
> ② **桌面布局收敛**：原「双栏网格 `md=6` + `md=6`」→ **单个 `MudItem xs="12"` 整行卡片**（去掉 `Style="height:100%"` 与「左列/右列」注释）。
> ③ **清理**：`Index.razor` 删 `@inject FinalInspectionPlanService FinalInspectionSvc` / `@inject NcrService NcrSvc`；`Index.razor.cs` 删 `_isLoadingCard3` + 14 个 `_card3*` 字段 + `LoadCard3Async()` + `Card3StatText()` + `OnInitializedAsync` 质量检验加载块（现仅注入 1 个 Service：`WorkOrderExecutionService`）。`FinalInspectionPlanService` / `NcrService` **仍由各自页面使用，Service 层不动**。
> ④ **文档同步**：`docs/模块设计/看板上下文详细设计.md` V3.9 → **V3.10**（状态/布局图/核心原则/质量检验章节改为移出说明/依赖汇总/目录结构/§6 备注）。
> 验证 `dotnet build MES.Blazor --no-incremental` 0 错 0 警。
> 历史变更（V84）：**首页新增「常用入口」横条（仅桌面端）**（2026-09-10，批二十七；**纯 WASM，后端/DB 零改动**）：
> ① **桌面端首页顶部新增「常用入口」**：`Index.razor` 桌面分支在 2 卡上方渲染一个 `MudPaper`，内含 `d-flex flex-wrap` 的 12 个 `MudButton Variant="Outlined" Size="Small"`（报表总览 / 订单列表 / 工单执行状况 / 用料计划 / 批次计划 / 成检计划 / 生产批次 / 过程检验 / 不合格报告 / 原料库 / 成品库 / 扫码报工），每项包 `<AuthorizeView Roles="@item.Policy" @key="item.Href">` 按角色过滤（`@key` 防按顺序复用致角色判定串位）。**手机端不含该区块**（移动导航仍走汉堡抽屉，`IsMobile` 分支不渲染）。
> ② **清单单一可改点**：新增 `MES.Blazor/Shared/AppShortcuts.cs`（静态 `Items`，顺序与 `AppMenu.Root` 一致）。**只做菜单子集的高频投影，非全量 90 叶复刻**——起因是"首页可否直接就是导航栏"，评估后不改为纯导航：桌面侧栏常驻且同源 `AppMenu`（全量宫格=重复）、全树约 90 叶平铺过长而只放 15 个一级又比侧栏多一步、且会丢掉首页唯一独有资产（`GetDashboardSummaryAsync` 全仓仅此一处调用）。
> ③ **策略取分组门控**（`OrderMenu`/`WorkOrderMenu`/`SchedulingMenu`/`BatchMenu`/`QualityMenu`/`WarehouseMenu`；报表总览叶子自带 `ReportView`；扫码报工 `null`=仅登录）→ 与侧栏菜单**可见性严格一致**，报表角色不会在首页看到菜单里没有的入口。⚠️ `AppMenu` 把策略挂在**分组节点**上（叶子自身 `Policy` 为 `null`），故须比「有效策略 = 自身 ?? 最近祖先分组」。
> ④ **防漂移测试**：新增 `MES.Tests/Components/AppShortcutsTests.cs` 4 项（每条 Href 必须存在于 `AppMenu.AllLeaves()`、Label 与有效策略须与菜单一致、无重复且 `<20` 条、除扫码报工外必须带策略、覆盖 7 个角色域）。
> ⑤ **文档同步**：`docs/模块设计/看板上下文详细设计.md` V3.8 → **V3.9**（布局图 / 核心原则 / 常用入口章节 / 依赖汇总 / 目录结构 / §6 备注）；本文 §2.12 首页条目标注改 `[首页看板+入口]`。
> 验证 `dotnet build MES.Api --no-incremental` 0 错 0 警 + `AppMenuTests` / `AppShortcutsTests` **13 例全绿**。
> 历史变更（V83）：**首页改造：删除「批次生产（段落流转分析）」卡，改 2 卡片布局**（2026-09-10，批二十六；**纯 WASM，后端/DB 零改动**）：
> ① **首页 `Index.razor` 由 3 卡改 2 卡**：删除原「批次生产（段落流转分析）」卡（6 列可排序表 + 三列合计）—— 它与「报表总览 → 生产执行 → 段落流转分析」卡 **100% 重复**（同 `SectionParagraphFlowAnalysisService.GetAnalysisAsync()`、同 `SectionParagraphFlowAnalysisDto`、同 6 列），且属"可排序多列明细表"（报表内容）。首页原则确立：**首页只放"结论数字 + 跳转"，不出现可排序的多列明细表**（明细/筛选/打印归「报表总览」）。
> ② **布局重排**：桌面原「左列 `md=6` 竖排 工单执行(50%) + 质量检验(50%) ／ 右列 `md=6` 批次生产(100%)」→ **左列 `md=6` = 工单执行、右列 `md=6` = 质量检验**（各 `Style="height:100%"`，两侧等高）；手机端由「工单执行 → 质量检验 → 批次生产」三张 `mh-card` 改两卡。
> ③ **保留内容**：工单执行（`GetDashboardSummaryAsync()`，全站唯一调用点，三阶段 单数/吨 + 急单，行点击分别跳 原锁/批次/成检）+ 质量检验（成检 3 档 + NCR 2 项）。
> ④ **清理**：`Index.razor.cs` 删卡片 2 字段（`_isLoadingCard2`/`_paragraphFlow*`/`_flow*`）与 7 个方法（`ToggleFlowSort`/`ApplyFlowSort`/`ComputeFlowFooter`/`FlowAbnormalCount`/`FlowSortIndicator`/`FlowRenderInt`/`FlowGetStatusColor`/`FlowGetPlanFlowJudgmentColor`）+ `SectionParagraphFlowAnalysisService` 注入 + `MES.Core.DTOs.Scheduling` using；`app.css` 删 `.mh-paragraph-list`/`.mh-paragraph-row`（已无引用）。`GetAnalysisAsync` 仍由 报表总览、批次计划页 共用，**Service 层不动**。
> ⑤ **文档同步**：`docs/模块设计/看板上下文详细设计.md` V3.7 → **V3.8**（布局/卡片章节/依赖汇总/目录结构全量改写）。
> 验证 `dotnet build MES.Api` 0 错 0 警。
> 历史变更（V82）：**订单负荷总量 原料锁定 3 行改名/搬列 + 成购口径收紧 + 原锁「待投原料」文案**（2026-09-10，批二十五；**前端 + 后端服务，无 DB 变更**）：
> ① **「现订单负荷总量」/「订单负荷总量」共享组件 WorkOrderLoadOverview**（`ProductionOverviewService`）：类别「原料」→**「原料锁定」**（1-1~1-3 + 汇总一致）；负荷节点 1-1「完善用料计划」→**「完善用料-原料类」**、1-2「执行用料计划」→**「执行用料-原料类」**、1-3「外购成品」→**「执行用料-成购类」**；表头「待计划量(吨)」→**「未编制计划(吨)」**、「在购量(等)(吨)」→**「待落实量(吨)」**；1-2 的待投料量**由「未编制计划」列搬入「待落实量」列**（`PendingPlanTons=null`、`InProcurementTons=待投料量`）；汇总行 未编制计划=行1-1、待落实量=行1-2+行1-3（总量守恒）。
> ② **成购口径统一收紧为「仅执行用料计划（ExecutePlan）工单」**（原为全部 ScheduleStage=2 工单）：排除质量补料/生产返整补足/完善用料计划；真库此三类成购缺口恒为 0，故数值不变。同步落点：`ProductionOverviewService`（行1-3）、`RawMaterialLockPlanAndExecutionService`（标量/待投原料矩阵/截日行）、前端 `RawMaterialLockPlanAndExecution.razor.cs`（`RecalculateSummary`/`RecalculateCutoffSummary`）。
> ③ **原锁文案「待投料」→「待投原料」**（与「成购」区分，两页同步）：原锁计划页「待投料量汇总」卡片顶部徽标 `待投原料: Xt`、矩阵标题「待投原料」、打印标题；报表 Tab3「原料需求」徽标同改，并**删除 `（N 行）` 行数标识**。
> 　报表 Tab3 另**删除「工单总数: N 批」「计划投料: Xt」两枚徽标**（2026-09-10 用户决策：此二值按全四档统计，易误导，只留 待投原料/成购）；成购矩阵非「执行用料计划」三行**已为 `-`**（`FormatMatrixPurchase`/`FormatPurchaseCell` 计数为 0 即显 `-`，无需改动）。
> ④ **理论待投料截日表**：行名 → **完善用料-原料类 / 执行用料-原料类 / 执行用料-成购类 / 合计**（后端 `RawMaterialLockPlanAndExecutionService` + 前端 `.razor.cs` 两处硬编码同步）；列头「待投料总量」→**「全期合计」**；第 3 行口径改为仅 ExecutePlan 工单缺口。
> ⑤ **成购矩阵只渲染「执行用料计划」一行**（**三处同步**：原锁计划页 `RawMaterialLockPlanAndExecution.razor`、报表 Tab3 `ReportOverview.razor`、用料计划页 `MaterialPlanOverview.razor`；后者带行/列/格点击下钻联动，原 3 空行本就不可点击，删后不损功能）：原 4 行中其余 3 档恒为 `-`，2026-09-10 用户决策**直接不渲染**（避免误读为「成购横跨 4 档」），底部「合计」行因与唯一数据行同值一并删除。**「成购」不是第 5 档，只是「执行用料计划」档内的一个分支**。三处行号均由 `Array.IndexOf(RawMaterialLockRemarkKeys.All, ExecutePlan)` 求（`PurchaseMatrixRowIndex`）。
> ⑥ **订单负荷总量行 1-1/1-2 与「待投原料」口径完全对齐**（2026-09-10 用户决策）：两行均**排除「单一成品采购」纯成购单**（`ProductionOverviewService.IsSingleFinishPurchase`，与 `RawMaterialLockPlanAndExecutionService` 同口径），行 1-3 成购**不排除** → 1-2 与 1-3 天然互斥不重复；`row1Remaining`（整体完工预计的原料待投料量）同步走该口径。
> ⑦ **口径模型备注**：原料锁定本质只两档 —— **完善计划 + 执行计划**；三行显示为「第 1 行=完善计划、第 2+3 行=执行计划拆出的 非成购/成购 两支」。截日卡片同此结构（完善用料-原料类 / 执行用料-原料类 / 执行用料-成购类）。
> ⑧ **报表·生产执行 Tab「冷轧拔近日排程」后流转列**：供应量与机台数均为 0（或目标组为空）时显 **`-`**（原用 `is { TargetGroupDisplay: { } tg }` 模式，空字符串也算命中 → 会渲染误导性的「0t / 0台」）；抽 `FlowStateText(FlowStateDto?)` helper（2026-09-10 用户决策）。计划排程页 `ColdRollPlans.razor` 本就有 `IsNullOrEmpty` 保护、行为不变。
> 验证 `dotnet build MES.Api` / `MES.Blazor` 0 错 0 警 + 定向 `ProductionOverviewServiceTests`/`RawMaterialLockPlanAndExecutionServiceTests` **21 例全绿**（含新增 `GetOverviewAsync_单一成品采购单_不进原料类_只进成购行`）。
> 历史变更（V81）：**菜单整序：订单在库成品改名 + 报表系统拍平「报表总览」 + 数据工具移至参数表后**（2026-09-10，批二十四；**纯 WASM，后端/DB 零改动**）：
> ① `AppMenu.Root` 订单管理组叶子 **「订单成品(在库)」改名「订单在库成品」**（Href `/orders/pending-delivery` 不变；页面正式名仍「订单成品(实时库存)」）。
> ② **「报表系统」单叶组拍平为一级单项「报表总览」** `/reports/overview`（删组节点，`ReportView` 门控移至叶子，与 数据工具/用户管理 单项一级形态统一），置首页下方第 2 位。
> ③ **「数据工具」单项自 生产标准/扫码管理 之间移至 参数表之后、用户管理之前**（扫码管理顺延前移一格）；一级顺序其余维持现状。定稿一级序：`首页, 报表总览, 订单管理, 工单管理, 计划排程, 批次管理, 质量管理, 物料管理, 仓库管理, 设备管理, 生产标准, 扫码管理, 工资结算, 参数表, 数据工具, 用户管理`。菜单单源树桌面/手机共用；`AppMenuTests.根级顺序` 断言同步。角色域文案（Roles 注释/用户管理角色名「报表系统」三档）**不随菜单改**。
> 验证 `dotnet build MES.Blazor` 0 错 0 警 + 定向 `AppMenuTests` 9 例全绿。
> 历史变更（V80）：**报表子表口径微调：月度汇总合并列 + 冷轧去标注/吨位小数 + 段落靠左 + 负荷表删延期行**（2026-09-10，批二十三；**纯 WASM，后端/DB 零改动**）：
> ① **质量管理 Tab「不合格品月度汇总」**（ReportOverview `/reports/overview`，NcrMonthlySummaryDto）：**删除 处置方式汇总/责任部门汇总/责任类别汇总 三列**，只留一列 **「合计」**（表头浅黄 #fff8e1 加粗、格背景 #fffde7）= 每处置方式行全年（12 月）合计量（=原 `r.TotalQuantity/TotalWeight`）；**责任类别/责任部门为空直接显「-」**（替换原「未填写」文字，处置方式空值仍显「未填写」）；注记文本同步。`.razor.cs` 删 `_ncrDeptTotals`/`_ncrCategoryTotals` 及 `ComputeNcrMonthlyRowspans` 求和，仅保留类别/部门 rowspan 计算。
> ② **生产执行 Tab「冷轧拔近日排程」**：**删「矛盾标注」列**（th/td 及 `hasConflict` 局部）；**吨位数值保留 1 位小数**（可流转量/计划流转量/后流转 t 值，`TonsText` helper 由 G29→F1，0 显 "0"；仅本卡使用）。
> ③ **生产执行 Tab「段落流转分析」**：待在产重量/计划重点批流转量/特急批重量 **三数值列 表头（去 `mud-table-cell--right`）/单元格（去 `text-center`）/页脚（去 `text-center`）全靠左**；总况判定/计划流转判定 Chip 列保持居中。
> ④ **Tab2「现订单负荷总量」= WorkOrderLoadOverview 共享组件**（报表 Tab2 及 计划排程 PlanOverview 同用）：`OverviewRows` 过滤**剔除「订单交期负荷」组三行（订单延期-原料/在产/成检）**，日期桶列保留（其余行日期桶有值）；**序号 4-0「整体完工预计」整行底色 #FFF8E1**（`.row-overall`，颜色可再调）。
> 验证 `dotnet build MES.Blazor`（含 MES.Api）0 错 0 警。
> 历史变更（V79）：**报表往来数据卡交互增强（默认折叠+行数下拉+显示行合计+表头去箭头） + 客户列表 ② 靠左 + 质量管理 Tab 标题分色**（2026-09-10，批二十二；**纯 WASM，后端/DB 零改动**）：
> ① **客户管理 Customers.razor ② 往来信息组数据格+页脚去 `text-center` 靠左**（`.razor.cs` 删 `_centerColumnKeys`/`IsNumericColumn`），同供应商/委外单位 V74 口径——客户列表列格式与 ② 列样式两个批（批二十一 V78 加三色）已闭环。
> ② **ReportOverview.razor `/reports/overview` 三张「往来数据」卡交互收敛**（客户卡在业务总况顶、供应商卡物料执行末尾、委外卡生产执行末尾）：卡片**默认折叠**（折叠键 `report:customer-trade`/`report:supplier-trade`/`report:outsource-trade`，localStorage 记忆，随 `DefaultCollapsedCards` 首次默认收起）；展开工具栏=220px 即时搜索 + **显示行数 MudSelect(10/20/50/0=全部，默认 10)** + `显示 N / 共 M 条｜合计按显示行计`；身份列点击排序保留但**去 ▲/▼ 箭头文字**，改 `.report-th-sorted` 浅蓝底(#e3f2fd)+加粗；tbody 只渲当前显示行；表格底部新增 `<tfoot>` 合计行（背景 #fffde7 加粗，客户 2 身份列 colspan=2 / 供应商 3 身份列 colspan=3 / 委外 2 身份列 colspan=2），各统计列 `*TradeSummary` 按当前显示行汇总，与单元格 `*TradeValues` 同 `withCount` 成员规则。
> ③ **质量管理 Tab 五张小表标题分色**（`.razor.cs` 逻辑不改，仅标题文字包 `<span class="report-tt report-tt-N">`）：不合格品实时待处理/不合格品月度汇总→`report-tt-1`(蓝)、待检批支重汇总→`report-tt-2`(绿)、近日成检量数据/月度成检量数据→`report-tt-3`(紫)——按「同源逻辑上下文同色」仿生产执行 1~5 色系。
> ④ CSS app.css +`.report-th-sort`（排序 th 基础：左对齐/浅灰底 #f5f5f5/1px 边框/pointer/no-select/nowrap）+`.report-th-sorted`（浅蓝 #e3f2fd 底 + 边框 #90caf9 + font-weight:700）；打印表头/合计走同 `<table>` 内联样式，`getTableHtml` 取 `table.outerHTML` 含 thead/tbody/tfoot。验证 `dotnet build MES.Blazor` 0 错 0 警。
> 历史变更（V78）：**报表总览三个 Tab 加「往来数据」表 + 源列表页 ② 列样式统一**（2026-09-10，批二十一；**纯 WASM，后端/DB 零改动**，三表复用既有 list 端点 + 已回填统计字段）：
> ① **ReportOverview.razor `/reports/overview`** 三个 Tab 各插一张「往来数据」卡（`summary-card`，**标题着色 + `::before` 左侧 4px 色条** `.report-tt-1~5` 五色，220px 即时搜索、整表一次性加载 `GetPagedAsync` PageSize=5000、身份列点击排序 ▲/▼、打印按钮；表格容器 `#report-{customer,supplier,outsource}-trade` `max-height:50vh` 内滚动）：
> 　- **业务总况 Tab 顶部「客户往来数据」**（同源订单上下文「客户管理」Customers.razor）：业务员/最终用户 + **累计接单** / 本年接单 / 本年已发货(整单/非整单) / 待发货(整单/非整单) / 待在产(整单未入库/扣除部分入库) **八统计列**（均 `z单/x吨/y万`；V123 起「待发货」两列表头暖橙、「待在产」两列表头冷紫，与源列表页同源）。**V124 起卡头带「接单日期区间」**：任一端填写即进入区间模式——「本年接单」→「区间接单」、「本年已发货」→「区间已发货」、「累计接单」置「—」；待发货/待在产属存量不受影响。
> 　- **物料执行 Tab 末尾「供应商往来数据」**（同源「供应商管理」Suppliers.razor）：供应商名称/物料分类/备注 + 累计出单 / 本年出单（z单/x吨/y万）/ 本年到货[扣除退货] / 待收货（x吨/y万）/ 本年退货（仅吨）。
> 　- **生产执行 Tab 末尾「委外单位往来数据」**（同源「委外单位管理」OutsourceVendors.razor，**排除本厂 IsWorkshop 行**）：委外单位名/委外工段（中文）+ 累计委外 / 本年委外（z单/x吨/y万）/ 本年回收[扣除退回] / 委外未回收（x吨/y万）/ 本年退回（仅吨）。
> ② **标题区分色**：物料执行 荒管类(1 蓝)/成品类(2 绿)/圆钢类(3 紫) + 供应商往来(**4 橙**)；生产执行 冷轧拔近日排程(1)/段落流转(2)/生产量(3)/委外(4) + 委外单位往来数据(**5 青**)。CSS：app.css `.report-tt` + `.report-tt-1~5`。
> ③ **三个源列表页 ② 统计列样式统一**（Customers/Suppliers/OutsourceVendors 三 `.razor.cs` 原私有 `BuildStatText/RenderStat*` 改走共享 `OrderOverviewFormatter.RenderTradeMarkup/Text`）：**单数蓝/吨绿/万橙三色、吨与万取整无小数、0 成分省略、全 0 显「—」**（此前吨/万为小数位）；悬停 title/打印同走纯文本同口径。
> ④ 共享格式化器 `OrderOverviewFormatter` 新增 `RenderTradeMarkup`（富文本）/`RenderTradeText`（纯文本）；新测试 `OrderOverviewFormatterTests` 10 例。验证 `dotnet build MES.Blazor` / `MES.Api` 0 错 0 警 + 定向 10 全绿。
> 历史变更（V77）：**报表业务总况 + 订单列表页 3 小表加「金额」三色展示**（2026-09-09，批二十；后端共享 `SettlementMoneyCalculator` 结算分治折算）：
> ① ReportOverview.razor `/reports/overview`（报表·业务总况 Tab）：「订单接单·出库及现负荷汇总」5×12 表，**接单量/出库量两行月度分布、成品库存(完工/未完工)/订单负荷量(实时)三行仅当前月格取值（余显 `-`）** 每格改 **`x吨/y万`**（重量 t、金额万元=项次总价结算分治折算，均取整；吨绿 `#2E7D32`/万橙 `#E65100` 区分，全 0 显 `-`）；两交期预估小表格改 **`z单/x吨/y万`**（蓝单 `#1565C0`/绿吨/万橙三色）。渲染走共享 `OrderOverviewFormatter`（内联样式保打印一致）。
> ② Orders.razor `/orders`「完成预估及延期风险」两折叠小表格改 `z单/x吨/y万` 三色（行标签「单数/重量」→「单数/重量/金额」+ 图例注记）；**原「延期罚款」急中急子集红标 `[*a/b]` 不再显示**（DTO `UrgentCount/UrgentWeight` 删除）。金额=桶内整单项次总价合计（未计价计 0）。
> 验证 `dotnet build MES.Api` / `dotnet build MES.Blazor` 0 错 0 警 + Order/Customer 定向单测 229 通过。
> 历史变更（V76）：**物料进出存报表 inout 月格改彩带入x/出y + 末尾拆「进出汇总/实时库存」两列**（2026-09-09）：
> ① MonthlyStock.razor `/warehouse/monthly-stock`（物料进出存报表，4 报表切换之 inout）：每月格**去月末结存**、改**彩带「入x/出y」**——入=浅绿底深绿字（`#1e7e34/#e6f4ea`）、出=浅红底深红字（`#c62828/#fdecea`），**内联样式**屏显/打印（getTableHtml）一致，0 侧省略；末尾单列拆**两列：「进出汇总」**（该行全年 入/出 TotalIn/TotalOut，同彩带格式，倒数第 2 列）**+「实时库存」**（只显示当前库存量 ClosingWeight 单值 t，原「实时数据」更名，末列）；入库/出库/库存报表单值展示不变。AskUserQuestion 拍板：去月结存 + 绿入/红出。
> ② 验证 `dotnet build MES.Blazor` 0 错 0 警。
> 历史变更（V75）：**工段委外列表计价三列默认隐藏**（2026-09-09，批十七追加）：
> ① SectionOutsources.razor `/section-outsources` 委外信息组 **计价单位/单价(元)/总价(元) 三列改默认隐藏**（列显隐勾选可显，需看价格时手动开）；创建页/详情保留价字段不变，扫码页不受影响（价格服务端自动落库）；`ColumnPrefsVersion` v3→**v4**（`col_prefs_section-outsources_v4`）。
> ② 验证 `dotnet build MES.Api` / `dotnet build MES.Blazor` 0 错 0 警。
> 历史变更（V74）：**委外单位列表批十八 UX 细化 + 供应商页 ② 数据靠左**（2026-09-09，批十八追加）：
> ① OutsourceVendors.razor `/outsource-vendors`（V73 两分组基础上）5 点细化：**「本厂/外协」字段改「本厂」**（本厂标「是」、外协留空，编辑态 MudSwitch、布尔筛选 是/外协）；**列改名** `本年回收`→**`本年回收[扣除退回]`**、`在委外未回收`→**`委外未回收`**；**本年回收[扣除退回]/委外未回收 两列补金额（按重量份额分摊）**——`OutsourceRecovery` 无单价，金额从发出单 `TotalAmount` 按份额分摊（回收仅正常 `RecoveryWeight`，非正常属退回、不产生金额；净欠=Max(0,发出−(正常回收+退回))，负数截 0），渲染 单/吨/万；**② 往来信息组数据一律靠左**；新建页 OutsourceVendorCreate.razor 表头同改「本厂」。仅标签/文案/宽度/CSS 变 → ColumnPrefsVersion 不 bump（仍 `col_prefs_outsource-vendors_v2`）。
> ② **Suppliers.razor（供应商管理）② 往来信息组数据同样一律靠左**（`.razor.cs` 删 `_centerColumnKeys/IsNumericColumn` 的 text-center 居中逻辑，数据格+页脚均左对齐）。
> ③ 服务层 `OutsourceVendorProfileService` DTO 补 `YearRecoveredAmount`（本年回收分摊金额，退回不产生）/`PendingAmount`（委外未回收分摊金额）；供应商页不涉及服务端改动。
> 验证 `dotnet build MES.Api` / `dotnet build MES.Blazor` 0 错 0 警 + OutsourceVendorProfileServiceTests 定向 19 通过。
> 历史变更（V73）：**委外单位档案列表仿供应商管理建 ② 往来信息 统计列组 + 打印选中**（2026-09-09，批十八）：
> ① OutsourceVendors.razor `/outsource-vendors` 由单组基本信息升级为 **① 基本信息 / ② 往来信息 两分组**（col-g1/g2 表头标色 + 分组标题栏，仿供应商 Suppliers.razor）：基本信息组保留 8 列原样（编码/委外单位名/委外工段(枚举)/本厂外协/联系人/联系电话/备注/状态，内联编辑不变）；② 往来信息组追加 5 只读统计列（不可编辑/排序/筛选）：**累计委外**（委外单数+发出吨+金额万）/ **本年委外**（按发出日期切本年，单数+吨+万）/ **本年回收[扣除退回]**（吨+金额万，按回收日期切本年取正常回收 RecoveryWeight）/ **委外未回收**（吨+金额万，净值=发出−(正常回收+退回)，负数截 0）/ **本年退回**（吨，非正常退回 UnprocessedWeight），全 0 显示「—」，页脚合计仅统计组列；② 组列均默认显示，编码/联系人/电话/备注默认隐藏；列偏好键 v1→**v2**（`col_prefs_outsource-vendors_v2`）。
> ② 工具栏右侧加 **「打印选中 (N)」**（Mode A `POST api/outsource-vendor/print-list-file`，BatchView，仿供应商）：选中行按当前可见列逐格转显示文本（② 组走统计文本 单/吨/万）→ `OrderPrintListRequest` → `openPdfFromApi` 预览/下载「委外单位列表.pdf」。
> ③ 服务层 `OutsourceVendorProfileService` 仿供应商 `AttachTradeStats` 回填 9 统计字段（页内明细行），统计口径=仅 `!IsInternal` 发出单且 (OutsourceVendor×SectionName) 命中档案行（本厂车间 IsWorkshop 行/档外文本均不计）。
> 验证 `dotnet build MES.Api` / `dotnet build MES.Blazor` 0 错 0 警 + OutsourceVendorProfileServiceTests 定向 18 通过。
> 历史变更（V72）：**批次上下文 委外单位档案主档页 + 工段委外加计价三字段、委外单位改档案驱动下拉**（2026-09-09，批次页面 15→17）：
> ① 新增 委外单位管理 `/outsource-vendors`（列表页，OutsourceVendors.razor）与 `/outsource-vendors/create`（创建页，OutsourceVendorCreate.razor），菜单置于「工段委外」与「工艺卡打印」之间；档案行键=委外单位名×委外工段、编码 VendorCode WV+4 自动，`IsWorkshop`=本厂车间（锁定冷轧拔、作工段委外厂内虚拟发外来源）；列表 8 列 编码(默认隐)/委外单位名/委外工段/本厂外协/联系人/联系电话/备注/状态(启用·停用)，列偏好键 `col_prefs_outsource-vendors_v1`。
> ② 工段委外 `/section-outsources` 委外信息组加 **计价单位(元/Kg·元/米·元/支)/单价/总价 三列**（发出重量之后，默认显）；委外单位改为档案驱动下拉（新建页/行内编辑按行工段过滤 active 档案，`CoerceValue=false`，先建档后可选），IsInternal 列改只读派生显示（档案 IsWorkshop 自动厂内，不再手动开关）；列偏好键 v2→**v3**（`col_prefs_section-outsources_v3`）。
> ③ 扫码报工工段委外表单同步：委外单位改档案下拉、去厂内开关（本厂车间档案自动厂内，价格由服务端按工段默认自动落库）。
> 验证 `dotnet build MES.Api` / `dotnet build MES.Blazor` 0 错 0 警 + SectionOutsource/OutsourceVendorProfile/OutsourceVendors 定向单测通过；真库迁移 `20260909112729_AddSectionOutsourcePricing` 已回填计价三列。
> 历史变更（V71）：**质量管理 15 张列表页默认列显隐收敛**（2026-09-09，成品检验/过程检验已于 V69 收敛，其余 13 页对齐；隐藏列均可经列选择器开启）：
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
| 生产执行 | 生产执行 | BatchViewer/Editor/Full + Admin | 17 | 8 |
| 质量 | 质量管理 | QualityViewer/Editor/Full + Admin | 32 | 16 |
| 物料 | 物料管理 | MaterialViewer/Editor/Full + Admin | 9 | 4 |
| 仓库 | 仓库管理 | WarehouseViewer/Editor/Full + Admin | 7 | 4 |
| 设备 | 设备管理 | EquipmentViewer/Editor/Full + Admin | 8 | 4 |
| 产品标准 | 产品标准 | StandardViewer/Editor/Full + Admin | 18 | 9 |
| 报表系统 | 报表总览 | ReportViewer/Editor/Full + Admin | 1 | 0 |
| 数据工具 | (独立按钮) | DataToolViewer/Editor/Full + Admin | 2 | 0 |
| 报工扫码 | (独立按钮) | 所有（仅登录） | 1 | 0 |
| 设备扫码 | (独立按钮) | 所有（仅登录） | 1 | 0 |
| 配置 | 参数表 | ConfigurationViewer/Editor/Full + Admin | 13 | 13 |
| 工资结算 | 工资结算 | SalaryViewer/Editor/Full + Admin | 11 | 10 |
| 用户管理 | (Admin按钮) | UserViewer/Editor/Full + Admin | 1 | 0 |

---

## 2. 各上下文详细页面清单

### 2.1 订单上下文

```
路由前缀: /orders, /customers, /orders/progress, /orders/pending-delivery
菜单: 订单管理 → [订单列表, 客户管理, 订单在库成品]

┌─ 订单管理 ───────────────────────────────────────────────┐
│                                                           │
│  Orders.razor          /orders              [列表页+内联编辑]│
│  OrderProgress.razor   /orders/progress     [进度树子页,行内进入]│
│      └ 共享组件 Shared/OrderProgressTree.razor（首页「订单进度查询」卡复用同组件）│
│        阶段分支 → 重量叶之下再下沉「生产批次名单」（V143）：            │
│        生产执行叶 = 在产 / 在途 两段，成品检验叶 = 单段；默认折叠，       │
│        展开后批号为蓝色可点 → 弹「批次执行进度」对话框                  │
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
菜单: 工单管理 → 工单生成 /workorders、需求调整 /workorders-demand-adjustment、
          工单用料 /material-plan-overview、用投料核查 /material-input-consistency、
          查询工单执行 /workorder-execution、查询定尺工单 /fixed-length-work-order-view
          （V144：原「工单操作 / 工单查询」两个三级分组取消，6 项并列二级）

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
菜单: 计划排程 → [负载总览, 原锁计划, 工单排程, 冷轧排程, 生产计划, 成检计划]

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
│  ※ 生产计划/成检计划「生产编号」可点开弹窗：              │
│     Shared/BatchProgressDialog（批次执行进度卡片，        │
│     不跳转详情页；需 BatchView 角色，无权限显纯文本）     │
│  ※ 已删除独立页面（数据改经生产计划页内嵌折叠与报表总览消费， │
│     后端接口保留）：                                       │
│     SectionProductionStatus, SectionParagraphFlowAnalysis │
└───────────────────────────────────────────────────────────┘
```

### 2.4 批次上下文

```
路由前缀: /batches, /production-execution-check, /production-records, /section-outsources,
         /outsource-vendors, /outsource-recoveries, /pickling-in-records, /pickling-out-records,
         /process-card-print
菜单: 生产执行 → [生产批次, 生产执行核查, 生产记录, 去油酸洗, 工段委外, 委外单位管理, 工艺卡打印]

┌─ 生产执行 ───────────────────────────────────────────────┐
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
│  OutsourceVendors.razor     /outsource-vendors [列表页]       │
│  OutsourceVendorCreate.razor /outsource-vendors/create [创建页]│
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
│          SectionOutsources, OutsourceVendors, OutsourceRecoveries, │
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
│    (重)/回收备注；列偏好键 col_prefs_section-outsources_v2。2026-09-09  │
│    再改（V72 升 v3）：加计价三列（计价单位/单价/总价，委外信息组发出重量 │
│    后，默认显），委外单位列改为档案下拉驱动（行内编辑 RenderEditVendorField │
│    取 active 档案按行工段过滤）、IsInternal 列改只读派生显示；列偏好键   │
│    col_prefs_section-outsources_v3。同日二调（V75 升 v4）：计价三列默认  │
│    隐藏（列显隐勾选可显），列偏好键 col_prefs_section-outsources_v4     │
│  ※ OutsourceVendors.razor 委外单位档案 /outsource-vendors（V72 新增、V73 建 ② │
│    往来信息统计列组）：委外单位×委外工段主档（行键 VendorName+SectionName 唯一、│
│    编码 VendorCode WV+4 位自动）、本厂车间 IsWorkshop 行（锁定冷轧拔、IsInternal │
│    厂内虚拟发外来源）与外协单位 IsWorkshop=外协 分流。①基本信息组 列=编码(默认隐)│
│    /委外单位名/委外工段(枚举)/本厂(boolean 本厂标是·外协留空)/联系人/联系电话/  │
│    备注/状态(启用·停用 chip)，内联编辑 + ConfirmDialog 删除 + 默认按编码降序；   │
│    ②往来信息组（V73 仿供应商、V74 细化）5 只读统计列=累计委外(单+吨+万)/本年    │
│    委外/本年回收[扣除退回](吨+金额万,金额按重量份额分摊、退回无金额)/委外未回收 │
│    (吨+金额万,净值截0)/本年退回(吨)，页脚合计，全 0 显「—」，② 组数据均靠左（供 │
│    应商页 ② 同样靠左）、默认显示；工具栏「打印选中」(Mode A POST                │
│    api/outsource-vendor/print-list-file)；列                              │
│    偏好键 v2（col_prefs_outsource-vendors_v2）；新建入口 OutsourceVendorCreate.razor│
│    /outsource-vendors/create（先建档后工段委外下拉才可选，供 SectionOutsource 新建│
│    /行内编辑/扫码按行工段过滤取数 GetActiveAsync）                              │
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
路由前缀: /quality/furnace, /quality/inspection-patrol, /quality/process-inspection, /quality/material-receive-checks, /quality/final-inspection,
         /quality/process-tracking, /quality/nonconforming-feedback, /quality/ncr,
         /quality/chemical-analysis, /quality/hardness-test, /quality/grain-size-test,
         /quality/pitting-corrosion-test, /quality/intergranular-corrosion-test,
         /quality/tensile-test, /quality/metallographic-test,
         /quality/flattening-test, /quality/flaring-test,
         /quality/lab-testing, /quality/certificates
菜单: 质量管理 → [巡检, 过程检验, 成检到料, 成品检验, 成检追踪, 不合格反馈, 不合格报告, 炉号/化学(子组), 理化检测, 质量证明书]
      炉号/化学子组: [炉号登记]

┌─ 质量管理 ───────────────────────────────────────────────┐
│                                                           │
│  FurnaceRegistrations.razor       /quality/furnace              [列表页]│
│  FurnaceRegistrationCreate.razor  /quality/furnace/create       [创建页]│
│                                                           │
│  InspectionPatrols.razor          /quality/inspection-patrol    [列表页]│
│  InspectionPatrolForm.razor       /quality/inspection-patrol/create [创建页]│
│  InspectionPatrolForm.razor       /quality/inspection-patrol/{Id:int}/edit [编辑页]│
│  InspectionPatrolViewDialog.razor （页面内查看弹窗，含明细/照片/打印）│
│                                                           │
│  ProcessInspections.razor         /quality/process-inspection   [列表页]│
│  ProcessInspectionCreate.razor    /quality/process-inspection/create [创建页]│
│                                                           │
│  MaterialReceiveChecks.razor        /quality/material-receive-checks      [列表页]│
│  MaterialReceiveCheckCreate.razor   /quality/material-receive-checks/create [创建页]│
│                                                           │
│  FinalInspections.razor           /quality/final-inspection     [列表页]│
│  FinalInspectionCreate.razor      /quality/final-inspection/create [创建页]│
│  InspectionPhotoDialog.razor     （共用照片弹窗：过程检/成检 补拍·查看·删除，无路由）│
│                                                           │
│  QualityProcessTracking.razor     /quality/process-tracking    [列表页]│
│                                                           │
│  NonconformingFeedbacks.razor     /quality/nonconforming-feedback            [列表页]│
│  NonconformingFeedbackForm.razor  /quality/nonconforming-feedback/create     [创建页]│
│  NonconformingFeedbackForm.razor  /quality/nonconforming-feedback/{Id:int}/edit [编辑页]│
│                                                           │
│  Ncrs.razor                      /quality/ncr                     [列表页+待处理双表]│
│  NcrForm.razor                   /quality/ncr/create               [创建页]       │
│  NcrForm.razor                   /quality/ncr/{Id:int}             [详情页]       │
│  ※ 建单/编辑页「生产编号」旁「来源照片」入口：            │
│     NcrSourcePhotoDialog（只读照片弹窗，来源记录无照片  │
│     则不渲染入口）                                         │
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
│  报表页: MonthlyStock（原生 table，4 报表切换：入库/出库/库存/物料进出存；入库按来源展开、出库按类型展开（含物料汇总合并列）；行=库房×物料类型，库房+物料类型双层合并单元格，无合计行；当前月之后月份单元格留空；物料进出存 inout V76：每月格彩带「入x/出y」（入浅绿/出浅红，内联样式屏显打印一致），末尾「进出汇总」（全年入/出，倒数第2列）+「实时库存」（只显当前库存 ClosingWeight 单值，原「实时数据」更名）；入库/出库/库存单值展示，库存「实时结存」=ClosingWeight；打印横向 A4 撑满页宽）│
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

### 2.9 产品标准上下文

```
路由前缀: /standard-registers, /grade-mappings, /grade-chemical-compositions, /grade-physical-properties, /sub-standard-quick-views, /standard-inspection-requirements, /factory-inspection-requirements, /chemical-composition, /chemical-validate
菜单: 产品标准 → [标准号列表, 标准号检验项要求, 工厂检验项要求, 牌号对照, 标准牌号化学成分, 工厂牌号化学成分, 工厂牌号化分验证, 牌号物理性能, 子标准速览]

┌─ 产品标准 ───────────────────────────────────────────────┐
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
│     已迁移至产品标准上下文，路由同步更新为产品标准前缀        │
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
│  Index.razor               /                  [首页看板+入口]│
│  Login.razor               /login             [登录页]       │
│  DataExchange.razor        /data-exchange     [数据工具]     │
│  ScanExecute.razor         /mobile-report     [报工扫码]     │
│  ScanQuality.razor         /mobile-quality/{Kind} [巡检 / 不合格反馈扫码]│
│  EquipmentRepair.razor     /equipment-repair  [扫码报修]     │
│  RepairExecute.razor       /repair-execute    [扫码维修]     │
│  EquipmentScan.razor       /equipment-scan    [设备扫码入口]   │
│  Admin/Users.razor         /admin/users         [用户管理]    │
│                                                           │
│ 首页看板：桌面上下堆叠三卡(各约半屏宽居中)；手机上下堆叠三卡  │
│   常用入口 + 订单进度查询 + 生产批次进度查询（订单负荷卡 V133 删）│
│ 导航栏：左侧 MudNavMenu 树形导航（200px 宽；单一数据源 AppMenu.Root，2026-09-10 定稿序）
│ 首页
│ ▸ 报表总览（原「报表系统」单叶组拍平为一级单项，置首页下第 2 位）
│ ▸ 订单管理 → 订单列表 / 客户管理 / 订单在库成品
│ ▸ 工单管理 → 工单生成 / 需求调整 / 工单用料 / 用投料核查 / 查询工单执行 / 查询定尺工单
│            （V144 六项并列二级，原「工单操作 / 工单查询」分组取消）
│ ▸ 计划排程 → 订单负荷总量 / 工单排程 / 冷轧排程 / 生产计划 / 成检计划
│ ▸ 生产执行 → 生产批次 / 生产执行核查 / 生产记录 / 去油酸洗 / 工段委外 / 委外单位管理 / 工艺卡打印
│            （V146 原「批次管理」更名；组内 7 项全属生产执行动作，与「计划排程」构成 计划→执行）
│ ▾ 质量管理（3 级嵌套）
│    巡检 / 过程检验 / 成检到料 / 成品检验 / 成检追踪
│    ▾ 不合格处置 → 不合格反馈 / 不合格报告
│    ▾ 炉号/化学 → 炉号登记
│    ▾ 理化检测 → 化学 / 硬度 / 晶粒度 / 点腐蚀 / 晶间腐蚀 / 室温拉伸 / 金相 / 压扁 / 扩口
│    质量证明书
│ ▸ 物料管理 → 采购订单 / 圆棒穿孔（圆棒穿孔 / 子项查询）/ 供应商管理
│ ▸ 仓库管理 → 原料库 / 成品库 / 在制品库 / 次品库 / 物料进出存报表
│ ▸ 设备管理 → 设备台账 / 维修工单 / 保养工单 / 点检记录
│ ▸ 产品标准 → 标准号列表 / 标准号检验项要求 / 子标准速览 / 牌号对照 /
│              标准牌号化学成分 / 牌号物理性能 / 工厂检验项要求 /
│              工厂牌号化学成分 / 工厂牌号化分验证
│ ▸ 扫码管理 → 报工扫码 / 巡检扫码 / 不合格反馈扫码 / 设备扫码 / 工位管理(ScanView) / 员工管理(ScanView)
│              （整组仅登录；后两项单独带 ScanView 档）
│ ▸ 工资结算 → 生产计件标准 / 成检计件标准 / 考勤表 / 杂辅工记录 / 集体计件评分 /
│              津贴与处罚 / 非计件工资 / 个人计件工资 / 集体计件月结 / 靠工计件月结 /
│              月工资津贴汇总
│ ▸ 参数表 → 工序组定义(批次/工艺) / 工段工量天数(排程/用料) / 交货状态附加天数(排程/用料) /
│            规格日产预估(工单执行) / 冷轧产能档案(冷轧排程) / 冷轧机台数配置(冷轧排程) /
│            冷轧机台组配置(冷轧排程) / 重点工段日产(生产总览) / 段落日产配置(段落流转) /
│            枚举显示配置(全局显示) / 字典显示配置(全局显示) / 系统参数(全局参数)
│ ▸ 数据工具（单项，置参数表之后、用户管理之前）
│ 用户管理
│                                                           │
└───────────────────────────────────────────────────────────┘
```

---

## 3. 列表页完整清单（需检查加载/排序/筛选）

共 **88 个列表页**，采用 `ServerData` + `ExcelFilter` 模式：

| # | 页面文件 | 路由 | 上下文 | 内联编辑 | 备注 |
|---|---------|------|-------|---------|------|
| 1 | Orders.razor | /orders | 订单 | ✅ | B23 列分组3组(基本信息/订单确认/订单执行)；「基本信息」=原基本信息+合同交付合并，默认仅显示订单号/签订日期/业务员/客户名称/交期截止/订单总重量/含项次数，余(最终客户/交期起始/延期罚款等)默认隐藏 + ExcelFilter + B23分组标题栏 + 搜索栏3组(模糊搜索+签订日期+交货日期) + 工具栏「完成预估及延期风险」折叠卡片（仅两张交期预估小表：订单(整单)完成预估 / 风险-已延期订单(整单)，单元格 `z单/x吨/y万` 三色（蓝单/绿吨/万橙，2026-09-09 加金额、不再显示延期罚款 `[*a/b]`），逐格联动筛选订单列表、各表可打印；原「接单-出库及现负荷」5指标×12月小表已删除）+ 工具栏「投料产出总况」折叠卡片（行=订单完成月，10 列 `完成月/订单数/生产投料/订单成品入库/余库料入库/次品入库/备料成品/投料产出率/产出成品比/退货`，默认近 12 个月（V126 起可改用「完成日期起/止 + 应用 + 清除」按完成日期区间取数，**V130 修正：服务端按订单真实完成日落入闭区间过滤、不受 12 个月限制**），重量四舍五入取整、比率分母 ≤0 显示 —，口径下拉可切全部/纯生产/单一生产类型 4 档，「订单数」= 该口径下真实相关订单数（该月在本生产类型范围内有生产批次的订单数，非该月完成订单总数；该口径下无相关订单的月整行隐藏），2026-09-14 新增），页面标题=订单列表） |
| 2 | Customers.razor | /customers | 订单 | ✅(①组档案列内联) | B23 列分组 2 组 + 底部合计 + 完整打印：① 基本信息（默认仅显 业务员/最终用户/状态，客户编码/客户单位/联系人/电话/地址/备注默认隐藏，内联编辑）；② 往来信息（8 业务统计只读列，服务层按「业务员+最终用户」聚合实时注入，仅 GetPagedAsync 回填；8 列均按「X单/吨/万」三位一体显示（单数=落入该状态桶的订单张数，可跨阶段并列），吨/万保留 1 位小数；金额按结算分治：过磅=实际公斤不封顶、理算/过磅-负=封顶合同额发货→库存→在产阶梯认领；待在产两桶仅统计主号未完成(阶段≠1)订单（已完成单欠产/未入库不计在产）；统计列不可排序/筛选（SortKey=null 标记）；底部合计仿订单=②组数值列页内合计；打印选中=Mode A 列表 PDF 按可见列含统计列完整打印 → print-list-file；ColumnPrefsVersion=v2） |
| 3 | GradeMappings.razor | /grade-mappings | 产品标准 | | |
| 4 | WorkOrders.razor | /workorders | 工单 | ✅ | 2026-09-08 默认隐藏 次号/最终客户/钢管制造（列偏好键升 col_prefs_workorders_v1） |
| 5 | MaterialPlanOverview.razor | /material-plan-overview | 工单 | | |
| 6 | **Batches.razor** | /batches | 批次 | | ✅ 已过规范检查 |
| 7 | **ProductionRecords.razor** | /production-records | 批次 | ✅ | 列分组4组（G1执行信息/G2产出数据/G3工艺参数/G4追溯信息）+ ExcelFilter + 内联编辑 + 分组标题栏；**2026-09-09 默认显隐收敛**：G1仅显 执行日期/生产编号/挂牌号/订单号/主号/工序名称/工段名称/工厂牌号/制造规格（工单号/执行序号 隐），G2仅显 加工支数/加工重量/产类/平头数/断切倍数/预成切/长度状态/成品长度/符合工单长度/切后支数（设备名称/班次/操作人 隐），G3工艺参数/G4追溯信息 整组隐；列偏好键 `ColumnPrefsVersion="v1"`（`col_prefs_production-records_v1`）；**列宽修复**：表格移除 `table-min-width`+`--table-min-width`（原把表根 min-width 锁到全部可见列宽总和致过宽），与生产批次表格设置对齐 |
| 8 | **SectionOutsources.razor** | /section-outsources | 批次 | ✅ | 列分组（委外信息+回收信息）+ 内联编辑；2026-09-09 默认列显隐收敛（V70 v2，列偏好键 col_prefs_section-outsources_v2）：委外信息 隐 要求收回日期/紧急；回收信息 隐 非正常回收(支)/非正常回收(重)/回收备注；**V72 再改（列偏好键 v2→v3）**：委外信息组加 计价单位(元/Kg·元/米·元/支 下拉)/单价/总价 三列（发出重量之后，默认显，厂内行禁用留空），委外单位列改档案下拉驱动（RenderEditVendorField 取 active 档案按 item 工段过滤、当前档外历史值不改可不拦），IsInternal 列改只读派生显示（不再手动开关），保存触发服务端重判定厂内/计价；列偏好键 col_prefs_section-outsources_v3 |
| 9 | **OutsourceRecoveries.razor** | /outsource-recoveries | 批次 | | 2026-09-09 默认列显隐收敛（首并列偏好键 col_prefs_outsource-recoveries_v1）：回收信息 隐 数据来源/更新时间 |
| 10 | PicklingInRecords.razor | /pickling-in-records | 批次 | | 去油/酸洗入缸记录（入缸报工）；2026-09-09 默认列显隐收敛 + 移除 table-min-width，2026-09-09 二调（列偏好键 v1→v2）：G1 默认仅显 登记日期/生产编号/挂牌号/订单号/主号/工序名称/工段名称/工厂牌号/制造规格/生产支数/生产重量/产类（工单号/执行序号/设备名称/班次/操作人/备注/数据来源/更新时间 隐）；G2 状态/完工日期 默认显示、完工班次/完工操作人 默认隐藏（由整组隐改回，col_prefs_pickling-in-records_v2） |
| 11 | PicklingOutRecords.razor | /pickling-out-records | 批次 | | 去油/酸洗完工记录；2026-09-09 默认隐 备注/数据来源/更新时间 + 移除 table-min-width，2026-09-09 二调（列偏好键 v1→v2）：入缸信息 增隐 设备名称（col_prefs_pickling-out-records_v2） |
| 12 | FurnaceRegistrations.razor | /quality/furnace | 质量 | | 2026-09-09 更新日期 默认隐藏（首并列偏好键 col_prefs_furnace-registration_v1） |
| 13 | ChemicalCompositions.razor | /chemical-composition | 产品标准 | | 原属质量上下文，已迁移 |
| 14 | ChemicalValidationRules.razor | /chemical-validate | 产品标准 | | 原属质量上下文，已迁移 |
| 15 | ProcessInspections.razor | /quality/process-inspection | 质量 | | 2026-09-09 默认列显隐收敛（默认显24列）：G1 隐 执行序号、G4 隐 理论返整重/理论入库重/理论报废重/次品情况描述、G5辅助信息整组隐 + 移除 table-min-width（列偏好键 col_prefs_process-inspection_v1）；2026-09-09 二调（列偏好键 v1→v2）：隐 工单号/设备名称/班次（col_prefs_process-inspection_v2）；**2026-09-11 V94 不合格处理四档化**（列偏好键 v2→**v3** `col_prefs_process-inspection_v3`）：G4 改 9 列（返整支/入在制库支/入次品库支/退货支/理论返整重/理论入在制重/理论入次库重/理论退货重/次品情况描述），新建页面同步；**2026-09-11 V96 组名更名**：G4「不合格处理」→「不合格品去向」（仍 4 档 9 列，列偏好键保持 v3）；**2026-09-12 V104 检验照片 + 单据式打印**（列偏好键 v3→**v4** `col_prefs_process-inspection_v4`）：新增「照片(N)」列（点击开 `InspectionPhotoDialog`，每条上限 3 张），工具栏新增「打印选中单据」（A4 竖版每条一页 + 照片，单次 ≤20 条）
| 16 | MaterialReceiveChecks.razor | /quality/material-receive-checks | 质量 | | 2026-09-09 默认列显隐收敛（首并列偏好键 col_prefs_material-receive-checks_v1）：隐 执行序/班次/数据来源/更新时间 |
| 17 | FinalInspections.razor | /quality/final-inspection | 质量 | | 2026-09-09 默认列显隐收敛（63列默认显33列）：G1 隐 资格等级、G2 隐 生产类型/制造物品/制造状态/交货状态/最终用户/来料单位/长度状态、G3 隐 非定尺长度范围、G4不合格处理全显；G5尺寸值/G6压力值/G7涡流超声波/G8辅助信息整组隐 + 移除 table-min-width（列偏好键 col_prefs_final-inspection_v1）；2026-09-09 二调（列偏好键 v1→v2）：隐 设备名称/班次/工单号（更新时间已于 G8 默认隐）（col_prefs_final-inspection_v2）；**2026-09-11 V94 不合格处理四档化**（列偏好键 v2→**v3** `col_prefs_final-inspection_v3`）：G4 改 9 列（返整支/可入备库支/入次品库支/退货支/理论返整重/理论可入备库重/理论入次库重/理论退货重/次品情况描述），新建页面同步；**2026-09-11 V96 不合格品去向五档化**（列偏好键 v3→**v4** `col_prefs_final-inspection_v4`，须重排新列位置）：G4 改 11 列（返整支/入在制库支/入次品库支/退货支/可入备库支/理论返整重/理论入在制重/理论入次库重/理论退货重/理论可入备库重/次品情况描述），组名「不合格处理」→「不合格品去向」，新建页 G4 同步 11 列；**2026-09-12 V104 检验照片 + 单据式打印**（列偏好键 v4→**v5** `col_prefs_final-inspection_v5`）：新增「照片(N)」列（点击开 `InspectionPhotoDialog`，每条上限 3 张），工具栏新增「打印选中单据」（A4 竖版每条一页 + 照片 + 按检验项目条件渲染专用参数，单次 ≤20 条） |
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
| 31 | Ncrs.razor | /quality/ncr | 质量 | | 列表页+分页汇总；2026-09-09 更新日期 默认隐藏；**2026-09-12 列偏好键 `col_prefs_ncrs_v1` → `col_prefs_ncrs_v2`（`ColumnPrefsVersion` v1→v2）**；**待处理拆两组两表**——「待处理批次」卡片与「不合格品实时待处理」表各按 `Bucket` 分「**正常提交**（来源=不合格反馈，无条件列出）」/「**超阈值遗漏**（来源=过程检验/成品检验，组级一条：让步放行支+各流向支 合计 > `NcrThreshold.Count` 且 合计÷组内总数 > `NcrThreshold.Percent`，**均严格大于**）」两节，各自条数 chip；超阈值遗漏行**可展开流向明细**（返整/入在制库/可入备库/入次品库/退货各 N 支）、「次品流向」列只读取组内最多流向（并列按 返整>入在制库>可入备库>入次品库>退货 定序；正常提交行为空）；超阈值遗漏组**不再提供行内忽略入口**（**2026-09-13 起**：删「显示已忽略」开关、已忽略组表格与「恢复」按钮，端点 `POST api/ncr/pending-checks/dismiss` / `.../{id}/restore` 已删除）；**忽略改为在「建单页『G1 问题反馈』标题右侧」点按钮、按正常建单流程落一张 `NcrStatus.Ignored` 报告**（V8.32 由「G1 之后另起一行」移入标题行），该组因 `SourceGroupKey` 命中（**排重不看状态**）**永久**不再列出（原「合计增长自动复活」取消）；状态列 chip 新增忽略档（`Color.Dark`）；G1 问题反馈组新增「**让步支数/让步重量/让步说明**」3 列（仅被动行有值，列序 `…次品支数/次品重量/问题描述/次品流向/让步支数/让步重量/让步说明`）；「反馈部门」口径=位置（过程检验/不合格反馈→工段；成品检验→成检项目）；建单页 `NcrForm` 待处理选择器**按两组分节**（被动行带入让步三字段 + 组键，主动来源清空让步字段），**待处理批次卡片默认折叠**，G1 第一行 = `反馈日期*/生产编号*/工单号/反馈部门/反馈人/物料类型*`、第二行 = `牌号/规格/次品支数/次品重量` +（被动）`次品流向`；**V8.32**：建单/编辑页 **G4 行序对齐 G2**（第一行 `责任类别/新增责任类型/添加/处理是否完结/完结日期`、第二行 `责任部门/生产责任人/生产操作日期`）、**「忽略」按钮移至 G1 标题右侧**、**反馈人显示去工号**（`FormatPersonName` 逐段剥离，页面 + 列表 + 导出：「张燕平(YG045)、赵路陈」→「张燕平、赵路陈」）。**V8.34**：待处理口径改**记录级**（「超阈值遗漏」逐条检验记录判定，一条记录一行；记录定位键 6 段含检验记录 Id；该条记录已建单/忽略即不列、同维度其它记录照常列出；存量旧 5 段键命中则该维度全不列）；「待处理批次」卡片新增**「检验日期」列**（置「检验项目」后）；建单/编辑页「生产编号」旁新增**「来源照片」入口**（有照片才显示，弹只读 `NcrSourcePhotoDialog`）。详见 `质量管理模块详细设计.md` V8.31 / V8.32 / V8.34 |
| 31.1 | NonconformingFeedbacks.razor | /quality/nonconforming-feedback | 质量 | | **2026-09-11 新增「不合格反馈」**（从「不合格报告」拆出的上游登记单，只登记问题+照片、不做处置、**不带任何处理状态**；「已处理」= 是否已生成 Ncr）：标准列表页（服务端分页/全字段排序/模糊搜索/反馈日期区间/ExcelFilter 列筛选/列显隐持久化，列偏好键 `col_prefs_quality_nonconforming_feedback_v1`，**列偏好版本 v2 → v3**）；17 列 → **19 列**（**2026-09-12 新增「来源类型」「检验项目」两列**），默认显 15 → **17**（隐藏 数据来源/更新时间）；操作列=**查看（含照片，QualityView 即可）+ 编辑（按 QualityEdit 门控）+ 删除（ConfirmDialog，连附件磁盘文件一并清理）**；**「照片」列可点击打开查看弹窗**（`NonconformingFeedbackViewDialog`：只读全字段 + 缩略图点击放大 + 打印单条 PDF）；新建/编辑页 `/quality/nonconforming-feedback/create` 与 `/{Id:int}/edit`（**来源类型三档：生产工段/过程检验/成品检验**；生产工段与过程检验 = 工序名称→制造规格→工段名称三级联动；**成品检验 = 工序 + 检验项目 + 只读「成检类型」（预检/终检，按工序与批次附加成检推导）**；不合格重量 = 来料重量÷来料支数×不合格支数 四舍五入取整、可手改、清空恢复自动；照片最多 9 张，客户端 canvas 压缩后经鉴权 API 上传，文件本体落服务器文件系统 `Attachment:RootPath`）。菜单：质量管理 → **不合格反馈**（在不合格报告之前） |
| 31.2 | InspectionPatrols.razor | /quality/inspection-patrol | 质量 | | **2026-09-11 新增「巡检」**（独立表检验类型，菜单为质量管理首项、位于「过程检验」之前）：标准列表页（服务端分页/全字段排序/模糊搜索/巡检日期区间/ExcelFilter 列筛选/列显隐持久化，列偏好键 `col_prefs_quality_inspection_patrol_v1`）；18 列默认显 16（隐藏 数据来源/更新时间）；操作列=**查看（含明细与两类照片，QualityView 即可）+ 编辑（按 QualityEdit 门控）+ 「是否闭环」一键切换（仅涉及整改的行显示）+ 删除（ConfirmDialog，连附件磁盘文件一并清理）**；**「巡检项数」「照片」列可点击打开查看弹窗**（`InspectionPatrolViewDialog`：只读全字段 + 明细表 + 缩略图点击放大 + 打印单条 PDF）；新建/编辑页 `/quality/inspection-patrol/create` 与 `/{Id:int}/edit`（巡检明细一对多动态行表、整改块按「涉及整改」条件渲染、在产单位·车间委外单位档案候选可手输、**巡检人/在产操作人为员工档案下拉且可手输（双通道）**、**在产设备名为纯手输文本框（无候选、可留空）**、照片按巡检/整改两类分列各最多 9 张）。 |
| 32 | OrderDemandAdjustment.razor | /workorders-demand-adjustment | 工单 | ✅ | 内联编辑催单/分批/暂停开关及调整备注（2026-09-08 默认隐藏 订单日期/最终客户/主号-预计完成日/主号-原锁备注；PageState 键升 page_state_workorders-demand-adjustment-v1） |
| 33 | RawMaterialLockPlanAndExecution.razor | /raw-material-lock-plan | 计划排程 | ✅ | G15 预执行 MudSwitch 内联编辑 + BudgetInputDate 日期输入（LEFT JOIN 实时查询，无计划安排按钮）；右上角「待投料量汇总」按钮展开汇总卡片（待投原料+成购两矩阵表 + 理论待投料截日「全期合计」，可打印）；成购口径 2026-09-10 收紧为仅「执行用料计划」工单，截日行名 完善用料-原料类/执行用料-原料类/执行用料-成购类 |
| 34 | StandardWorkDays.razor | /standard-work-days | 配置 | ✅ | 查改一体表 |
| 35 | StandardWorkDayDeliveryStates.razor | /standard-work-day-delivery-states | 配置 | ✅ | 查改一体表 |
| 36 | ConfigParameters.razor | /config-parameters | 配置 | ✅ | 查改一体表 |
| 40 | WorkOrderSchedules.razor | /scheduling-plans | 计划排程 | | LEFT JOIN 实时查询模式（WorkOrderExecutionSummary + WorkOrderPlan 薄表），G15 内联编辑 + 计划安排按钮 + 2026-09-08 最终客户默认隐藏（列偏好键 col_prefs_workorderschedules_v1） |
| 41 | DailyOutputEstimates.razor | /daily-output-estimates | 配置 | ✅ | 查改一体表 |
| 42 | Workstations.razor | /workstations | 配置 | ✅ | 查改一体表 |
| 43 | Employees.razor | /employees | 配置 | ✅ | 查改一体表（列偏好 v5，2026-09-03 靠工计件六期：「靠工系数」前新增**靠工岗位**多选列 AttendancePositions，候选=计件活岗 GET api/employee/piece-positions，保存岗位英文 Key 逗号串，显示逐项中文） |
| 44 | ColdRollPlans.razor | /cold-roll-plans | 计划排程 | | 冷轧按规格维度聚合时间桶分布计划 + 简化/明细视图切换 + 打印功能 + 排程编辑模式（在轧要求/待轧要求/待轧序/待轧设备号/单机单日量）+ **右上角排机估算折叠表（4行×5列，懒加载，可打印）** + **排程建议折叠卡片（半自动：三步决策 特急锁定→流转保底→产能平衡，组级+行级明细表，「一键采用建议」走 save-all 全量同步）** + 搜索栏+ExcelFilter列筛选 |
| 45 | BatchPlans.razor | /batch-plans | 计划排程 | | 全量加载 Items 模式 + **工段筛选 Tab 配置驱动**（`GET api/batch-plan/section-tab-options`：冷轧/冷拔=工序组定义启用冷轧拔工序逐工序、普通工段=工段工量天数启用工段扣除冷轧拔/检验/入库且内抛/内修磨独立、末尾固定荒管检/在制检，2026-08-30 起新增工序自动出现）+ 列分组标题栏 + 列显隐（永久隐藏 22 列：冷轧排程 5 组 + 工单需求调整 + 批次基础信息多余字段）+ 客户端排序/筛选 + 6 项 Tab 汇总（批次数/总重量/计划流转批次/重量/计划重点批次/重量，重点按 PlanFlowLevel==1 急+）+ 汇总重量单位吨(t) + G13 生产计划组只读（仅抢单/计划备注内联编辑） + 2026-09-08 批次基础信息组默认显隐（v2→v3）：制造状态换源默认显示、交货状态/长度状态默认隐藏、重量(kg)→重量 + **「生产编号」可点开「批次执行进度」弹窗**（`BatchProgressDialog`，不跳转批次详情页；需 `BatchView` 角色，无权限者保持纯文本） |
| 46 | FinalInspectionPlan.razor | /final-inspection-plan | 计划排程 | | 全量加载 Items 模式 + 五档Tab(全部/待到料/待检验/检验中/完成检验待入库) + 待检批支重汇总卡片（行=检验项，列=检验项/待到料/待检验+检验中/汇总数据，0值显"-"，可打印）+ 客户排序/筛选 + 列分组 G1-G6（G1批次/G2排程/G3成检状态/G4技术要求检验项/G5各项检验日期/G6数量）+ 紧急程度 MudChip 颜色渲染；默认列显隐收敛（V56）：G1 批次仅显 生产编号/生产类型/制造状态/工厂牌号/规格/长度状态/支数/重量/订单号/主号/业务员，G3 成检状态仅显 成检阶段/到料日期，G5 各项检验日期+G6 数量整组默认隐藏，余默认隐藏（可经列选择器打开）+ **「生产编号」可点开「批次执行进度」弹窗**（`BatchProgressDialog`，不跳转批次详情页；需 `BatchView` 角色，无权限者保持纯文本） |
| 47 | StandardRegisters.razor | /standard-registers | 产品标准 | | ExcelFilter 列筛选 + RenderCell 模板 + FooterContent 分页汇总 + 导航至详情页；Save/SaveItem 返回 Id 防 SeqNo 重复 |
| 48 | GradeChemicalCompositions.razor | /grade-chemical-compositions | 产品标准 | ✅ | 15元素内联编辑 + 全列ExcelFilter(17列) + 列显隐 |
| 49 | GradePhysicalProperties.razor | /grade-physical-properties | 产品标准 | ✅ | 12物理性能字段内联编辑 + 全列ExcelFilter(12列) + 列显隐 |
| 50 | SubStandardQuickViews.razor | /sub-standard-quick-views | 产品标准 | | 全列ExcelFilter(23列)，按标准号快速查看24项检验项目引用标准 |
| 51 | StandardInspectionRequirements.razor | /standard-inspection-requirements | 产品标准 | ✅ | 全列ExcelFilter(23列)，标准号检验项要求+内联编辑 |
| 52 | FactoryInspectionRequirements.razor | /factory-inspection-requirements | 产品标准 | ✅ | 工厂检验项要求，全列ExcelFilter(30列)，29检验字段内联编辑 + 打印（选中+全部） |
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
| 87 | ProductionExecutionCheck.razor | /production-execution-check | 批次 | | 生产执行核查页（2026-09-08 生产执行拆分两入口之一，当时组名「批次管理」，2026-09-15 更名）：承载「错疑-生产批次执行」聚合卡（即原「批次-错疑执行」，2026-09-09 改名；4 类错疑 匹配工单/工段流转/有效投料/成品切割 批次数+领料重量合计，**默认折叠、点开才懒加载**，BatchCount>0 可点选联动筛下列表，可取消筛选；列头错疑列手动筛选同样可用）+ 页内自足精简批次列表（服务端分页，列组=批次与工单（批次与执行及关联工单合并，生产编号/状态/工单号/工单关注 + 挂牌号/次号隐藏；V61 裁剪 当前工序/当前工段/截止执行日/工段完工/订单号/主号 六列）/ 执行核查（**4 灯** 匹配工单/工段流转/投料需调整/成切存疑 必显；V62 成切存疑由投料组回归）/ 理论产出对照（**错疑缘由数据对照**：过程检理论成支·现理论成支/理论成品重·成切需求/执行/支数；V64 取消 过程检成重 列；V63 组名「投料与有效量」改名点题 + 过程检列前移到 现理论成支 前 + 列名精简（过程检理论成品支→过程检理论成支、理论成品支→现理论成支，理论成品重列名保持）；V62 裁 领料支数/领料重量/现有效原料支数/现有效原料重量/缺陷-返整量/缺陷-纯次品量 六列，页底合计）；只读仅「查看详情」跳 /batches/{id}，无删除/编辑，无通知轮询/无打印全部）；**顶部无搜索栏**（2026-09-09 模糊搜索+登记日期整行删除，定位靠错疑卡联动+列头筛选））；列偏好 key `batchExecutionCheck_v6`、页面状态 key `batchExecutionCheck`（排序/列筛选） |
| 88 | **OutsourceVendors.razor** | /outsource-vendors | 批次 | ✅ | 委外单位档案主档页（V72 新增，2026-09-09）：委外单位×委外工段 主档，行键 VendorName+SectionName 唯一（英文字段名大小写不敏感）、编码 VendorCode WV+4 位（新增自动，列表列默认隐藏）；8 列=编码(默认隐)/委外单位名/委外工段(工段枚举下拉)/本厂外协(IsWorkshop MudSwitch，勾选本厂车间→工段锁定冷轧拔)/联系人/联系电话/备注/状态(启用·停用 MudChip)，内联编辑 + ConfirmDialog 删除；服务端分页 + ExcelFilter + 列头排序 + 模糊搜索（委外单位名/联系人）+ 默认按编码降序 + 方向键导航；列偏好键 `col_prefs_outsource-vendors_v1`；新建入口 → 独立创建页 OutsourceVendorCreate.razor `/outsource-vendors/create`（本厂车间 IsWorkshop 仅冷轧拔可选）；本档供工段委外新建/行内编辑/扫码 按行工段过滤取数（`GetActiveAsync` active 全量），未建档委外单位工段委外不可选（先建档） |

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
| 全站手机壳 | `MobileLayout.razor` 内真实渲染的 `<div class="mh-shell">`（**手机竖屏下所有非首页页面的统一收敛钩子**，见 `wwwroot/css/app.css`「全站手机壳」段：页头按钮组独占换行 / MudGrid 子项逐行全宽 / 留白密度 / **宽表：粘性表头 + 粘性首列 + 勾选框首列时粘第 2 列 + 滚动区 `calc(100vh-140px)`** / `.list-toolbar` 换行）。⚠️ 用真实 div 做钩子，**禁止改成祖先容器 + 后代选择器**（2026-09-14 真机样式失效即此因） |
| 移动横屏提示条 | `LandscapeHintBanner.razor`（原 MobileLayout 顶部壳级组件：竖屏宽表页提示横屏、localStorage 关闭持久、锁竖屏浏览器特制文案）+ `LandscapeHintRule.cs`（宽/窄页判定：宽页 = AppMenu.AllLeaves() − `/` − `/mobile-report`·`/equipment-scan`·`/mobile-quality/patrol`·`/mobile-quality/feedback` 窄叶，排除登录/进出货/维修点选窄页与 `/create`·`/edit` 表单路径；防漂移单测 `MES.Tests/Components/LandscapeHintRuleTests.cs`）。**⚠️ 2026-09-14 起 `MobileLayout` 不再自动渲染该组件**（用户拍板「竖屏也要可用」，V135）——组件与判定规则均保留不删，恢复只需把 `MobileLayout.razor` 里注释掉的那行调用加回 |

---

## 7. 「生产编号 → 生产批次详情」链接台账

> 登记日期：2026-09-10（批三十三）。核查范围：全部列表页中显示「生产编号 / 生产批号 / 批次号」类列者。
> 目的：统一「点生产编号 → 看生产批次」的入口口径，避免各页行为漂移；遗漏项在此登记后续补。

### 7.1 链接方式选型原则

| 选型 | 判定条件 | 理由 |
|------|---------|------|
| **直接链接详情页** | 列表**行主体 = 批次**（一行一批次），「生产编号」是该行的主定位字段 | 用户点击意图就是"看这个批次的全部"；无跳转丢状态问题 |
| **弹窗（批次执行进度）** | 列表**行主体 ≠ 批次**（一行 = 计划/排程/待办），「生产编号」只是行内一个属性 | 用户只想"瞟一眼这批到哪了"，切页会丢失当前筛选/页签/分页状态 |
| **不链接** | 列语义是**仓库批次 / 库存批次**（`InventoryBatchDto.BatchNo`、`InventoryBatchNo`），与生产批次是不同实体 | 语义不同，链接过去是错误跳转 |
| **不链接（有意）** | 该列已是别的业务动作入口 | 见 7.4 |

### 7.2 已具备链接（现状基线）

**直接链接 `/batches/{id}`（12 处）**

| 页面 | 路由 | 权限策略 | 备注 |
|------|------|---------|------|
| 生产批次 | `/batches` | BatchView | 详情列 `ViewDetail` |
| 生产记录 | `/production-records` | BatchView | |
| 生产执行检查 | `/production-execution-check` | BatchView | |
| 工艺卡打印 | `/process-card-print` | BatchView | |
| 工段委外 | `/section-outsources` | BatchView | |
| 去油酸洗入缸记录 | `/pickling-in-records` | BatchView | **2026-09-10 新增** |
| 质量过程跟踪 | `/quality-process-tracking` | QualityView | 页级 QualityView ⊄ BatchView，**未做降级**（既有实现） |
| 物料到货检验 | `/material-receive-checks` | QualityView | 同上 |
| 生产过程检验 | `/process-inspections` | QualityView | 同上 |
| 成品检验 | `/quality/final-inspection` | QualityView | **2026-09-10 新增**；QualityView ⊄ BatchView → **按角色降级** |
| 不合格报告 | `/quality/ncrs` | 仅登录（无页级策略） | **2026-09-10 新增**；后端补 `ProductionBatchId`；**按角色降级** |
| 报表总览·NCR 待处理 | `/reports/overview` | ReportView | **2026-09-10 新增**；ReportView ⊆ BatchView 无需降级 |

**弹窗「批次执行进度」（4 处）**

| 页面 | 路由 | 权限策略 | 备注 |
|------|------|---------|------|
| 生产计划 | `/batch-plans` | SchedulingView | 走 `Shared/BatchProgressDialog.razor`；SchedulingView ⊄ BatchView → 按角色降级 |
| 成检计划 | `/final-inspection-plan` | SchedulingView | 同上 |
| NCR 建单/编辑页 | `/quality/ncr/create`、`/quality/ncr/{id}` | QualityView | **2026-09-13 新增**；「生产编号」为输入控件，**在字段右侧另加 `Timeline` 图标按钮**作为入口（非点击字段本身）；QualityView ⊄ BatchView → **按角色降级**；批次主键来自 `NcrLookupResultDto.ProductionBatchId`（后端 V8.33 回带） |
| 订单进度树（`Shared/OrderProgressTree.razor`） | `/orders/progress` + 首页「订单进度查询」卡 | **仅登录（无页级策略）** | **2026-09-15 V143 新增**；叶子「生产批次名单」展开后批号可点（在产 / 在途分段，检验叶单段）；**无需权限降级**——`api/batch/{id}/tracking` 已于 2026-09-14 放宽为 `[Authorize]` 仅需登录（首页放开），点批号不会 403 |

> ⚠️ **权限降级实现要点**：`Roles.Policies.BatchView` 是**逗号分隔多角色串**，`ClaimsPrincipal.IsInRole` 只认单角色名 → 必须 `Policies.BatchView.Split(',', RemoveEmptyEntries|TrimEntries).Any(user.IsInRole)`，否则恒 false、链接对所有人失效。

### 7.3 缺口台账（2026-09-10 核查，**本次未实现**）

**A 组 · DTO 已含 `ProductionBatchId`，纯前端即可补（无后端/DB 改动）**

| 页面 | 路由 | 列标签 | 列 Key | 未补原因 |
|------|------|--------|--------|---------|
| 去油酸洗出缸记录 | `/pickling-out-records` | 批次号 | BatchNo | 本轮范围外，用户拍板"仅登记" |
| 用料计划 · Tab6 在产改制计划 | `/work-order-material-plan` | 生产编号 | `InProcessReworkPlanDto.BatchNo` | 同上 |
| 用料计划 · Tab7 在产主工单计划 | `/work-order-material-plan` | 生产编号 | `InMainWorkOrderPlanDto.BatchNo` | 同上 |
| 成检计件类别 · 试算记录表 | `/payroll/final-inspection-categories` | 生产编号 | `rec.BatchNo` | 试算样本行，D 组待定 |

**B 组 · DTO 无批次 Id，需后端补字段（照 NCR 模式：查询后按 BatchNo 反查 `ProductionBatch` 回填）**

| 页面 | 路由 | 列标签 | 备注 |
|------|------|--------|------|
| 委外回收 | `/outsource-recoveries` | 生产编号 | `OutsourceRecoveryDto` 无批次 Id，可经 `SectionOutsourceId` → `SectionOutsource.ProductionBatchId` 派生 |
| 用料计划 · Tab4 库存使用计划 | `/work-order-material-plan` | 批次号 | `InventoryPlanDto` 只有 `InventoryBatchNo`/`BatchNo`，无 Id |
| 用料计划 · Tab5 库料改制计划 | `/work-order-material-plan` | 批次号 | 同上 |
| 入库历史 | `/inbound-history` | 生产批号 | `InventoryBatchDto.ProductionBatchNo` 为 string，无 Id |
| 库存查询 | `/warehouse-inventory` | 生产批号 | 同上 |
| 订单成品(实时库存) | `/orders/pending-delivery` | 生产批号 | `PendingDeliveryItemDto.ProductionBatchNo` 为 string，无 Id |

**C 组 · 8 个试验页（列标签「生产编号」，字段名 `BatchNo`）**

扩口 `/flaring-tests`、压扁 `/flattening-tests`、晶粒度 `/grain-size-tests`、硬度 `/hardness-tests`、
晶间腐蚀 `/intergranular-corrosion-tests`、金相 `/metallographic-tests`、点腐蚀 `/pitting-corrosion-tests`、室温拉伸 `/tensile-tests`。

> 用户 2026-09-10 明确：**此类检验以「生产编号」为基准，不是炉号** —— 列标签「生产编号」是**正确的**。
> **✅ 字段名已按用户要求正名（V91）**：`FurnaceNo` → **`BatchNo`**（实体/DTO/Service/前端/单测/DbContext/DataExchange 全链路 + 迁移 `20260910103357`）。`ChemicalAnalysis` 的 `FurnaceNo` 为**真炉号**，未改。
> 本组**仍属"应链接但未链接"**（用户拍板仅登记台账），后续若要加链接，需后端按 `BatchNo` 值反查批次回填 Id（同 NCR 做法）。

**D 组 · 语义待定（先确认再动）**

| 项 | 说明 |
|---|---|
| 仓库批次 vs 生产批次 | 出库历史 `/outbound-history`、待出库作业页的「批次号/仓库批次」为**仓库批次**语义，**不应**跳生产批次详情；入库历史/库存查询的「生产批号」则确属生产批次 |
| 成检计件试算行 | 试算表是样本记录，点击跳批次详情的收益待评估（可能只想看计件命中结果） |

### 7.4 有意不加链接

| 位置 | 原因 |
|------|------|
| 不合格报告「待处理批次」卡片 | 该处「生产编号」已是「**创建 NCR**」动作入口（`CreateFromPending`），再加批次详情链接会动作冲突 |
| 各类 `*Create.razor` 表单内的「生产编号」 | 是**输入控件**（`IsRequired = true`），非展示列；**点击字段本身不开链接**（NCR 建单页改在字段右侧另用图标按钮作入口，见图 7.2） |
| 出库历史「退货-原仓库批」、仓库出库作业页 | 仓库批次语义，见 7.3 D 组 |

---

> 使用方式：询问关于页面结构、上下文归属、列表页检查范围等问题时，可引用此文档作为参考基础。
