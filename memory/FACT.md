# MES项目 - 项目存档

## 项目概要
MES (制造执行系统) 工单管理子系统，.NET 8 Blazor WASM + WebAPI。

## 已实现功能
### 工单管理
- 工单列表（服务端分页、搜索、导航）
- 工单详情页（14字段紧凑布局，同类工单上下导航，用料计划菜单入口）
- 用料计划状态总览（MaterialPlanStatus: 未计划/部分/理论满足/满足/超量）

### 用料计划（4种类型）
1. **原料采购计划** (PurchaseSemiPlan) - `/workorders/{id}/material-plan`
   - 含用料测算（调整壁厚、成材率、投料倍率、正品率、工艺路线）
   - 14字段工单信息卡片
2. **成品采购计划** (PurchaseFinishedPlan) - `/workorders/{id}/finish-plan`
   - 极简设计，不涉及测算
   - 14字段工单信息卡片
3. **库存使用计划** (InventoryPlan, ReworkType=null) - `/workorders/{id}/inventory-plan`
   - 从可用库存列表中选取进行计划
   - 编辑字段背景色区分（橙色rgba高亮）
   - 可用库存数据从现有库存表查询
4. **库料改制计划** (InventoryPlan, ReworkType!=null) - `/workorders/{id}/rework-plan`
   - 三种改制类型：空拉改制/少道次改制/人工选择改制
   - 改制工艺路线（ProcessPlan JSON数组）
   - 三种改制类型各自不同的库存筛选规则
   - 与库存使用计划共用 InventoryPlan 表，通过 ReworkType 区分

### 满足率计算
- **工单级**：4类计划各自计算满足率后相加（Sum），上限999%，再用 CalculateOverallStatus 映射为状态
- **主号级**：同一订单+主号下所有工单的4类计划合并重算，使用1.02/1.05系数
- **订单级**：主号级聚合，全部满足=满足，存在部分/未计划=部分
- 涉及生产（原料采购/库料改制）：乘以1.02（支数）/1.05（重量）系数
- 不涉及生产（成品采购/库存使用）：实际值，不乘系数

### 页面规范
- 列表页：MudTable表格 + MudPaper卡片布局
- 操作列：仅保留"打印"+"取消"按钮（已移除"编辑"+"刷新状态"）
- 创建页："返回上一级" → 对应列表页
- 列表页："返回用料计划总览" → /material-plan-overview
- 工单详情页：两个返回按钮（"工单首页"→/workorders + "用料计划总览"→/material-plan-overview）
- 删除确认：ConfirmDialog（参数：Title/ContentText/ConfirmText/Color）

### 14字段工单基本信息（所有计划页面统一）
1. 工单号 | 2. 订单号/主号/次号 | 3. 物料名称 | 4. 结算方式
5. 交货状态 | 6. 延期罚款 | 7. 交货日期 | 8. 工厂牌号
9. 规格 | 10. 外径公差 | 11. 壁厚公差 | 12. 长度状态
13. 数量/重量 | 14. 理论单支重

### 理论单支重计算
公式（在DtoMapper中计算）：
- 外径实际值 = 公称外径 - 0.5×外径负偏差 + 0.5×外径正偏差
- 壁厚实际值 = 公称壁厚 - 0.5×壁厚负偏差 + 0.5×壁厚正偏差
- 每米重量 = (外径实际值 - 壁厚实际值) × 壁厚实际值 × 0.02466
- 理论单支重 = 每米重量 × 最长长度 / 1000（定尺取MaxLength，非定尺取4500mm）
- 结果保留3位小数

### 用料占比显示顺序
PlanProportionText 显示顺序：库→改→原→成（固定顺序，不受类型影响）

### 打印实现
- 4种计划均使用 QuestPDF 生成正式PDF
- 打印按钮 → 后端API返回base64 → 前端JS `openPdf()` 通过 Blob URL 打开
- 打印实现类：`MaterialPlanPrintHelper.cs`（QuestPDF Fluent API）

