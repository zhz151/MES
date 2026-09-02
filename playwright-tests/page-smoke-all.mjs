/**
 * Phase 3 — TC20-23 页面烟测
 *
 * 测试内容：
 *   TC20: 页面加载 — 每个页面能渲染，无 WASM 崩溃
 *   TC21: 表格数据 — 有数据的页面表格行数 > 0 或总记录数 > 0
 *   TC22: 列显隐按钮 — ColumnDisplaySelect 或 "列显示" 按钮存在
 *   TC23: 打印按钮 — 打印相关按钮存在
 *
 * 使用: node playwright-tests/page-smoke-all.mjs
 * 前提: MES.Blazor 运行在 http://localhost:5000, MES.Api 运行在 http://localhost:7000
 */
import { chromium } from '../playwright-tests/node_modules/playwright/index.mjs';

const BASE_URL = 'http://localhost:5000';

// ============================================================
// 页面分类
// ============================================================
// LIST: 主列表页（有 MudTable），执行 TC20-TC23
// CREATE: 创建/编辑页（以表单为主），执行 TC20 仅
// PARAM: 需要路由参数（有 {id} 占位符），跳过
// The rest: 普通页，执行 TC20 仅
const PAGES = [
  // ================ 首页/登录 ================
  { name: 'Index',          path: '/',                        type: 'SPECIAL' },
  { name: 'Login',          path: '/login',                   type: 'SPECIAL', skip: true },

  // ================ Admin ================
  { name: 'Users',          path: '/admin/users',             type: 'LIST' },

  // ================ Batch ================
  { name: 'Batches',               path: '/batches',                      type: 'LIST' },
  { name: 'BatchCreate',           path: '/batches/create',               type: 'CREATE' },
  { name: 'BatchDetail',           path: '/batches/1',                    type: 'PARAM' },
  { name: 'BatchEdit',             path: '/batches/1/edit',               type: 'PARAM' },
  { name: 'PicklingInRecords',     path: '/pickling-in-records',          type: 'LIST' },
  { name: 'PicklingInRecordCreate',path: '/pickling-in-records/create',   type: 'CREATE' },
  { name: 'PicklingOutRecords',    path: '/pickling-out-records',         type: 'LIST' },
  { name: 'OutsourceRecoveries',   path: '/outsource-recoveries',         type: 'LIST' },
  { name: 'OutsourceRecoveryCreate',path: '/section-outsources/create-recovery', type: 'CREATE' },
  { name: 'SectionOutsources',     path: '/section-outsources',           type: 'LIST' },
  { name: 'SectionOutsourceCreate',path: '/section-outsources/create',    type: 'CREATE' },
  { name: 'ProductionRecords',     path: '/production-records',           type: 'LIST' },
  { name: 'ProductionRecordCreate',path: '/production-records/create',    type: 'CREATE' },
  { name: 'ProcessCardPrint',      path: '/process-card-print',           type: 'SPECIAL' },

  // ================ Configuration ================
  { name: 'ConfigParameters',            path: '/config-parameters',                 type: 'LIST' },
  { name: 'Employees',                   path: '/employees',                        type: 'LIST' },
  { name: 'Workstations',                path: '/workstations',                     type: 'LIST' },
  { name: 'DailyOutputEstimates',        path: '/daily-output-estimates',           type: 'LIST' },
  { name: 'DailyProductionCapacities',   path: '/daily-production-capacities',      type: 'LIST' },
  { name: 'StandardWorkDays',            path: '/standard-work-days',               type: 'LIST' },
  { name: 'StandardWorkDayDeliveryStates',path: '/standard-work-day-delivery-states',type: 'LIST' },
  { name: 'DictValueDefinitions',        path: '/dict-value-definitions',           type: 'LIST' },
  { name: 'EnumDisplayDefinitions',      path: '/enum-display-definitions',         type: 'LIST' },
  { name: 'ProcessDefinitions',          path: '/process-definitions',              type: 'LIST' },
  { name: 'SectionParagraphConfigSettings',path: '/section-paragraph-config-settings',type: 'LIST' },

  // ================ Equipment ================
  { name: 'Equipments',            path: '/equipment',              type: 'LIST' },
  { name: 'EquipmentCreate',       path: '/equipment/create',       type: 'CREATE' },
  { name: 'InspectionRecords',     path: '/inspection-records',     type: 'LIST' },
  { name: 'InspectionRecordCreate',path: '/inspection-records/create', type: 'CREATE' },
  { name: 'MaintenanceOrders',     path: '/maintenance-orders',     type: 'LIST' },
  { name: 'MaintenanceOrderCreate',path: '/maintenance-orders/create',type: 'CREATE' },
  { name: 'RepairOrders',          path: '/repair-orders',          type: 'LIST' },
  { name: 'RepairOrderCreate',     path: '/repair-orders/create',   type: 'CREATE' },
  { name: 'EquipmentRepair',       path: '/equipment-repair',       type: 'CREATE' },
  { name: 'EquipmentScan',         path: '/equipment-scan',         type: 'CREATE' },
  { name: 'RepairExecute',         path: '/repair-execute',         type: 'CREATE' },

  // ================ Materials ================
  // Materials / MaterialCreate 已随物料档案主档归档删除（2026-08）
  { name: 'Suppliers',            path: '/suppliers',              type: 'LIST' },
  { name: 'SupplierCreate',       path: '/suppliers/create',       type: 'CREATE' },
  { name: 'PurchaseOrders',       path: '/purchase-orders',        type: 'LIST' },
  { name: 'PurchaseOrderCreate',  path: '/purchase-orders/create', type: 'CREATE' },
  { name: 'SubcontractOrders',    path: '/subcontract-orders',     type: 'LIST' },
  { name: 'SubcontractOrderCreate',path: '/subcontract-orders/create',type: 'CREATE' },
  { name: 'SubcontractOrderDetail',path: '/subcontract-orders/1',   type: 'PARAM' },
  { name: 'SubcontractReturnItems',path: '/subcontract-return-items', type: 'LIST' },
  { name: 'PurchaseOrderDetail',  path: '/purchase-orders/1',      type: 'PARAM' },

  // ================ Orders ================
  { name: 'Orders',               path: '/orders',                 type: 'LIST' },
  { name: 'OrderCreate',          path: '/orders/create',          type: 'CREATE' },
  { name: 'Customers',            path: '/customers',              type: 'LIST' },
  { name: 'CustomerCreate',       path: '/customers/create',       type: 'CREATE' },
  { name: 'OrderDetail',          path: '/orders/1',               type: 'PARAM' },
  { name: 'ProductRequirement',   path: '/orders/1/requirements',  type: 'PARAM' },  // TODO: use real order id

  // ================ Quality ================
  { name: 'Certificates',                  path: '/quality/certificates',                 type: 'LIST' },
  { name: 'CertificateCreate',             path: '/quality/certificates/create',          type: 'CREATE' },
  { name: 'CertificateDetail',             path: '/quality/certificates/1',               type: 'PARAM' },
  { name: 'ChemicalAnalyses',              path: '/quality/chemical-analysis',            type: 'LIST' },
  { name: 'ChemicalAnalysisCreate',        path: '/quality/chemical-analysis/create',     type: 'CREATE' },
  { name: 'FinalInspections',              path: '/quality/final-inspection',             type: 'LIST' },
  { name: 'FinalInspectionCreate',         path: '/quality/final-inspection/create',      type: 'CREATE' },
  { name: 'FlaringTests',                  path: '/quality/flaring-test',                 type: 'LIST' },
  { name: 'FlaringTestCreate',             path: '/quality/flaring-test/create',          type: 'CREATE' },
  { name: 'FlatteningTests',               path: '/quality/flattening-test',              type: 'LIST' },
  { name: 'FlatteningTestCreate',          path: '/quality/flattening-test/create',       type: 'CREATE' },
  { name: 'FurnaceRegistrations',          path: '/quality/furnace',                      type: 'LIST' },
  { name: 'FurnaceRegistrationCreate',     path: '/quality/furnace/create',               type: 'CREATE' },
  { name: 'GrainSizeTests',                path: '/quality/grain-size-test',              type: 'LIST' },
  { name: 'GrainSizeTestCreate',           path: '/quality/grain-size-test/create',       type: 'CREATE' },
  { name: 'HardnessTests',                 path: '/quality/hardness-test',                type: 'LIST' },
  { name: 'HardnessTestCreate',            path: '/quality/hardness-test/create',         type: 'CREATE' },
  { name: 'IntergranularCorrosionTests',   path: '/quality/intergranular-corrosion-test',type: 'LIST' },
  { name: 'IntergranularCorrosionTestCreate',path: '/quality/intergranular-corrosion-test/create', type: 'CREATE' },
  { name: 'MaterialReceiveChecks',         path: '/quality/material-receive-checks',      type: 'LIST' },
  { name: 'MaterialReceiveCheckCreate',    path: '/quality/material-receive-checks/create',type: 'CREATE' },
  { name: 'MetallographicTests',           path: '/quality/metallographic-test',          type: 'LIST' },
  { name: 'MetallographicTestCreate',      path: '/quality/metallographic-test/create',   type: 'CREATE' },
  { name: 'Ncrs',                          path: '/quality/ncr',                          type: 'LIST' },
  { name: 'NcrForm',                       path: '/quality/ncr/create',                   type: 'CREATE' },
  { name: 'PittingCorrosionTests',         path: '/quality/pitting-corrosion-test',       type: 'LIST' },
  { name: 'PittingCorrosionTestCreate',    path: '/quality/pitting-corrosion-test/create',type: 'CREATE' },
  { name: 'ProcessInspections',            path: '/quality/process-inspection',           type: 'LIST' },
  { name: 'ProcessInspectionCreate',       path: '/quality/process-inspection/create',    type: 'CREATE' },
  { name: 'QualityProcessTracking',        path: '/quality/process-tracking',             type: 'LIST' },
  { name: 'TensileTests',                  path: '/quality/tensile-test',                 type: 'LIST' },
  { name: 'TensileTestCreate',             path: '/quality/tensile-test/create',          type: 'CREATE' },

  // ================ Scheduling ================
  { name: 'BatchPlans',               path: '/batch-plans',                   type: 'LIST' },
  { name: 'ColdRollPlans',            path: '/cold-roll-plans',               type: 'LIST' },
  { name: 'ColdRollCapacities',       path: '/cold-roll-capacities',          type: 'LIST' },
  { name: 'ColdRollMachineConfigs',   path: '/cold-roll-machine-configs',     type: 'LIST' },
  { name: 'ColdRollMachineGroupConfigs',path: '/cold-roll-machine-group-configs',type: 'LIST' },
  { name: 'FinalInspectionPlan',      path: '/final-inspection-plan',         type: 'LIST' },
  { name: 'PlanOverview',             path: '/plan-overview',                 type: 'SPECIAL' },  // 纯看板页，无表格
  { name: 'RawMaterialLockPlan',      path: '/raw-material-lock-plan',        type: 'LIST' },
  { name: 'WorkOrderSchedules',       path: '/scheduling-plans',              type: 'LIST' },

  // ================ StandardRegister ================
  { name: 'StandardRegisters',               path: '/standard-registers',                type: 'LIST' },
  { name: 'StandardRegisterCreate',          path: '/standard-registers/create',          type: 'CREATE' },
  { name: 'StandardRegisterDetail',          path: '/standard-registers/1',               type: 'PARAM' },
  { name: 'FactoryInspectionRequirements',   path: '/factory-inspection-requirements',    type: 'LIST' },
  { name: 'FactoryInspectionRequirementCreate',path: '/factory-inspection-requirements/create', type: 'CREATE' },
  { name: 'ChemicalCompositions',            path: '/chemical-composition',              type: 'LIST' },
  { name: 'ChemicalCompositionCreate',       path: '/chemical-composition/create',        type: 'CREATE' },
  { name: 'ChemicalValidationRules',         path: '/chemical-validate',                 type: 'LIST' },
  { name: 'ChemicalValidationRuleCreate',    path: '/chemical-validate/create',          type: 'CREATE' },
  { name: 'GradeChemicalCompositions',       path: '/grade-chemical-compositions',       type: 'LIST' },
  { name: 'GradeChemicalCompositionCreate',  path: '/grade-chemical-compositions/create', type: 'CREATE' },
  { name: 'GradeMappings',                   path: '/grade-mappings',                    type: 'LIST' },
  { name: 'GradeMappingCreate',              path: '/grade-mappings/create',              type: 'CREATE' },
  { name: 'GradePhysicalProperties',         path: '/grade-physical-properties',          type: 'LIST' },
  { name: 'GradePhysicalPropertyCreate',     path: '/grade-physical-properties/create',   type: 'CREATE' },
  { name: 'StandardInspectionRequirements',  path: '/standard-inspection-requirements',  type: 'LIST' },
  { name: 'StandardInspectionRequirementCreate',path: '/standard-inspection-requirements/create',type: 'CREATE' },
  { name: 'SubStandardQuickViews',           path: '/sub-standard-quick-views',          type: 'LIST' },
  { name: 'SubStandardQuickViewCreate',      path: '/sub-standard-quick-views/create',   type: 'CREATE' },

  // ================ Tools ================
  { name: 'DataExchange', path: '/data-exchange',  type: 'SPECIAL' },
  { name: 'ScanExecute',  path: '/mobile-report',  type: 'SPECIAL' },

  // ================ Warehouse ================
  { name: 'WarehouseInventory', path: '/warehouse',                 type: 'SPECIAL' },  // 无参=仓库导航卡（成品库/原料库/在制品库/次品库），带 {Code} 才是库存表格
  { name: 'WarehouseInbound',   path: '/warehouse/inbound',         type: 'SPECIAL' },  // 同 /warehouse 双路由导航
  { name: 'WarehouseOutbound',  path: '/warehouse/outbound',        type: 'SPECIAL' },  // 同 /warehouse 双路由导航
  { name: 'InboundHistory',     path: '/warehouse/inbound-history',  type: 'LIST' },
  { name: 'OutboundHistory',    path: '/warehouse/outbound-history', type: 'LIST' },
  { name: 'PendingDelivery',    path: '/orders/pending-delivery', type: 'LIST' },
  { name: 'MonthlyStock',       path: '/warehouse/monthly-stock',  type: 'SPECIAL' },  // 报表网格页（MudGrid），非表格列表

  // ================ WorkOrders ================
  { name: 'WorkOrders',              path: '/workorders',                     type: 'LIST' },
  { name: 'WorkOrderExecution',      path: '/workorder-execution',           type: 'LIST' },
  { name: 'WorkOrderGenerate',       path: '/workorders/generate',           type: 'SPECIAL' },
  { name: 'WorkOrderRelation',       path: '/workorders/relation',           type: 'PARAM' },  // 依赖导航上下文，直接访问=空态
  { name: 'OrderDemandAdjustment',   path: '/workorders-demand-adjustment',  type: 'LIST' },
  { name: 'MaterialPlanOverview',    path: '/material-plan-overview',         type: 'LIST' },
  { name: 'FixedLengthWorkOrderView',path: '/fixed-length-work-order-view',   type: 'LIST' },
  { name: 'WorkOrderDetail',         path: '/workorders/1',                   type: 'PARAM' },
  { name: 'WorkOrderFinishPlanCreate',path: '/workorders/1/finish-plan/create',type: 'PARAM' },
  { name: 'WorkOrderInMainWorkOrderPlanCreate',path: '/workorders/1/in-main-work-order-plan/create',type: 'PARAM' },
  { name: 'WorkOrderInProcessReworkPlanCreate',path: '/workorders/1/in-process-rework-plan/create',type: 'PARAM' },
  { name: 'WorkOrderInventoryPlanCreate',path: '/workorders/1/inventory-plan/create',type: 'PARAM' },
  { name: 'WorkOrderMaterialPlan',   path: '/workorders/1/material-plan',     type: 'PARAM' },
  { name: 'WorkOrderMaterialPlanCreate',path: '/workorders/1/material-plan/create',type: 'PARAM' },
  { name: 'WorkOrderPiercingPlanCreate',path: '/workorders/1/piercing-plan/create',type: 'PARAM' },
  { name: 'WorkOrderPiercingPlanEdit',path: '/workorders/1/piercing-plan/edit/1',type: 'PARAM' },
  { name: 'WorkOrderReworkPlanCreate',path: '/workorders/1/rework-plan/create',type: 'PARAM' },

  // ================ Reports ================
  { name: 'ReportOverview',      path: '/reports/overview',            type: 'SPECIAL' },
];

