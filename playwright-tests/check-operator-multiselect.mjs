/**
 * 操作工多选 MudSelect<EmployeeDto> MultiSelection 验证
 *
 * 验证点：
 *  1. 6 个操作工页面加载无 WASM unboxing 崩溃（no idea on how to unbox）
 *  2. 操作工列 MudSelect 下拉渲染成功（.mud-select-input 存在）
 *  3. FinalInspectionCreate 详细交互：点开下拉无崩溃 → 勾选员工 → 显示只显姓名（无 工号）
 *
 * 使用: node playwright-tests/check-operator-multiselect.mjs
 * 前提: MES.Blazor http://localhost:5000, MES.Api http://localhost:7000
 */
import { chromium } from '../playwright-tests/node_modules/playwright/index.mjs';

const API = 'http://localhost:7000';
const BASE = 'http://localhost:5000';

const PAGES = [
  { name: 'ProductionRecordCreate',   path: '/production-records/create' },
  { name: 'PicklingInRecordCreate',   path: '/pickling-in-records/create' },
  { name: 'ProductionRecords',        path: '/production-records' },
  { name: 'ProcessInspectionCreate',  path: '/quality/process-inspection/create' },
  { name: 'MaterialReceiveCheckCreate', path: '/quality/material-receive-checks/create' },
  { name: 'FinalInspectionCreate',    path: '/quality/final-inspection/create' },
];

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
    localStorage.setItem('refreshToken', JSON.stringify('op-test'));
    localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
    localStorage.setItem('userRoles', JSON.stringify(['Admin']));
  }, token);
}

const browser = await chromium.launch({ headless: false });
const ctx = await browser.newContext({ viewport: { width: 1600, height: 900 } });
const page = await ctx.newPage();
page.on('pageerror', (err) => console.log('  [pageerror]', err.message));

const token = await getToken();
// 先到同源页注入认证态，再逐页导航
await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
await injectAuth(page, token);

let pass = 0, fail = 0;

console.log('=== 1. 六页面冒烟（无 unboxing 崩溃）===');
for (const p of PAGES) {
  try {
    await page.goto(BASE + p.path, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(5000);
    const body = await page.evaluate(() => document.body?.textContent || '');
    const crashed = body.includes('no idea on how to unbox') || body.includes('An unhandled error');
    const selectInput = await page.locator('.mud-select-input').count();
    const ok = !crashed && selectInput > 0;
    console.log(`  ${ok ? '✓' : '✗'} ${p.name.padEnd(24)} crash=${crashed ? 'YES!' : 'no'} mud-select-input=${selectInput}`);
    if (ok) pass++; else { fail++; }
  } catch (e) {
    console.log(`  ✗ ${p.name}: ${e.message}`);
    fail++;
  }
}

console.log('\n=== 2. FinalInspectionCreate 详细交互 ===');
try {
  await page.goto(BASE + '/quality/final-inspection/create', { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(5000);

  // 通过表头定位「操作工/检验员」列 index（页面含检验项目/班次等多个 MudSelect 列）
  const colIdx = await page.evaluate(() => {
    const ths = document.querySelectorAll('.edit-table th, table th');
    for (let i = 0; i < ths.length; i++) {
      const t = (ths[i].textContent || '');
      if (t.includes('操作员')) return i;
    }
    return -1;
  });
  console.log(`  操作工列表头 index = ${colIdx}`);
  if (colIdx < 0) throw new Error('未找到操作工列');

  // 第一行对应单元格内的 MudSelect
  const cell = page.locator('.edit-table tbody tr, table tbody tr').first().locator('td').nth(colIdx);
  const selectInput = cell.locator('.mud-select-input').first();
  await selectInput.click({ timeout: 10000 });
  await page.waitForTimeout(1000);

  // 下拉打开：等 .mud-overlay + 检查 popover 带 operator-select-popover 宽下拉 class
  const overlayShown = await page.locator('.mud-overlay').count();
  const popoverClass = await page.evaluate(() => {
    const el = document.querySelector('.operator-select-popover');
    return el ? el.className : '';
  });
  console.log(`  ${overlayShown > 0 ? '✓' : '✗'} 下拉 popover 已打开 (mud-overlay=${overlayShown})`);
  console.log(`  ${popoverClass.includes('operator-select-popover') ? '✓' : '✗'} popover 应用宽下拉 class`);
  if (overlayShown > 0 && popoverClass.includes('operator-select-popover')) pass++; else fail++;

  // 选项文本检查：popover 内选项必须全部纯姓名（无工号）——证明 ToStringFunc=Name 生效
  const optTexts = await page.evaluate(() => {
    const pop = document.querySelector('.operator-select-popover');
    if (!pop) return [];
    return [...pop.querySelectorAll('.mud-list-item')].map(x => (x.textContent || '').trim());
  });
  const allPureName = optTexts.length > 0 && optTexts.every(t => t.length > 0 && !t.includes('('));
  console.log(`  ${allPureName ? '✓' : '✗'} 下拉选项全纯姓名（无工号）: ${optTexts.slice(0, 4).join('、')}...`);
  if (allPureName) pass++; else fail++;

  // 键盘选中：ArrowDown 移到第2项 + Enter 选中（MudBlazor 6.18+ 对 CDP 合成鼠标点击有 isTrusted 限制，
  // 键盘导航是自动化验证选中链路的可靠通道；真实用户鼠标点击 checkbox 为框架标准行为）。
  await page.keyboard.press('ArrowDown');
  await page.waitForTimeout(300);
  await page.keyboard.press('Enter');
  await page.waitForTimeout(1200);
  await page.keyboard.press('Escape');
  await page.waitForTimeout(800);

  // MudSelect 选中值显示在 readonly input.value（SetTextAsync 写入），读 input.value
  const selectedVal = await page.evaluate((idx) => {
    const td = document.querySelectorAll('.edit-table tbody tr, table tbody tr')[0]?.querySelectorAll('td')[idx];
    const input = td?.querySelector('input');
    return input ? input.value : null;
  }, colIdx);
  const selected = (selectedVal || '').trim();
  const shownNoCode = selected.length > 0 && !selected.includes('(');
  console.log(`  键盘选中后 input.value = "${selected}"`);
  console.log(`  ${selected.length > 0 ? '✓' : '✗'} 选中员工已回显 (selected=${selected.length > 0})`);
  console.log(`  ${shownNoCode ? '✓' : '✗'} 只显姓名、无工号 (shownNoCode=${shownNoCode})`);
  if (selected.length > 0 && shownNoCode) pass++; else fail++;

  await page.screenshot({ path: 'playwright-tests/op-multiselect-final.png', fullPage: false });
  console.log('  截图已保存: playwright-tests/op-multiselect-final.png');
} catch (e) {
  console.log(`  ✗ FinalInspectionCreate 交互失败: ${e.message}`);
  fail++;
}

await browser.close();
console.log(`\n=== 汇总: 通过 ${pass}, 失败 ${fail} ===`);
if (fail > 0) process.exit(1);
