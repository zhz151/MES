/**
 * API 集成测试：通过真实 HTTP 请求验证排序/筛选/搜索/筛选上下文端点
 *
 * 测试内容：
 *   1. 对每个 Controller 的列表端点发送 GET 请求，验证 200 + 有效响应
 *   2. 使用 SortBy 参数测试排序
 *   3. 使用 Keyword 参数测试模糊搜索
 *   4. 使用 Filters 参数测试筛选
 *   5. 调用 filter-contexts 端点验证可用性
 *
 * 覆盖范围：72 个 Controller（全部），59 个有标准列表端点
 * 使用: node playwright-tests/api-verify-sort-filter-search.mjs
 * 前提: MES.Api 运行在 http://localhost:7000
 */

const BASE_URL = 'http://localhost:7000';
const AUTH = { email: 'admin@mes.com', password: 'Admin@123' };

// ============================================================
// Controller 映射表
// ============================================================
const CONTROLLERS = [
  // ========== Batch (4 controllers, 6 端点) ==========
  { name: 'Batch',                         route: 'api/batch',                  listPath: 'list',                       fcPath: 'filter-contexts' },
  { name: 'Pickling',                      route: 'api/pickling',               listPath: 'list',                       fcPath: 'filter-contexts' },
  { name: 'PicklingOutRecord',             route: 'api/pickling',               listPath: 'out-records/list',           fcPath: 'out-records/filter-contexts' },
  { name: 'SectionOutsource',              route: 'api/section-outsource',      listPath: 'list',                       fcPath: 'filter-contexts' },
  { name: 'SectionOutsourceRecovery',      route: 'api/section-outsource',      listPath: 'recoveries/list',            fcPath: 'recoveries/filter-contexts' },
  { name: 'ProductionRecord',              route: 'api/production-record',      listPath: 'all/records',                fcPath: 'all/filter-contexts' },

  // ========== Configuration (8 controllers, 8 端点) ==========
  { name: 'ConfigParameter',               route: 'api/config-parameter',               listPath: 'list' },
  { name: 'Employee',                      route: 'api/employee',                      listPath: 'list' },
  { name: 'Workstation',                   route: 'api/workstation',                   listPath: 'list' },
  { name: 'DailyOutputEstimate',           route: 'api/daily-output-estimate',         listPath: 'list' },
  { name: 'DailyProductionCapacity',       route: 'api/daily-production-capacity',     listPath: 'list' },
  { name: 'StandardWorkDay',               route: 'api/standard-work-day',              listPath: 'list' },
  { name: 'StandardWorkDayDeliveryState',  route: 'api/standard-work-day-delivery-state', listPath: 'list' },
  { name: 'SectionFlowCategorySettings',   route: 'api/section-flow-category-settings', listPath: '' }, // [HttpGet] GetAll，无分页

  // ========== Equipment (4 controllers, 4 端点) ==========
  { name: 'Equipment',                     route: 'api/equipment',            listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'InspectionRecord',              route: 'api/inspection-record',    listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'MaintenanceOrder',              route: 'api/maintenance-order',    listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'RepairOrder',                   route: 'api/repair-order',         listPath: 'list',      fcPath: 'filter-contexts' },

  // ========== Infrastructure (3 controllers, 1 端点) ==========
  { name: 'User',                          route: 'api/users',                listPath: 'list' },  // 标准列表，无 fc
  // Auth 和 Scan 无标准列表端点

  // ========== Materials (4 controllers, 4 端点) ==========
  { name: 'Material',                      route: 'api/material',             listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'Supplier',                      route: 'api/supplier',             listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'PurchaseOrder',                 route: 'api/purchase-order',       listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'SubcontractOrder',              route: 'api/subcontract',          listPath: 'list',      fcPath: 'filter-contexts' },

  // ========== Orders (2 controllers, 2 端点) ==========
  { name: 'Order',                         route: 'api/order',                listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'Customer',                      route: 'api/customer',             listPath: 'list',      fcPath: 'filter-contexts' },
  // ProductRequirement 无独立列表端点（嵌套在 Order 下）

  // ========== Quality (16 controllers, all 有 fc) ==========
  { name: 'Certificate',                   route: 'api/certificate',          listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'ChemicalAnalysis',              route: 'api/chemical-analysis',    listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'FinalInspection',               route: 'api/final-inspection',     listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'FlaringTest',                   route: 'api/flaring-test',         listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'FlatteningTest',                route: 'api/flattening-test',      listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'FurnaceRegistration',           route: 'api/furnace-registration', listPath: 'all-list',  fcPath: 'filter-contexts' },
  { name: 'GrainSizeTest',                 route: 'api/grain-size-test',      listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'HardnessTest',                  route: 'api/hardness-test',        listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'IntergranularCorrosionTest',    route: 'api/intergranular-corrosion-test', listPath: 'all', fcPath: 'filter-contexts' },
  { name: 'MaterialReceiveCheck',          route: 'api/material-receive-check', listPath: 'all',     fcPath: 'filter-contexts' },
  { name: 'MetallographicTest',            route: 'api/metallographic-test',  listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'Ncr',                           route: 'api/ncr',                  listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'PittingCorrosionTest',          route: 'api/pitting-corrosion-test', listPath: 'all',     fcPath: 'filter-contexts' },
  { name: 'ProcessInspection',             route: 'api/process-inspection',   listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'QualityProcessTracking',        route: 'api/quality-process-tracking', listPath: 'list',  fcPath: 'filter-contexts' },
  { name: 'TensileTest',                   route: 'api/tensile-test',         listPath: 'all',       fcPath: 'filter-contexts' },

  // ========== Scheduling (11 controllers, 3 有标准列表) ==========
  { name: 'BatchPlan',                     route: 'api/batch-plan',            listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'WorkOrderSchedule',             route: 'api/workorder-schedule',    listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'RawMaterialLockPlan',           route: 'api/raw-material-lock-plan', listPath: 'list' },
  // BatchPlanSchedule, BatchPlanTarget, ColdRollPlan, ColdRollSpecSchedule, FinalInspectionPlan,
  // ProductionOverview, SectionFlowAnalysis, SectionProductionStatus — 无标准列表端点

  // ========== StandardRegister (8 controllers, 8 端点) ==========
  { name: 'StandardRegister',              route: 'api/standard-register',    listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'ChemicalComposition',           route: 'api/chemical-composition', listPath: 'all',       fcPath: 'filter-contexts' },
  { name: 'ChemicalValidationRule',        route: 'api/chemical-validation-rule', listPath: 'all',   fcPath: 'filter-contexts' },
  { name: 'GradeChemicalComposition',      route: 'api/grade-chemical-composition', listPath: 'list', fcPath: 'filter-contexts' },
  { name: 'GradeMapping',                  route: 'api/grade-mapping',         listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'GradePhysicalProperty',         route: 'api/grade-physical-property', listPath: 'list',   fcPath: 'filter-contexts' },
  { name: 'StandardInspectionRequirement', route: 'api/standard-inspection-requirement', listPath: 'list', fcPath: 'filter-contexts' },
  { name: 'SubStandardQuickView',          route: 'api/sub-standard-quick-view', listPath: 'list',   fcPath: 'filter-contexts' },

  // ========== Warehouse (4 controllers, 4 端点) ==========
  { name: 'Inventory',                     route: 'api/inventory',            listPath: 'list',                     fcPath: 'inventory-filter-contexts' },
  { name: 'InventoryOutbound',             route: 'api/inventory',            listPath: 'outbound-records',         fcPath: 'outbound-filter-contexts' },
  { name: 'PendingDelivery',               route: 'api/pending-delivery',     listPath: 'list',                     fcPath: 'filter-contexts' },
  { name: 'Warehouse',                     route: 'api/warehouse',            listPath: 'list',                     fcPath: 'filter-contexts' },

  // ========== Work Orders (4 controllers, 3 端点) ==========
  { name: 'WorkOrder',                     route: 'api/workorder',            listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'WorkOrderExecution',            route: 'api/workorder-execution',  listPath: 'list',      fcPath: 'filter-contexts' },
  { name: 'OrderDemandAdjustment',         route: 'api/order-demand-adjustment', listPath: 'list',  fcPath: 'filter-contexts' },
  { name: 'Notification',                  route: 'api/notification',         listPath: 'list' },
  // MaterialPlan 无标准列表端点（参数化路由）
];