// ============================================================
// 已知缺口豁免（页面保留 LIST 确保 TC20 加载检查，但单项检查预期缺失 → 跳过不阻塞）
// ============================================================
const TC22_EXEMPT = new Set([
  'Users',                        // 用户管理列表页未实现列显隐（已知规范缺口）
  'SectionParagraphConfigSettings', // 段落配置页按类筛选视图，无列显隐
]);
const TC23_EXEMPT = new Set([
  'Users',                        // 用户管理页无打印功能（预期）
  'ConfigParameters', 'DailyOutputEstimates', 'DailyProductionCapacities',
  'StandardWorkDays', 'StandardWorkDayDeliveryStates', 'DictValueDefinitions',
  'EnumDisplayDefinitions', 'ProcessDefinitions', 'SectionParagraphConfigSettings',
  'ColdRollCapacities', 'ColdRollMachineConfigs', 'ColdRollMachineGroupConfigs', // 配置页无打印（预期）
]);

// ============================================================
// 辅助函数
// ============================================================
async function getToken() {
  const res = await fetch('http://localhost:7000/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: 'admin@mes.com', password: 'Admin@123' }),
  });
  const data = await res.json();
  return data.data?.token || data.token;
}

async function injectAuth(page, token) {
  await page.evaluate((t) => {
    localStorage.setItem('authToken', JSON.stringify(t));
    localStorage.setItem('refreshToken', JSON.stringify('smoke-test-refresh'));
    localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
    localStorage.setItem('userRoles', JSON.stringify(['Admin']));
  }, token);
}

