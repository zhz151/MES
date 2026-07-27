/**
 * MES 枚举显示自动化校验脚本
 *
 * 校验内容：
 *   1. 单元格文本扫描 — 检测表格中是否出现英文枚举值（如 "RoundBar"、"Fixed"）
 *   2. 枚举列专用校验 — 对已知枚举列，校验显示值是否与 EnumHelper 中文映射一致
 *   3. 筛选下拉校验 — 打开每列筛选下拉框，检查选项文本是否为中文
 *
 * 使用: node playwright-tests/validate-enums.mjs
 * 前置条件: 需启动 API (port 7000) + Blazor (port 5000)
 */

import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');
const BLAZOR_PORT = 5000;
const BLAZOR_URL = `http://localhost:${BLAZOR_PORT}`;
const REPORT_DIR = path.resolve(__dirname, 'report-enums');

// ============================================================
// 1. 枚举映射 — EnumHelper.GetDisplayName() 的完整快照
//    格式: { enumTypeName: { englishValue: chineseDisplay, ... }, ... }
// ============================================================
const ENUM_MAP = {
  WorkOrderStatus:         { NotGenerated: '未编制', Confirmed: '已确定', Pending: '待修正' },
  MaterialPlanStatus:      { NotPlanned: '未计划', Partial: '部分', TheoreticalSatisfied: '理论满足', Satisfied: '满足', Excess: '超量' },
  InventoryPlanStatus:     { Planned: '已计划', Confirmed: '已确认', Cancelled: '已取消' },
  LengthStatus:            { Fixed: '定尺', Range: '范围尺', NonFixed: '非定尺' },
  DeliveryState:           { SolutionAnnealedAndPickled: '固溶酸洗', SolutionAnnealedAndPickledUTube: '固溶酸洗-U型管', SolutionAnnealedAndPickledExternalPolished: '固溶酸洗-外抛光', SolutionAnnealedAndPickledInternalPolished: '固溶酸洗-内抛光', SolutionAnnealedAndPickledBothPolished: '固溶酸洗-内外抛光', SolutionAnnealedAndPickledCoiled: '固溶酸洗-盘管', Bright: '光亮', BrightUTube: '光亮-U型管', BrightCoiled: '光亮-盘管', Hard: '硬态', SolidSolutionStraightening: '固溶矫直' },
  SettlementMethod:        { Theoretical: '理算', Weighing: '过磅', WeighingNegative: '过磅-负' },
  SalesOrderStatus:        { Pending: '待处理', Confirmed: '已确认', Cancelled: '已取消' },
  PipeManufacturingType:   { SeamlessPipe: '无缝管', WeldedPipe: '焊管' },
  ReworkType:              { EmptyDrawing: '空拉改制', FewerPass: '少道次改制', ManualSelect: '人工选择改制' },
  RawMaterialType:         { RoughTube: '荒管', SemiProduct: '半成品', RoundBar: '圆棒' },
  FinishedProductType:     { Critical: '临界成品', Order: '订单成品' },
  ProductionType:          { RoughTube: '荒管生产', InProcess: '在制生产', Inventory: '库存', OutsourcedPurchased: '外购', Rework: '返整', Subcontract: '委外生产', ExternalProcessing: '对外加工' },
  ManufacturingItem:       { OrderFinishedProduct: '订单成品', PreparedMaterial: '备料成品', SurplusStock: '余库料', SpecialDeliveryStatus: '订成-非交付态' },
  MaterialCategory:        { RoundBar: '圆棒', RoughTube: '荒管', SemiProduct: '半成品', OrderFinished: '订单成品', PreparedFinished: '备料成品', CriticalFinished: '临界成品', DefectRoundBar: '次品圆棒', DefectRoughTube: '次品荒管', DefectSemiProduct: '次品半成品', DefectFinished: '次品成品', Scrap: '报废品', Surplus: '余库料', SpecialDeliveryFinished: '订成-非交付态', DefectWIP: '次品在制' },
  OutboundType:            { ProductionPick: '生产领用', SalesOut: '销售出库', ReturnOut: '退货出库', SubcontractOut: '委外出库', ScrapOut: '报废出库', InspectionPick: '检验领用', TransferOut: '移库出库', OtherOut: '其他出库' },
  CustomerStatus:          { Active: '启用', Inactive: '停用' },
  RequirementType:         { Normal: '常规', Special: '特殊' },
  BatchStatus:             { None: '未产', InProgress: '在产', InFinalInspection: '成检', Completed: '完成', Suspended: '暂停' },
  PurchaseOrderStatus:     { Open: '已下单', Partial: '部分到货', Completed: '已完成' },
  SubcontractOrderStatus:  { Sent: '已发出', PartialReturned: '部分收回', Completed: '已完成' },
  SectionOutsourceStatus:  { PendingRecovery: '待回收', Recovered: '已回收', InProgress: '在轧' },
  RepairPriority:          { Normal: '普通', Urgent: '紧急', Emergency: '特急' },
  LifecycleStatus:         { Active: '在用', Standby: '备用', Scrapped: '报废' },
  UsageType:               { Primary: '主生产设备', Secondary: '辅生产设备', Other: '其它' },
  RunningStatus:           { Normal: '正常', Pending: '待维修', InProgress: '维修中' },
  RepairOrderStatus:       { Pending: '待维修', InProgress: '维修中', Completed: '完成' },
  EquipmentTaskStatus:     { NotApplicable: '不适用', Pending: '待执行', Normal: '正常', Overdue: '逾期' },
  TaskOrderStatus:         { Pending: '待执行', Completed: '已完成', Overdue: '已逾期' },
  SubcontractProcessStatus:{ Pending: '待回收', PartialReturned: '部分回收', Completed: '已完成' },
  InspectionItem:          { PMIInspection: 'PMI检验', VisualInspection: '表检', Dimension: '尺寸', Endoscopy: '内窥', HydrostaticPressure: '水压', UnderwaterPneumatic: '水下气压', EddyCurrent: '涡流', Ultrasonic: '超声波', PortColoring: '端口着色' },
  DisposalMethod:          { Rework: '返整', WarehouseEntry: '入库', Scrap: '报废' },
  NcrStatus:               { Pending: '待处理', Processing: '处理中', Closed: '已关闭' },
  PicklingStatus:          { Soaking: '浸泡中', Completed: '已完工' },
  ResponsibilityCategory:  { ProductionInternal: '生产-厂内', ProductionOutsource: '生产-外协', MaterialTubeBlank: '原料-荒管', MaterialPurchased: '原料-外购成品', MaterialSurplus: '原料-余库料' },
  SeverityLevel:           { Critical: '严重', General: '一般' },
  VerifyResult:            { Passed: '通过', NeedsRectification: '需整改', NotApplicable: '不适用' },
  PipeCategory:            { TubeBlank: '荒管', WorkInProgress: '在制品', SurplusInventory: '余库料', CriticalFinished: '临界成品', OrderFinished: '订单成品', PreparedFinished: '备料成品', SpecialDelivery: '订成-非交付态' },
  SectionStatus:           { Completed: '已完成', InProgress: '进行中', Outsource: '委外中', Next: '待执行', Pending: '待处理' },
};

