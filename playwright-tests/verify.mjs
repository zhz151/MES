/**
 * MES 前端自动化验证脚本
 * 覆盖所有列表页的共性检查项 + 截图归档
 *
 * 使用: node playwright-tests/verify.mjs
 * 前置条件: 需启动 API (port 7000) + Blazor (port 5000)
 */

import { chromium } from 'playwright';
import { spawn } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import http from 'http';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');
const API_PORT = 7000;
const BLAZOR_PORT = 5000;
const API_URL = `http://localhost:${API_PORT}`;
const BLAZOR_URL = `http://localhost:${BLAZOR_PORT}`;
const REPORT_DIR = path.resolve(__dirname, 'report');

// ============================================================
// 页面配置
// ============================================================
const LIST_PAGES = [
  // ---- 订单管理 ----
  { url: '/orders',             name: '订单管理',         module: 'Orders', hasPrint: true, hasSearch: true, hasDateFilter: true },
  { url: '/customers',          name: '客户管理',         module: 'Orders', hasPrint: true, hasSearch: true },

  // ---- 工单管理 ----
  { url: '/workorders',         name: '工单列表',         module: 'WorkOrders', hasPrint: true, hasSearch: true },
  { url: '/workorder-execution',name: '工单执行概览',     module: 'WorkOrders', hasPrint: false, hasSearch: true },
  { url: '/material-plan-overview', name: '用料计划概览', module: 'WorkOrders', hasPrint: false, hasSearch: true },
  { url: '/workorders-demand-adjustment', name: '工单需求调整', module: 'WorkOrders', hasPrint: false, hasSearch: true },

  // ---- 计划排程 ----
  { url: '/batch-plans',        name: '批次计划',         module: 'Scheduling', hasPrint: false, hasSearch: true },
  { url: '/cold-roll-plans',    name: '冷轧计划',         module: 'Scheduling', hasPrint: false, hasSearch: true },
  { url: '/final-inspection-plan', name: '成品检计划',   module: 'Scheduling', hasPrint: false, hasSearch: true },
  { url: '/raw-material-lock-plan', name: '原材料锁定计划', module: 'Scheduling', hasPrint: false, hasSearch: true },
  { url: '/scheduling-plans',   name: '排程总览',         module: 'Scheduling', hasPrint: false, hasSearch: true },

  // ---- 批次管理 ----
  { url: '/batches',            name: '批次列表',         module: 'Batches', hasPrint: true, hasSearch: true },
  { url: '/section-outsources', name: '工序外协',         module: 'Batches', hasPrint: true, hasSearch: true },
  { url: '/production-records', name: '生产记录',         module: 'Batches', hasPrint: false, hasSearch: true },
  { url: '/pickling-in-records',name: '酸洗入记录',       module: 'Batches', hasPrint: false, hasSearch: true },
  { url: '/pickling-out-records',name: '酸洗出记录',      module: 'Batches', hasPrint: false, hasSearch: true },
  { url: '/outsource-recoveries',name: '外协回收',        module: 'Batches', hasPrint: false, hasSearch: true },
  { url: '/process-card-print', name: '工艺卡打印',       module: 'Batches', hasPrint: true, hasSearch: true },

  // ---- 质量管理 ----
  { url: '/quality/ncr',        name: 'NCR 不合格品',    module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/final-inspection', name: '成品检验',  module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/material-receive-checks', name: '来料检验', module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/process-inspection', name: '过程检验',module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/furnace',    name: '炉批号登记',       module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/certificates', name: '质量证明书',     module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/process-tracking', name: '质量过程追溯', module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/tensile-test', name: '拉伸试验',       module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/hardness-test', name: '硬度试验',      module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/grain-size-test', name: '晶粒度试验',  module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/flattening-test', name: '压扁试验',    module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/flaring-test', name: '扩口试验',       module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/pitting-corrosion-test', name: '点腐蚀试验', module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/intergranular-corrosion-test', name: '晶间腐蚀试验', module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/metallographic-test', name: '金相试验', module: 'Quality', hasPrint: true, hasSearch: true },
  { url: '/quality/chemical-analysis', name: '化学分析',  module: 'Quality', hasPrint: true, hasSearch: true },

  // ---- 物料管理 ----
  { url: '/materials',          name: '物料列表',         module: 'Materials', hasPrint: true, hasSearch: true },
  { url: '/purchase-orders',    name: '采购订单',         module: 'Materials', hasPrint: true, hasSearch: true },
  { url: '/subcontract-orders', name: '委外订单',         module: 'Materials', hasPrint: true, hasSearch: true },
  { url: '/suppliers',          name: '供应商管理',       module: 'Materials', hasPrint: true, hasSearch: true },

  // ---- 设备管理 ----
  { url: '/equipment',          name: '设备列表',         module: 'Equipment', hasPrint: true, hasSearch: true },
  { url: '/repair-orders',      name: '维修工单',         module: 'Equipment', hasPrint: true, hasSearch: true },
  { url: '/maintenance-orders', name: '保养计划',         module: 'Equipment', hasPrint: true, hasSearch: true },
  { url: '/inspection-records', name: '点检记录',         module: 'Equipment', hasPrint: true, hasSearch: true },

  // ---- 仓库管理 ----
  { url: '/warehouse',          name: '库存查询',         module: 'Warehouse', hasPrint: true, hasSearch: true },
  { url: '/warehouse/inbound-history', name: '入库历史',  module: 'Warehouse', hasPrint: true, hasSearch: true },
  { url: '/warehouse/outbound-history', name: '出库历史', module: 'Warehouse', hasPrint: true, hasSearch: true },
  { url: '/orders/pending-delivery', name: '订单成品(实时库存)', module: 'Orders', hasPrint: true, hasSearch: true },

  // ---- 生产标准 ----
  { url: '/standard-registers', name: '标准号管理',       module: 'StandardRegister', hasPrint: true, hasSearch: true },
  { url: '/chemical-composition', name: '化学成分',       module: 'StandardRegister', hasPrint: true, hasSearch: true },
  { url: '/chemical-validate', name: '化学验证规则',      module: 'StandardRegister', hasPrint: true, hasSearch: true },
  { url: '/grade-chemical-compositions', name: '牌号成分', module: 'StandardRegister', hasPrint: true, hasSearch: true },
  { url: '/grade-mappings',     name: '牌号映射',         module: 'StandardRegister', hasPrint: true, hasSearch: true },
  { url: '/grade-physical-properties', name: '牌号物性',  module: 'StandardRegister', hasPrint: true, hasSearch: true },
  { url: '/standard-inspection-requirements', name: '标准检验要求', module: 'StandardRegister', hasPrint: true, hasSearch: true },
  { url: '/sub-standard-quick-views', name: '子标准速查', module: 'StandardRegister', hasPrint: true, hasSearch: true },

  // ---- 配置管理 ----
  { url: '/config-parameters',  name: '参数配置',         module: 'Config', hasPrint: false, hasSearch: true },
  { url: '/employees',          name: '员工管理',         module: 'Config', hasPrint: true, hasSearch: true },
  { url: '/workstations',       name: '工位管理',         module: 'Config', hasPrint: true, hasSearch: true },
  { url: '/standard-work-days', name: '标准工时',         module: 'Config', hasPrint: false, hasSearch: true },
  { url: '/daily-production-capacities', name: '日产能',  module: 'Config', hasPrint: false, hasSearch: true },
  { url: '/daily-output-estimates', name: '日产量预估',   module: 'Config', hasPrint: false, hasSearch: true },
  { url: '/standard-work-day-delivery-states', name: '标准交期状态', module: 'Config', hasPrint: false, hasSearch: true },

  // ---- 其他 ----
  { url: '/admin/users',        name: '用户管理',         module: 'Admin', hasPrint: false, hasSearch: true },
  { url: '/data-exchange',      name: '数据交换',         module: 'Tools', hasPrint: false, hasSearch: true },

  // ---- 报表系统 ----
  { url: '/reports/overview',   name: '报表总览',         module: 'Reports', hasPrint: true, hasSearch: false, customTable: true },
];

