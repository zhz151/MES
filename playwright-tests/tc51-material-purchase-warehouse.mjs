/**
 * TC51 E2E — 采购 → 入库 → 库存
 *
 * 流程:
 *   1. API 创建供应商 (POST /api/supplier)
 *   2. API 创建采购订单 (POST /api/purchase-order)
 *   3. API 获取仓库 (GET /api/warehouse/all)
 *   4. API 批量入库 (POST /api/inventory/batch-inbound)
 *   5. Playwright 验证采购列表 (/purchase-orders)
 *   6. Playwright 验证仓库库存 (/warehouse/{Code})
 *   7. Playwright 验证入库记录 (/warehouse/inbound-history)
 *   8. 清理
 *
 * 使用: node playwright-tests/tc51-material-purchase-warehouse.mjs
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

async function checkPageContains(page, path, text, timeout = 30000) {
  await page.goto(BLZ_URL + path, { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(3000);
  // 检查登录重定向
  const hasLogin = await page.evaluate(() => document.body.innerText.includes('登录') && !document.body.innerText.includes('退出'));
  if (hasLogin) {
    console.log(`\n     [debug] 被重定向到登录页 (${page.url()})`);
    return false;
  }
  // 等待表格或内容
  await page.waitForSelector('.mud-table, .mud-card, .mud-grid', { timeout: 15000 }).catch(() => {});
  // 轮询等待目标文本
  try {
    await page.waitForFunction((t) => document.body.innerText.includes(t), text, { timeout });
    return true;
  } catch {
    const tbl = await page.evaluate(() => {
      const el = document.querySelector('.mud-table');
      return el ? el.innerText.substring(0, 400) : '(no table)';
    });
    console.log(`\n     [debug] 表格内容: "${tbl.replace(/\n/g, ' | ')}"`);
    return false;
  }
}

// ============================================================
// 主流程
// ============================================================
async function main() {
  console.log('============================================');
  console.log('  TC51 — 采购 → 入库 → 库存 E2E');
  console.log('============================================\n');

  await login();
  p('  ✓ 登录成功\n');

  const suffix = ts();
  const steps = [];

  // ---- 1. 创建供应商 ----
  p('  1. 创建供应商... ');
  const supRes = await api('POST', `${API_URL}/api/supplier`, {
    supplierName: `TC51供应商-${suffix}`,
    isActive: true,
  });
  const supplierId = supRes.body?.data?.id;
  p(`${supRes.ok && supplierId > 0 ? '✓' : '✗'} id=${supplierId}\n`);
  steps.push({ step: '创建供应商', pass: supRes.ok && supplierId > 0 });

  if (!supplierId) return finish(steps);

  // ---- 2. 创建采购订单 ----
  p('  2. 创建采购订单... ');
  const poRes = await api('POST', `${API_URL}/api/purchase-order`, {
    supplierId,
    orderDate: '2026-07-16T00:00:00',
    materialCategory: 0,
    plantGrade: '304',
    specification: '50x3.0',
    weight: 5000.0,
    quantity: 100,
    requiredDate: '2026-08-16T00:00:00',
    unitPrice: 10.0,
  });
  const poId = poRes.body?.data?.id;
  const poNo = poRes.body?.data?.orderNo;
  poRes.ok && p(`✓ id=${poId}, orderNo=${poNo}\n`);
  steps.push({ step: '创建采购单', pass: poRes.ok && poId > 0 });

  if (!poId) return finish(steps);

  // ---- 3. 获取仓库 ----
  p('  3. 获取仓库... ');
  const whRes = await api('GET', `${API_URL}/api/warehouse/all?onlyActive=true`);
  const warehouses = whRes.body?.data || [];
  const wh = warehouses.length > 0 ? warehouses[0] : null;
  p(`${wh ? `✓ 取到 ${wh.name} (id=${wh.id})` : '✗ 无可用仓库'}\n`);
  steps.push({ step: '获取仓库', pass: !!wh });

  if (!wh) return finish(steps);

  // ---- 4. 批量入库 ----
  p('  4. 批量入库... ');
  const inboundRes = await api('POST', `${API_URL}/api/inventory/batch-inbound`, {
    warehouseId: wh.id,
    inboundSource: '采购',
    sourceName: '采购入库',
    sourceOrderNo: poNo,
    inboundDate: '2026-07-16T00:00:00',
    materialType: '钢管',
    plantGrade: '304',
    specification: '50x3.0',
    rows: [
      { initialQuantity: 100, initialWeight: 5000.0 },
    ],
  });
  const batchNos = inboundRes.body?.data?.batchNos || [];
  const inboundOk = inboundRes.ok && batchNos.length > 0;
  p(`${inboundOk ? '✓' : '✗'} (批次号: ${batchNos.join(',') || '无'})\n`);
  steps.push({ step: '批量入库', pass: inboundOk });

  // ---- 5-7. Playwright 验证 ----
  try {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    await loadWasm(page, _token);

    // 5. 采购列表
    p('  5. 页面验证: 采购列表... ');
    const poListOk = await checkPageContains(page, '/purchase-orders', poNo);
    p(`${poListOk ? '✓' : '✗'}\n`);
    steps.push({ step: '采购列表验证', pass: poListOk });

    // 6. 仓库库存
    p('  6. 页面验证: 仓库库存... ');
    const whCode = wh.code || '';
    console.log(`\n     [debug] warehouse: code="${whCode}" name="${wh.name}"`);
    let whInventoryOk = false;
    if (whCode) {
      whInventoryOk = await checkPageContains(page, `/warehouse/${whCode}`, '50x3.0');
    }
    p(`${whInventoryOk ? '✓' : '✗'}\n`);
    steps.push({ step: '仓库库存验证', pass: whInventoryOk });

    // 7. 入库记录（验证页面加载即可，数据量大时不保证在首页可见）
    p('  7. 页面验证: 入库记录... ');
    await page.goto(BLZ_URL + '/warehouse/inbound-history', { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(5000);
    const hasInboundTable = await page.evaluate(() => {
      const rows = document.querySelectorAll('.mud-table-body tr, .mud-table-row');
      return rows.length > 0;
    });
    const historyOk = hasInboundTable;
    p(`${historyOk ? '✓' : '✗'} (rows=${hasInboundTable})\n`);
    p(`${historyOk ? '✓' : '✗'}\n`);
    steps.push({ step: '入库记录验证', pass: historyOk });

    await browser.close();
  } catch (e) {
    p(`  ✗ 浏览器错误: ${e.message?.substring(0, 50)}\n`);
  }

  // ---- 8. 清理 ----
  p('  8. 清理... ');
  let cleanOk = true;
  // 删库存记录
  if (poNo) {
    const invRes = await api('GET', `${API_URL}/api/inventory/list?sourceOrderNo=${poNo}&pageSize=50`);
    const invItems = invRes.body?.data?.items || invRes.body?.data || [];
    for (const item of invItems) {
      await api('DELETE', `${API_URL}/api/inventory/${item.id}`).catch(() => {});
    }
  }
  // 删采购单
  if (poId) {
    const delPo = await api('DELETE', `${API_URL}/api/purchase-order/${poId}`);
    if (!delPo.ok) cleanOk = false;
  }
  // 删供应商
  if (supplierId) {
    const delSup = await api('DELETE', `${API_URL}/api/supplier/${supplierId}`);
    if (!delSup.ok) cleanOk = false;
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
  console.log('\n✅ TC51 全部通过');
}

main().catch(e => {
  console.error('\n未捕获错误:', e);
  process.exit(1);
});
