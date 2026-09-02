/**
 * 考勤表「岗位类别」下拉筛选交互验证：选择某岗位类别后，网格行数应只含该类别员工
 *
 * 使用: node playwright-tests/verify-attendance-filter.mjs
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
  return data.data?.token || data.token;
}

const token = await getToken();

// API 侧统计各岗位类别人数（参考基准）
const auth = { Authorization: `Bearer ${token}` };
const monthRes = await fetch(`${API}/api/attendance/month?year=2026&month=9`, { headers: auth });
const month = (await monthRes.json()).data;
const catCount = {};
for (const e of month.employees) {
  const k = e.positionCategory || '(空)';
  catCount[k] = (catCount[k] || 0) + 1;
}
const targetCat = Object.keys(catCount).find((k) => catCount[k] < month.employees.length && k !== '(空)');
console.log(`API 员工总数=${month.employees.length} 岗位类别分布=${JSON.stringify(catCount)}`);
if (!targetCat) { console.log('无可用筛选目标'); process.exit(1); }

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1800, height: 1000 } });
const page = await ctx.newPage();
page.on('pageerror', (err) => console.log('  [pageerror]', err.message));
await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
await page.evaluate((t) => {
  localStorage.setItem('authToken', JSON.stringify(t));
  localStorage.setItem('refreshToken', JSON.stringify('att-flt'));
  localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
  localStorage.setItem('userRoles', JSON.stringify(['Admin']));
}, token);
await page.goto(BASE + '/payroll/attendance', { waitUntil: 'networkidle', timeout: 90000 });
await page.waitForTimeout(7000);

const totalRows = await page.evaluate(() => document.querySelectorAll('.attendance-grid tbody tr').length);
console.log(`初始网格行数=${totalRows}`);

// 点开「岗位类别」下拉
const catSelect = page.locator('.mud-select', { hasText: '岗位类别' }).first();
await catSelect.click();
await page.waitForTimeout(1000);

// 点击目标岗位类别选项（显示中文名，从 display-map 取）
const mapRes = await fetch(`${API}/api/dict-value-definition/display-map`, { headers: auth });
const map = (await mapRes.json()).data;
const catDisplay = (map['PositionCategoryKey'] || {})[targetCat] || targetCat;
console.log(`选择岗位类别: ${targetCat} → 「${catDisplay}」`);

const opt = page.locator('.mud-list-item', { hasText: catDisplay }).first();
await opt.click();
await page.waitForTimeout(1200);

const filteredRows = await page.evaluate(() => document.querySelectorAll('.attendance-grid tbody tr').length);
console.log(`筛选后网格行数=${filteredRows}（预期=${catCount[targetCat]}）`);
const ok = filteredRows === catCount[targetCat] && filteredRows < totalRows;
console.log(`  ${ok ? '✓' : '✗'} 岗位类别筛选生效（行数 ${totalRows} → ${filteredRows}）`);

// 筛选后每行岗位类别列应为该类别中文
const allMatch = await page.evaluate((disp) => {
  const rows = [...document.querySelectorAll('.attendance-grid tbody tr')];
  return rows.every((tr) => (tr.children[2].textContent || '').trim() === disp);
}, catDisplay);
console.log(`  ${allMatch ? '✓' : '✗'} 筛选后所有行岗位类别列=「${catDisplay}」`);

await page.screenshot({ path: 'playwright-tests/attendance-filter.png', fullPage: false });
await browser.close();
console.log(`\n=== 汇总: ${ok && allMatch ? '通过' : '失败'} ===`);
if (!(ok && allMatch)) process.exit(1);