// ============================================================
// 已知 SortKey（来自 verify-sort-keys.mjs）
// ============================================================
// Hand-rolled switch 服务：精确列出所有分支
// ApplySort 服务：提供通用字段（Id/CreatedTime 等几乎所有实体都有）
const SORT_KEYS = {
  // --- Hand-rolled: OrderService ---
  'Order': ['ordernumber', 'signdate', 'status', 'salesman', 'customername',
    'endcustomer', 'deliverystart', 'totalcontractweight', 'schedulestage'],

  // --- Hand-rolled: ProductionRecordService ---
  'ProductionRecord': ['batchno', 'processname', 'sectionname', 'sequencenumber',
    'equipmentname', 'operator', 'shift', 'quantity', 'weight', 'plantgrade', 'datasource'],

  // --- Hand-rolled: WorkOrderExecutionService ---
  'WorkOrderExecution': ['workorderno', 'salesman', 'customername', 'plantgrade',
    'specification', 'totalquantity', 'totalweight', 'inputquantity', 'inputstatus',
    'deliverystate', 'materialplanrate', 'schedulestage', 'urgencylevel'],

  // --- Hand-rolled: InventoryService ---
  'Inventory': ['batchno', 'materialtype', 'inbounddate', 'remainingweight',
    'plantgrade', 'specification', 'heatno', 'remainingquantity'],

  // --- Hand-rolled: InventoryOutbound ---
  'InventoryOutbound': ['batchno', 'outbounddate', 'outboundtype', 'outboundquantity',
    'outboundweight', 'outboundmeters', 'targetcompany'],

  // --- Hand-rolled: MaterialReceiveCheck ---
  'MaterialReceiveCheck': ['batchno', 'receivedate', 'checker', 'plantgrade',
    'specification', 'workorderno', 'datasource'],

  // --- Hand-rolled: CertificateService ---
  'Certificate': ['certificateno', 'customername', 'issuedate',
    'productstandard', 'productname', 'deliverystatus'],

  // --- ApplySort 通用服务：几乎所有实体都有 CreatedTime ---
  'Batch': ['createdtime'],
  'Pickling': ['createdtime'],
  'PicklingOutRecord': ['createdtime'],
  'SectionOutsource': ['createdtime'],
  'SectionOutsourceRecovery': ['createdtime'],
  'ConfigParameter': ['createdtime'],
  'Employee': ['createdtime'],
  'Workstation': ['createdtime'],
  'DailyOutputEstimate': ['createdtime'],
  'DailyProductionCapacity': ['createdtime'],
  'StandardWorkDay': ['createdtime'],
  'StandardWorkDayDeliveryState': ['createdtime'],
  'Equipment': ['createdtime'],
  'InspectionRecord': ['createdtime'],
  'MaintenanceOrder': ['createdtime'],
  'RepairOrder': ['createdtime'],
  'User': ['createdtime'],
  'Material': ['createdtime'],
  'Supplier': ['createdtime'],
  'PurchaseOrder': ['createdtime'],
  'SubcontractOrder': ['createdtime'],
  'Customer': ['createdtime'],
  'ChemicalAnalysis': ['createdtime'],
  'FinalInspection': ['createdtime'],
  'FlaringTest': ['createdtime'],
  'FlatteningTest': ['createdtime'],
  'FurnaceRegistration': ['createdtime'],
  'GrainSizeTest': ['createdtime'],
  'HardnessTest': ['createdtime'],
  'IntergranularCorrosionTest': ['createdtime'],
  'MetallographicTest': ['createdtime'],
  'Ncr': ['createdtime'],
  'PittingCorrosionTest': ['createdtime'],
  'ProcessInspection': ['createdtime'],
  'QualityProcessTracking': ['createdtime'],
  'TensileTest': ['createdtime'],
  'BatchPlan': ['createdtime'],
  'WorkOrderSchedule': ['createdtime'],
  'RawMaterialLockPlan': ['createdtime'],
  'StandardRegister': ['createdtime'],
  'ChemicalComposition': ['createdtime'],
  'ChemicalValidationRule': ['createdtime'],
  'GradeChemicalComposition': ['createdtime'],
  'GradeMapping': ['createdtime'],
  'GradePhysicalProperty': ['createdtime'],
  'StandardInspectionRequirement': ['createdtime'],
  'SubStandardQuickView': ['createdtime'],
  'PendingDelivery': ['createdtime'],
  'Warehouse': ['createdtime'],
  'WorkOrder': ['createdtime'],
  'OrderDemandAdjustment': ['createdtime'],
  'Notification': ['createdtime'],
};

