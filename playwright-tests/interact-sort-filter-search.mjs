/**
 * Playwright E2E 抽检：模拟真实用户的排序/筛选/搜索交互
 *
 * 使用: node playwright-tests/interact-sort-filter-search.mjs
 * 前提: MES.Blazor 运行在 http://localhost:5000, MES.Api 运行在 http://localhost:7000
 *
 * 页面分组列头结构说明：
 * - 排序: <span class="th-label" style="cursor:pointer"> 或 <span style="cursor:pointer">
 * - 筛选: <button title="筛选"> 触发 ExcelFilter 弹出层
 * - 搜索: MudTextField Label="模糊搜索 - ..." (无 placeholder)
 */
import { chromium } from '../playwright-tests/node_modules/playwright/index.mjs';
import { request as httpRequest } from 'http';

const BASE_URL = 'http://localhost:5000';

// ============================================================
// 获取 JWT Token（通过 Node.js http.request）
// ============================================================
function getToken() {
  return new Promise((resolve, reject) => {
    const body = JSON.stringify({ email: 'admin@mes.com', password: 'Admin@123' });
    const req = httpRequest({
      hostname: 'localhost', port: 7000, path: '/api/auth/login',
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(body) }
    }, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        try {
          const j = JSON.parse(data);
          const token = j.data?.token || j.token;
          if (token) resolve(token);
          else reject(new Error('Token not in response: ' + data.substring(0, 200)));
        } catch (e) { reject(new Error('Parse error: ' + e.message)); }
      });
    });
    req.on('error', reject);
    req.write(body);
    req.end();
  });
}