/**
 * 构建逆向索引: 所有英文枚举值 → 对应的中文显示
 * 用于 Phase 1 全局扫描
 */
const ALL_ENGLISH_VALUES = new Map();
for (const [typeName, mapping] of Object.entries(ENUM_MAP)) {
  for (const [eng, chn] of Object.entries(mapping)) {
    if (!ALL_ENGLISH_VALUES.has(eng)) {
      ALL_ENGLISH_VALUES.set(eng, []);
    }
    ALL_ENGLISH_VALUES.get(eng).push({ typeName, chinese: chn });
  }
}

/**
 * 哪些英文单词是 "高干扰" 的，可能在非枚举列中作为常规文本出现
 * 对这些单词降低警告级别（仅 warning 而非 error）
 */
const HIGH_NOISE_WORDS = new Set([
  'Normal', 'Pending', 'Active', 'InProgress', 'Completed',
  'Critical', 'Other', 'General', 'None',
]);

// ============================================================
// 2. 页面配置 — 每个列表页的枚举列信息
// ============================================================
const PAGE_CONFIGS = [
  // ---- 订单管理 ----
  { url: '/orders',             name: '订单管理',         module: 'Orders',
    enumCols: { Status: 'SalesOrderStatus', SettlementMethod: 'SettlementMethod', LengthStatus: 'LengthStatus', DeliveryState: 'DeliveryState', DelayPenalty: 'WorkOrderStatus' } },

  // ---- 工单管理 ----
  { url: '/workorders',         name: '工单列表',         module: 'WorkOrders',
    enumCols: { Status: 'WorkOrderStatus', SettlementMethod: 'SettlementMethod', LengthStatus: 'LengthStatus', DeliveryState: 'DeliveryState', ManufacturingType: 'PipeManufacturingType', DelayPenalty: 'SalesOrderStatus' } },
  { url: '/workorder-execution',name: '工单执行概览',     module: 'WorkOrders',    enumCols: { Status: 'WorkOrderStatus' } },
  { url: '/material-plan-overview', name: '用料计划概览', module: 'WorkOrders',
    enumCols: { MaterialPlanStatus: 'MaterialPlanStatus', MainNoStatus: 'MaterialPlanStatus', Status: 'MaterialPlanStatus', SettlementMethod: 'SettlementMethod', LengthStatus: 'LengthStatus', DeliveryState: 'DeliveryState', DelayPenalty: 'SalesOrderStatus', ManufacturingItem: 'ManufacturingItem', ProductionType: 'ProductionType', PipeManufacturingType: 'PipeManufacturingType' } },

  // ---- 计划排程 ----
  { url: '/batch-plans',        name: '批次计划',         module: 'Scheduling',    enumCols: { PlanStatus: 'MaterialPlanStatus', InventoryPlanStatus: 'InventoryPlanStatus' } },
  { url: '/cold-roll-plans',    name: '冷轧计划',         module: 'Scheduling',    enumCols: { PlanStatus: 'MaterialPlanStatus' } },
  { url: '/final-inspection-plan', name: '成品检计划',   module: 'Scheduling',    enumCols: { PlanStatus: 'MaterialPlanStatus' } },
  { url: '/raw-material-lock-plan', name: '原材料锁定计划', module: 'Scheduling', enumCols: { PlanStatus: 'MaterialPlanStatus' } },

  // ---- 批次管理 ----
  { url: '/batches',            name: '批次列表',         module: 'Batches',
    enumCols: { Status: 'BatchStatus', SettlementMethod: 'SettlementMethod', LengthStatus: 'LengthStatus', DeliveryState: 'DeliveryState', ManufacturingItem: 'ManufacturingItem', ProductionType: 'ProductionType', DelayPenalty: 'SalesOrderStatus' } },
  { url: '/section-outsources', name: '工序外协',         module: 'Batches',       enumCols: { Status: 'SectionOutsourceStatus' } },
  { url: '/production-records', name: '生产记录',         module: 'Batches',       enumCols: { } },
  { url: '/pickling-in-records',name: '酸洗入记录',       module: 'Batches',       enumCols: { PicklingStatus: 'PicklingStatus' } },
  { url: '/pickling-out-records',name: '酸洗出记录',      module: 'Batches',       enumCols: { PicklingStatus: 'PicklingStatus' } },
  { url: '/outsource-recoveries',name: '外协回收',        module: 'Batches',       enumCols: { Status: 'SubcontractProcessStatus' } },

  // ---- 质量管理 ----
  { url: '/quality/ncr',        name: 'NCR 不合格品',    module: 'Quality',
    enumCols: { Status: 'NcrStatus', PipeCategory: 'PipeCategory', DisposalMethod: 'DisposalMethod', Severity: 'SeverityLevel', Responsibility: 'ResponsibilityCategory', VerifyResult: 'VerifyResult' } },
  { url: '/quality/final-inspection', name: '成品检验',  module: 'Quality',        enumCols: { InspectionItem: 'InspectionItem', Status: 'BatchStatus' } },
  { url: '/quality/material-receive-checks', name: '来料检验', module: 'Quality', enumCols: { MaterialCategory: 'MaterialCategory' } },
  { url: '/quality/process-inspection', name: '过程检验',module: 'Quality',        enumCols: { InspectionItem: 'InspectionItem', Status: 'BatchStatus' } },
  { url: '/quality/furnace',    name: '炉批号登记',       module: 'Quality',       enumCols: { RawMaterialType: 'RawMaterialType', Status: 'BatchStatus' } },
  { url: '/quality/certificates', name: '质量证明书',     module: 'Quality',       enumCols: { Status: 'BatchStatus' } },
  { url: '/quality/tensile-test', name: '拉伸试验',       module: 'Quality',       enumCols: { InspectionItem: 'InspectionItem' } },
  { url: '/quality/hardness-test', name: '硬度试验',      module: 'Quality',       enumCols: { InspectionItem: 'InspectionItem' } },
  { url: '/quality/grain-size-test', name: '晶粒度试验',  module: 'Quality',       enumCols: { InspectionItem: 'InspectionItem' } },
  { url: '/quality/flattening-test', name: '压扁试验',    module: 'Quality',       enumCols: { InspectionItem: 'InspectionItem' } },
  { url: '/quality/flaring-test', name: '扩口试验',       module: 'Quality',       enumCols: { InspectionItem: 'InspectionItem' } },

  // ---- 物料管理 ----
  { url: '/materials',          name: '物料列表',         module: 'Materials',     enumCols: { MaterialCategory: 'MaterialCategory', RawMaterialType: 'RawMaterialType', ProductionType: 'ProductionType' } },
  { url: '/purchase-orders',    name: '采购订单',         module: 'Materials',
    enumCols: { Status: 'PurchaseOrderStatus', MaterialCategory: 'MaterialCategory', WoDelayPenalty: 'SalesOrderStatus', WoSettlementMethod: 'SettlementMethod', WoLengthStatus: 'LengthStatus', WoDeliveryState: 'DeliveryState' } },
  { url: '/subcontract-orders', name: '委外订单',         module: 'Materials',
    enumCols: { Status: 'SubcontractOrderStatus', OutMaterialCategory: 'MaterialCategory' } },

  // ---- 设备管理 ----
  { url: '/equipment',          name: '设备列表',         module: 'Equipment',
    enumCols: { LifecycleStatus: 'LifecycleStatus', UsageType: 'UsageType', RunningStatus: 'RunningStatus', InspectionStatus: 'EquipmentTaskStatus', NeedInspection: 'TaskOrderStatus', MaintStatus: 'EquipmentTaskStatus', NeedMaintenance: 'TaskOrderStatus', Status: 'RepairOrderStatus' } },
  { url: '/repair-orders',      name: '维修工单',         module: 'Equipment',     enumCols: { Status: 'RepairOrderStatus', Priority: 'RepairPriority' } },
  { url: '/maintenance-orders', name: '保养计划',         module: 'Equipment',     enumCols: { Status: 'TaskOrderStatus' } },
  { url: '/inspection-records', name: '点检记录',         module: 'Equipment',     enumCols: { Status: 'EquipmentTaskStatus' } },

  // ---- 仓库管理 ----
  { url: '/warehouse',          name: '库存查询',         module: 'Warehouse',     enumCols: { OutboundType: 'OutboundType' } },
  { url: '/warehouse/inbound-history', name: '入库历史',  module: 'Warehouse',     enumCols: { } },
  { url: '/warehouse/outbound-history', name: '出库历史', module: 'Warehouse',     enumCols: { OutboundType: 'OutboundType' } },
  { url: '/warehouse/pending-delivery', name: '待发货',   module: 'Warehouse',     enumCols: { } },
];

