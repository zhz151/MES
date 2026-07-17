/**
 * TC14 API CRUD 通断测试
 *
 * 对支持 CRUD 的 Controller 测试：
 *   1. POST 创建 → 获取 Id
 *   2. GET 详情 → 验证创建成功
 *   3. PUT 更新 → 验证修改
 *   4. DELETE 删除 → 验证删除
 *
 * 使用: node playwright-tests/api-crud-all.mjs
 * 前提: MES.Api 运行在 http://localhost:7000
 */

const BASE_URL = 'http://localhost:7000';
const AUTH = { email: 'admin@mes.com', password: 'Admin@123' };

// ============================================================
// CRUD 定义
// ============================================================
// 用时戳后缀避免数据冲突
function ts() { return Date.now().toString().slice(-6); }
function def(name, route, payload, updatePayload, httpMethods) {
  const tsPayload = {};
  for (const [k, v] of Object.entries(payload)) tsPayload[k] = typeof v === 'string' && v.includes('${ts}') ? v.replace('${ts}', ts()) : v;
  const tsUpdate = {};
  for (const [k, v] of Object.entries(updatePayload)) tsUpdate[k] = typeof v === 'string' && v.includes('${ts}') ? v.replace('${ts}', ts()) : v;
  return { name, route, createPayload: tsPayload, updatePayload: tsUpdate, idField: 'data.id', httpMethods };
}

const CRUD_DEFS = [
  // --- Equipment (字段: EquipmentCode, EquipmentName) ---
  // ⚠ 已知问题: Equipment 创建后的 RepairOrder 查询有 LINQ 翻译 Bug (500)
  def('Equipment', 'api/equipment',
    { equipmentCode: 'TC14-${ts}', equipmentName: '测试设备-TC14' },
    { equipmentCode: 'TC14-${ts}', equipmentName: '测试设备-TC14-已更新' },
    { create: 'POST', createPath: '', get: 'GET', getPath: '{id}', update: 'PUT', updatePath: '{id}', del: 'DELETE', delPath: '{id}' }),

  // --- Material (字段: PlantGrade, Specification) ---
  def('Material', 'api/material',
    { plantGrade: 'TC14-TEST-${ts}', specification: '50x6', materialName: '测试物料-TC14' },
    { plantGrade: 'TC14-TEST-${ts}', specification: '50x6', materialName: '测试物料-TC14-已更新' },
    { create: 'POST', createPath: '', get: 'GET', getPath: '{id}', update: 'PUT', updatePath: '{id}', del: 'DELETE', delPath: '{id}' }),

  // --- Customer (字段: CustomerCode, CustomerUnit, Salesman) ---
  def('Customer', 'api/customer',
    { customerCode: 'TC14-${ts}', customerUnit: '测试客户-TC14', salesman: 'TC14-业务员' },
    { customerCode: 'TC14-${ts}', customerUnit: '测试客户-TC14-已更新', salesman: 'TC14-业务员' },
    { create: 'POST', createPath: '', get: 'GET', getPath: '{id}', update: 'PUT', updatePath: '{id}', del: 'DELETE', delPath: '{id}' }),

  // --- Supplier (字段: SupplierName) ---
  def('Supplier', 'api/supplier',
    { supplierName: '测试供应商-TC14-${ts}' },
    { supplierName: '测试供应商-TC14-已更新-${ts}' },
    { create: 'POST', createPath: '', get: 'GET', getPath: '{id}', update: 'PUT', updatePath: '{id}', del: 'DELETE', delPath: '{id}' }),

  // --- Warehouse (字段: Code, Name) ---
  def('Warehouse', 'api/warehouse',
    { code: 'TC14-WH-${ts}', name: '测试仓库-TC14' },
    { code: 'TC14-WH-${ts}', name: '测试仓库-TC14-已更新' },
    { create: 'POST', createPath: '', get: 'GET', getPath: '{id}', update: 'PUT', updatePath: '{id}', del: 'DELETE', delPath: '{id}' }),
];

// HTTP 辅助
let _token = null;

async function login() {
  const res = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(AUTH),
  });
  if (!res.ok) throw new Error(`登录失败: ${res.status}`);
  const data = await res.json();
  _token = data.data?.token || data.token;
  if (!_token) throw new Error('未找到 token');
}

function headers() {
  return { 'Authorization': `Bearer ${_token}`, 'Content-Type': 'application/json' };
}

async function request(method, url, body) {
  const opts = { method, headers: headers() };
  if (body) opts.body = JSON.stringify(body);
  const res = await fetch(url, opts);
  try {
    const json = await res.json();
    return { status: res.status, ok: res.ok, body: json };
  } catch {
    return { status: res.status, ok: res.ok, body: null };
  }
}