// ============================================================
// 全局结果统计
// ============================================================
const results = {
  totalPages: LIST_PAGES.length,
  passed: 0,
  failed: 0,
  skipped: 0,
  details: [],
  errors: [],
};

// ============================================================
// 检查条件
// ============================================================
const CHECKS = {
  pageLoad:       '页面加载',
  tableExists:    'MudTable 存在',
  dataLoaded:     '数据加载',
  searchExists:   '搜索框存在',
  columnDisplay:  '列显隐按钮存在',
  sortHeader:     '排序列头存在',
  pagerExists:    '分页器存在',
  printButton:    '打印按钮存在',
  groupHeader:    '分组标题栏存在',
  checkbox:       '多选复选框存在',
};

// ============================================================
// 辅助函数
// ============================================================
async function waitForServer(url, maxRetries = 30, interval = 2000) {
  for (let i = 0; i < maxRetries; i++) {
    try {
      await new Promise((resolve, reject) => {
        const req = http.get(url, (res) => { res.resume(); resolve(); });
        req.on('error', reject);
        req.setTimeout(3000, () => { req.destroy(); reject(new Error('timeout')); });
      });
      console.log(`  ✓ 服务已就绪: ${url}`);
      return true;
    } catch {
      if (i % 5 === 0) process.stdout.write(`  等待服务启动 (${i + 1}/${maxRetries})...\n`);
      await new Promise(r => setTimeout(r, interval));
    }
  }
  throw new Error(`服务 ${url} 未能启动`);
}