async function checkPage(page, pageDef) {
  const result = { name: pageDef.name, path: pageDef.path, tc20: null, tc21: null, tc22: null, tc23: null, errors: [] };

  try {
    // 导航到页面
    await page.goto(BASE_URL + pageDef.path, { waitUntil: 'networkidle', timeout: 60000 });

    // TC20: 检查 WASM 崩溃 — 等 MudTable 或页面内容出现
    const hasContent = await Promise.race([
      page.waitForSelector('.mud-table, .mud-card, .mud-grid, .page-content, #app', { timeout: 25000 })
        .then(() => true).catch(() => false),
      page.waitForFunction(() => {
        // 检查 SPA 编译错误
        return document.querySelector('.mud-table,.mud-card,.mud-grid') !== null
          || (document.querySelector('#app')?.children.length > 1)
          || document.querySelector('.page-content') !== null;
      }, { timeout: 20000 }).then(() => true).catch(() => false),
    ]);

    if (hasContent) {
      result.tc20 = true;
    } else {
      // 检查是否有错误信息
      const errorMsg = await page.evaluate(() => {
        const body = document.body?.textContent || '';
        return body.includes('An unhandled error') || body.includes('加载失败') || body.includes('404')
          ? body.substring(0, 200) : null;
      });
      result.tc20 = errorMsg || false;
    }

    // 等待更长时间让 SPA 渲染完成
    await page.waitForTimeout(3000);

    // LIST 页面：执行更多检查
    if (pageDef.type === 'LIST') {
      // TC21: 表格行数
      const rowCount = await page.evaluate(() => {
        const rows = document.querySelectorAll('.mud-table-body .mud-table-row, .mud-table-body tr');
        return rows.length;
      });
      result.tc21 = rowCount > 0 || '0行数据';

      // TC22: 列显隐按钮（豁免名单内=预期缺失，跳过）
      if (TC22_EXEMPT.has(pageDef.name)) {
        result.tc22 = null;
      } else {
        const hasColToggle = await page.evaluate(() => {
          // ColumnDisplaySelect 渲染为 MudIconButton
          const buttons = document.querySelectorAll('button');
          for (const btn of buttons) {
            const text = btn.textContent?.toLowerCase() || '';
            const title = btn.title?.toLowerCase() || '';
            if (text.includes('列') || title.includes('列') ||
                text.includes('column') || title.includes('column') ||
                text.includes('显示')) return true;
          }
          return false;
        });
        result.tc22 = hasColToggle;
      }

      // TC23: 打印按钮（豁免名单内=预期缺失，跳过）
      if (TC23_EXEMPT.has(pageDef.name)) {
        result.tc23 = null;
      } else {
        const hasPrintBtn = await page.evaluate(() => {
          const buttons = document.querySelectorAll('button');
          for (const btn of buttons) {
            const text = btn.textContent?.toLowerCase() || '';
            const title = btn.title?.toLowerCase() || '';
            if (text.includes('打印') || title.includes('打印') ||
                text.includes('print') || title.includes('print') ||
                btn.querySelector('svg')?.outerHTML?.includes('Print')) return true;
          }
          return false;
        });
        result.tc23 = hasPrintBtn;
      }

      // 检查控制台错误（WASM 崩溃）
      const consoleErrors = await page.evaluate(() => {
        return window.__smokeErrors || [];
      }).catch(() => []);
      if (consoleErrors.length > 0) {
        result.errors = consoleErrors.slice(0, 3);
      }
    }

    return result;
  } catch (e) {
    result.tc20 = false;
    result.errors = [e.message?.substring(0, 200) || 'Unknown error'];
    return result;
  }
}

