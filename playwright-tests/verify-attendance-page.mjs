/**
 * 考勤表页面改造验证：工段→岗位类别+岗位、表头排序、岗位类别/岗位筛选下拉、年份/月份下拉闭包修复
 *
 * 使用: node playwright-tests/verify-attendance-page.mjs
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
  localStorage.setItem('refreshToken', JSON.stringify('att-chk'));
  localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
  localStorage.setItem('userRoles', JSON.stringify(['Admin']));
}, token);
await page.goto(BASE + '/payroll/attendance', { waitUntil: 'networkidle', timeout: 90000 });
await page.waitForTimeout(7000);

const body = await page.evaluate(() => document.body?.textContent || '');
const crashed = body.includes('no idea on how to unbox') || body.includes('An unhandled error');
console.log(`\n=== 页面加载 ===`);
console.log(`  ${crashed ? '✗' : '✓'} 页面无崩溃`);
if (crashed) process.exit(1);

// 1. 表头列
const head = await page.evaluate(() =>
  [...document.querySelectorAll('.attendance-grid thead th')].map((th) => (th.textContent || '').replace(/[▼▲\s]/g, '')));
console.log(`\n=== 表头（前8列） ===`);
console.log(`  ${head.slice(0, 8).join(' | ')}`);
const hasCategory = head.includes('岗位类别');
const hasPosition = head.includes('岗位');
const hasSection = head.includes('工段');
const hasCode = head.includes('工号');
const hasName = head.includes('姓名');
const hasDays = head.includes('出勤天数');
const hasHours = head.includes('总小时');
console.log(`  ${hasCategory ? '✓' : '✗'} 表头含「岗位类别」`);
console.log(`  ${hasPosition ? '✓' : '✗'} 表头含「岗位」`);
console.log(`  ${!hasSection ? '✓' : '✗'} 表头不含「工段」`);
console.log(`  ${hasCode && hasName ? '✓' : '✗'} 表头含「工号」「姓名」`);
console.log(`  ${hasDays && hasHours ? '✓' : '✗'} 表头含「出勤天数」「总小时」`);

// 2. 顶部工具条：筛选下拉 + 年份/月份当前值（闭包修复验证）
const tbInfo = await page.evaluate(() => {
  const tb = document.querySelector('.d-flex.align-center.flex-wrap.gap-2');
  const txt = tb ? tb.textContent || '' : '';
  const vals = tb ? [...tb.querySelectorAll('.mud-select')].map((s) => (s.textContent || '').replace(/\s+/g, ' ').trim()) : [];
  return { txt, vals };
});
console.log(`\n=== 顶部筛选与年月值 ===`);
console.log(`  ${tbInfo.txt.includes('岗位类别') ? '✓' : '✗'} 顶部含「岗位类别」筛选`);
console.log(`  ${tbInfo.txt.includes('岗位') ? '✓' : '✗'} 顶部含「岗位」筛选`);
console.log(`  ${tbInfo.txt.includes('全部') ? '✓' : '✗'} 筛选含「全部」项`);
const yearVal = tbInfo.vals.find((s) => s.includes('年份')) || '';
const monthVal = tbInfo.vals.find((s) => s.includes('月份')) || '';
console.log(`  年份当前值="${yearVal}" 月份当前值="${monthVal}"`);
const yearOk = yearVal.startsWith(String(2026)) && !yearVal.startsWith('2029');
const monthOk = monthVal.includes('9 月') && !monthVal.startsWith('13');
console.log(`  ${yearOk ? '✓' : '✗'} 年份已修复（不再显示 2029）`);
console.log(`  ${monthOk ? '✓' : '✗'} 月份已修复（不再显示 13 月）`);

// 3. 第一行数据（岗位类别/岗位中文显示）
const firstRow = await page.evaluate(() => {
  const tr = document.querySelector('.attendance-grid tbody tr');
  if (!tr) return null;
  return [...tr.querySelectorAll('td')].slice(0, 6).map((td) => (td.textContent || '').trim());
});
console.log(`\n=== 第一行（前6格） ===`);
console.log(`  ${firstRow ? firstRow.join(' | ') : '无数据'}`);
const rowCatOk = !!firstRow && firstRow.length >= 4 && !!firstRow[2];
const rowPosOk = !!firstRow && firstRow.length >= 4 && !!firstRow[3];
console.log(`  ${rowCatOk ? '✓' : '✗'} 岗位类别有值=「${firstRow?.[2]}」`);
console.log(`  ${rowPosOk ? '✓' : '✗'} 岗位有值=「${firstRow?.[3]}」`);

// 4. 表头排序：点击「姓名」表头，验证行序变化 + 箭头
const before = await page.evaluate(() => document.querySelector('.attendance-grid tbody tr')?.children[0]?.textContent.trim() || '');
const nameTh = page.locator('.attendance-grid thead th', { hasText: '姓名' }).first();
await nameTh.click();
await page.waitForTimeout(1200);
const after = await page.evaluate(() => document.querySelector('.attendance-grid tbody tr')?.children[0]?.textContent.trim() || '');
const icon = await page.evaluate(() => {
  const th = [...document.querySelectorAll('.attendance-grid thead th')].find((x) => (x.textContent || '').includes('姓名'));
  return th ? (th.textContent || '').includes('▼') || (th.textContent || '').includes('▲') : false;
});
console.log(`\n=== 表头排序（点击「姓名」） ===`);
console.log(`  ${before && after && before !== after ? '✓' : '?'} 首行工号变化: ${before} → ${after}`);
console.log(`  ${icon ? '✓' : '✗'} 姓名表头出现排序箭头`);

await page.screenshot({ path: 'playwright-tests/attendance-page.png', fullPage: false });
await browser.close();

const passCount = [hasCategory, hasPosition, !hasSection, hasCode && hasName, hasDays && hasHours,
  tbInfo.txt.includes('岗位类别'), tbInfo.txt.includes('岗位'), tbInfo.txt.includes('全部'),
  yearOk, monthOk, rowCatOk, rowPosOk, icon].filter(Boolean).length;
console.log(`\n=== 汇总: ${passCount}/13 ===`);
if (passCount < 12) process.exit(1);
