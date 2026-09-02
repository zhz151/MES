/**
 * 员工管理列表显示 vs 参数表(DictValueDefinitions) DisplayName 一致性验证
 *
 * 验证点：
 *  1. fetch 参数表 display-map，取 PositionKey / PositionCategoryKey 的 DisplayName
 *  2. fetch 员工列表第一行，取 position / department 英文 Key
 *  3. playwright 读取员工列表岗位列/岗位类别列实际显示文本
 *  4. 对比：列表显示文本 是否 == 参数表里该 Key 的 DisplayName
 *
 * 使用: node playwright-tests/check-employee-dict-display.mjs
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

const token = await getToken();
const auth = { Authorization: `Bearer ${token}` };

// 1. 参数表 display-map
const mapRes = await fetch(`${API}/api/dict-value-definition/display-map`, { headers: auth });
const mapJson = await mapRes.json();
const map = mapJson.data || mapJson;
const positionMap = map['PositionKey'] || {};
const categoryMap = map['PositionCategoryKey'] || {};

// 2. 员工列表第一行（与页面一致的 Code 升序排序）
const empRes = await fetch(`${API}/api/employee/list?pageIndex=1&pageSize=1&sortBy=code&isDescending=false`, { headers: auth });
const empJson = await empRes.json();
const first = empJson.data?.items?.[0];
console.log('=== 参数表(display-map) 抽样 ===');
console.log(`  PositionKey 行数=${Object.keys(positionMap).length}`);
console.log(`  PositionKey 样本=${Object.entries(positionMap).slice(0, 4).map(([k, v]) => `${k}=>${v}`).join(', ')}`);
console.log(`  PositionCategoryKey 行数=${Object.keys(categoryMap).length}`);
console.log(`  PositionCategoryKey 样本=${Object.entries(categoryMap).map(([k, v]) => `${k}=>${v}`).join(', ')}`);

if (!first) { console.log('员工列表为空，无法对比'); process.exit(1); }
console.log(`\n=== 员工列表第一行（英文存值） ===`);
console.log(`  工号=${first.code} 姓名=${first.name} position=${first.position} department=${first.department} salaryMode=${first.salaryMode}`);
console.log(`  参数表里 position 对应显示=${positionMap[first.position] ?? '(未找到!)'}`);
console.log(`  参数表里 department 对应显示=${categoryMap[first.department] ?? '(未找到!)'}`);

// 3. playwright 读列表实际显示文本
const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1700, height: 950 } });
const page = await ctx.newPage();
page.on('pageerror', (err) => console.log('  [pageerror]', err.message));
// 模拟「已登录用户整页打开」：先注入 token 再整页加载 /employees（MainLayout 首次初始化时已认证）
await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
await page.evaluate((t) => {
  localStorage.setItem('authToken', JSON.stringify(t));
  localStorage.setItem('refreshToken', JSON.stringify('emp-dict'));
  localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
  localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
  localStorage.setItem('userRoles', JSON.stringify(['Admin']));
}, token);
await page.goto(BASE + '/employees', { waitUntil: 'networkidle', timeout: 90000 });
await page.waitForTimeout(6000);

const body = await page.evaluate(() => document.body?.textContent || '');
const crashed = body.includes('no idea on how to unbox') || body.includes('An unhandled error');
console.log(`\n=== 列表实际显示（playwright） ===`);
console.log(`  ${crashed ? '✗' : '✓'} 页面无崩溃`);
if (crashed) process.exit(1);

const cols = await page.evaluate(() => {
  const ths = [...document.querySelectorAll('.mud-table-root thead th')];
  const row = document.querySelector('.mud-table-root tbody tr.mud-table-row');
  const tds = row ? [...row.querySelectorAll('td')] : [];
  const out = [];
  for (let i = 0; i < ths.length && i < tds.length; i++) {
    const label = (ths[i].textContent || '').replace(/[▼▲\s]/g, '');
    const val = (tds[i].textContent || '').trim();
    if (['岗位', '岗位类别', '工资结算模式'].includes(label)) out.push({ label, val });
  }
  return out;
});
for (const c of cols) console.log(`  「${c.label}」列显示="${c.val}"`);

// 4. 对比
let pass = 0, fail = 0;
for (const c of cols) {
  if (c.label === '岗位') {
    const expected = positionMap[first.position] ?? '';
    const ok = c.val === expected;
    console.log(`  ${ok ? '✓' : '✗'} 岗位列显示=="${c.val}" 参数表=="${expected}"`);
    ok ? pass++ : fail++;
  }
  if (c.label === '岗位类别') {
    const expected = categoryMap[first.department] ?? '';
    const ok = c.val === expected;
    console.log(`  ${ok ? '✓' : '✗'} 岗位类别列显示=="${c.val}" 参数表=="${expected}"`);
    ok ? pass++ : fail++;
  }
}

await page.screenshot({ path: 'playwright-tests/employee-dict-display.png', fullPage: false });
await browser.close();
console.log(`\n=== 汇总: 通过 ${pass}, 失败 ${fail} ===`);
if (fail > 0) process.exit(1);