// ============================================================
// 跳过测试的控制器（无标准列表端点）
// ============================================================
const SKIP_LIST_TEST = new Set([
  'SectionFlowCategorySettings',
]);

// ============================================================
// HTTP 辅助函数
// ============================================================
let _token = null;

async function login() {
  const res = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(AUTH),
  });
  if (!res.ok) throw new Error(`登录失败: ${res.status} ${res.statusText}`);
  const data = await res.json();
  // 兼容两种响应格式：{ success, data: { token } } 或 { token }
  _token = data.data?.token || data.token;
  if (!_token) throw new Error('登录响应中未找到 token');
  return _token;
}

function headers() {
  return {
    'Authorization': `Bearer ${_token}`,
    'Content-Type': 'application/json',
  };
}

async function get(url) {
  const res = await fetch(url, { headers: headers() });
  const body = await res.json();
  return { status: res.status, ok: res.ok, body };
}

// ============================================================
// 测试函数
// ============================================================
async function testBasicList(ctrl) {
  if (SKIP_LIST_TEST.has(ctrl.name)) return { pass: null, reason: '无标准列表端点', url: null };

  const url = `${BASE_URL}/${ctrl.route}/${ctrl.listPath}?PageSize=3`;
  const { status, ok, body } = await get(url);

  if (!ok) return { pass: false, status, error: `HTTP ${status}`, url };

  // 验证响应结构
  if (body.success !== true) return { pass: false, status, error: `success=false: ${body.message || ''}`, url, body };

  // 检查 data 字段
  const hasData = body.data !== undefined && body.data !== null;
  if (!hasData) return { pass: false, status, error: '响应缺少 data 字段', url, body };

  return { pass: true, status, url, body };
}

