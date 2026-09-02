/**
 * 验证：列表非编辑态操作员/检验员/确认人列显示纯姓名（无「姓名(工号)」）
 * 前提: MES.Api http://localhost:7000, MES.Blazor http://localhost:5000
 */
import { chromium } from './node_modules/playwright/index.mjs';

const API = 'http://localhost:7000';
const BASE = 'http://localhost:5000';

const PAGES = [
  { name: '生产记录',  path: '/production-records',            label: '操作人' },
  { name: '酸洗入',    path: '/pickling-in-records',           label: '操作人' },
  { name: '酸洗出',    path: '/pickling-out-records',          label: '操作人' },
  { name: '过程检验',  path: '/quality/process-inspection',    label: '检验员' },
  { name: '成检',      path: '/quality/final-inspection',      label: '操作员' },
  { name: '成检到料',  path: '/quality/material-receive-checks', label: '确认人' },
  { name: '设备检验',  path: '/inspection-records',            label: '点检人' },
  { name: '质量追踪',  path: '/quality/process-tracking',      label: '确认人' },
];

async function getToken() {
  const res = await fetch(`${API}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: 'admin@mes.com', password: 'Admin@123' }),
  });
  const data = await res.json();
  return data.data?.token || data.token;
}

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1600, height: 900 } });
const page = await ctx.newPage();
page.on('pageerror', (err) => console.log('  [pageerror]', err.message));

const token = await getToken();
await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
await page.evaluate((t) => {
  localStorage.setItem('authToken', JSON.stringify(t));
  localStorage.setItem('refreshToken', JSON.stringify('verify'));
  localStorage.setItem('userRoles', JSON.stringify(['Admin']));
}, token);

// 匹配「姓名(工号)」或「姓名(code)」：中文名 + 括号内容
const NAME_CODE_RE = /[\u4e00-\u9fa5]{1,6}\s*\([^()]+\)/;

let pass = 0, fail = 0;
for (const p of PAGES) {
  try {
    await page.goto(BASE + p.path, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(4000);
    const result = await page.evaluate((label) => {
      const ths = [...document.querySelectorAll('thead th')];
      let colIdx = -1;
      for (let i = 0; i < ths.length; i++) {
        const t = (ths[i].textContent || '').trim();
        if (t === label || t.includes(label)) { colIdx = i; break; }
      }
      if (colIdx < 0) return { found: false, samples: [], count: 0 };
      const cells = [];
      document.querySelectorAll('tbody tr').forEach((tr) => {
        const tds = tr.querySelectorAll('td');
        if (tds.length > colIdx) cells.push((tds[colIdx].textContent || '').trim());
      });
      const samples = cells.filter((c) => c && c !== '');
      return { found: true, samples: samples.slice(0, 5), count: cells.length };
    }, p.label);
    if (!result.found) {
      console.log(`  ✗ ${p.name} (${p.path}): 未找到列「${p.label}」`);
      fail++;
      continue;
    }
    const bad = result.samples.filter((s) => NAME_CODE_RE.test(s));
    const ok = result.count === 0 || bad.length === 0;
    if (ok) {
      console.log(`  ✓ ${p.name} (${p.path}): ${result.count} 行操作员列无工号; 示例=[${result.samples.slice(0, 3).join(' | ') || '(空)'}]`);
      pass++;
    } else {
      console.log(`  ✗ ${p.name} (${p.path}): 发现工号残留 ${JSON.stringify(bad)}`);
      fail++;
    }
  } catch (e) {
    console.log(`  ✗ ${p.name}: ${e.message}`);
    fail++;
  }
}

await browser.close();
console.log(`\n=== 汇总: 通过 ${pass}, 失败 ${fail} ===`);
if (fail > 0) process.exit(1);
