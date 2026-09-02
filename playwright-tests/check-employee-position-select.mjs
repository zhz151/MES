/**
 * 员工管理页「岗位」列下拉编辑验证（bug 排查：岗位字典化后无下拉？）
 *
 * 验证点：
 *  1. 岗位列存在且默认显示
 *  2. 行编辑态下「岗位」列渲染为 MudSelect（而非纯文本/文本框）
 *  3. 打开岗位下拉，统计选项（应含 14 个岗位中文 + 请选择）
 *  4. 对照组：「岗位类别」列编辑态同样应为 MudSelect，下拉应含 4 个岗位类别中文
 *
 * 使用: node playwright-tests/check-employee-position-select.mjs
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
    localStorage.setItem('refreshToken', JSON.stringify('emp-pos-check'));
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

let pass = 0, fail = 0;

console.log('=== 员工页加载 ===');
await page.goto(BASE + '/employees', { waitUntil: 'networkidle', timeout: 90000 });
await page.waitForTimeout(6000);

const body = await page.evaluate(() => document.body?.textContent || '');
const crashed = body.includes('no idea on how to unbox') || body.includes('An unhandled error');
console.log(`  ${crashed ? '✗' : '✓'} 页面无崩溃`);
if (crashed) fail++; else pass++;

// 定位列 index（精确匹配纯文本，去排序箭头/空格，防「岗位」误匹配到「岗位类别」）
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
if (posIdx < 0) { console.log('  ✗ 未找到岗位列'); fail++; }

// 进入编辑态
const rows = page.locator('.mud-table-root tbody tr.mud-table-row');
const firstRow = rows.first();
const actionTd = firstRow.locator('td').last();
const editBtn = actionTd.locator('.mud-icon-button').nth(1);
await editBtn.click({ timeout: 10000 });
await page.waitForTimeout(1500);

async function checkSelectCell(colIdx, label, expectItems) {
  const cell = rows.first().locator('td').nth(colIdx);
  const selInput = cell.locator('.mud-select-input').first();
  const n = await selInput.count();
  console.log(`  ${n > 0 ? '✓' : '✗'} 编辑态「${label}」列渲染为 MudSelect (count=${n})`);
  if (n > 0) pass++; else { fail++; return; }

  // 当前值（只读显示 / 输入框占位）
  const curVal = await cell.locator('input').first().inputValue().catch(() => '');
  console.log(`    当前值="${curVal}"`);

  // 打开下拉
  await selInput.click({ timeout: 10000 });
  await page.waitForTimeout(1200);

  const opts = await page.evaluate(() => {
    const pop = document.querySelector('.mud-popover-open .mud-list, .mud-popover.mud-popover-open .mud-list');
    if (!pop) return { count: 0, texts: [] };
    const items = [...pop.querySelectorAll('.mud-list-item')].map(x => (x.textContent || '').trim()).filter(Boolean);
    return { count: items.length, texts: items };
  });
  console.log(`    下拉选项数=${opts.count}`);
  console.log(`    选项: ${opts.texts.join('、')}`);
  const hasAll = expectItems.every(it => opts.texts.includes(it));
  console.log(`  ${hasAll ? '✓' : '✗'} 下拉包含全部预期选项 (expect=${expectItems.length})`);
  if (hasAll && opts.count > 0) pass++; else fail++;

  await page.keyboard.press('Escape');
  await page.waitForTimeout(600);
}

if (posIdx >= 0) {
  console.log('\n=== 岗位列（Position）编辑态检查 ===');
  const expectPositions = ['成品检验', '酸洗', '高速轧机', '矫直', '切割', '生产后勤', '修磨', '污水处理', '固溶', '60冷轧', '办公室', '材料仓库', '生产车间', '轧拉机'];
  await checkSelectCell(posIdx, '岗位', expectPositions);
}

if (deptIdx >= 0) {
  console.log('\n=== 岗位类别列（Department）编辑态检查（对照组） ===');
  const expectCategories = ['车间生产', '质检', '生产后勤', '生技部'];
  await checkSelectCell(deptIdx, '岗位类别', expectCategories);
}

await page.screenshot({ path: 'playwright-tests/employee-position-select.png', fullPage: false });
console.log('  截图已保存: playwright-tests/employee-position-select.png');

await browser.close();
console.log(`\n=== 汇总: 通过 ${pass}, 失败 ${fail} ===`);
if (fail > 0) process.exit(1);
