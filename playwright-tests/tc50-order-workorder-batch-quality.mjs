/**
 * TC50 E2E — 订单 → 工单 → 批次 → 质检
 *
 * 流程:
 *   1. API 创建客户 (POST /api/customer)
 *   2. API 创建订单含行项目 (POST /api/order)
 *   3. API 获取工单生成数据 (GET /api/workorder/items-for-generation)
 *   4. API 生成工单 (POST /api/workorder/generate)
 *   5. API 创建批次 (POST /api/batch)
 *   6. API 获取工序组 (GET /api/batch/{id}/records)
 *   7. API 创建过程检验 (POST /api/process-inspection/batch)
 *   8. Playwright 验证订单列表 (/orders)
 *   9. Playwright 验证工单列表 (/workorders)
 *   10. Playwright 验证批次列表 (/batches)
 *   11. Playwright 验证过程检验列表 (/quality/process-inspection)
 *   12. 清理
 *
 * 使用: node playwright-tests/tc50-order-workorder-batch-quality.mjs
 * 前提: MES.Api -> localhost:7000, MES.Blazor -> localhost:5000
 */

import { chromium } from 'playwright';

const API_URL = 'http://localhost:7000';
const BLZ_URL = 'http://localhost:5000';
const AUTH = { email: 'admin@mes.com', password: 'Admin@123' };

function ts() { return Date.now().toString().slice(-6); }
let _token = null;

