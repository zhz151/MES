/**
 * 员工管理页「岗位/岗位类别」下拉选项 vs 参数表(DictValueDefinition) 全量对比
 *
 * 一次性定位：下拉缺项、多余项、中文与参数表不一致项
 * 使用: node playwright-tests/check-employee-position-vs-config.mjs
 * 前提: MES.Blazor http://localhost:5000, MES.Api http://localhost:7000
 */
import { chromium } from '../playwright-tests/node_modules/playwright/index.mjs';

const API = 'http://localhost:7000';
const BASE = 'http://localhost:5000';

async function getToken() {
  const res = await fetch(`${API}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: 'admin@mes.com', password: 'Admin@123' }),
  });
  const data = await res.json();
  const token = data.data?.token || data.token;
  if (!token) throw new Error('登录失败');
  return token;
}

async function injectAuth(page, token) {
  await page.evaluate((t) => {
    localStorage.setItem('authToken', JSON.stringify(t));
    localStorage.setItem('refreshToken', JSON.stringify('emp-pos-cfg'));
    localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
    localStorage.setItem('userRoles', JSON.stringify(['Admin']));
  }, token);
}

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1700, height: 950 } });
const page = await ctx.newPage();
page.on('pageerror', (err) => console.log('  [pageerror]', err.message));

const token = await getToken();
await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
await injectAuth(page, token);

// 1) 参数表全量（页面上下文跨域 fetch，API CORS 允许）
const cfg = await page.evaluate(async () => {
  const token = JSON.parse(localStorage.getItem('authToken') || 'null');
  const r = await fetch('http://localhost:7000/api/dict-value-definition/display-map', {
    headers: { 'Authorization': 'Bearer ' + token },
  });
  const d = await r.json();
  return { pos: d.data.PositionKey || {}, cat: d.data.PositionCategoryKey || {} };
});
console.log('=== 参数表(DictValueDefinition) 全量 ===');
console.log(`  PositionKey 行数=${Object.keys(cfg.pos).length}: ${JSON.stringify(cfg.pos)}`);
console.log(`  PositionCategoryKey 行数=${Object.keys(cfg.cat).length}: ${JSON.stringify(cfg.cat)}`);

// 2) 打开员工页，进入编辑态，抓下拉
await page.goto(BASE + '/employees', { waitUntil: 'networkidle', timeout: 90000 });
await page.waitForTimeout(6000);

async function colIndex(label) {
  return await page.evaluate((lbl) => {
    const ths = document.querySelectorAll('.mud-table-root th');
    for (let i = 0; i < ths.length; i++) {
      const txt = (ths[i].textContent || '').replace(/[▼▲\s]/g, '');
      if (txt === lbl) return i;
    }
    return -1;
  }, label);
}

const posIdx = await colIndex('岗位');
const deptIdx = await colIndex('岗位类别');
console.log(`  岗位列 index=${posIdx}, 岗位类别列 index=${deptIdx}`);

const rows = page.locator('.mud-table-root tbody tr.mud-table-row');
const firstRow = rows.first();
await firstRow.locator('td').last().locator('.mud-icon-button').nth(1).click({ timeout: 10000 });
await page.waitForTimeout(1500);

async function grabOptions(colIdx) {
  const cell = rows.first().locator('td').nth(colIdx);
  await cell.locator('.mud-select-input').first().click({ timeout: 10000 });
  await page.waitForTimeout(1200);
  const opts = await page.evaluate(() => {
    const pop = document.querySelector('.mud-popover-open .mud-list, .mud-popover.mud-popover-open .mud-list');
    if (!pop) return [];
    return [...pop.querySelectorAll('.mud-list-item')].map(x => (x.textContent || '').trim()).filter(Boolean);
  });
  await page.keyboard.press('Escape');
  await page.waitForTimeout(600);
  return opts;
}

console.log('\n=== 下拉实际选项 ===');
const posOpts = posIdx >= 0 ? await grabOptions(posIdx) : [];
const deptOpts = deptIdx >= 0 ? await grabOptions(deptIdx) : [];
console.log(`  岗位下拉(${posOpts.length}): ${posOpts.join('、')}`);
console.log(`  岗位类别下拉(${deptOpts.length}): ${deptOpts.join('、')}`);

// 3) 对比：参数表中文 vs 下拉
console.log('\n=== 对比分析 ===');
function compare(optTexts, cfgMap, label) {
  // 下拉去「请选择」
  const real = optTexts.filter(t => t !== '请选择');
  // 参数表中文值集合
  const cfgCn = new Set(Object.values(cfgMap));
  const cfgKeys = Object.keys(cfgMap);
  // 下拉缺（参数表有、下拉无）
  const missing = [...cfgCn].filter(cn => !real.includes(cn));
  // 下拉多余（下拉有、参数表中文无）
  const extra = real.filter(t => !cfgCn.has(t));
  console.log(`[${label}] 下拉项=${real.length}, 参数表项=${cfgCn.size}`);
  if (missing.length) console.log(`  ✗ 参数表有但下拉缺(${missing.length}): ${missing.join('、')}`);
  else console.log(`  ✓ 无缺项`);
  if (extra.length) console.log(`  ✗ 下拉多余/参数表无此中文(${extra.length}): ${extra.join('、')}`);
  else console.log(`  ✓ 无多余项`);
  // 名称一致性：逐 Key 校验下拉是否用参数表中文
  const bad = cfgKeys.filter(k => cfgMap[k] && !real.includes(cfgMap[k]));
  if (bad.length) console.log(`  ✗ 以下 Key 的下拉显示≠参数表中文: ${bad.map(k => k + '=' + cfgMap[k]).join(', ')}`);
  else console.log(`  ✓ 参数表各 Key 中文在下拉均能找到`);
}

compare(posOpts, cfg.pos, '岗位');
compare(deptOpts, cfg.cat, '岗位类别');

await browser.close();
console.log('\n完成');