## 技术栈
- .NET 8 / EF Core / SQL Server
- Blazor WASM + MudBlazor UI
- 三层架构：Controller-Service-Data
- 贫血实体：所有实体继承BaseEntity
- QuestPDF 2026.2.4（社区版许可）

## 项目结构
```
MES.sln
├── MES.Api/          # WebAPI控制器
├── MES.Blazor/       # 前端Blazor页面
│   ├── Pages/        # 页面组件
│   ├── Shared/       # 共享组件（ConfirmDialog等）
│   ├── Services/     # 前端服务
│   ├── wwwroot/
│   │   └── js/       # 前端JS（print.js等）
│   └── Helpers/      # DisplayHelper等
├── MES.Core/         # 核心层(DTOs/Interfaces/Enums)
├── MES.Data/         # 数据层(Entities/Migrations)
├── MES.Services/     # 业务逻辑层
│   ├── Mapping/      # DtoMapper
│   └── Printing/     # 打印模板（MaterialPlanPrintHelper.cs）
└── MES.Tests/        # 测试项目
```

## 关键文件索引
- `MES.Blazor/Pages/WorkOrderDetail.razor` - 工单详情（紧凑布局）
- `MES.Blazor/Pages/WorkOrderMaterialPlan.razor` - 原料采购计划
- `MES.Blazor/Pages/WorkOrderMaterialPlanCreate.razor` - 原料采购计划创建
- `MES.Blazor/Pages/WorkOrderFinishPlan.razor` - 成品采购计划
- `MES.Blazor/Pages/WorkOrderFinishPlanCreate.razor` - 成品采购计划创建
- `MES.Blazor/Pages/WorkOrderInventoryPlan.razor` - 库存使用计划
- `MES.Blazor/Pages/WorkOrderInventoryPlanCreate.razor` - 库存使用计划创建
- `MES.Blazor/Pages/WorkOrderReworkPlan.razor` - 库料改制计划（3个Tab）
- `MES.Blazor/Pages/WorkOrderReworkPlanCreate.razor` - 库料改制计划创建
- `MES.Blazor/Pages/MaterialPlanOverview.razor` - 用料计划总览
- `MES.Core/DTOs/InventoryPlanDto.cs` - 库存计划DTO（含ReworkType/ProcessPlan）
- `MES.Core/Enums/InventoryPlanStatus.cs` - 库存计划状态枚举
- `MES.Core/Enums/ReworkType.cs` - 改制类型枚举
- `MES.Services/Mapping/DtoMapper.cs` - DTO映射（含UnitWeight计算）
- `MES.Data/Entities/InventoryPlan.cs` - 库存计划实体（含ReworkType/ProcessPlan字段）
- `MES.Blazor/Shared/ConfirmDialog.razor` - 确认弹窗组件
- `MES.Services/MaterialPlanService.cs` - 用料计划业务逻辑（含满足率计算）
- `MES.Services/Printing/MaterialPlanPrintHelper.cs` - QuestPDF打印模板
- `MES.Api/Controllers/MaterialPlanController.cs` - 用料计划API
- `MES.Blazor/wwwroot/js/print.js` - PDF打印前端JS（Blob URL方案）

## 构建状态
- 0 errors, 2 warnings (CS0414 未使用字段，不影响功能)

## 设计文档位置（docs/）
工单上下文设计文档已拆分到三个文件中：
- `docs/数据库设计/工单上下文数据库设计.md` — 5张实体的完整表定义
- `docs/模块设计/工单模块详细设计.md` — 工单流程 + 用料计划页面设计 + 满足率规则
- `docs/接口设计/工单上下文接口设计.md` — 枚举定义 + DTO + API端点（含17个MaterialPlan端点）

## 当前分支状态
- **分支**: `fix/remaining-issues`
- **基础分支**: `main`
- **未提交**: 31 modified + 15 untracked（用料计划页面/PurchaseSemiPlan实体/InventoryPlan实体/InventorPlanDto/Printing/迁移文件等）
- **关键改动**: 仓库上下文修正（内联编辑/确认弹窗/验证汇总）、用料计划完整实现（4种类型）、打印功能（QuestPDF）、文档拆分重构
- **构建**: 0 errors, 2 warnings (CS0414)
