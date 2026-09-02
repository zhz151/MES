/**
 * 考勤表方向键单元格导航 + 表头 sticky 锁定验证
 * 1) 方向键：←→ 同行日期格移动、↑↓ 同列跨员工移动，边界不放行
 * 2) 聚焦全选：方向键跳到有值的格子全选（便于直接覆盖输入）
 * 3) 表头锁定：容器内 scrollTop 滚动后 thead 仍贴容器顶部；sticky-left 工号列水平固定
 *
 * 使用: node playwright-tests/verify-attendance-nav-sticky.mjs
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
  localStorage.setItem('refreshToken', JSON.stringify('att-nav'));
  localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
  localStorage.setItem('userRoles', JSON.stringify(['Admin']));
}, token);

await page.goto(BASE + '/payroll/attendance', { waitUntil: 'domcontentloaded', timeout: 90000 });
await page.waitForSelector('.attendance-grid tbody input.attendance-cell-input', { timeout: 60000 });
await page.waitForTimeout(2000);

// 当前焦点格信息：type=cell 时 row=员工行序 col=行内第几个日期格
async function activeCell() {
  return page.evaluate(() => {
    const a = document.activeElement;
    if (!a || !a.classList || !a.classList.contains('attendance-cell-input')) return { type: 'none', row: -1, col: -1 };
    const tr = a.closest('tr');
    const tbody = tr.closest('tbody');
    const rows = [...tbody.querySelectorAll('tr')];
    const row = rows.indexOf(tr);
    const colInputs = [...tr.querySelectorAll('input.attendance-cell-input')];
    const col = colInputs.indexOf(a);
    return {
      type: 'cell', row, col, val: a.value,
      sel: a.selectionStart + ',' + a.selectionEnd,
    };
  });
}

const row0Inputs = page.locator('.attendance-grid tbody tr').first().locator('input.attendance-cell-input');
const rowCount = await page.locator('.attendance-grid tbody tr').count();
console.log(`\n=== 页面 ===`);
console.log(`  员工行数=${rowCount}`);

const checks = [];
const check = (name, cond) => { checks.push([name, !!cond]); console.log(`  ${cond ? '✓' : '✗'} ${name}`); };

// --- 方向键导航 ---
console.log(`\n=== 方向键导航 ===`);
await row0Inputs.nth(0).click();
let a0 = await activeCell();
check('点击第1行第1天 → 焦点 (0,0)', a0.type === 'cell' && a0.row === 0 && a0.col === 0);

await page.keyboard.press('ArrowRight');
let a1 = await activeCell();
check('→ 到 (0,1)', a1.row === 0 && a1.col === 1);

await page.keyboard.press('ArrowRight');
let a2 = await activeCell();
check('→ 到 (0,2)', a2.row === 0 && a2.col === 2);

await page.keyboard.press('ArrowDown');
let a3 = await activeCell();
check('↓ 到 (1,2) 同列跨员工', a3.row === 1 && a3.col === 2);

await page.keyboard.press('ArrowLeft');
let a4 = await activeCell();
check('← 到 (1,1)', a4.row === 1 && a4.col === 1);

await page.keyboard.press('ArrowUp');
let a5 = await activeCell();
check('↑ 回 (0,1)', a5.row === 0 && a5.col === 1);

// 左边界：行首 col0 再 ← 不放行
await page.keyboard.press('ArrowLeft');
let a6 = await activeCell();
check('行首(0,0) 再 ← 不越界', a6.row === 0 && a6.col === 0);

// 上边界：第1行 ↑ 不放行
await page.keyboard.press('ArrowUp');
let a7 = await activeCell();
check('第1行 ↑ 不越界', a7.row === 0 && a7.col === 0);

// 下边界：末行 ↓ 不放行
const lastInputs = page.locator('.attendance-grid tbody tr').last().locator('input.attendance-cell-input');
await lastInputs.nth(0).click();
await page.keyboard.press('ArrowDown');
let a8 = await activeCell();
const lastRow = rowCount - 1;
check(`末行(行${lastRow}) ↓ 不越界`, a8.row === lastRow && a8.col === 0);

// --- 聚焦全选（有值时）---
console.log(`\n=== 聚焦全选 ===`);
// 给 (0,0) 填 5
await row0Inputs.nth(0).click();
await row0Inputs.nth(0).fill('5');
await row0Inputs.nth(0).blur();
await page.waitForTimeout(400);
// 点 (0,1) 再 ← 回 (0,0)，应全选 "5"
await row0Inputs.nth(1).click();
await page.keyboard.press('ArrowLeft');
await page.waitForTimeout(200);
let s1 = await activeCell();
check(`方向键回有值格全选（sel=${s1.sel}, val=${s1.val}）`, s1.row === 0 && s1.col === 0 && s1.sel === '0,1');

// --- 表头 sticky 锁定 ---
console.log(`\n=== 表头 sticky 锁定 ===`);
const stickyBefore = await page.evaluate(() => {
  const sc = document.querySelector('.attendance-scroll');
  const th = document.querySelector('.attendance-grid thead th');
  const rec = sc.getBoundingClientRect();
  const thr = th.getBoundingClientRect();
  return { scrollTop: sc.scrollTop, canScroll: sc.scrollHeight > sc.clientHeight, containerTop: rec.top, thTop: thr.top, diff: thr.top - rec.top };
});
check(`容器可垂直滚动（scrollHeight>clientHeight）`, stickyBefore.canScroll);
check(`滚动前表头贴容器顶（Δ=${stickyBefore.diff.toFixed(1)}px）`, Math.abs(stickyBefore.diff) < 2);

await page.evaluate(() => {
  const sc = document.querySelector('.attendance-scroll');
  sc.scrollTop = 500;
  sc.scrollLeft = 200;
});
await page.waitForTimeout(300);

const stickyAfter = await page.evaluate(() => {
  const sc = document.querySelector('.attendance-scroll');
  const th = document.querySelector('.attendance-grid thead th');
  const rec = sc.getBoundingClientRect();
  const thr = th.getBoundingClientRect();
  const firstCell = document.querySelector('.attendance-grid tbody tr td'); // sticky-left 工号列
  return {
    scrollTop: sc.scrollTop, scrollLeft: sc.scrollLeft,
    containerTop: rec.top, thTop: thr.top, diff: thr.top - rec.top,
    thLeft: thr.left,
    firstCellLeft: firstCell.getBoundingClientRect().left,
    bodyLeft: rec.left,
  };
});
check(`滚动后表头仍贴容器顶（Δ=${stickyAfter.diff.toFixed(1)}px）`, Math.abs(stickyAfter.diff) < 2);
check(`横向滚动后 sticky-left 工号列贴容器左`, Math.abs(stickyAfter.firstCellLeft - stickyAfter.bodyLeft) < 2);
await page.screenshot({ path: 'playwright-tests/attendance-nav-sticky.png', fullPage: false });

// 清理刚输入的测试值（避免污染真库月度数据）
await row0Inputs.nth(0).click();
await row0Inputs.nth(0).fill('');
await row0Inputs.nth(0).blur();
await page.waitForTimeout(400);

console.log(`\n=== 结果 ===`);
const pass = checks.filter(([, ok]) => ok).length;
checks.forEach(([name, ok]) => console.log(`  ${ok ? '✓' : '✗'} ${name}`));
console.log(`\n=== 汇总: ${pass}/${checks.length} ===`);
await browser.close();
if (pass !== checks.length) process.exit(1);
