/**
 * 操作工/检验员多选：列宽加大 + 下拉面板压缩 验证
 *
 * 验证点：
 *  1. 6 个页面渲染无 unboxing 崩溃，且操作工 MudSelect 带 operator-input class
 *  2. 编辑页操作工列宽已加大（ProductionRecordCreate 160px / FinalInspectionCreate 130px）
 *  3. 列表页 ProductionRecords 操作人列宽 160px
 *  4. 操作工输入框 max-width 突破 compact-input 130px → 190px
 *  5. 下拉面板：勾选框与姓名间距 margin-right≈2px、选项行高 min-height≈32px（压缩生效）
 *  6. 键盘选中回显仍只显姓名（无工号）回归
 *
 * 使用: node playwright-tests/check-operator-width.mjs
 * 前提: MES.Blazor http://localhost:5000, MES.Api http://localhost:7000
 */
import { chromium } from '../playwright-tests/node_modules/playwright/index.mjs';

const API = 'http://localhost:7000';
const BASE = 'http://localhost:5000';

const PAGES = [
  { name: 'ProductionRecordCreate',   path: '/production-records/create', colLabel: '操作工' },
  { name: 'PicklingInRecordCreate',   path: '/pickling-in-records/create', colLabel: '操作人' },
  { name: 'ProductionRecords',        path: '/production-records', colLabel: '操作人' },
  { name: 'ProcessInspectionCreate',  path: '/quality/process-inspection/create', colLabel: '检验员' },
  { name: 'MaterialReceiveCheckCreate', path: '/quality/material-receive-checks/create', colLabel: '检验员' },
  { name: 'FinalInspectionCreate',    path: '/quality/final-inspection/create', colLabel: '操作员' },
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
await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
await injectAuth(page, token);

let pass = 0, fail = 0;
const ok = (cond, msg) => { console.log(`  ${cond ? '✓' : '✗'} ${msg}`); cond ? pass++ : fail++; };

console.log('=== 1. 六页面冒烟 + operator-input class ===');
for (const p of PAGES) {
  try {
    await page.goto(BASE + p.path, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(5000);
    const body = await page.evaluate(() => document.body?.textContent || '');
    const crashed = body.includes('no idea on how to unbox') || body.includes('An unhandled error');
    // 列表页 ProductionRecords 非编辑态操作人列显示文本（无 MudSelect），仅要求无崩溃；编辑页要求出现 operator-input
    if (p.name === 'ProductionRecords') {
      ok(!crashed, `${p.name.padEnd(26)} crash=${crashed ? 'YES!' : 'no'}（非编辑态文本列，无 MudSelect 属预期）`);
    } else {
      const opInput = await page.locator('.mud-select.operator-input').count();
      ok(!crashed && opInput > 0, `${p.name.padEnd(26)} crash=${crashed ? 'YES!' : 'no'} operator-input=${opInput}`);
    }
  } catch (e) {
    ok(false, `${p.name}: ${e.message}`);
  }
}

console.log('\n=== 2. 编辑页操作工列宽已加大 ===');
try {
  await page.goto(BASE + '/production-records/create', { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(5000);
  const wProd = await page.evaluate(() => {
    const ths = document.querySelectorAll('table th');
    for (const th of ths) if ((th.textContent || '').includes('操作工')) return { w: th.getBoundingClientRect().width, style: th.getAttribute('style') || '' };
    return null;
  });
  ok(wProd && wProd.w >= 150, `ProductionRecordCreate 操作工列宽=${wProd?.w.toFixed(0)}px (style=${wProd?.style})`);

  await page.goto(BASE + '/quality/final-inspection/create', { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(5000);
  const wFinal = await page.evaluate(() => {
    const ths = document.querySelectorAll('table th');
    for (const th of ths) if ((th.textContent || '').includes('操作员')) return th.getBoundingClientRect().width;
    return 0;
  });
  ok(wFinal >= 120, `FinalInspectionCreate 操作员列宽=${wFinal.toFixed(0)}px`);
} catch (e) {
  ok(false, `列宽验证: ${e.message}`);
}

console.log('\n=== 3. 列表页 ProductionRecords 操作人列宽 ===');
try {
  await page.goto(BASE + '/production-records', { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(5000);
  const wList = await page.evaluate(() => {
    const ths = document.querySelectorAll('table th');
    for (const th of ths) if ((th.textContent || '').includes('操作人')) return th.getBoundingClientRect().width;
    return 0;
  });
  ok(wList >= 140, `ProductionRecords 操作人列宽=${wList.toFixed(0)}px`);
} catch (e) {
  ok(false, `列表页列宽验证: ${e.message}`);
}

console.log('\n=== 4. 操作工输入框 max-width + 下拉面板压缩（FinalInspectionCreate）===');
try {
  await page.goto(BASE + '/quality/final-inspection/create', { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(5000);
  const colIdx = await page.evaluate(() => {
    const ths = document.querySelectorAll('table th');
    for (let i = 0; i < ths.length; i++) if ((ths[i].textContent || '').includes('操作员')) return i;
    return -1;
  });
  if (colIdx < 0) throw new Error('未找到操作员列');

  const cell = page.locator('table tbody tr').first().locator('td').nth(colIdx);
  await cell.locator('.mud-select-input').first().click({ timeout: 10000 });
  await page.waitForTimeout(1000);

  const ui = await page.evaluate(() => {
    const op = document.querySelector('.mud-select.operator-input');
    const pop = document.querySelector('.operator-select-popover');
    const item = pop?.querySelector('.mud-list-item');
    const icon = item?.querySelector('.mud-list-item-icon');
    return {
      maxWidth: op ? getComputedStyle(op).maxWidth : null,
      slotPadding: op ? getComputedStyle(op.querySelector('.mud-input-slot')).padding : null,
      itemMinHeight: item ? getComputedStyle(item).minHeight : null,
      itemPadding: item ? getComputedStyle(item).paddingTop + '/' + getComputedStyle(item).paddingBottom : null,
      iconMinWidth: icon ? getComputedStyle(icon).minWidth : null,
      iconMarginRight: icon ? getComputedStyle(icon).marginRight : null,
    };
  });
  ok(ui.maxWidth === '190px', `操作工输入框 max-width=${ui.maxWidth}（期望190px，突破compact-input 130px）`);
  ok(ui.itemMinHeight === '32px', `下拉选项 min-height=${ui.itemMinHeight}（期望32px 压缩）`);
  ok(ui.iconMinWidth === '24px' && ui.iconMarginRight === '0px', `勾选框(icon)与姓名间距 min-width=${ui.iconMinWidth} margin-right=${ui.iconMarginRight}（期望24px/0px 无空格占位）`);
  console.log(`    slotPadding=${ui.slotPadding} itemPadding=${ui.itemPadding}`);

  await page.keyboard.press('ArrowDown');
  await page.waitForTimeout(300);
  await page.keyboard.press('Enter');
  await page.waitForTimeout(1200);
  await page.keyboard.press('Escape');
  await page.waitForTimeout(800);
  const selected = await page.evaluate((idx) => {
    const td = document.querySelectorAll('table tbody tr')[0]?.querySelectorAll('td')[idx];
    return td?.querySelector('input')?.value?.trim() || '';
  }, colIdx);
  ok(selected.length > 0 && !selected.includes('('), `键盘选中回显 input.value="${selected}" 只显姓名无工号`);
} catch (e) {
  ok(false, `下拉面板验证: ${e.message}`);
}

await browser.close();
console.log(`\n=== 汇总: 通过 ${pass}, 失败 ${fail} ===`);
if (fail > 0) process.exit(1);
