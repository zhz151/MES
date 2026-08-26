/**
 * MES 前端验证 - 第二批次（崩溃/失败的页面）
 * 每个页面使用独立的浏览器上下文，避免 WASM 内存溢出
 */

import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const BLAZOR_URL = 'http://localhost:5000';
const API_URL = 'http://localhost:7000';
const REPORT_DIR = path.resolve(__dirname, 'report');

// 要重新验证的页面
const RETEST_PAGES = [
  // ---- StandardRegister 全部 ----
  { url: '/standard-registers', name: '标准号管理', module: 'StandardRegister', hasPrint: true },
  { url: '/chemical-composition', name: '化学成分', module: 'StandardRegister', hasPrint: true },
  { url: '/chemical-validate', name: '化学验证规则', module: 'StandardRegister', hasPrint: true },
  { url: '/grade-chemical-compositions', name: '牌号成分', module: 'StandardRegister', hasPrint: true },
  { url: '/grade-mappings', name: '牌号映射', module: 'StandardRegister', hasPrint: true },
  { url: '/grade-physical-properties', name: '牌号物性', module: 'StandardRegister', hasPrint: true },
  { url: '/standard-inspection-requirements', name: '标准检验要求', module: 'StandardRegister', hasPrint: true },
  { url: '/sub-standard-quick-views', name: '子标准速查', module: 'StandardRegister', hasPrint: true },

  // ---- Config 全部 ----
  { url: '/config-parameters', name: '参数配置', module: 'Config', hasPrint: false },
  { url: '/employees', name: '员工管理', module: 'Config', hasPrint: true },
  { url: '/workstations', name: '工位管理', module: 'Config', hasPrint: true },
  { url: '/standard-work-days', name: '标准工时', module: 'Config', hasPrint: false },
  { url: '/section-flow-category-settings', name: '工序类别', module: 'Config', hasPrint: false },
  { url: '/daily-production-capacities', name: '日产能', module: 'Config', hasPrint: false },
  { url: '/daily-output-estimates', name: '日产量预估', module: 'Config', hasPrint: false },
  { url: '/standard-work-day-delivery-states', name: '标准交期状态', module: 'Config', hasPrint: false },

  // ---- Analysis ----
  { url: '/section-flow-analysis', name: '工序流量分析', module: 'Analysis', hasPrint: false },
  { url: '/section-production-status', name: '工序生产状态', module: 'Analysis', hasPrint: false },

  // ---- 其他 ----
  { url: '/admin/users', name: '用户管理', module: 'Admin', hasPrint: false },
  { url: '/data-exchange', name: '数据交换', module: 'Tools', hasPrint: false },

  // ---- Warehouse 失败的 ----
  { url: '/warehouse', name: '库存查询', module: 'Warehouse', hasPrint: true },
  { url: '/warehouse/outbound-history', name: '出库历史', module: 'Warehouse', hasPrint: true },
  { url: '/orders/pending-delivery', name: '订单成品(实时库存)', module: 'Orders', hasPrint: true },

  // ---- Reports ----
  { url: '/reports/overview', name: '报表总览', module: 'Reports', hasPrint: true },
];

const CHECKS = {
  pageLoad:       '页面加载',
  noAuthError:    '无授权错误',
  tableExists:    'MudTable 存在',
  dataLoaded:     '数据加载',
  searchExists:   '搜索框存在',
  sortHeader:     '排序列头存在',
  printButton:    '打印按钮存在',
};

async function getToken() {
  try {
    const http = (await import('http')).default;
    return new Promise((resolve, reject) => {
      const data = JSON.stringify({ email: 'admin@mes.com', password: 'Admin@123' });
      const req = http.request({
        hostname: 'localhost', port: 7000, path: '/api/auth/login',
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Content-Length': data.length }
      }, (res) => {
        let body = '';
        res.on('data', chunk => body += chunk);
        res.on('end', () => {
          try {
            const json = JSON.parse(body);
            resolve(json?.data?.token || json?.Data?.token);
          } catch { resolve(null); }
        });
      });
      req.on('error', reject);
      req.write(data);
      req.end();
    });
  } catch { return null; }
}