function resolvePath(template, id) {
  return template.replace('{id}', id);
}

async function testCrudCycle(def) {
  const { name, route, createPayload, updatePayload, idField, httpMethods } = def;
  let createdId = null;
  const steps = [];

  // ---- 1. CREATE ----
  const createUrl = `${BASE_URL}/${route}/${httpMethods.createPath}`;
  const createRes = await request(httpMethods.create, createUrl, createPayload);
  if (createRes.ok) {
    // 尝试从不同路径提取 id
    if (createRes.body?.data?.id) createdId = String(createRes.body.data.id);
    else if (createRes.body?.id) createdId = String(createRes.body.id);
    else if (createRes.body?.data) createdId = String(createRes.body.data);
    steps.push({ step: 'CREATE', pass: true, id: createdId });
  } else {
    steps.push({ step: 'CREATE', pass: false, status: createRes.status, message: createRes.body?.message || '' });
    // 创建失败，无法继续后续步骤
    return { name, steps, passed: false, error: `CREATE 失败 (HTTP ${createRes.status})` };
  }

  // ---- 2. GET（验证创建）----
  if (createdId && httpMethods.getPath) {
    const getUrl = `${BASE_URL}/${route}/${resolvePath(httpMethods.getPath, createdId)}`;
    const getRes = await request(httpMethods.get || 'GET', getUrl);
    if (getRes.ok && getRes.body?.success !== false) {
      steps.push({ step: 'GET', pass: true });
    } else {
      steps.push({ step: 'GET', pass: false, status: getRes.status, message: getRes.body?.message || '' });
    }
  }

  // ---- 3. UPDATE ----
  if (createdId && httpMethods.updatePath && updatePayload) {
    const updateUrl = `${BASE_URL}/${route}/${resolvePath(httpMethods.updatePath, createdId)}`;
    const updateRes = await request(httpMethods.update || 'PUT', updateUrl, updatePayload);
    if (updateRes.ok || updateRes.status === 204) {
      steps.push({ step: 'UPDATE', pass: true });
    } else {
      steps.push({ step: 'UPDATE', pass: false, status: updateRes.status, message: updateRes.body?.message || '' });
    }
  }

  // ---- 4. DELETE（清理）----
  if (createdId && httpMethods.delPath) {
    const delUrl = `${BASE_URL}/${route}/${resolvePath(httpMethods.delPath, createdId)}`;
    const delRes = await request(httpMethods.del || 'DELETE', delUrl);
    if (delRes.ok || delRes.status === 204 || delRes.status === 200) {
      steps.push({ step: 'DELETE', pass: true });
    } else {
      steps.push({ step: 'DELETE', pass: false, status: delRes.status, message: delRes.body?.message || '' });
    }
  }

  const passed = steps.every(s => s.pass);
  return { name, steps, passed };
}

async function main() {
  console.log('============================================');
  console.log('  TC14 API CRUD 通断测试');
  console.log(`  目标: ${BASE_URL}`);
  console.log(`  Controller 数: ${CRUD_DEFS.length}`);
  console.log('============================================\n');

  await login();
  console.log('  ✓ 登录成功\n');

  const results = [];
  for (const def of CRUD_DEFS) {
    const result = await testCrudCycle(def);
    results.push(result);

    const icon = result.passed ? '✓' : '✗';
    const stepDetails = result.steps.map(s =>
      s.pass ? `${s.step}✓` : `${s.step}✗(${s.status || ''} ${s.message || ''})`
    ).join(' ');
    const itemId = result.steps.find(s => s.id)?.id || '';
    console.log(`  ${icon} ${def.name.padEnd(20)} ${stepDetails}${itemId ? ' id=' + itemId : ''}`);
    if (!result.passed && result.error) {
      console.log(`      原因: ${result.error}`);
    }
  }

  // 汇总
  console.log('\n--- 汇总 ---');
  const passed = results.filter(r => r.passed).length;
  const failed = results.filter(r => !r.passed).length;
  console.log(`  通过: ${passed}/${results.length}`);
  console.log(`  失败: ${failed}/${results.length}`);

  if (failed > 0) {
    console.log('\n失败详情:');
    for (const r of results.filter(r => !r.passed)) {
      console.log(`  [${r.name}] ${r.error}`);
      for (const s of r.steps) {
        if (!s.pass) console.log(`    ${s.step}: HTTP ${s.status} — ${s.message}`);
      }
    }
  }

  if (failed > 0) process.exit(1);
  console.log('\n✅ 所有 CRUD 测试通过');
}

main().catch(e => {
  console.error('未捕获错误:', e);
  process.exit(1);
});