async function checkPage(page, pageConfig) {
  const { url, name, module, hasPrint, hasSearch } = pageConfig;
  const pageResults = { page: name, url, module, checks: {}, screenshots: [], passed: true };

  try {
    console.log(`\n  ── ${name} (${url}) ──`);

    // 1. 页面加载 - 先在每个页面前注入 token 确保认证
    await page.evaluate(() => {
      // 检查是否已有 token，没有则注入
      if (!localStorage.getItem('authToken')) {
        localStorage.setItem('authToken', 'test-token');
      }
    });

    await page.goto(`${BLAZOR_URL}${url}`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(3000); // 等待 Blazor 渲染
    pageResults.checks[CHECKS.pageLoad] = true;

    // 截图1: 全页
    const screenshotDir = path.join(REPORT_DIR, module);
    fs.mkdirSync(screenshotDir, { recursive: true });
    const s1 = path.join(screenshotDir, `${name.replace(/[/\\?%*:|"<>]/g, '_')}.png`);
    await page.screenshot({ path: s1, fullPage: true });
    pageResults.screenshots.push(s1);

    // 检查页面是否有错误
    const pageContent = await page.content();
    const hasError = pageContent.includes('there was an unhandled exception') ||
                     pageContent.includes('Unhandled error') ||
                     pageContent.includes('发生错误');
    const notAuthorized = pageContent.includes('Not authorized');
    if (notAuthorized) {
      console.log(`  ⚠ 页面未授权，尝试重新注入 token 后刷新...`);
    }
    if (hasError) {
      console.log(`  ✗ 页面加载错误: ${name}`);
      pageResults.checks[CHECKS.pageLoad] = false;
      pageResults.passed = false;
    }

    // 如果是未授权，尝试重新登录
    if (notAuthorized) {
      await page.evaluate(async () => {
        try {
          const res = await fetch('http://localhost:7000/api/auth/login', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: 'admin@mes.com', password: 'Admin@123' }),
          });
          const data = await res.json();
          if (data?.data?.token) {
            localStorage.setItem('authToken', data.data.token);
            localStorage.setItem('refreshToken', data.data.refreshToken || data.data.token);
          }
        } catch(e) { /* ignore */ }
      });
      await page.reload({ waitUntil: 'networkidle', timeout: 30000 });
      await page.waitForTimeout(3000);
    }

    // 2. 表格是否存在（报表总览为 Tab 汇总页，首屏业务总况是 HTML table，走 customTable）
    const table = pageConfig.customTable ? await page.$('.mud-tabs table') : await page.$('.mud-table');
    pageResults.checks[CHECKS.tableExists] = !!table;
    console.log(`  ${table ? '✓' : '✗'} ${CHECKS.tableExists}: ${table ? '存在' : '不存在'}`);
    if (!table) { pageResults.passed = false; }

    // 3. 表格是否有行数据（tbody tr）
    if (table) {
      const rows = pageConfig.customTable ? await page.$$('#report-inout-summary-table tbody tr') : await page.$$('.mud-table-body .mud-table-row');
      pageResults.checks[CHECKS.dataLoaded] = rows.length > 0;
      console.log(`  ${rows.length > 0 ? '✓' : ' '} ${CHECKS.dataLoaded}: ${rows.length} 行`);

      // 4. 搜索框
      const searchInput = await page.$('input[placeholder*="搜索"], input[aria-label*="搜索"], .mud-input input');
      pageResults.checks[CHECKS.searchExists] = hasSearch ? !!searchInput : '跳过';
      if (hasSearch) {
        console.log(`  ${searchInput ? '✓' : '✗'} ${CHECKS.searchExists}: ${searchInput ? '存在' : '不存在'}`);
        if (!searchInput) pageResults.passed = false;
      }

      // 5. 列显隐按钮（ColumnDisplaySelect）- 查找包含"列"文本的按钮或图标按钮
      const colDisplayBtn = await page.evaluate(() => {
        const buttons = document.querySelectorAll('button');
        return Array.from(buttons).some(b => b.textContent.includes('列') || b.innerHTML.includes('列')) ||
               Array.from(document.querySelectorAll('*')).some(el => el.textContent === '列显隐');
      });
      pageResults.checks[CHECKS.columnDisplay] = colDisplayBtn;
      console.log(`  ${colDisplayBtn ? '✓' : ' '} ${CHECKS.columnDisplay}: ${colDisplayBtn ? '存在' : '未找到'}`);

      // 6. 排序列头（th 可点击排序）
      const sortHeaders = await page.$$('.mud-table-head th, .mud-th-sortable');
      pageResults.checks[CHECKS.sortHeader] = sortHeaders.length > 0;
      console.log(`  ${sortHeaders.length > 0 ? '✓' : ' '} ${CHECKS.sortHeader}: ${sortHeaders.length} 个排序列头`);

      // 7. 分页器
      const pager = await page.$('.mud-table-pager, .mud-pagination');
      pageResults.checks[CHECKS.pagerExists] = !!pager;
      console.log(`  ${pager ? '✓' : ' '} ${CHECKS.pagerExists}: ${pager ? '存在' : '不存在'}`);

      // 8. 分组标题栏
      const groupHeaders = await page.$$('[class*="col-g"], [class*="group-header"]');
      pageResults.checks[CHECKS.groupHeader] = groupHeaders.length > 0;
      console.log(`  ${groupHeaders.length > 0 ? '✓' : ' '} ${CHECKS.groupHeader}: ${groupHeaders.length} 个分组标题`);

      // 9. 复选框（多选）
      const checkboxes = await page.$$('input[type="checkbox"], .mud-checkbox');
      pageResults.checks[CHECKS.checkbox] = checkboxes.length > 0;

      // 10. 打印按钮
      if (hasPrint) {
        const printBtns = await page.$$('button:has-text("打印")');
        pageResults.checks[CHECKS.printButton] = printBtns.length > 0;
        console.log(`  ${printBtns.length > 0 ? '✓' : '✗'} ${CHECKS.printButton}: ${printBtns.length} 个打印按钮`);
        if (printBtns.length === 0) pageResults.passed = false;
      } else {
        pageResults.checks[CHECKS.printButton] = '跳过';
      }
    }

    results.passed += pageResults.passed ? 1 : 0;
    results.details.push(pageResults);

  } catch (err) {
    console.log(`  ✗ ${name}: ${err.message}`);
    pageResults.passed = false;
    pageResults.error = err.message;
    results.details.push(pageResults);
    results.errors.push({ page: name, url, error: err.message });
  }
}