// ============================================================
// 主流程
// ============================================================
async function main() {
  console.log('============================================');
  console.log('  Phase 3 — 页面烟测 (TC20-TC23)');
  console.log('============================================\n');

  // 获取 Token
  console.log('▶ 获取 Token...');
  let token;
  try { token = await getToken(); console.log('  ✓ OK\n'); }
  catch (e) { console.error('  ✗ ' + e.message); process.exit(1); }

  // 启动浏览器
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });

  // 注入页面错误监听
  await page.exposeFunction('__smokeErrorCapture', (msg) => {
    if (!globalThis.__smokeErrors) globalThis.__smokeErrors = [];
    globalThis.__smokeErrors.push(msg);
  });

  const startTime = Date.now();
  const results = { tc20: { pass: 0, fail: [], skip: 0 }, tc21: { pass: 0, fail: [], skip: 0 }, tc22: { pass: 0, fail: [], skip: 0 }, tc23: { pass: 0, fail: [], skip: 0 } };
  const pageResults = [];
  let totalPages = 0;

  try {
    // 先加载首页加载 WASM runtime
    console.log('▶ 加载 WASM Runtime（首次加载较慢）...');
    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(5000);
    await injectAuth(page, token);
    await page.waitForTimeout(2000);
    console.log('  ✓ WASM 加载完成\n');

    const listPages = PAGES.filter(p => p.type === 'LIST' && !p.skip);
    const otherPages = PAGES.filter(p => p.type !== 'LIST' && !p.skip);
    const skippedPages = PAGES.filter(p => p.skip || p.type === 'PARAM');

    console.log(`  列表页: ${listPages.length}  创建/特殊页: ${otherPages.length}  跳过: ${skippedPages.length}\n`);

    // 先测列表页（全检查）
    console.log('── 列表页 (TC20-TC23) ──');
    for (const p of listPages) {
      process.stdout.write(`  ${p.name.padEnd(30)} `);
      const r = await checkPage(page, p);
      pageResults.push(r);
      totalPages++;

      const t20 = r.tc20 === true ? '✓' : '✗';
      const t21 = r.tc21 === true ? 'T✓' : (r.tc21 === null ? 'T—' : 'T✗');
      const t22 = r.tc22 === true ? 'C✓' : (r.tc22 === null ? 'C—' : 'C✗');
      const t23 = r.tc23 === true ? 'P✓' : (r.tc23 === null ? 'P—' : 'P✗');

      process.stdout.write(`${t20} ${t21} ${t22} ${t23}`);
      if (r.tc20 === true) results.tc20.pass++;
      else results.tc20.fail.push(r);
      if (r.tc21 === true) results.tc21.pass++;
      else if (r.tc21 === null) results.tc21.skip++;
      else results.tc21.fail.push(r);
      if (r.tc22 === true) results.tc22.pass++;
      else if (r.tc22 === null) results.tc22.skip++;
      else results.tc22.fail.push(r);
      if (r.tc23 === true) results.tc23.pass++;
      else if (r.tc23 === null) results.tc23.skip++;
      else results.tc23.fail.push(r);

      process.stdout.write('\n');
    }

    // 再测创建页/特殊页（仅 TC20）
    console.log('\n── 创建/特殊页 (TC20 仅) ──');
    for (const p of otherPages) {
      process.stdout.write(`  ${p.name.padEnd(30)} `);
      const r = await checkPage(page, p);
      pageResults.push(r);
      totalPages++;

      const t20 = r.tc20 === true ? '✓' : '✗';
      process.stdout.write(`${t20} (仅检查加载)`);
      if (r.tc20 === true) results.tc20.pass++;
      else results.tc20.fail.push(r);

      process.stdout.write('\n');
    }

    // ============================================================
    // 汇总
    // ============================================================
    const elapsed = ((Date.now() - startTime) / 1000 / 60).toFixed(1);
    console.log(`\n\n=== 汇总 (${totalPages} 页, ${elapsed} 分钟) ===`);
    console.log(`  TC20 页面加载: ${results.tc20.pass}/${totalPages} 通过, ${results.tc20.fail.length} 失败`);
    console.log(`  TC21 表格数据: ${results.tc21.pass}/${totalPages} 通过, ${results.tc21.fail.length} 失败, ${results.tc21.skip} 跳过`);
    console.log(`  TC22 列显隐按钮: ${results.tc22.pass}/${totalPages} 通过, ${results.tc22.fail.length} 失败, ${results.tc22.skip} 跳过`);
    console.log(`  TC23 打印按钮: ${results.tc23.pass}/${totalPages} 通过, ${results.tc23.fail.length} 失败, ${results.tc23.skip} 跳过`);

    // 输出失败详情
    const allFail = [...results.tc20.fail, ...results.tc21.fail, ...results.tc22.fail, ...results.tc23.fail];
    if (allFail.length > 0) {
      console.log('\n  失败详情:');
      for (const f of allFail) {
        console.log(`    [${f.name}] ${f.path}`);
        if (f.errors?.length) console.log(`      ${f.errors[0]}`);
      }
    }

    const hasFailure = results.tc20.fail.length > 0;
    console.log(hasFailure ? '\n✗ 存在页面加载失败' : '\n✅ 所有页面加载通过');
    if (hasFailure) process.exit(1);

  } catch (e) {
    console.error('\nFATAL:', e.message);
    process.exit(1);
  } finally {
    await browser.close();
  }
}

main();
