/**
 * 考勤表原生 input 输入回归 + 性能测量
 * 1) 多天输入功能：第1/2/3天=8/7/6 独立写入、汇总联动
 * 2) 性能：页面加载到可输入耗时、连续输入 10 格总耗时（应显著快于 MudNumericField 版本）
 *
 * 使用: node playwright-tests/verify-attendance-input.mjs
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
const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1800, height: 1000 } });
const page = await ctx.newPage();
page.on('pageerror', (err) => console.log('  [pageerror]', err.message));
await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
await page.evaluate((t) => {
  localStorage.setItem('authToken', JSON.stringify(t));
  localStorage.setItem('refreshToken', JSON.stringify('att-inp'));
  localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
  localStorage.setItem('userRoles', JSON.stringify(['Admin']));
}, token);

const t0 = Date.now();
await page.goto(BASE + '/payroll/attendance', { waitUntil: 'domcontentloaded', timeout: 90000 });
await page.waitForSelector('.attendance-grid tbody input.attendance-cell-input', { timeout: 60000 });
const loadMs = Date.now() - t0;
await page.waitForTimeout(2000);

const inputCount = await page.evaluate(() => document.querySelectorAll('.attendance-grid tbody input.attendance-cell-input').length);
console.log(`\n=== 页面加载 ===`);
console.log(`  输入框总数=${inputCount}`);
console.log(`  加载到可输入耗时=${loadMs}ms`);

const firstRow = page.locator('.attendance-grid tbody tr').first();
const inputs = firstRow.locator('input.attendance-cell-input');

// 多天功能回归（第1/2/3天 8/7/6，此时格子干净）
for (const [idx, val] of [[0, '8'], [1, '7'], [2, '6']]) {
  const box = inputs.nth(idx);
  await box.click();
  await box.fill(val);
  await box.blur();
}
await page.waitForTimeout(800);

const state = await page.evaluate(() => {
  const tr = document.querySelector('.attendance-grid tbody tr');
  const tds = [...tr.querySelectorAll('td')];
  const n = tds.length;
  const vals = [...tr.querySelectorAll('input.attendance-cell-input')].slice(0, 3).map((i) => i.value);
  const tf = document.querySelector('.attendance-grid tfoot tr');
  const totals = tf ? [...tf.querySelectorAll('td')].slice(1, 4).map((td) => td.textContent.trim()) : [];
  return { days: tds[n - 2].textContent.trim(), hours: tds[n - 1].textContent.trim(), vals, totals };
});
console.log(`\n=== 多天功能 ===`);
console.log(`  第1/2/3天框内值=${JSON.stringify(state.vals)}`);
console.log(`  出勤天数=${state.days} 总小时=${state.hours}`);
console.log(`  当日合计(第1/2/3天)=${JSON.stringify(state.totals)}`);

const okVals = JSON.stringify(state.vals) === '["8","7","6"]';
const okDays = state.days === '3';
const okHours = state.hours === '21';
const okTotals = JSON.stringify(state.totals) === '["8","7","6"]';

// 性能测量：连续输入 10 格（模拟连续录入），测量总耗时
const t1 = Date.now();
for (let i = 0; i < 10; i++) {
  const box = inputs.nth(i);
  await box.click();
  await box.fill(String(8 + (i % 5)));
  await box.blur();
}
const inputMs = Date.now() - t1;
console.log(`\n=== 性能 ===`);
console.log(`  连续输入10格耗时=${inputMs}ms（含每格一次全页汇总刷新）`);

console.log(`\n=== 结果 ===`);
console.log(`  ${okVals ? '✓' : '✗'} 三个日期各写入 8/7/6 且值保留`);
console.log(`  ${okDays ? '✓' : '✗'} 出勤天数=3`);
console.log(`  ${okHours ? '✓' : '✗'} 总小时=21`);
console.log(`  ${okTotals ? '✓' : '✗'} 当日合计第1/2/3天=8/7/6`);
console.log(`  ${loadMs < 30000 ? '✓' : '✗'} 页面加载可输入 <30s（=${loadMs}ms）`);
await page.screenshot({ path: 'playwright-tests/attendance-input.png', fullPage: false });
await browser.close();
const pass = [okVals, okDays, okHours, okTotals, loadMs < 30000].filter(Boolean).length;
console.log(`\n=== 汇总: ${pass}/5 ===`);
if (pass < 5) process.exit(1);