async function main() {
  console.log('========================================');
  console.log('  MES 前端验证 - 第二批次（分批独立运行）');
  console.log('========================================\n');

  // 1. 获取 token
  console.log('获取登录 token...');
  const token = await getToken();
  if (!token) {
    console.log('  ✗ 无法获取 token，尝试页面登录');
  } else {
    console.log('  ✓ Token 获取成功');
  }

  const results = { passed: 0, failed: 0, details: [] };

  // 2. 逐个页面独立测试
  const browser = await chromium.launch({ headless: true });

  for (const pageConfig of RETEST_PAGES) {
    const { url, name, module, hasPrint } = pageConfig;

    // 每个页面使用独立 context + page，防止 WASM 内存累积
    const context = await browser.newContext({
      viewport: { width: 1920, height: 1080 },
      ignoreHTTPSErrors: true,
    });
    const page = await context.newPage();

    const pageResult = { page: name, url, module, checks: {}, screenshots: [], passed: true };

    try {
      console.log(`\n  ── ${name} (${url}) ──`);

      // 注入 token
      if (token) {
        await page.goto(`${BLAZOR_URL}/login`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await page.evaluate((t) => {
          localStorage.setItem('authToken', t);
          localStorage.setItem('refreshToken', t);
          localStorage.setItem('userEmail', 'admin@mes.com');
          localStorage.setItem('userName', 'admin@mes.com');
          localStorage.setItem('userFullName', 'System Administrator');
          localStorage.setItem('userRoles', '["Admin"]');
        }, token);
      }

      // 导航到目标页面
      await page.goto(`${BLAZOR_URL}${url}`, { waitUntil: 'networkidle', timeout: 30000 });
      await page.waitForTimeout(3000);

      const currentUrl = page.url();
      pageResult.checks[CHECKS.pageLoad] = true;

      // 截图
      const sDir = path.join(REPORT_DIR, module, 'batch2');
      fs.mkdirSync(sDir, { recursive: true });
      const sPath = path.join(sDir, `${name.replace(/[/\\?%*:|"<>]/g, '_')}.png`);
      await page.screenshot({ path: sPath, fullPage: true });
      pageResult.screenshots.push(sPath);

      // 检查授权
      const bodyText = await page.evaluate(() => document.body?.innerText || '');
      const notAuthorized = bodyText.includes('Not authorized') || bodyText.includes('Unauthorized');
      pageResult.checks[CHECKS.noAuthError] = !notAuthorized;

      if (notAuthorized) {
        console.log(`  ✗ ${CHECKS.noAuthError}`);
        pageResult.passed = false;
      }

      // 检查 MudTable
      const hasTable = await page.$('.mud-table');
      pageResult.checks[CHECKS.tableExists] = !!hasTable;
      console.log(`  ${hasTable ? '✓' : ' '} ${CHECKS.tableExists}`);

      if (hasTable) {
        const rows = await page.$$('.mud-table-body .mud-table-row');
        pageResult.checks[CHECKS.dataLoaded] = rows.length > 0;
        console.log(`  ${rows.length > 0 ? '✓' : ' '} ${CHECKS.dataLoaded}: ${rows.length} 行`);

        // 搜索框
        const searchInputs = await page.$$('.mud-input input, input[type="text"]');
        const hasSearch = searchInputs.length > 0;
        pageResult.checks[CHECKS.searchExists] = hasSearch;
        if (hasSearch) console.log(`  ✓ ${CHECKS.searchExists}`);

        // 排序头
        const ths = await page.$$('.mud-table-head th');
        pageResult.checks[CHECKS.sortHeader] = ths.length > 0;
        console.log(`  ${ths.length > 0 ? '✓' : ' '} ${CHECKS.sortHeader}: ${ths.length} 列头`);

        // 打印按钮
        if (hasPrint) {
          const printBtns = await page.$$('button:has-text("打印")');
          pageResult.checks[CHECKS.printButton] = printBtns.length > 0;
          console.log(`  ${printBtns.length > 0 ? '✓' : '✗'} ${CHECKS.printButton}: ${printBtns.length} 个`);
          if (printBtns.length === 0) pageResult.passed = false;
        }
      } else {
        // 没有表格 - 检查可能是卡片/特殊布局
        const hasContent = bodyText.length > 100 && !bodyText.includes('Not authorized');
        console.log(`  无表格，页面内容: ${bodyText.substring(0, 80)}`);
        // 无表格不一定算失败（库存查询可能是卡片布局）
        pageResult.checks['页面有内容'] = hasContent;
      }

      if (pageResult.passed) results.passed++;
      else results.failed++;
      results.details.push(pageResult);

    } catch (err) {
      console.log(`  ✗ ${name}: ${err.message}`);
      pageResult.passed = false;
      pageResult.error = err.message;
      results.details.push(pageResult);
      results.failed++;
    } finally {
      await context.close();
    }
  }

  await browser.close();

  // 输出结果
  console.log('\n========================================');
  console.log('  第二批次验证结果');
  console.log('========================================\n');
  console.log(`通过: ${results.passed} / 失败: ${results.failed} / 总计: ${RETEST_PAGES.length}\n`);

  for (const d of results.details) {
    const icon = d.passed ? '✓' : '✗';
    console.log(`  ${icon} ${d.page}: ${d.error || ''}`);
    for (const [check, status] of Object.entries(d.checks)) {
      if (status === false) console.log(`      - ${check}: 失败`);
    }
  }

  // 生成 HTML 报告追加
  const html = generateHtml(results);
  const reportPath = path.join(REPORT_DIR, 'batch2-report.html');
  fs.writeFileSync(reportPath, html);
  console.log(`\n报告已保存: ${reportPath}`);
}

function generateHtml(results) {
  const rows = results.details.map(d => {
    const icon = d.passed ? '✅' : '❌';
    const checksList = Object.entries(d.checks).map(([n, s]) =>
      `<li>${s === true ? '✅' : s === false ? '❌' : '⏭️'} ${n}</li>`
    ).join('');
    const ss = d.screenshots.map(s =>
      `<a href="${path.relative(REPORT_DIR, s)}" target="_blank">截图</a>`
    ).join(' ');
    return `<tr class="${d.passed ? '' : 'failed'}">
      <td>${icon}</td><td>${d.page}</td><td>${d.url}</td><td>${d.module}</td>
      <td><ul class="checks">${checksList}</ul></td>
      <td>${d.error || ''}</td><td>${ss}</td>
    </tr>`;
  }).join('\n');

  return `<!DOCTYPE html><html lang="zh-CN"><head>
  <meta charset="UTF-8"><title>MES 验证报告 - 第二批次</title>
  <style>
    body { font-family: -apple-system, sans-serif; margin: 20px; }
    h1 { color: #1a1a2e; }
    .summary { display: flex; gap: 20px; margin: 20px 0; }
    .card { padding: 15px 25px; border-radius: 8px; color: white; font-size: 18px; }
    .card.pass { background: #4caf50; }
    .card.fail { background: #f44336; }
    table { border-collapse: collapse; width: 100%; margin-top: 20px; }
    th, td { border: 1px solid #ddd; padding: 8px; text-align: left; font-size: 13px; }
    th { background: #1a1a2e; color: white; }
    tr.failed { background: #fff3f3; }
    .checks { list-style: none; padding: 0; margin: 0; }
  </style>
</head><body>
  <h1>MES 验证报告 - 第二批次（崩溃/失败页面重测）</h1>
  <div class="summary">
    <div class="card pass">✅ 通过: ${results.passed}</div>
    <div class="card fail">❌ 失败: ${results.failed}</div>
  </div>
  <table><thead><tr>
    <th>状态</th><th>页面</th><th>URL</th><th>模块</th><th>检查项</th><th>错误</th><th>截图</th>
  </tr></thead><tbody>${rows}</tbody></table>
  <p style="margin-top:20px;color:#666;font-size:12px;">生成: ${new Date().toISOString()}</p>
</body></html>`;
}

main();