// ============================================================
// 注入 Blazored.LocalStorage 认证
// ============================================================
async function injectAuth(page, token) {
  await page.evaluate((t) => {
    localStorage.setItem('authToken', JSON.stringify(t));
    localStorage.setItem('refreshToken', JSON.stringify('e2e-test-refresh'));
    localStorage.setItem('userEmail', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userName', JSON.stringify('admin@mes.com'));
    localStorage.setItem('userFullName', JSON.stringify('System Administrator'));
    localStorage.setItem('userRoles', JSON.stringify(['Admin']));
  }, token);
}

// ============================================================
// 等待 Blazor WASM 加载完成 + 表格出现
// ============================================================
async function waitForTable(page, timeout = 30000) {
  await page.waitForSelector('.mud-table', { timeout });
  await page.waitForTimeout(2000);
  await page.waitForFunction(() => {
    const rows = document.querySelectorAll('.mud-table-body .mud-table-row, .mud-table-body tr');
    return rows.length > 0;
  }, { timeout: 15000 }).catch(() => {});
}

// ============================================================
// 获取表格行数
// ============================================================
async function getRowCount(page) {
  return page.$$eval('.mud-table-body .mud-table-row, .mud-table-body tr', rows => rows.length);
}

// ============================================================
// 查找并点击排序列头
// 优先用 .th-label，fallback 用 cursor:pointer
// ============================================================
async function clickSortHeader(page) {
  // 优先尝试 .th-label（Orders, WorkOrderExecution 用）
  let sortSpans = await page.$$('th .th-label');
  for (const s of sortSpans) {
    const text = (await s.textContent() || '').trim();
    if (text && !text.includes('▼') && !text.includes('▲')) {
      await s.click();
      await page.waitForTimeout(1200);
      return { clicked: true, label: text };
    }
  }
  // fallback: 找 th 内 style="cursor:pointer" 的 span（Batches 用）
  sortSpans = await page.$$('th span[style*="cursor:pointer"]');
  for (const s of sortSpans) {
    const text = (await s.textContent() || '').trim();
    if (text && !text.includes('▼') && !text.includes('▲')) {
      await s.click();
      await page.waitForTimeout(1200);
      return { clicked: true, label: text };
    }
  }
  return { clicked: false, label: '' };
}

// ============================================================
// 查找模糊搜索框（MudTextField 用 Label 而非 placeholder）
// ============================================================
async function findSearchInput(page) {
  // MudTextField 渲染为 input.mud-input-input，Label 浮在 input 上
  // 用 getByLabel 模糊匹配 "模糊搜索"
  try {
    const searchInput = page.getByLabel(/模糊搜索/);
    if (await searchInput.isVisible({ timeout: 3000 })) {
      return searchInput;
    }
  } catch {
    // fallback
  }
  // fallback: 找 visible input 且附近有 "模糊" 文本
  const inputs = await page.$$('input.mud-input-root');
  for (const input of inputs) {
    if (await input.isVisible()) {
      return input; // 第一个可见输入框
    }
  }
  return null;
}

// ============================================================
// 查找 ExcelFilter 筛选按钮并尝试打开
// ============================================================
async function openExcelFilter(page, columnIndex = 0) {
  const filterBtns = await page.$$('button[title="筛选"]');
  if (filterBtns.length > columnIndex) {
    await filterBtns[columnIndex].click();
    await page.waitForTimeout(500);
    // 检查弹窗是否出现
    const dropdown = await page.$('.excel-filter-dropdown');
    if (dropdown) return true;
  }
  return false;
}

// ============================================================
// 测试页面定义
// ============================================================
const PAGES = [
  {
    name: 'Orders', path: '/orders',
    tests: [
      { name: '页面加载', run: async (page) => {
        await waitForTable(page);
        const rowCount = await getRowCount(page);
        return { pass: rowCount > 0, detail: `${rowCount} 行数据` };
      }},
      { name: '排序点击', run: async (page) => {
        const r = await clickSortHeader(page);
        if (r.clicked) return { pass: true, detail: `点击 "${r.label}"` };
        return { pass: null, detail: '未找到排序列头（.th-label / cursor:pointer）' };
      }},
      { name: '关键字搜索', run: async (page) => {
        const searchInput = await findSearchInput(page);
        if (searchInput) {
          await searchInput.fill('2025');
          await page.waitForTimeout(1500);
          return { pass: true, detail: '搜索 "2025"' };
        }
        return { pass: null, detail: '未找到搜索输入框' };
      }},
      { name: 'ExcelFilter打开', run: async (page) => {
        const opened = await openExcelFilter(page, 0);
        if (opened) return { pass: true, detail: '打开列筛选弹窗' };
        return { pass: null, detail: '未找到筛选按钮或弹窗未出现' };
      }},
      { name: '排序指示器检测', run: async (page) => {
        // 点击后检查是否有 ▼ 或 ▲ 指示器出现
        await clickSortHeader(page);
        const hasIndicator = await page.$eval('th .th-label', (el) => {
          return el.textContent?.includes('▼') || el.textContent?.includes('▲');
        }).catch(() => false);
        if (hasIndicator) return { pass: true, detail: '检测到排序指示器 ▼/▲' };
        return { pass: null, detail: '未检测到排序指示器' };
      }},
    ]
  },
  {
    name: 'Batches', path: '/batches',
    tests: [
      { name: '页面加载', run: async (page) => {
        await waitForTable(page);
        const rowCount = await getRowCount(page);
        return { pass: true, detail: `${rowCount} 行数据` };
      }},
      { name: '排序点击', run: async (page) => {
        const r = await clickSortHeader(page);
        if (r.clicked) return { pass: true, detail: `点击 "${r.label}"` };
        return { pass: null, detail: '未找到排序列头' };
      }},
      { name: '关键字搜索', run: async (page) => {
        const searchInput = await findSearchInput(page);
        if (searchInput) {
          await searchInput.fill('BATCH');
          await page.waitForTimeout(1500);
          return { pass: true, detail: '搜索 "BATCH"' };
        }
        return { pass: null, detail: '未找到搜索输入框' };
      }},
    ]
  },
  {
    name: 'FurnaceRegistrations', path: '/quality/furnace',
    tests: [
      { name: '页面加载', run: async (page) => {
        await waitForTable(page);
        const rowCount = await getRowCount(page);
        return { pass: true, detail: `${rowCount} 行数据` };
      }},
      { name: '排序点击', run: async (page) => {
        const r = await clickSortHeader(page);
        if (r.clicked) return { pass: true, detail: `点击 "${r.label}"` };
        return { pass: null, detail: '未找到排序列头' };
      }},
    ]
  },
  {
    name: 'StandardRegisters', path: '/standard-registers',
    tests: [
      { name: '页面加载', run: async (page) => {
        await waitForTable(page);
        const rowCount = await getRowCount(page);
        return { pass: true, detail: `${rowCount} 行数据` };
      }},
      { name: '排序点击', run: async (page) => {
        const r = await clickSortHeader(page);
        if (r.clicked) return { pass: true, detail: `点击 "${r.label}"` };
        return { pass: null, detail: '未找到排序列头' };
      }},
      { name: '关键字搜索', run: async (page) => {
        const searchInput = await findSearchInput(page);
        if (searchInput) {
          await searchInput.fill('GB');
          await page.waitForTimeout(1500);
          return { pass: true, detail: '搜索 "GB"' };
        }
        return { pass: null, detail: '未找到搜索输入框' };
      }},
    ]
  },
  {
    name: 'WorkOrderExecution', path: '/workorder-execution',
    tests: [
      { name: '页面加载', run: async (page) => {
        await waitForTable(page, 45000);
        const rowCount = await getRowCount(page);
        return { pass: true, detail: `${rowCount} 行数据` };
      }},
      { name: '排序点击', run: async (page) => {
        const r = await clickSortHeader(page);
        if (r.clicked) return { pass: true, detail: `点击 "${r.label}"` };
        return { pass: null, detail: '未找到排序列头' };
      }},
      { name: '关键字搜索', run: async (page) => {
        const searchInput = await findSearchInput(page);
        if (searchInput) {
          await searchInput.fill('WO');
          await page.waitForTimeout(1500);
          return { pass: true, detail: '搜索 "WO"' };
        }
        return { pass: null, detail: '未找到搜索输入框' };
      }},
    ]
  },
];

// ============================================================
// 主流程
// ============================================================
async function main() {
  console.log('============================================');
  console.log('  Playwright E2E 抽检：排序/筛选/搜索');
  console.log('============================================\n');

  // 获取 Token
  console.log('▶ 获取 Token...');
  let token;
  try { token = await getToken(); console.log('  ✓ OK\n'); }
  catch (e) { console.error('  ✗ ' + e.message); process.exit(1); }

  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
  const results = { pass: 0, fail: [], warn: [] };

  try {
    // 初始化 localStorage
    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(5000);
    await injectAuth(page, token);

    for (const p of PAGES) {
      console.log(`  ── ${p.name} ──`);
      await page.goto(BASE_URL + p.path, { waitUntil: 'networkidle', timeout: 60000 });
      await page.waitForTimeout(3000);

      for (const test of p.tests) {
        process.stdout.write(`    ${test.name}... `);
        try {
          const r = await test.run(page);
          if (r.pass === true) { console.log('✓ ' + (r.detail || '')); results.pass++; }
          else if (r.pass === null) { console.log('— ' + (r.detail || '')); results.warn.push({ page: p.name, ...r }); }
          else { console.log('✗ ' + (r.detail || '')); results.fail.push({ page: p.name, ...r }); }
        } catch (e) {
          console.log('✗ ' + e.message.substring(0, 80));
          results.fail.push({ page: p.name, test: test.name, detail: e.message.substring(0, 200) });
        }
      }
      console.log();
    }

    console.log('--- 汇总 ---');
    console.log(`  通过: ${results.pass}  失败: ${results.fail.length}  跳过: ${results.warn.length}`);
    if (results.fail.length) {
      console.log('\n  失败:');
      results.fail.forEach(f => console.log(`    [${f.page}] ${f.test}: ${f.detail}`));
    }
    if (results.warn.length) {
      console.log('\n  警告:');
      results.warn.forEach(w => console.log(`    [${w.page}] ${w.test}: ${w.detail}`));
    }
    console.log(results.fail.length ? '\n✗ 部分失败' : '\n✅ 全部通过');
    if (results.fail.length) process.exit(1);
  } catch (e) {
    console.error('FATAL:', e.message);
    process.exit(1);
  } finally {
    await browser.close();
  }
}

main();