// ============================================================
// 主流程
// ============================================================
async function main() {
  console.log('========================================');
  console.log('  MES 前端自动化验证脚本');
  console.log('========================================\n');

  // 确保报告目录
  fs.mkdirSync(REPORT_DIR, { recursive: true });

  // 1. 等待服务启动
  console.log('检查服务状态...\n');
  try {
    await waitForServer(`${BLAZOR_URL}/login`);
  } catch (err) {
    console.error(`\n✗ ${err.message}`);
    console.log('\n请先启动服务:');
    console.log('  1. cd MES.Api && dotnet run --launch-profile http');
    console.log('  2. cd MES.Blazor && dotnet run --launch-profile http');
    process.exit(1);
  }

  // 2. 启动浏览器
  console.log('\n启动浏览器...');
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1920, height: 1080 },
    ignoreHTTPSErrors: true,
  });
  const page = await context.newPage();

  // 3. 登录（通过 API 获取 token 并注入 localStorage）
  console.log('\n登录系统...');
  try {
    // 方式 A: 通过页面登录
    await page.goto(`${BLAZOR_URL}/login`, { waitUntil: 'networkidle', timeout: 20000 });
    await page.waitForTimeout(2000);

    // 查找并填写邮箱输入框
    const inputs = await page.$$('input');
    for (const input of inputs) {
      const type = await input.getAttribute('type');
      const id = await input.getAttribute('id');
      if (type === 'text' || type === 'email' || id?.includes('Email')) {
        await input.fill('admin@mes.com');
        break;
      }
    }

    // 查找密码输入框
    const pwdInput = await page.$('input[type="password"]');
    if (pwdInput) await pwdInput.fill('Admin@123');

    // 点击登录按钮
    const loginBtn = await page.$('button:has-text("登录")');
    if (loginBtn) await loginBtn.click();

    await page.waitForTimeout(3000);

    // 检查是否登录成功
    let currentUrl = page.url();
    if (currentUrl.includes('/login')) {
      console.log('  页面登录未成功，尝试 API 直接登录...');
      // 方式 B: 通过 API 获取 token
      const tokenResponse = await page.evaluate(async () => {
        try {
          const res = await fetch('http://localhost:7000/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: 'admin@mes.com', password: 'Admin@123' }),
          });
          const data = await res.json();
          return data;
        } catch (e) { return { error: e.message }; }
      });

      if (tokenResponse?.data?.token) {
        // 注入 token 到 localStorage
        await page.evaluate((token) => {
          localStorage.setItem('authToken', token);
          localStorage.setItem('refreshToken', token);
        }, tokenResponse.data.token);
        console.log('  ✓ Token 获取成功，已注入 localStorage');
      } else {
        console.log('  ✗ 登录失败:', JSON.stringify(tokenResponse));
        // 尝试直接跳过认证验证页面
      }
    } else {
      console.log('  ✓ 页面登录成功');
    }
  } catch (err) {
    console.log(`  ⚠ 登录过程异常: ${err.message}`);
  }

  // 导航到首页验证登录状态
  await page.goto(`${BLAZOR_URL}/`, { waitUntil: 'networkidle', timeout: 20000 });
  await page.waitForTimeout(3000);
  console.log(`  首页加载完成: ${page.url()}`);

  // 4. 遍历所有页面
  console.log('\n========================================');
  console.log('  开始验证页面');
  console.log('========================================\n');

  for (const pageConfig of LIST_PAGES) {
    try {
      await checkPage(page, pageConfig);
    } catch (err) {
      console.log(`  ✗ ${pageConfig.name}: 验证异常 - ${err.message}`);
      results.errors.push({ page: pageConfig.name, url: pageConfig.url, error: err.message });
      results.failed++;
    }
  }

  // 5. 生成报告
  await browser.close();

  console.log('\n========================================');
  console.log('  验证报告');
  console.log('========================================\n');

  const passedCount = results.details.filter(d => d.passed).length;
  const failedCount = results.details.filter(d => !d.passed).length;

  console.log(`总计: ${results.details.length} 页`);
  console.log(`通过: ${passedCount} 页`);
  console.log(`失败: ${failedCount} 页`);

  if (failedCount > 0) {
    console.log('\n--- 失败的页面 ---');
    for (const d of results.details.filter(d => !d.passed)) {
      console.log(`  ✗ ${d.page} (${d.url}): ${d.error || '检查项未通过'}`);
      for (const [check, status] of Object.entries(d.checks)) {
        if (status === false) console.log(`    - ${check}: 失败`);
      }
    }
  }

  // 生成 HTML 报告
  const reportHtml = generateHtmlReport(results);
  const reportPath = path.join(REPORT_DIR, 'index.html');
  fs.writeFileSync(reportPath, reportHtml);
  console.log(`\n报告已保存: ${reportPath}`);

  // 统计模块分布
  const moduleStats = {};
  for (const d of results.details) {
    moduleStats[d.module] = moduleStats[d.module] || { total: 0, passed: 0, failed: 0 };
    moduleStats[d.module].total++;
    if (d.passed) moduleStats[d.module].passed++;
    else moduleStats[d.module].failed++;
  }

  console.log('\n--- 模块统计 ---');
  for (const [mod, stats] of Object.entries(moduleStats)) {
    const icon = stats.failed > 0 ? '✗' : '✓';
    console.log(`  ${icon} ${mod}: ${stats.passed}/${stats.total}`);
  }

  console.log('\n========================================\n');

  process.exit(failedCount > 0 ? 1 : 0);
}

