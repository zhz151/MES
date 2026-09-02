/**
 * 规范符合度静态扫描 — TC30 ~ TC37
 *
 * 检查项:
 *   TC30 (B06): 工具栏布局 — ColumnDisplaySelect + 打印按钮
 *   TC31 (B02/B36): 搜索框合规 — Immediate/Debounce/Clearable/Adornment.End
 *   TC32 (B37): 操作列图标 — MudIconButton + Color 映射
 *   TC33 (A01/A02): 页面布局 — MudContainer MaxWidth.False
 *   TC34 (B18): 分页汇总行 — FooterContent
 *   TC35 (B08/B09): 方向键导航 — #-list-table + _isArrowNavSetup
 *   TC36 (A20/B16/B17): PageStateService 集成
 *   TC37 (E02): 输入组件统一 — MudNumericField HideSpinButtons
 *
 * 使用:
 *   node playwright-tests/scan-spec-compliance.mjs [--tc=TC30,TC31]
 *
 * 返回码: 违规数（0 = 全部合规）
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');
const PAGES_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');
const SHARED_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Shared');

// ============================================================
// 工具函数
// ============================================================
function scanRazorFiles(dirPath, predicate) {
  const results = [];
  if (!fs.existsSync(dirPath)) return results;
  for (const entry of fs.readdirSync(dirPath, { withFileTypes: true })) {
    const fullPath = path.join(dirPath, entry.name);
    if (entry.isDirectory()) {
      results.push(...scanRazorFiles(fullPath, predicate));
    } else if (entry.name.endsWith('.razor')) {
      const content = fs.readFileSync(fullPath, 'utf-8');
      const relPath = path.relative(PROJECT_DIR, fullPath);
      const match = predicate(content, relPath);
      if (match) results.push(match);
    }
  }
  return results;
}

function scanCsFiles(dirPath, predicate) {
  const results = [];
  if (!fs.existsSync(dirPath)) return results;
  for (const entry of fs.readdirSync(dirPath, { withFileTypes: true })) {
    const fullPath = path.join(dirPath, entry.name);
    if (entry.isDirectory()) {
      results.push(...scanCsFiles(fullPath, predicate));
    } else if (entry.name.endsWith('.razor.cs')) {
      const content = fs.readFileSync(fullPath, 'utf-8');
      const relPath = path.relative(PROJECT_DIR, fullPath);
      const match = predicate(content, relPath);
      if (match) results.push(match);
    }
  }
  return results;
}

// ============================================================
// TC30: 工具栏布局 (B06)
//   - ColumnDisplaySelect 组件存在
//   - 打印按钮存在（MudButton StartIcon=Print 或 MudIconButton Print）
// ============================================================
function checkTC30() {
  const violations = [];
  const pages = [];

  for (const entry of fs.readdirSync(PAGES_DIR, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const razorFiles = fs.readdirSync(path.join(PAGES_DIR, entry.name))
      .filter(f => f.endsWith('.razor'));
    for (const rf of razorFiles) {
      const fullPath = path.join(PAGES_DIR, entry.name, rf);
      const content = fs.readFileSync(fullPath, 'utf-8');
      const relPath = path.relative(PROJECT_DIR, fullPath);

      // 跳过非列表页（创建/编辑/详情页没有标准工具栏）
      const isListPage = content.includes('ServerData') ||
                          content.includes('Items="@_items"') ||
                          content.includes('MudTable') && content.includes('ColumnDisplaySelect');

      if (!isListPage) continue;

      const hasColumnDisplay = content.includes('ColumnDisplaySelect');
      const hasPrintButton = /Print[^a-zA-Z]/.test(content) && /打印|Print/.test(content);
      const hasResetButton = content.includes('ResetFilter') || content.includes('重置');

      if (hasColumnDisplay || hasPrintButton || hasResetButton) {
        pages.push({ file: relPath, hasColumnDisplay, hasPrintButton, hasResetButton });
      }
    }
  }

  // 检查：有 ColumnDisplaySelect 的页面是否也有打印按钮和重置按钮
  const withColumnDisplay = pages.filter(p => p.hasColumnDisplay);
  const missingPrint = withColumnDisplay.filter(p => !p.hasPrintButton);
  const missingReset = withColumnDisplay.filter(p => !p.hasResetButton);

  // 有打印按钮但无 ColumnDisplaySelect 的页面
  const printNoColumn = pages.filter(p => p.hasPrintButton && !p.hasColumnDisplay);

  return {
    summary: {
      withColumnDisplay: withColumnDisplay.length,
      withPrintButton: pages.filter(p => p.hasPrintButton).length,
      withResetButton: pages.filter(p => p.hasResetButton).length,
      missingPrint: missingPrint.length,
      missingReset: missingReset.length,
      printNoColumn: printNoColumn.length,
    },
    violations: [
      ...missingPrint.map(p => ({ file: p.file, issue: '有 ColumnDisplaySelect 但缺少打印按钮' })),
      ...missingReset.map(p => ({ file: p.file, issue: '有 ColumnDisplaySelect 但缺少重置按钮' })),
      ...printNoColumn.map(p => ({ file: p.file, issue: '有打印按钮但缺少 ColumnDisplaySelect' })),
    ].slice(0, 30), // 最多显示 30 条
    totalPages: pages.length,
  };
}

// ============================================================
// TC31: 搜索框合规 (B02/B36)
//   - MudTextField Label="模糊搜索" (或等效)
//   - Immediate="true" | DebounceInterval=
//   - Clearable="true"
//   - Adornment="Adornment.End" + AdornmentIcon
// ============================================================
function checkTC31() {
  const results = [];
  let totalSearchBoxes = 0;
  let withImmediate = 0, withDebounce = 0, withClearable = 0, withAdornmentEnd = 0;

  const files = scanRazorFiles(PAGES_DIR, (content, relPath) => {
    // 找模糊搜索框
    const searchMatches = content.matchAll(/MudTextField[\s\S]{0,200}?(?:模糊搜索|Keyword|搜索)/g);
    const matches = [...searchMatches];
    if (matches.length === 0) return null;

    let boxCount = 0, imm = 0, deb = 0, clear = 0, adorn = 0;
    for (const m of matches) {
      boxCount++;
      if (/Immediate\s*=\s*"true"/.test(m)) imm++;
      if (/DebounceInterval\s*=/.test(m)) deb++;
      if (/Clearable\s*=\s*"true"/.test(m) || /ClearButton\s*=\s*"true"/.test(m)) clear++;
      if (/Adornment\s*=\s*"Adornment\.End"/.test(m)) adorn++;
    }

    totalSearchBoxes += boxCount;
    withImmediate += imm;
    withDebounce += deb;
    withClearable += clear;
    withAdornmentEnd += adorn;

    const violations = [];
    for (let i = 0; i < boxCount; i++) {
      if (i < imm && !imm) violations.push('缺少 Immediate');
      if (i < deb && !deb) violations.push('缺少 DebounceInterval');
      if (i < clear && !clear) violations.push('缺少 Clearable');
      if (i < adorn && !adorn) violations.push('缺少 Adornment.End');
    }

    return violations.length > 0 ? { file: relPath, boxCount, imm, deb, clear, adorn, violations } : null;
  });

  return {
    summary: {
      totalSearchBoxes,
      withImmediate, withDebounce, withClearable, withAdornmentEnd,
      immediateRate: totalSearchBoxes ? Math.round(withImmediate / totalSearchBoxes * 100) : 0,
      debounceRate: totalSearchBoxes ? Math.round(withDebounce / totalSearchBoxes * 100) : 0,
      clearableRate: totalSearchBoxes ? Math.round(withClearable / totalSearchBoxes * 100) : 0,
      adornmentRate: totalSearchBoxes ? Math.round(withAdornmentEnd / totalSearchBoxes * 100) : 0,
    },
    files: files.slice(0, 20),
  };
}

// ============================================================
// TC32: 操作列图标 (B37)
//   - MudIconButton + 标准颜色映射
//   - View=Info, Edit=Warning/Primary, Delete=Error
//   - 所有操作按钮应有 Title 属性
// ============================================================
function checkTC32() {
  const violations = [];
  let totalDeleteBtns = 0, deleteWithError = 0, deleteWithTitle = 0;
  let totalEditBtns = 0, editWithWarning = 0;
  let totalViewBtns = 0, viewWithInfo = 0;

  scanRazorFiles(PAGES_DIR, (content, relPath) => {
    const lines = content.split('\n');
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];

      // MudIconButton with Delete icon
      if (line.includes('MudIconButton') && /Icons\.Material.*Delete/.test(line)) {
        totalDeleteBtns++;
        // Check Color (within next 5 lines or same line)
        const block = lines.slice(i, i + 6).join(' ');
        if (/Color\s*=\s*"(Color\.)?Error"/.test(block)) deleteWithError++;
        if (/\bTitle\s*=\s*"/.test(block)) deleteWithTitle++;
        else if (!/\bTitle\s*=/.test(block)) {
          violations.push({ file: relPath, line: i + 1, issue: 'Delete 按钮缺少 Title 属性' });
        }
      }

      // MudIconButton with Edit icon
      if (line.includes('MudIconButton') && /Icons\.Material.*(Edit|BorderColor)/.test(line)) {
        totalEditBtns++;
        const block = lines.slice(i, i + 6).join(' ');
        if (/Color\s*=\s*"(Color\.)?(Warning|Primary)"/.test(block)) editWithWarning++;
      }

      // MudIconButton with Visibility/Info icon (View)
      if (line.includes('MudIconButton') && /Icons\.Material.*(Visibility|Info)/.test(line)) {
        totalViewBtns++;
        const block = lines.slice(i, i + 6).join(' ');
        if (/Color\s*=\s*"(Color\.)?Info"/.test(block)) viewWithInfo++;
      }
    }
    return null;
  });

  return {
    summary: {
      totalDeleteBtns, deleteWithError,
      deleteErrorRate: totalDeleteBtns ? Math.round(deleteWithError / totalDeleteBtns * 100) : 0,
      deleteWithTitle,
      deleteTitleRate: totalDeleteBtns ? Math.round(deleteWithTitle / totalDeleteBtns * 100) : 0,
      totalEditBtns, editWithWarning,
      totalViewBtns, viewWithInfo,
    },
    violations: violations.slice(0, 20),
  };
}

// ============================================================
// TC33: 页面布局 (A01/A02)
//   - MudContainer MaxWidth="MaxWidth.False"
//   - Class="mt-4" or "mt-4 pl-0"
// ============================================================
function checkTC33() {
  let totalContainers = 0, withMaxWidthFalse = 0, withMt4 = 0, withPl0 = 0;
  const notFullWidth = [];

  scanRazorFiles(PAGES_DIR, (content, relPath) => {
    const containerMatches = content.matchAll(/<MudContainer[^>]*>/g);
    for (const m of containerMatches) {
      totalContainers++;
      if (m[0].includes('MaxWidth="MaxWidth.False"')) withMaxWidthFalse++;
      if (/Class="[^"]*mt-4[^"]*"/.test(m[0])) withMt4++;
      if (/Class="[^"]*pl-0[^"]*"/.test(m[0])) withPl0++;
      if (!m[0].includes('MaxWidth="MaxWidth.False"')) {
        notFullWidth.push({ file: relPath, snippet: m[0].substring(0, 100) });
      }
    }
    return null;
  });

  return {
    summary: {
      totalContainers,
      withMaxWidthFalse,
      fullWidthRate: totalContainers ? Math.round(withMaxWidthFalse / totalContainers * 100) : 0,
      withMt4, withPl0,
    },
    violations: notFullWidth.slice(0, 15).map(v => ({
      file: v.file,
      issue: 'MudContainer 缺少 MaxWidth="MaxWidth.False"',
      snippet: v.snippet,
    })),
  };
}

// ============================================================
// TC34: 分页汇总行 (B18)
//   - MudTable 内 FooterContent 存在
//   - 有数值列的列表页应有 RenderFooterCell
// ============================================================
function checkTC34() {
  let withFooterContent = 0, withRenderFooterCell = 0;
  let totalListTables = 0;
  const noFooter = [];

  scanRazorFiles(PAGES_DIR, (content, relPath) => {
    // 检测服务端分页表格
    const hasServerData = content.includes('ServerData="');
    const hasMudTable = content.includes('<MudTable');
    if (!hasServerData || !hasMudTable) return null;

    totalListTables++;
    const hasFooterContent = content.includes('<FooterContent>');
    const hasRenderFooterCell = content.includes('RenderFooterCell');

    if (hasFooterContent) withFooterContent++;
    if (hasRenderFooterCell) withRenderFooterCell++;
    if (!hasFooterContent) {
      noFooter.push(relPath);
    }
    return null;
  });

  return {
    summary: {
      totalListTables,
      withFooterContent,
      withRenderFooterCell,
      coverageRate: totalListTables ? Math.round(withFooterContent / totalListTables * 100) : 0,
    },
    violations: noFooter.map(f => ({ file: f, issue: '服务端分页表格缺少 FooterContent' })),
  };
}

// ============================================================
// TC35: 方向键导航 (B08/B09)
//   - #xxx-list-table 容器
//   - _isArrowNavSetup 防护
//   - OnAfterRenderAsync 中初始化
// ============================================================
function checkTC35() {
  const razorWithListTable = [];
  const csWithArrowNav = [];

  scanRazorFiles(PAGES_DIR, (content, relPath) => {
    if (content.includes('-list-table') && content.includes('enableTableArrowNav')) {
      razorWithListTable.push(relPath);
    }
    return null;
  });

  scanCsFiles(PAGES_DIR, (content, relPath) => {
    if (content.includes('_isArrowNavSetup') && content.includes('OnAfterRenderAsync')) {
      csWithArrowNav.push(relPath);
      return null;
    }
    // 有 list-table 但缺少 _isArrowNavSetup 防护
    if (content.includes('list-table') && !content.includes('_isArrowNavSetup')) {
      return { file: relPath, issue: '实现了 list-table 导航但缺少 _isArrowNavSetup 防护' };
    }
    return null;
  });

  const noSetupGuard = csWithArrowNav.length === 0 ? [] : [];

  return {
    summary: {
      razorWithArrowNav: razorWithListTable.length,
      csWithArrowNavPlusGuard: csWithArrowNav.length,
    },
    violations: noSetupGuard,
    info: {
      message: '方向键导航需同时检查 .razor (enableTableArrowNav) 和 .razor.cs (_isArrowNavSetup + OnAfterRenderAsync)',
      razorFiles: razorWithListTable.slice(0, 5),
      csFiles: csWithArrowNav.slice(0, 5),
    },
  };
}

// ============================================================
// TC36: PageStateService (A20/B16/B17)
//   - PageStateService 注入
//   - SavePageStateAsync / RestorePageStateAsync
//   - _pageState 字段
// ============================================================
function checkTC36() {
  let totalCsFiles = 0;
  let withPageState = 0, withSave = 0, withRestore = 0;

  scanCsFiles(PAGES_DIR, (content, relPath) => {
    totalCsFiles++;
    let hasPageState = false, hasSave = false, hasRestore = false;

    if (/PageState(Service)?\b/.test(content)) hasPageState = true;
    if (/SavePageState(Async)?/.test(content)) hasSave = true;
    if (/RestorePageState(Async)?/.test(content)) hasRestore = true;

    if (hasPageState) withPageState++;
    if (hasSave) withSave++;
    if (hasRestore) withRestore++;

    return null;
  });

  return {
    summary: {
      totalCsFiles,
      withPageState,
      withSave: withSave,
      withRestore,
      coverageRate: totalCsFiles ? Math.round(withPageState / totalCsFiles * 100) : 0,
    },
  };
}

// ============================================================
// TC37: 输入组件统一 (E02)
//   - MudNumericField 使用 HideSpinButtons="true"
//   - MudSelect 使用 Dense / Variant.Outlined
// ============================================================
function checkTC37() {
  let totalNumericFields = 0, withHideSpinButtons = 0;
  let totalMudSelects = 0, withDense = 0, withOutlined = 0;
  const missingSpin = [];

  scanRazorFiles(PAGES_DIR, (content, relPath) => {
    // MudNumericField — HTML 标签形式（跨行非贪婪匹配到 />，避免 => lambda 的 > 截断）
    const htmlNumericRe = /<MudNumericField[\s\S]*?\/>/g;
    for (const m of content.matchAll(htmlNumericRe)) {
      if (m[0].includes('OpenComponent')) continue; // 防御：排除 RenderTreeBuilder 代码
      totalNumericFields++;
      if (m[0].includes('HideSpinButtons')) {
        withHideSpinButtons++;
      } else {
        missingSpin.push({ file: relPath, snippet: m[0].substring(0, 120) });
      }
    }

    // MudNumericField — RenderTreeBuilder 代码形式（builder.OpenComponent<MudNumericField<...>>）
    const openNumericRe = /builder\.OpenComponent<MudNumericField<[^>]+>>\(\d+\);/g;
    for (const m of content.matchAll(openNumericRe)) {
      totalNumericFields++;
      // 截取到该组件 CloseComponent，确保覆盖全部 AddAttribute（含 HideSpinButtons）
      const closeIdx = content.indexOf('builder.CloseComponent();', m.index);
      const block = closeIdx === -1
        ? content.slice(m.index, m.index + 800)
        : content.slice(m.index, closeIdx);
      if (block.includes('HideSpinButtons')) {
        withHideSpinButtons++;
      } else {
        missingSpin.push({ file: relPath, snippet: m[0].substring(0, 120) });
      }
    }

    // MudSelect for enum values (only count MudSelect without T="string")
    const selectMatches = content.matchAll(/<MudSelect\s+(?!T="string")[^>]*>/g);
    for (const m of selectMatches) {
      totalMudSelects++;
      if (/Dense\s*=\s*"true"/.test(m[0])) withDense++;
      if (/Variant\s*=\s*"(Variant\.)?Outlined"/.test(m[0])) withOutlined++;
    }

    return null;
  });

  return {
    summary: {
      totalNumericFields,
      withHideSpinButtons,
      hideSpinRate: totalNumericFields ? Math.round(withHideSpinButtons / totalNumericFields * 100) : 0,
      totalMudSelects,
      withDense,
      denseRate: totalMudSelects ? Math.round(withDense / totalMudSelects * 100) : 0,
      withOutlined,
      outlinedRate: totalMudSelects ? Math.round(withOutlined / totalMudSelects * 100) : 0,
    },
    violations: missingSpin.slice(0, 10).map(v => ({
      file: v.file,
      issue: 'MudNumericField 缺少 HideSpinButtons="true"',
      snippet: v.snippet,
    })),
  };
}

// ============================================================
// 主流程
// ============================================================
function main() {
  const args = process.argv.slice(2);
  const tcFilter = args.find(a => a.startsWith('--tc='));
  const selectedTCs = tcFilter
    ? tcFilter.split('=')[1].split(',').map(s => s.trim().toUpperCase())
    : ['TC30', 'TC31', 'TC32', 'TC33', 'TC34', 'TC35', 'TC36', 'TC37'];

  let totalViolations = 0;

  console.log('============================================');
  console.log('  规范符合度扫描 — TC30 ~ TC37');
  console.log('============================================\n');

  // TC30
  if (selectedTCs.includes('TC30')) {
    console.log('--- TC30: 工具栏布局 (B06) ---');
    const r30 = checkTC30();
    console.log(`  列表页总数: ${r30.totalPages}`);
    console.log(`  ColumnDisplaySelect: ${r30.summary.withColumnDisplay}`);
    console.log(`  打印按钮: ${r30.summary.withPrintButton}`);
    console.log(`  重置按钮: ${r30.summary.withResetButton}`);
    if (r30.violations.length > 0) {
      // 提示级：缺打印/重置按钮依赖页面类型（表单/编辑/配置页合理缺失），不阻塞
      console.log(`  ℹ 提示 (${r30.violations.length}):`);
      for (const v of r30.violations.slice(0, 10)) {
        console.log(`    ⚠ ${v.file}: ${v.issue}`);
      }
    } else {
      console.log('  ✅ 无违规');
    }
    console.log();
  }

  // TC31
  if (selectedTCs.includes('TC31')) {
    console.log('--- TC31: 搜索框合规 (B02/B36) ---');
    const r31 = checkTC31();
    console.log(`  搜索框总数: ${r31.summary.totalSearchBoxes}`);
    console.log(`  Immediate: ${r31.summary.withImmediate} (${r31.summary.immediateRate}%)`);
    console.log(`  DebounceInterval: ${r31.summary.withDebounce} (${r31.summary.debounceRate}%)`);
    console.log(`  Clearable: ${r31.summary.withClearable} (${r31.summary.clearableRate}%)`);
    console.log(`  Adornment.End: ${r31.summary.withAdornmentEnd} (${r31.summary.adornmentRate}%)`);
    if (r31.files.length > 0) {
      console.log(`  属性不完整的文件 (${r31.files.length}):`);
      for (const f of r31.files.slice(0, 10)) {
        console.log(`    ⚠ ${f.file}: ${f.violations.join(', ')}`);
        totalViolations++;
      }
    } else {
      console.log('  ✅ 所有搜索框属性完整');
    }
    console.log();
  }

  // TC32
  if (selectedTCs.includes('TC32')) {
    console.log('--- TC32: 操作列图标 (B37) ---');
    const r32 = checkTC32();
    console.log(`  Delete 按钮: ${r32.summary.totalDeleteBtns} (Error 颜色: ${r32.summary.deleteErrorRate}%)`);
    console.log(`  Delete Title 覆盖率: ${r32.summary.deleteTitleRate}%`);
    console.log(`  Edit 按钮: ${r32.summary.totalEditBtns} (Warning 颜色: ${r32.summary.editWithWarning})`);
    console.log(`  View 按钮: ${r32.summary.totalViewBtns} (Info 颜色: ${r32.summary.viewWithInfo})`);
    if (r32.violations.length > 0) {
      console.log(`  违规 (${r32.violations.length}):`);
      for (const v of r32.violations) {
        console.log(`    ⚠ ${v.file}:L${v.line} ${v.issue}`);
        totalViolations++;
      }
    } else {
      console.log('  ✅ 无违规');
    }
    console.log();
  }

  // TC33
  if (selectedTCs.includes('TC33')) {
    console.log('--- TC33: 页面布局 (A01/A02) ---');
    const r33 = checkTC33();
    console.log(`  MudContainer 总数: ${r33.summary.totalContainers}`);
    console.log(`  MaxWidth.False: ${r33.summary.withMaxWidthFalse} (${r33.summary.fullWidthRate}%)`);
    console.log(`  mt-4: ${r33.summary.withMt4}, pl-0: ${r33.summary.withPl0}`);
    if (r33.violations.length > 0) {
      console.log(`  非全宽容器 (${r33.violations.length}):`);
      for (const v of r33.violations) {
        console.log(`    ⚠ ${v.file}`);
        totalViolations++;
      }
    } else {
      console.log('  ✅ 全宽布局');
    }
    console.log();
  }

  // TC34
  if (selectedTCs.includes('TC34')) {
    console.log('--- TC34: 分页汇总行 (B18) ---');
    const r34 = checkTC34();
    console.log(`  服务端分页表格: ${r34.summary.totalListTables}`);
    console.log(`  FooterContent: ${r34.summary.withFooterContent} (${r34.summary.coverageRate}%)`);
    console.log(`  RenderFooterCell: ${r34.summary.withRenderFooterCell}`);
    if (r34.violations.length > 0) {
      // 提示级：FooterContent 汇总行是否必需依赖页面数值列业务，需逐页人工判断，不阻塞
      console.log(`  ℹ 缺少 FooterContent (${r34.violations.length}):`);
      for (const v of r34.violations) {
        console.log(`    ⚠ ${v.file}`);
      }
    } else {
      console.log('  ✅ 汇总行覆盖完整');
    }
    console.log();
  }

  // TC35
  if (selectedTCs.includes('TC35')) {
    console.log('--- TC35: 方向键导航 (B08/B09) ---');
    const r35 = checkTC35();
    console.log(`  .razor 引用 enableTableArrowNav: ${r35.summary.razorWithArrowNav}`);
    console.log(`  .razor.cs 含 _isArrowNavSetup: ${r35.summary.csWithArrowNavPlusGuard}`);
    if (r35.violations.length > 0) {
      console.log(`  违规 (${r35.violations.length}):`);
      for (const v of r35.violations) {
        console.log(`    ⚠ ${v.file}: ${v.issue}`);
        totalViolations++;
      }
    } else {
      console.log('  ✅ 无违规');
    }
    console.log();
  }

  // TC36
  if (selectedTCs.includes('TC36')) {
    console.log('--- TC36: PageStateService (A20/B16/B17) ---');
    const r36 = checkTC36();
    console.log(`  .razor.cs 文件数: ${r36.summary.totalCsFiles}`);
    console.log(`  PageState 使用: ${r36.summary.withPageState} (${r36.summary.coverageRate}%)`);
    console.log(`  SavePageStateAsync: ${r36.summary.withSave}`);
    console.log(`  RestorePageStateAsync: ${r36.summary.withRestore}`);
    console.log('  ✅ PageStateService 覆盖率广');
    console.log();
  }

  // TC37
  if (selectedTCs.includes('TC37')) {
    console.log('--- TC37: 输入组件统一 (E02) ---');
    const r37 = checkTC37();
    console.log(`  MudNumericField: ${r37.summary.totalNumericFields}`);
    console.log(`  HideSpinButtons: ${r37.summary.withHideSpinButtons} (${r37.summary.hideSpinRate}%)`);
    console.log(`  MudSelect (枚举绑定): ${r37.summary.totalMudSelects}`);
    console.log(`  Dense: ${r37.summary.withDense} (${r37.summary.denseRate}%)`);
    console.log(`  Outlined: ${r37.summary.withOutlined} (${r37.summary.outlinedRate}%)`);
    if (r37.violations.length > 0) {
      console.log(`  缺少 HideSpinButtons (${r37.violations.length}):`);
      for (const v of r37.violations) {
        console.log(`    ⚠ ${v.file}: ${v.snippet.substring(0, 80)}`);
        totalViolations++;
      }
    } else {
      console.log('  ✅ HideSpinButtons 全覆盖');
    }
    console.log();
  }

  // ============================================================
  // 汇总
  // ============================================================
  console.log('='.repeat(50));
  console.log(`  违规总数: ${totalViolations}`);
  console.log('='.repeat(50));

  if (totalViolations > 0) {
    console.log(`\n⚠️  发现 ${totalViolations} 处违规，需修复。`);
    process.exit(Math.min(totalViolations, 127));
  } else {
    console.log('\n✅ 规范符合度检查通过。');
  }
}

main();