async function testSort(ctrl) {
  if (SKIP_LIST_TEST.has(ctrl.name)) return { pass: null, reason: '无标准列表端点', url: null };

  const sortKeys = SORT_KEYS[ctrl.name];
  if (!sortKeys || sortKeys.length === 0) return { pass: null, reason: '无已知 SortKey', url: null };

  const results = [];
  for (const key of sortKeys.slice(0, 3)) { // 最多 3 个
    const url = `${BASE_URL}/${ctrl.route}/${ctrl.listPath}?SortBy=${key}&IsDescending=true&PageSize=3`;
    const { status, ok, body } = await get(url);

    if (!ok) {
      results.push({ pass: false, sortKey: key, error: `HTTP ${status}`, url });
    } else if (body.success !== true) {
      results.push({ pass: false, sortKey: key, error: `success=false: ${body.message || ''}`, url });
    } else {
      results.push({ pass: true, sortKey: key, url });
    }
  }
  return { pass: results.some(r => r.pass), details: results };
}

async function testSearch(ctrl) {
  if (SKIP_LIST_TEST.has(ctrl.name)) return { pass: null, reason: '无标准列表端点', url: null };

  const url = `${BASE_URL}/${ctrl.route}/${ctrl.listPath}?Keyword=0&PageSize=3`;
  const { status, ok, body } = await get(url);

  if (!ok) return { pass: false, status, error: `HTTP ${status}`, url };
  if (body.success !== true) return { pass: false, status, error: `success=false: ${body.message || ''}`, url };
  return { pass: true, status, url };
}

async function testFilter(ctrl) {
  if (SKIP_LIST_TEST.has(ctrl.name)) return { pass: null, reason: '无标准列表端点', url: null };

  const filters = JSON.stringify([{ Field: 'Id', Operator: 'greaterThan', Value: '0' }]);
  const url = `${BASE_URL}/${ctrl.route}/${ctrl.listPath}?PageSize=3&filters=${encodeURIComponent(filters)}`;
  const { status, ok, body } = await get(url);

  if (!ok) return { pass: false, status, error: `HTTP ${status}`, url };
  if (body.success !== true) return { pass: false, status, error: `success=false: ${body.message || ''}`, url };
  return { pass: true, status, url };
}

async function testFilterContexts(ctrl) {
  if (!ctrl.fcPath) return { pass: null, reason: '无 filter-contexts 端点', url: null };

  const url = `${BASE_URL}/${ctrl.route}/${ctrl.fcPath}`;
  const { status, ok, body } = await get(url);

  if (!ok) return { pass: false, status, error: `HTTP ${status}`, url };
  if (body.success !== true) return { pass: false, status, error: `success=false: ${body.message || ''}`, url };

  const data = body.data;
  if (typeof data !== 'object' || data === null) {
    return { pass: false, status, error: 'filter-contexts 数据格式异常（非对象）', url };
  }

  const keyCount = Object.keys(data).length;
  return { pass: true, status, keyCount, url };
}