// ============================================================
// HTML 报告生成
// ============================================================
function generateHtmlReport(results) {
  const rows = results.details.map(d => {
    const statusIcon = d.passed ? '✅' : '❌';
    const checksList = Object.entries(d.checks).map(([name, status]) => {
      const icon = status === true ? '✅' : status === false ? '❌' : '⏭️';
      return `<li>${icon} ${name}</li>`;
    }).join('');

    const screenshotLinks = d.screenshots.map(s => {
      const relPath = path.relative(REPORT_DIR, s);
      return `<a href="${relPath}" target="_blank">截图</a>`;
    }).join(' ');

    return `<tr>
      <td>${statusIcon}</td>
      <td>${d.page}</td>
      <td>${d.url}</td>
      <td>${d.module}</td>
      <td><ul class="checks">${checksList}</ul></td>
      <td>${d.error || ''}</td>
      <td>${screenshotLinks}</td>
    </tr>`;
  }).join('\n');

  const passed = results.details.filter(d => d.passed).length;
  const failed = results.details.filter(d => !d.passed).length;

  return `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8">
  <title>MES 前端验证报告</title>
  <style>
    body { font-family: -apple-system, sans-serif; margin: 20px; color: #333; }
    h1 { color: #1a1a2e; }
    .summary { display: flex; gap: 20px; margin: 20px 0; }
    .summary-card { padding: 15px 25px; border-radius: 8px; color: white; font-size: 18px; }
    .summary-card.pass { background: #4caf50; }
    .summary-card.fail { background: #f44336; }
    .summary-card.total { background: #2196f3; }
    table { border-collapse: collapse; width: 100%; margin-top: 20px; }
    th, td { border: 1px solid #ddd; padding: 8px 12px; text-align: left; font-size: 13px; }
    th { background: #1a1a2e; color: white; }
    tr:nth-child(even) { background: #f9f9f9; }
    tr.failed { background: #fff3f3; }
    .checks { list-style: none; padding: 0; margin: 0; }
    .checks li { white-space: nowrap; }
    a { color: #1976d2; text-decoration: none; }
    a:hover { text-decoration: underline; }
  </style>
</head>
<body>
  <h1>MES 前端自动化验证报告</h1>
  <div class="summary">
    <div class="summary-card total">📊 总计: ${results.details.length} 页</div>
    <div class="summary-card pass">✅ 通过: ${passed} 页</div>
    <div class="summary-card fail">❌ 失败: ${failed} 页</div>
  </div>
  <table>
    <thead>
      <tr>
        <th>状态</th><th>页面</th><th>URL</th><th>模块</th><th>检查项</th><th>错误</th><th>截图</th>
      </tr>
    </thead>
    <tbody>
      ${rows}
    </tbody>
  </table>
  <p style="margin-top: 20px; color: #666; font-size: 12px;">
    生成时间: ${new Date().toISOString()}
  </p>
</body>
</html>`;
}

main();