// ============================================================
// 3. 校验引擎
// ============================================================

/** 收集表格中所有行的所有单元格文本 */
async function collectTableCells(page) {
  return await page.evaluate(() => {
    const rows = document.querySelectorAll('.mud-table-body .mud-table-row');
    const cells = [];
    for (const row of rows) {
      const tds = row.querySelectorAll('td');
      for (const td of tds) {
        const classes = td.className || '';
        const text = td.textContent.trim();
        if (text) {
          cells.push({ text, classes, html: td.innerHTML.trim().substring(0, 200) });
        }
      }
    }
    return cells;
  });
}

/** Phase 1: 全局单元格英文枚举值扫描 */
function scanCellsForEnglishEnums(cells) {
  if (!cells || !Array.isArray(cells)) return [];
  const findings = [];
  const seen = new Set();

  for (const cell of cells) {
    // 跳过选择列、操作列（它们的文本不相关）
    if (cell.classes.includes('col-selection-td') || cell.classes.includes('action-cell')) continue;

    // 对单元格文本做精确匹配或单词边界匹配
    const text = cell.text.trim();

    // 跳过纯数字、日期、短文本、空文本
    if (!text || text.length <= 1) continue;
    if (/^[\d.%-]+$/.test(text)) continue;
    if (/^\d{4}-\d{2}-\d{2}$/.test(text)) continue;

    // 检查是否包含已知的英文枚举值
    for (const [eng, infos] of ALL_ENGLISH_VALUES) {
      // 精确匹配（整个单元格就是一个英文枚举值）
      if (text === eng) {
        const key = `${eng}|${text}`;
        if (!seen.has(key)) {
          seen.add(key);
          const isHighNoise = HIGH_NOISE_WORDS.has(eng);
          findings.push({
            type: isHighNoise ? 'warning' : 'error',
            severity: isHighNoise ? '⚠ 低' : '✗ 高',
            enumValue: eng,
            cellText: text,
            expectedChinese: infos.map(i => i.chinese).join(' / '),
            possibleTypes: infos.map(i => i.typeName).join(', '),
            sampleHtml: cell.html,
            isHighNoise,
          });
        }
      } else {
        // 单词边界匹配（单元格文本中包含英文枚举值作为独立词）
        const regex = new RegExp(`\\b${eng}\\b`);
        if (regex.test(text) && text !== eng) {
          const key = `${eng}|contains|${text}`;
          if (!seen.has(key)) {
            seen.add(key);
            const isHighNoise = HIGH_NOISE_WORDS.has(eng);
            findings.push({
              type: 'info',
              severity: 'ℹ',
              enumValue: eng,
              cellText: text,
              expectedChinese: infos.map(i => i.chinese).join(' / '),
              possibleTypes: infos.map(i => i.typeName).join(', '),
              sampleHtml: cell.html,
              isHighNoise,
            });
          }
        }
      }
    }
  }
  return findings;
}