// ============================================================
// HTTP 辅助
// ============================================================
async function login() {
  const res = await fetch(`${API_URL}/api/auth/login`, {
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

async function api(method, url, body) {
  const opts = { method, headers: headers() };
  if (body) opts.body = JSON.stringify(body);
  const res = await fetch(url, opts);
  let json = null;
  try { json = await res.json(); } catch {}
  return { status: res.status, ok: res.ok, body: json };
}

function p(msg) { process.stdout.write(msg); }

// ============================================================
// Playwright 辅助
// ============================================================
async function injectAuth(page, token) {
  await page.evaluate((t) => {
    localStorage.setItem('authToken', JSON.stringify(t));
    localStorage.setItem('refreshToken', JSON.stringify('e2e-test-refresh'));
    localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
    localStorage.setItem('userRoles', JSON.stringify(['Admin']));
  }, token);
}

async function loadWasm(page, token) {
  await page.goto(BLZ_URL + '/', { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(5000);
  await injectAuth(page, token);
  await page.waitForTimeout(2000);
}

async function checkPageContains(page, path, text, timeout = 25000) {
  await page.goto(BLZ_URL + path, { waitUntil: 'networkidle', timeout: 60000 });
  await Promise.race([
    page.waitForSelector('.mud-table', { timeout }).catch(() => {}),
    page.waitForTimeout(5000),
  ]);
  await page.waitForTimeout(2000);
  const bodyText = await page.evaluate(() => document.body.innerText || '');
  return bodyText.includes(text);
}

// ============================================================
// 主流程
// ============================================================
async function main() {
  console.log('============================================');
  console.log('  TC50 — 订单 → 工单 → 批次 → 质检 E2E');
  console.log('============================================\n');

  await login();
  p('  ✓ 登录成功\n');

  const suffix = ts();
  // 订单号必须恰好 11 位 ([StringLength(11)])
  const orderNumber = `TC50-${suffix.padStart(6, '0')}`;
  const steps = [];

  // ---- 1. 创建客户 ----
  p('  1. 创建客户... ');
  const custRes = await api('POST', `${API_URL}/api/customer`, {
    customerCode: `TC50-${suffix}`,
    customerUnit: `TC50测试客户-${suffix}`,
    salesman: '测试业务员',
    status: 'Active',
  });
  const customerId = custRes.body?.data?.id;
  p(`${custRes.ok && customerId > 0 ? '✓' : '✗'} id=${customerId}\n`);
  steps.push({ step: '创建客户', pass: custRes.ok && customerId > 0 });

  if (!customerId) return finish(steps);

  // ---- 2. 创建订单（含行项目） ----
  p('  2. 创建订单... ');
  const orderRes = await api('POST', `${API_URL}/api/order`, {
    orderNumber,
    signDate: '2026-07-16T00:00:00',
    customerId,
    salesman: '测试业务员',
    items: [
      {
        sequence: 1,
        deliveryDate: '2026-08-16T00:00:00',
        delayPenalty: false,
        settlementMethod: 0,
        pipeManufacturingType: 0,
        standardNo: 'GB/T 9948-2025',
        deliveryState: 0,
        standardGrade: 'X5CrNi18-10',
        plantGrade: '30400',
        outerDiameter: 50.0,
        wallThickness: 3.0,
        outerDiameterNegative: 0.1,
        outerDiameterPositive: 0.2,
        wallThicknessNegative: 0.1,
        wallThicknessPositive: 0.2,
        lengthStatus: 0,
        minLength: 6000,
        maxLength: 6000,
        quantity: 100,
        meters: 600,
        contractWeight: 2200.0,
      },
    ],
  });
  const orderId = orderRes.body?.data?.id;
  const orderNoFromResponse = orderRes.body?.data?.orderNumber;
  p(`${orderRes.ok && orderId > 0 ? '✓' : '✗'} id=${orderId}\n`);
  steps.push({ step: '创建订单', pass: orderRes.ok && orderId > 0 });
  // 如果服务器返回的订单号不同，用服务器的值
  const actualOrderNo = orderNoFromResponse || orderNumber;
  if (orderNoFromResponse && orderNoFromResponse !== orderNumber) {
    console.log(`     [debug] orderNumber sent=${orderNumber} received=${orderNoFromResponse}`);
  }

  if (!orderId) return finish(steps);

  // ---- 2.5 确认订单（修改状态为 Confirmed） ----
  p('  2.5 确认订单... ');
  // 先获取 RowVersion
  const detailRes = await api('GET', `${API_URL}/api/order/${orderId}`);
  const rowVersion = detailRes.body?.data?.rowVersion;
  let confirmOk = false;
  if (rowVersion) {
    const confirmRes = await api('PUT', `${API_URL}/api/order/${orderId}`, {
      status: 'Confirmed',
      rowVersion,
    });
    confirmOk = confirmRes.ok;
  }
  p(`${confirmOk ? '✓' : '✗'}\n`);
  steps.push({ step: '确认订单', pass: confirmOk });
  if (!confirmOk) return finish(steps);

  // ---- 3. 获取工单生成数据 ----
  p('  3. 获取工单生成数据... ');
  console.log(`\n     [debug] querying with salesOrderNo=${actualOrderNo}`);
  const genItemsRes = await api('GET', `${API_URL}/api/workorder/items-for-generation?salesOrderNo=${actualOrderNo}`);
  const genItems = genItemsRes.body?.data || [];
  const genItemsOk = genItemsRes.ok && genItems.length > 0;
  p(`${genItemsOk ? '✓' : '✗'} (${genItems.length} 项)\n`);
  steps.push({ step: '获取生成数据', pass: genItemsOk });

  // ---- 4. 生成工单 ----
  p('  4. 生成工单... ');
  const prodMainNo = `M${suffix}`;
  const woRes = await api('POST', `${API_URL}/api/workorder/generate`, {
    salesOrderNo: actualOrderNo,
    generateMode: 0,
    workOrders: [
      {
        productionMainNo: prodMainNo,
        productionSubNo: `C${suffix.slice(0, 2)}`,
        orderItemIds: [1],   // Sequence 值
      },
    ],
  });
  const generatedWos = woRes.body?.data || [];
  const woId = generatedWos.length > 0 ? generatedWos[0].id : null;
  const woNo = generatedWos.length > 0 ? generatedWos[0].workOrderNo : null;
  p(`${woRes.ok && woId ? '✓' : '✗'} id=${woId}, workOrderNo=${woNo}\n`);
  steps.push({ step: '生成工单', pass: woRes.ok && !!woId });

  if (!woId) return finish(steps);

  // ---- 5. 创建批次 ----
  p('  5. 创建批次... ');
  const batchRes = await api('POST', `${API_URL}/api/batch`, {
    workOrderNo: woNo,
    productionMainNo: prodMainNo,
    tagNo: `TAG-${suffix}`,
    productionType: 'RoughTube',
    manufacturingItem: 'OrderFinishedProduct',
    materialName: 'SeamlessPipe',
    productionRatio: 1,
    plantGrade: '30400',
    specification: '50x3.0',
    standardCode: 'GB/T 14976',
    settlementMethod: 'Weighing',
    technicalRequirements: 'Normal',
    deliveryState: 'SolutionAnnealedAndPickled',
    salesOrderNo: actualOrderNo,
    sourcePlantGrade: '30400',
    sourceSpecification: '50x3.0',
    inputQuantity: 100,
    inputWeight: 2200.0,
    lengthStatus: 'Fixed',
    totalQuantity: 100,
    totalWeight: 2200.0,
    processGroups: [
      {
        processName: '冷轧',
        manufacturingSpec: '50x3.0',
        manufacturingMultiple: 1,
        coldRollDraw: 1,
      },
    ],
  });
  if (!batchRes.ok && batchRes.body) console.log(`\n     [debug] batch: ${JSON.stringify(batchRes.body).substring(0, 200)}`);
  const batchId = batchRes.body?.data?.id;
  const batchOk = batchRes.ok && batchId > 0;
  p(`${batchOk ? '✓' : '✗'} id=${batchId}\n`);
  steps.push({ step: '创建批次', pass: batchOk });

  // ---- 6. 获取工序组 ----
  p('  6. 获取工序组... ');
  let pgOk = false;
  let processGroupId = null;
  if (batchId) {
    const pgRes = await api('GET', `${API_URL}/api/batch/${batchId}/records`);
    const pgs = pgRes.body?.data || [];
    pgOk = pgRes.ok && pgs.length > 0;
    processGroupId = pgs.length > 0 ? pgs[0].id : null;
  }
  p(`${pgOk ? '✓' : '✗'} (${pgOk ? '有工序组' : '无'})\n`);
  steps.push({ step: '获取工序组', pass: pgOk });

  // ---- 7. 创建过程检验 ----
  p('  7. 创建过程检验... ');
  let piIds = [];
  if (batchId) {
    // 需要 batchNo，先查批次详情
    const batchDetailRes = await api('GET', `${API_URL}/api/batch/${batchId}`);
    const batchNo = batchDetailRes.body?.data?.batchNo || `TAG-${suffix}`;

    const piRes = await api('POST', `${API_URL}/api/process-inspection/batch`, [
      {
        batchNo,
        processName: '冷轧',
        manufacturingSpec: '50x3.0',
        sectionName: '冷轧工段',
        inspectionDate: '2026-07-16T00:00:00',
        inspector: '质检员',
        shift: '白班',
        quantity: 100,
        weight: 2000.0,
        inspectionItem: '外观检查',
        qualifiedQuantity: 98,
        qualifiedWeight: 4900.0,
        defectScrapQuantity: 2,
      },
    ]);
    const piData = piRes.body?.data || [];
    piIds = piData.map(d => d.id).filter(id => id > 0);
    if (!piRes.ok && piRes.body) console.log(`\n     [debug] pi: ${JSON.stringify(piRes.body).substring(0, 300)}`);
    p(`${piRes.ok && piIds.length > 0 ? '✓' : '✗'} (${piIds.length} 条, HTTP ${piRes.status})\n`);
    steps.push({ step: '创建过程检验', pass: piRes.ok && piIds.length > 0 });
  } else {
    p('— (无批次，跳过)\n');
    steps.push({ step: '创建过程检验', pass: false });
  }

  // ---- 8-11. Playwright 验证 ----
  let listOk = false;
  try {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    await loadWasm(page, _token);

    // 8. 验证订单列表
    p('  8. 页面验证: 订单列表... ');
    listOk = await checkPageContains(page, '/orders', orderNumber);
    p(`${listOk ? '✓' : '✗'}\n`);
    steps.push({ step: '订单列表验证', pass: listOk });

    // 9. 验证工单列表
    p('  9. 页面验证: 工单列表... ');
    const woListOk = woNo ? await checkPageContains(page, '/workorders', woNo) : false;
    p(`${woListOk ? '✓' : '✗'}\n`);
    steps.push({ step: '工单列表验证', pass: woListOk });

    // 10. 验证批次列表
    p('  10. 页面验证: 批次列表... ');
    const batchListOk = await checkPageContains(page, '/batches', `TAG-${suffix}`);
    p(`${batchListOk ? '✓' : '✗'}\n`);
    steps.push({ step: '批次列表验证', pass: batchListOk });

    // 11. 验证过程检验列表
    p('  11. 页面验证: 过程检验列表... ');
    // 使用 '冷轧' 作为搜索词，因为过程检验列表应该显示工序名
    const piListOk = await checkPageContains(page, '/quality/process-inspection', '冷轧');
    p(`${piListOk ? '✓' : '✗'}\n`);
    steps.push({ step: '过程检验验证', pass: piListOk });

    await browser.close();
  } catch (e) {
    p(`  ✗ 浏览器错误: ${e.message?.substring(0, 50)}\n`);
  }

  // ---- 12. 清理 ----
  p('  12. 清理... ');
  let cleanOk = true;

  // 删过程检验
  for (const piId of piIds) {
    const del = await api('DELETE', `${API_URL}/api/process-inspection/${piId}`);
    if (!del.ok) cleanOk = false;
  }
  // 删批次
  if (batchId) {
    const del = await api('DELETE', `${API_URL}/api/batch/${batchId}`);
    if (!del.ok) cleanOk = false;
  }
  // 删工单
  if (woId) {
    const del = await api('DELETE', `${API_URL}/api/workorder/${woId}`);
    if (!del.ok) {
      // 尝试软删除
      await api('POST', `${API_URL}/api/workorder/${woId}/soft-delete`).catch(() => {});
    }
  }
  // 删订单
  if (orderId) {
    const del = await api('DELETE', `${API_URL}/api/order/${orderId}`);
    if (!del.ok) cleanOk = false;
  }
  // 删客户
  if (customerId) {
    const del = await api('DELETE', `${API_URL}/api/customer/${customerId}`);
    if (!del.ok) cleanOk = false;
  }

  p(`${cleanOk ? '✓' : '✗（部分失败）'}\n`);

  return finish(steps);
}

function finish(steps) {
  const passed = steps.filter(s => s.pass !== false).length;
  const failed = steps.filter(s => s.pass === false);

  console.log('\n--- 汇总 ---');
  console.log(`  通过: ${passed}/${steps.length}`);
  console.log(`  失败: ${failed.length}/${steps.length}`);
  if (failed.length > 0) {
    console.log('\n失败步骤:');
    for (const f of failed) console.log(`  ✗ ${f.step}`);
  }
  if (failed.length > 0) process.exit(1);
  console.log('\n✅ TC50 全部通过');
  process.exit(0);
}

main().catch(e => {
  console.error('\n未捕获错误:', e);
  process.exit(1);
});
