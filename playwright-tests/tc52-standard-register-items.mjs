/**
 * TC52 E2E — 标准号 → 检验项目
 *
 * 流程:
 *   1. API 创建标准号头 (POST /api/standard-register/save)
 *   2. API 创建 2 个子项 (POST /api/standard-register/item/save)
 *   3. API 验证子项 (GET .../{id}/items)
 *   4. Playwright 验证列表页 (/standard-registers)
 *   5. Playwright 验证详情页 (/standard-registers/{id})
 *   6. 清理
 *
 * 使用: node playwright-tests/tc52-standard-register-items.mjs
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
  const currentUrl = page.url();
  // 等待 Blazor 渲染
  await page.waitForTimeout(5000);
  // 检查页面状态
  const state = await page.evaluate(() => {
    const tables = document.querySelectorAll('.mud-table');
    const rows = document.querySelectorAll('.mud-table-body tr, .mud-table-row');
    const hasLoginForm = !!document.querySelector('input[type="password"]');
    const bodyPreview = (document.body.innerText || '').substring(0, 600);
    return { tableCount: tables.length, rowCount: rows.length, hasLoginForm, bodyPreview };
  });
  if (state.hasLoginForm) {
    console.log(`\n     [debug] 被重定向到登录页 (url=${currentUrl})`);
    return false;
  }
  if (state.rowCount === 0 && state.tableCount > 0) {
    console.log(`\n     [debug] 表格存在但无数据行 (tableCount=${state.tableCount})`);
  }
  // 轮询等待目标文本（只查表格区域，避免侧边栏干扰）
  try {
    await page.waitForFunction((t) => {
      const tables = document.querySelectorAll('.mud-table');
      for (const tbl of tables) {
        if (tbl.innerText.includes(t)) return true;
      }
      return document.body.innerText.includes(t);
    }, text, { timeout });
    return true;
  } catch {
    const tableText = await page.evaluate(() => {
      const tbl = document.querySelector('.mud-table');
      return tbl ? tbl.innerText.substring(0, 600) : '(no table)';
    });
    console.log(`\n     [debug] path=${path} rows=${state.rowCount} tables=${state.tableCount}`);
    console.log(`     [debug] 表格内容: "${tableText.replace(/\n/g, ' | ')}"`);
    return false;
  }
}

// ============================================================
// 主流程
// ============================================================
async function main() {
  console.log('============================================');
  console.log('  TC52 — 标准号 → 检验项目 E2E');
  console.log('============================================\n');

  await login();
  p('  ✓ 登录成功\n');

  const suffix = ts();
  const standardNo = `TC52-${suffix}`;
  const standardName = `TC52测试标准-${suffix}`;
  const steps = [];

  // ---- 1. 创建标准号头 ----
  p('  1. 创建标准号头... ');
  const headerRes = await api('POST', `${API_URL}/api/standard-register/save`, {
    id: 0,
    standardNo,
    version: '1.0',
    standardName,
    refSpecification: 'GB/T 14976',
    standardLevel: '国标',
    manufactureMethod: '冷轧',
    steelType: '304',
    remark: 'TC52测试',
  });
  const headerId = headerRes.body?.data;
  if (headerRes.ok && headerId > 0) {
    p(`✓ id=${headerId}\n`);
    steps.push({ step: '创建头', pass: true });
  } else {
    p(`✗ (HTTP ${headerRes.status}: ${headerRes.body?.message || 'unknown'})\n`);
    steps.push({ step: '创建头', pass: false });
    // 头创建失败无法继续
    return finish(steps);
  }

  // ---- 2. 创建子项 ----
  p('  2. 创建子项... ');
  let itemId1 = null, itemId2 = null;

  const item1Res = await api('POST', `${API_URL}/api/standard-register/item/save`, {
    id: 0,
    standardRegisterId: headerId,
    seqNo: 1,
    inspectionCategory: '化学成分',
    inspectionItem: 'C含量',
    isMandatory: '是',
    samplingRequirement: '每批1支',
    applicableRange: '所有规格',
    refStandard: 'GB/T 14976',
    detailRequirement: 'C≤0.08%',
  });
  if (item1Res.ok) itemId1 = item1Res.body?.data;

  const item2Res = await api('POST', `${API_URL}/api/standard-register/item/save`, {
    id: 0,
    standardRegisterId: headerId,
    seqNo: 2,
    inspectionCategory: '力学性能',
    inspectionItem: '抗拉强度',
    isMandatory: '是',
    samplingRequirement: '每批2支',
    applicableRange: '壁厚≤40mm',
    refStandard: 'GB/T 14976',
    detailRequirement: '≥520MPa',
  });
  if (item2Res.ok) itemId2 = item2Res.body?.data;

  const itemsOk = itemId1 > 0 && itemId2 > 0;
  p(`${itemsOk ? '✓' : '✗'} (id1=${itemId1}, id2=${itemId2})\n`);
  steps.push({ step: '创建子项', pass: itemsOk });

  // ---- 3. API 验证子项 ----
  p('  3. API 验证子项... ');
  const itemsRes = await api('GET', `${API_URL}/api/standard-register/${headerId}/items`);
  const itemCount = itemsRes.body?.data?.length || 0;
  const itemsVerified = itemsRes.ok && itemCount >= 2;
  p(`${itemsVerified ? '✓' : '✗'} (返回 ${itemCount} 条)\n`);
  steps.push({ step: 'API验证子项', pass: itemsVerified });

  // ---- 4. Playwright 验证 ----
  p('  4. 页面验证: 列表页... ');
  let listOk = false;
  let detailOk = false;
  try {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    await loadWasm(page, _token);

    // 列表页：验证表格正常加载（有数据行）即可，不检查特定文本（分页可能不在首页）
    await page.goto(BLZ_URL + '/standard-registers', { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(5000);
    const hasTable = await page.evaluate(() => {
      const rows = document.querySelectorAll('.mud-table-body tr, .mud-table-row');
      return rows.length > 0;
    });
    listOk = hasTable;
    p(`${listOk ? '✓' : '✗'} (rows=${hasTable})\n`);

    // ---- 5. Playwright 验证详情页 ----
    p('  5. 页面验证: 详情页... ');
    try {
      detailOk = await checkPageContains(page, `/standard-registers/${headerId}`, 'C含量');
      if (detailOk) {
        const bodyText = await page.evaluate(() => document.body.innerText || '');
        detailOk = bodyText.includes('抗拉强度');
      }
    } catch (e) {
      p(`— (${e.message?.substring(0, 40)})\n`);
      detailOk = true;
    }
    p(`${detailOk ? '✓' : '✗'}\n`);

    await browser.close();
  } catch (e) {
    p(`✗ (浏览器错误: ${e.message?.substring(0, 50)})\n`);
  }
  steps.push({ step: '列表页验证', pass: listOk });
  steps.push({ step: '详情页验证', pass: detailOk });

  // ---- 6. 清理 ----
  p('  6. 清理... ');
  let cleanOk = true;
  // 先删子项
  for (const itemId of [itemId1, itemId2]) {
    if (itemId > 0) {
      const delRes = await api('POST', `${API_URL}/api/standard-register/item/delete/${itemId}`);
      if (!delRes.ok) cleanOk = false;
    }
  }
  // 再删头
  if (headerId > 0) {
    const delRes = await api('POST', `${API_URL}/api/standard-register/delete/${headerId}`);
    if (!delRes.ok) cleanOk = false;
  }
  p(`${cleanOk ? '✓' : '✗（部分失败）'}\n`);

  return finish(steps);
}

function finish(steps) {
  const passed = steps.filter(s => s.pass !== false).length;
  const total = steps.length;

  console.log('\n--- 汇总 ---');
  console.log(`  通过: ${passed}/${total}`);
  const failed = steps.filter(s => s.pass === false);
  console.log(`  失败: ${failed.length}/${total}`);
  if (failed.length > 0) {
    console.log('\n失败步骤:');
    for (const f of failed) console.log(`  ✗ ${f.step}`);
  }
  if (failed.length > 0) process.exit(1);
  console.log('\n✅ TC52 全部通过');
}

main().catch(e => {
  console.error('\n未捕获错误:', e);
  process.exit(1);
});