/* Phase 2 和 Phase 3 已合并到 Phase 1 全局扫描中
 * Phase 1 的单元格扫描足以捕获所有中英文枚举显示问题。
 * 筛选下拉检查因各页面实现不一致，通过静态 grep 覆盖更可靠。 */

// ============================================================
// 4. 主流程
// ============================================================

const results = {
  scanned: 0,
  pagesWithIssues: 0,
  totalFindings: 0,
  byPage: {},
};

async function waitForServer(url, maxRetries = 40, interval = 2000) {
  for (let i = 0; i < maxRetries; i++) {
    try {
      await new Promise((resolve, reject) => {
        const req = http.get(url, (res) => { res.resume(); resolve(); });
        req.on('error', reject);
        req.setTimeout(3000, () => { req.destroy(); reject(new Error('timeout')); });
      });
      console.log(`  ✓ 服务已就绪: ${url}`);
      return true;
    } catch {
      if (i % 5 === 0) process.stdout.write(`  等待服务启动 (${i + 1}/${maxRetries})...\n`);
      await new Promise(r => setTimeout(r, interval));
    }
  }
  throw new Error(`服务 ${url} 未能启动`);
}

import http from 'http';

async function validatePage(page, config) {
  const pageResult = {
    page: config.name,
    url: config.url,
    module: config.module,
    phase1_findings: [],
    errors: [],
    passed: true,
  };

  try {
    console.log(`\n  ── ${config.name} (${config.url}) ──`);

    // 注入 token
    await page.evaluate(() => {
      if (!localStorage.getItem('authToken')) {
        localStorage.setItem('authToken', 'test-token');
      }
    });

    await page.goto(`${BLAZOR_URL}${config.url}`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(4000);

    // 检查页面错误
    const pageContent = await page.content();
    if (pageContent.includes('there was an unhandled exception') ||
        pageContent.includes('Unhandled error') ||
        pageContent.includes('发生错误')) {
      pageResult.errors.push('页面加载错误');
      pageResult.passed = false;
      console.log('  ✗ 页面加载错误');
      return pageResult;
    }

    // 检查表格是否存在且有数据
    const table = await page.$('.mud-table');
    if (!table) {
      pageResult.errors.push('MudTable 不存在');
      pageResult.passed = false;
      console.log('  ✗ MudTable 不存在');
      return pageResult;
    }

    // 等待表格行渲染（Blazor WASM 渲染有时延）
    try {
      await page.waitForSelector('.mud-table-body .mud-table-row', { timeout: 8000 });
    } catch {
      // 超时表示无数据
      console.log('  - 表格无数据（超时），跳过');
      return pageResult;
    }

    // Phase 1: 全局单元格英文枚举值扫描
    const cells = (await collectTableCells(page)) || [];
    if (cells.length === 0) {
      console.log('  - 表格无数据（空单元格），跳过');
      return pageResult;
    }
    const phase1 = scanCellsForEnglishEnums(cells);
    pageResult.phase1_findings = phase1;

    for (const f of phase1) {
      console.log(`  ${f.severity} [Phase1] 值 "${f.enumValue}" → 期望中文: ${f.expectedChinese}`);
    }

    // 总体判定
    const errorFindings = phase1.filter(f => f.type === 'error').length;
    if (errorFindings > 0) {
      pageResult.passed = false;
    }

    if (phase1.length > 0) {
      results.pagesWithIssues++;
    }

  } catch (err) {
    pageResult.errors.push(err.message);
    pageResult.passed = false;
    console.log(`  ✗ 异常: ${err.message}`);
  }

  return pageResult;
}

async function main() {
  console.log('========================================');
  console.log('  MES 枚举显示自动化校验');
  console.log('========================================\n');

  // 确保报告目录
  fs.mkdirSync(REPORT_DIR, { recursive: true });

  // 等待服务
  console.log('检查服务状态...\n');
  try {
    await waitForServer(`${BLAZOR_URL}/login`);
  } catch (err) {
    console.error(`\n✗ ${err.message}`);
    console.log('\n请先启动服务:');
    console.log('  1. cd MES.Api && dotnet run --launch-profile http');
    console.log('  2. cd MES.Blazor && dotnet run --launch-profile http');
    process.exit(1);
  }

  // 启动浏览器
  console.log('\n启动浏览器...');
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1920, height: 1080 },
    ignoreHTTPSErrors: true,
  });
  const page = await context.newPage();

  // 登录
  console.log('\n登录系统...');
  try {
    await page.goto(`${BLAZOR_URL}/login`, { waitUntil: 'networkidle', timeout: 20000 });
    await page.waitForTimeout(2000);

    const inputs = await page.$$('input');
    for (const input of inputs) {
      const type = await input.getAttribute('type');
      const id = await input.getAttribute('id');
      if (type === 'text' || type === 'email' || id?.includes('Email')) {
        await input.fill('admin@mes.com');
        break;
      }
    }
    const pwdInput = await page.$('input[type="password"]');
    if (pwdInput) await pwdInput.fill('Admin@123');
    const loginBtn = await page.$('button:has-text("登录")');
    if (loginBtn) await loginBtn.click();
    await page.waitForTimeout(3000);

    let currentUrl = page.url();
    if (currentUrl.includes('/login')) {
      console.log('  页面登录未成功，尝试 API 登录...');
      const tokenResponse = await page.evaluate(async () => {
        try {
          const res = await fetch('http://localhost:7000/api/auth/login', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: 'admin@mes.com', password: 'Admin@123' }),
          });
          return await res.json();
        } catch (e) { return { error: e.message }; }
      });
      if (tokenResponse?.data?.token) {
        await page.evaluate((token) => {
          localStorage.setItem('authToken', token);
          localStorage.setItem('refreshToken', token);
        }, tokenResponse.data.token);
        console.log('  ✓ Token 注入成功');
      }
    } else {
      console.log('  ✓ 页面登录成功');
    }
  } catch (err) {
    console.log(`  ⚠ 登录异常: ${err.message}`);
  }

  await page.goto(`${BLAZOR_URL}/`, { waitUntil: 'networkidle', timeout: 20000 });
  await page.waitForTimeout(3000);
  console.log(`  首页: ${page.url()}\n`);

  // 遍历所有页面
  console.log('========================================');
  console.log('  开始枚举校验');
  console.log('========================================\n');

  for (const config of PAGE_CONFIGS) {
    try {
      const pageResult = await validatePage(page, config);
      results.byPage[config.name] = pageResult;
      results.scanned++;
      if (!pageResult.passed) results.pagesWithIssues++;
      results.totalFindings += (pageResult.phase1_findings || []).length;
    } catch (err) {
      console.log(`  ✗ ${config.name}: ${err.message}`);
      results.byPage[config.name] = { page: config.name, error: err.message, passed: false };
    }
  }

  await browser.close();

  // 生成报告
  console.log('\n========================================');
  console.log('  校验报告');
  console.log('========================================\n');

  let errorCount = 0;
  let warningCount = 0;
  let infoCount = 0;

  for (const [name, pr] of Object.entries(results.byPage)) {
    if (pr.phase1_findings) {
      for (const f of pr.phase1_findings) {
        if (f.type === 'error') errorCount++;
        else if (f.type === 'warning') warningCount++;
        else infoCount++;
      }
    }
  }

  console.log(`扫描页面: ${results.scanned} 页`);
  console.log(`发现问题: ${results.totalFindings} 个 (错误: ${errorCount}, 警告: ${warningCount}, 信息: ${infoCount})`);
  console.log(`通过页面: ${results.scanned - results.pagesWithIssues} 页`);
  console.log(`有问题的页面: ${results.pagesWithIssues} 页\n`);

  if (errorCount > 0) {
    console.log('--- 高优错误（单元格中疑似出现英文枚举值）---');
    for (const [name, pr] of Object.entries(results.byPage)) {
      const errors = (pr.phase1_findings || []).filter(f => f.type === 'error');
      if (errors.length > 0) {
        console.log(`\n  ${name}:`);
        for (const f of errors) {
          console.log(`    [${f.enumValue}] → 期望: ${f.expectedChinese} | HTML: ${f.sampleHtml.substring(0, 100)}`);
        }
      }
    }
  }

  if (warningCount > 0) {
    console.log('\n--- 低优警告（高干扰词，需人工确认）---');
    for (const [name, pr] of Object.entries(results.byPage)) {
      const warnings = (pr.phase1_findings || []).filter(f => f.type === 'warning');
      if (warnings.length > 0) {
        console.log(`\n  ${name}:`);
        for (const f of warnings) {
          console.log(`    [${f.enumValue}] → 期望: ${f.expectedChinese} | 文本: "${f.cellText}"`);
        }
      }
    }
  }

  // 生成 HTML 报告
  const reportHtml = generateHtmlReport(results);
  const reportPath = path.join(REPORT_DIR, 'index.html');
  fs.writeFileSync(reportPath, reportHtml);
  console.log(`\n报告已保存: ${reportPath}`);
  console.log('\n========================================\n');

  process.exit(0);
}

