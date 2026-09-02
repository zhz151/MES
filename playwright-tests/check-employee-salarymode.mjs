/**
 * 员工管理页 SalaryMode 下拉编辑验证（bug 修复实测）
 *
 * 验证点：
 *  1. 列默认全显：12 列全显示（含 岗位类别/岗位备注/工资结算模式/工资结算备注）
 *  2. 「部门」列名不再出现，「岗位类别」存在
 *  3. 行编辑态下 SalaryMode（工资结算模式）列 MudSelect 下拉可打开、有 6 个选项、可键盘选中回显
 *  4. 页面无 WASM unboxing 崩溃
 *
 * 使用: node playwright-tests/check-employee-salarymode.mjs
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
    localStorage.setItem('refreshToken', JSON.stringify('emp-check'));
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

// ===== 1. 页面加载 + 崩溃检查 =====
console.log('=== 1. 员工页加载与列名检查 ===');
await page.goto(BASE + '/employees', { waitUntil: 'networkidle', timeout: 90000 });
await page.waitForTimeout(6000);

const body = await page.evaluate(() => document.body?.textContent || '');
const crashed = body.includes('no idea on how to unbox') || body.includes('An unhandled error');
console.log(`  ${crashed ? '✗' : '✓'} 页面无崩溃 (crashed=${crashed})`);
if (crashed) fail++; else pass++;

const headers = await page.evaluate(() => {
  return [...document.querySelectorAll('.mud-table-root th')].map(x => (x.textContent || '').trim()).filter(Boolean);
});
console.log(`  表头: ${headers.join(' | ')}`);

const hasPositionCategory = headers.some(h => h.includes('岗位类别'));
const hasDept = headers.some(h => h.includes('部门'));
const hasSalaryMode = headers.some(h => h.includes('工资结算模式'));
console.log(`  ${hasPositionCategory ? '✓' : '✗'} 存在「岗位类别」列 (hasPositionCategory=${hasPositionCategory})`);
console.log(`  ${!hasDept ? '✓' : '✗'} 无「部门」列名 (hasDept=${hasDept})`);
console.log(`  ${hasSalaryMode ? '✓' : '✗'} 存在「工资结算模式」列且默认显示 (hasSalaryMode=${hasSalaryMode})`);
if (hasPositionCategory && !hasDept && hasSalaryMode) pass++; else fail++;

// 顺序检查：工号 → 姓名 → 生产工段 → 工序组 → 成检到料 → 成检项目 → 启用 → 岗位类别 → 岗位 → 岗位备注 → 工资结算模式 → 工资结算备注
const orderKeys = ['工号', '姓名', '生产工段', '工序组', '成检到料', '成检项目', '启用', '岗位类别', '岗位', '岗位备注', '工资结算模式', '工资结算备注'];
const orderOk = headers.length >= orderKeys.length && orderKeys.every((k, i) => headers[i].includes(k));
console.log(`  ${orderOk ? '✓' : '✗'} 列顺序符合定稿`);
if (orderOk) pass++; else fail++;

// ===== 2. 行编辑态 SalaryMode 下拉 =====
console.log('\n=== 2. SalaryMode 下拉编辑验证 ===');
try {
  // 定位「工资结算模式」表头 index
  const smColIdx = await page.evaluate(() => {
    const ths = document.querySelectorAll('.mud-table-root th');
    for (let i = 0; i < ths.length; i++) {
      if ((ths[i].textContent || '').includes('工资结算模式')) return i;
    }
    return -1;
  });
  console.log(`  工资结算模式列 index = ${smColIdx}`);
  if (smColIdx < 0) throw new Error('未找到工资结算模式列');

  // 第一行数据行的操作列（最后一列）内的编辑按钮（第二个 icon-button，第一个是二维码）
  const rows = page.locator('.mud-table-root tbody tr.mud-table-row');
  const firstRow = rows.first();
  const actionTd = firstRow.locator('td').last();
  const editBtn = actionTd.locator('.mud-icon-button').nth(1);
  await editBtn.click({ timeout: 10000 });
  await page.waitForTimeout(1500);

  // 编辑态：工资结算模式列 MudSelect input
  const smCell = rows.first().locator('td').nth(smColIdx);
  const smInput = smCell.locator('.mud-select-input').first();
  const smInputCount = await smInput.count();
  console.log(`  ${smInputCount > 0 ? '✓' : '✗'} 编辑态工资结算模式列渲染为 MudSelect (count=${smInputCount})`);
  if (smInputCount > 0) pass++; else fail++;

  if (smInputCount > 0) {
    // 点击打开下拉
    await smInput.click({ timeout: 10000 });
    await page.waitForTimeout(1200);

    // 检查 popover 内选项
    const opts = await page.evaluate(() => {
      const pop = document.querySelector('.mud-popover-open .mud-list, .mud-popover.mud-popover-open .mud-list');
      if (!pop) return { count: 0, texts: [] };
      const items = [...pop.querySelectorAll('.mud-list-item')].map(x => (x.textContent || '').trim()).filter(Boolean);
      return { count: items.length, texts: items };
    });
    console.log(`  ${opts.count > 0 ? '✓' : '✗'} 下拉已打开且有选项 (count=${opts.count})`);
    console.log(`    选项: ${opts.texts.join('、')}`);
    if (opts.count > 0) pass++; else fail++;

    const expectModes = ['个人计件', '集体计件', '靠工计件', '计小时', '计日期', '固定月薪'];
    const allModes = expectModes.every(m => opts.texts.includes(m));
    console.log(`  ${allModes ? '✓' : '✗'} 6 个工资结算模式全在选项内 (allModes=${allModes})`);
    if (allModes) pass++; else fail++;

    // 键盘选中（ArrowDown 到第2项 + Enter），模拟真实选择链路
    await page.keyboard.press('ArrowDown');
    await page.waitForTimeout(300);
    await page.keyboard.press('Enter');
    await page.waitForTimeout(1200);
    await page.keyboard.press('Escape');
    await page.waitForTimeout(800);

    const selVal = await page.evaluate((idx) => {
      const row = document.querySelector('.mud-table-root tbody tr.mud-table-row');
      const td = row?.querySelectorAll('td')[idx];
      const input = td?.querySelector('input');
      return input ? input.value : null;
    }, smColIdx);
    console.log(`  选中后 input.value = "${(selVal || '').trim()}"`);
    const shown = ((selVal || '').trim().length > 0);
    console.log(`  ${shown ? '✓' : '✗'} 选中值已回显 (shown=${shown})`);
    if (shown) pass++; else fail++;

    await page.screenshot({ path: 'playwright-tests/employee-salarymode.png', fullPage: false });
    console.log('  截图已保存: playwright-tests/employee-salarymode.png');
  }
} catch (e) {
  console.log(`  ✗ SalaryMode 下拉验证失败: ${e.message}`);
  fail++;
}

await browser.close();
console.log(`\n=== 汇总: 通过 ${pass}, 失败 ${fail} ===`);
if (fail > 0) process.exit(1);