// ============================================================
// 运行测试
// ============================================================
async function main() {
  console.log('============================================');
  console.log('  API 集成测试：排序 / 筛选 / 搜索 / 筛选上下文');
  console.log(`  目标: ${BASE_URL}`);
  console.log(`  Controller 总数: ${CONTROLLERS.length}`);
  console.log('============================================\n');

  // 登录
  console.log('▶ 登录认证...');
  try {
    await login();
    console.log(`  ✓ 登录成功 (token 已获取)\n`);
  } catch (e) {
    console.error(`  ✗ 登录失败: ${e.message}`);
    process.exit(1);
  }

  const results = {
    list: { pass: 0, fail: [], skip: 0 },
    sort: { pass: 0, fail: [], skip: 0 },
    search: { pass: 0, fail: [], skip: 0 },
    filter: { pass: 0, fail: [], skip: 0 },
    fc: { pass: 0, fail: [], skip: 0 },
  };

  for (const ctrl of CONTROLLERS) {
    const name = ctrl.name;

    // 基础列表
    const listRes = await testBasicList(ctrl);
    if (listRes.pass === true) results.list.pass++;
    else if (listRes.pass === null) results.list.skip++;
    else results.list.fail.push({ name, ...listRes });

    // 排序
    const sortRes = await testSort(ctrl);
    if (sortRes.pass === null) results.sort.skip++;
    else if (sortRes.pass) results.sort.pass++;
    else results.sort.fail.push({ name, ...sortRes });

    // 搜索
    const searchRes = await testSearch(ctrl);
    if (searchRes.pass === true) results.search.pass++;
    else if (searchRes.pass === null) results.search.skip++;
    else results.search.fail.push({ name, ...searchRes });

    // 筛选
    const filterRes = await testFilter(ctrl);
    if (filterRes.pass === true) results.filter.pass++;
    else if (filterRes.pass === null) results.filter.skip++;
    else results.filter.fail.push({ name, ...filterRes });

    // Filter Contexts
    const fcRes = await testFilterContexts(ctrl);
    if (fcRes.pass === true) results.fc.pass++;
    else if (fcRes.pass === null) results.fc.skip++;
    else results.fc.fail.push({ name, ...fcRes });

    // 实时输出
    const status = listRes.pass ? '✓' : (listRes.pass === null ? '—' : '✗');
    const sortMark = sortRes.pass === true ? 'S✓' : (sortRes.pass === null ? 'S—' : 'S✗');
    const searchMark = searchRes.pass ? 'Q✓' : (searchRes.pass === null ? 'Q—' : 'Q✗');
    const filterMark = filterRes.pass ? 'F✓' : (filterRes.pass === null ? 'F—' : 'F✗');
    const fcMark = fcRes.pass === true ? 'C✓' : (fcRes.pass === null ? 'C—' : 'C✗');
    const failDetails = [];
    if (listRes.pass === false) failDetails.push('LIST');
    if (sortRes.pass === false) failDetails.push('SORT');
    if (searchRes.pass === false) failDetails.push('SEARCH');
    if (filterRes.pass === false) failDetails.push('FILTER');
    if (fcRes.pass === false) failDetails.push('FC');
    const detail = failDetails.length > 0 ? ` ← ${failDetails.join(', ')}` : '';
    const items = listRes.pass && listRes.body?.data?.items
      ? ` (${listRes.body.data.items.length} items)`
      : '';
    console.log(`  ${status} ${name.padEnd(35)} ${sortMark} ${searchMark} ${filterMark} ${fcMark}${detail}${items}`);
  }

  // ============================================================
  // 输出汇总
  // ============================================================
  console.log('\n--- 汇总 ---');
  let allPass = true;

  for (const [category, label] of [['list', '基础列表'], ['sort', '排序'], ['search', '搜索'], ['filter', '筛选'], ['fc', '筛选上下文']]) {
    const r = results[category];
    const total = r.pass + r.fail.length + r.skip;
    console.log(`  ${label}: ${r.pass}/${total} 通过, ${r.fail.length} 失败, ${r.skip} 跳过`);

    if (r.fail.length > 0) {
      allPass = false;
      console.log(`    失败详情:`);
      for (const f of r.fail) {
        const urlStr = f.url ? f.url.substring(0, 120) : 'N/A';
        console.log(`      [${f.name}] ${f.error || ''} — ${urlStr}`);
        if (f.details) {
          for (const d of f.details) {
            if (!d.pass) {
              console.log(`        SortKey="${d.sortKey}": ${d.error || ''}`);
            }
          }
        }
      }
    }
  }

  if (allPass) {
    console.log('\n✅ 所有 API 测试通过');
  } else {
    console.log('\n✗ 存在失败的测试，请检查以上详情');
    process.exit(1);
  }
}

main().catch(e => {
  console.error('未捕获错误:', e);
  process.exit(1);
});