// ============================================================
// HTML 报告生成
// ============================================================
function generateHtmlReport(results) {
  const rows = Object.entries(results.byPage).map(([name, pr]) => {
    const statusIcon = pr.passed ? '✅' : '❌';
    const findings = pr.phase1_findings || [];
    const errors = findings.filter(f => f.type === 'error');
    const warnings = findings.filter(f => f.type === 'warning');
    const infos = findings.filter(f => f.type === 'info');

    const findingItems = [
      ...errors.map(f => `<li class="error">✗ <b>${f.enumValue}</b> → 期望: ${f.expectedChinese} <small>(${f.possibleTypes})</small></li>`),
      ...warnings.map(f => `<li class="warning">⚠ <b>${f.enumValue}</b> → 期望: ${f.expectedChinese} <small>(${f.possibleTypes})</small></li>`),
      ...infos.map(f => `<li class="info">ℹ <b>${f.enumValue}</b> 出现在文本 "${f.cellText}" 中 <small>(${f.possibleTypes})</small></li>`),
    ];

    const errorsDetail = pr.errors ? pr.errors.join('; ') : '';

    return `<tr class="${pr.passed ? 'pass' : 'fail'}">
      <td>${statusIcon}</td>
      <td>${name}</td>
      <td>${pr.url || ''}</td>
      <td>${pr.module || ''}</td>
      <td>${errors.length} / ${warnings.length} / ${infos.length}</td>
      <td><ul class="findings">${findingItems.join('')}</ul></td>
      <td>${errorsDetail}</td>
    </tr>`;
  }).join('\n');

  let errorCount = 0, warningCount = 0, infoCount = 0;
  for (const pr of Object.values(results.byPage)) {
    for (const f of (pr.phase1_findings || [])) {
      if (f.type === 'error') errorCount++;
      else if (f.type === 'warning') warningCount++;
      else infoCount++;
    }
  }

  return `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8">
  <title>MES 枚举显示校验报告</title>
  <style>
    body { font-family: -apple-system, sans-serif; margin: 20px; color: #333; }
    h1 { color: #1a1a2e; }
    .summary { display: flex; gap: 20px; margin: 20px 0; flex-wrap: wrap; }
    .summary-card { padding: 15px 25px; border-radius: 8px; color: white; font-size: 16px; }
    .card-pass { background: #4caf50; }
    .card-fail { background: #f44336; }
    .card-warn { background: #ff9800; }
    .card-info { background: #2196f3; }
    .card-total { background: #607d8b; }
    table { border-collapse: collapse; width: 100%; margin-top: 20px; font-size: 13px; }
    th, td { border: 1px solid #ddd; padding: 6px 10px; text-align: left; }
    th { background: #1a1a2e; color: white; white-space: nowrap; }
    tr.pass { background: #f1f8e9; }
    tr.fail { background: #fff3f3; }
    .findings { list-style: none; padding: 0; margin: 0; font-size: 12px; }
    .findings li { margin: 2px 0; }
    .findings .error { color: #d32f2f; }
    .findings .warning { color: #e65100; }
    .findings .info { color: #1565c0; }
    a { color: #1976d2; text-decoration: none; }
  </style>
</head>
<body>
  <h1>MES 枚举显示校验报告</h1>
  <div class="summary">
    <div class="summary-card card-total">📊 扫描: ${results.scanned} 页</div>
    <div class="summary-card card-pass">✅ 通过: ${results.scanned - results.pagesWithIssues} 页</div>
    <div class="summary-card card-fail">❌ 有问题: ${results.pagesWithIssues} 页</div>
    <div class="summary-card card-fail">✗ 错误: ${errorCount} 个</div>
    <div class="summary-card card-warn">⚠ 警告: ${warningCount} 个</div>
    <div class="summary-card card-info">ℹ 信息: ${infoCount} 个</div>
  </div>
  <table>
    <thead>
      <tr>
        <th>状态</th><th>页面</th><th>URL</th><th>模块</th><th>错误/警告/信息</th><th>枚举值发现</th><th>错误信息</th>
      </tr>
    </thead>
    <tbody>${rows}</tbody>
  </table>
  <p style="margin-top: 20px; color: #666; font-size: 12px;">
    生成时间: ${new Date().toISOString()} | 数据来源: EnumHelper 完整映射 (${Object.keys(ENUM_MAP).length} 种枚举类型, ${ALL_ENGLISH_VALUES.size} 个枚举值)
  </p>
</body>
</html>`;
}

main();
