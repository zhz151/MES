/**
 * 枚举一致性扫描 — TC06 + TC40
 *
 * TC06 (A16): 检测手写 switch 映射中文（枚举显示回潮）
 *   规则: 所有枚举/整数字段转中文显示的场合必须委托 DisplayHelper
 *   禁止手写 switch → return "中文" 模式
 *
 * TC40: 枚举值完整性
 *   规则: API 返回的枚举字段都能通过 DisplayHelper 获得中文显示
 *
 * 使用:
 *   node playwright-tests/scan-enum-consistency.mjs
 *   node playwright-tests/scan-enum-consistency.mjs --tc=TC06  (只运行单项)
 *
 * 返回码: 违规数
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');
const PAGES_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');
const HELPERS_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Helpers');
const SERVICES_DIR = path.join(PROJECT_DIR, 'MES.Services');
const ENUMS_DIR = path.join(PROJECT_DIR, 'MES.Core', 'Enums');
const SHARED_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Shared');

// ============================================================
// 工具函数
// ============================================================
function readAllFiles(dir, ext) {
  const results = [];
  if (!fs.existsSync(dir)) return results;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fp = path.join(dir, entry.name);
    if (entry.isDirectory()) results.push(...readAllFiles(fp, ext));
    else if (entry.name.endsWith(ext)) results.push(fp);
  }
  return results;
}

function getRelative(p) {
  return path.relative(PROJECT_DIR, p);
}

// ============================================================
// TC06: 检测手写 switch → 中文的枚举显示回潮
// ============================================================
function checkTC06() {
  const violations = [];

  // 扫描 .razor.cs 文件
  const csFiles = [
    ...readAllFiles(PAGES_DIR, '.razor.cs'),
    ...readAllFiles(SHARED_DIR, '.razor.cs'),
  ];

  // 已知合法的 switch 模式（不违规）:
  // 1. DisplayHelper 内部的 switch
  // 2. ColumnDef DisplayConverter lambda
  // 3. Enum.ToString() 转换（非中文映射）
  // 4. switch 用于非显示目的（状态机、权限判断等）
  const ALLOWED_PATTERNS = [
    /switch\s*\(/i,  // 非直接匹配，在行级二次判断
  ];

  // 已知的 DisplayHelper 方法名（合法调用）
  const DISPHELPER_METHODS = [
    'GetDisplayName', 'GetEnumDisplay', 'TryGetEnumDisplay',
    'GetProductionTypeText', 'GetManufacturingItemText', 'GetMaterialPlanStatusText',
    'GetInputStatusText', 'GetFlowStatusText', 'GetValidMainNoStatusText',
    'GetScheduleStageText', 'GetNcrStatusText', 'GetSeverityLevelText',
    'GetDisposalMethodText', 'GetVerifyResultText', 'GetReportTypeText',
    'GetBatchStatusText', 'GetWorkOrderStatusText',
    'GetDeliveryStateText', 'GetInspectionResultText',
  ];

  for (const file of csFiles) {
    const content = fs.readFileSync(file, 'utf-8');
    const lines = content.split('\n');
    const relPath = getRelative(file);

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];

      // 跳过注释和 DisplayHelper 定义文件
      if (line.trim().startsWith('//') || line.trim().startsWith('/*') || line.trim().startsWith('*')) continue;
      if (relPath.includes('DisplayHelper')) continue;

      // 模式: switch(variable) { ... return "中文" ... }
      // 检测 switch 表达式/语句 + 附近有中文字符串返回
      const switchMatch = line.match(/\bswitch\s*\(/);
      if (!switchMatch) continue;

      // 跳过渲染列 switch（switch (col.Key) 是单元格渲染分支，非枚举中文映射）
      if (/switch\s*\(\s*col\.Key\s*\)/.test(line)) continue;

      // 检查 switch 块中是否有中文字符串返回
      let j = i;
      let braceDepth = 0;
      let foundSwitch = false;
      let hasChineseReturn = false;
      let switchSnippet = '';

      while (j < Math.min(i + 80, lines.length)) {
        switchSnippet += lines[j] + '\n';
        if (lines[j].includes('=>') && /=>\s*"[^"]*[\u4e00-\u9fff]/.test(lines[j]) &&
            !lines[j].includes('DisplayHelper')) {
          hasChineseReturn = true;
        }
        // switch expression: variable switch { pattern => "中文" }
        if (/\bswitch\b/.test(lines[j]) && !foundSwitch) foundSwitch = true;
        // Check closing
        if (lines[j].includes('};') && foundSwitch) break;
        j++;
      }

      if (hasChineseReturn && foundSwitch) {
        // 检查是否使用了 DisplayHelper
        const block = lines.slice(i, Math.min(i + 40, lines.length)).join(' ');
        const usesDisplayHelper = DISPHELPER_METHODS.some(m => block.includes(m));
        const isColumnDef = block.includes('DisplayConverter') || block.includes('EnumOptions');

        if (!usesDisplayHelper && !isColumnDef) {
          violations.push({
            file: relPath,
            line: i + 1,
            snippet: line.trim().substring(0, 100),
          });
        }
      }
    }
  }

  return violations;
}

// ============================================================
// TC40: 枚举值完整性 — DisplayHelper 覆盖度
// ============================================================
function checkTC40() {
  // 读取 DisplayHelper.cs 中的 GetXxxText 方法
  const displayHelperPath = path.join(HELPERS_DIR, 'DisplayHelper.cs');
  if (!fs.existsSync(displayHelperPath)) {
    return { error: 'DisplayHelper.cs not found', violations: [] };
  }

  const dhContent = fs.readFileSync(displayHelperPath, 'utf-8');

  // 提取 DisplayHelper 中注册的所有 GetXxxText 方法
  const registeredMethods = new Set();
  const methodRegex = /public\s+static\s+string\s+(Get\w+Text)\s*\(/g;
  let m;
  while ((m = methodRegex.exec(dhContent)) !== null) {
    registeredMethods.add(m[1]);
  }

  // 提取所有 enum 显示相关的方法
  const enumHelperMethods = new Set();
  const ehMethodRegex = /public\s+static\s+string\s+(Get\w+(?:Display|Name|Text))\s*\(/g;
  while ((m = ehMethodRegex.exec(dhContent)) !== null) {
    enumHelperMethods.add(m[1]);
  }

  // 扫描 .razor.cs 和 .razor 文件，查找 enum 显示调用
  const allCsFiles = [
    ...readAllFiles(PAGES_DIR, '.razor.cs'),
    ...readAllFiles(PAGES_DIR, '.razor'),
    ...readAllFiles(SERVICES_DIR, '.cs'),
  ];

  // 记录所有 enum 转换相关的调用
  const enumDisplayCalls = {};
  for (const file of allCsFiles) {
    const content = fs.readFileSync(file, 'utf-8');
    const relPath = getRelative(file);

    // 找手写 switch → 中文（而不是通过 DisplayHelper）
    if (content.includes('switch') && content.match(/=>\s*"[^"]*[\u4e00-\u9fff]/)) {
      // 检查是否绕过 DisplayHelper
      if (!content.includes('DisplayHelper.')) {
        enumDisplayCalls[relPath] = (enumDisplayCalls[relPath] || 0) + 1;
      }
    }
  }

  return {
    registeredMethods: [...registeredMethods].sort(),
    enumHelperMethods: [...enumHelperMethods].sort(),
    potentialRawCalls: Object.entries(enumDisplayCalls)
      .filter(([_, count]) => count > 0)
      .map(([file, count]) => ({ file, count })),
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
    : ['TC06', 'TC40'];

  let totalViolations = 0;

  console.log('============================================');
  console.log('  枚举一致性扫描 — TC06 + TC40');
  console.log('============================================\n');

  // TC06
  if (selectedTCs.includes('TC06')) {
    console.log('--- TC06: 枚举显示 switch 回潮检测 ---');
    const violations = checkTC06();
    if (violations.length > 0) {
      console.log(`  发现 ${violations.length} 处潜在违规:\n`);
      for (const v of violations) {
        console.log(`  ⚠ ${v.file}:L${v.line}`);
        console.log(`    ${v.snippet}`);
        totalViolations++;
      }
      console.log(`\n  提示: 应使用 DisplayHelper.GetXxxText() 替代手写 switch`);
    } else {
      console.log('  ✅ 未发现手写 switch 映射中文的违规');
    }
    console.log();
  }

  // TC40
  if (selectedTCs.includes('TC40')) {
    console.log('--- TC40: 枚举值完整性 ---');
    const r40 = checkTC40();
    if (r40.error) {
      console.log(`  ❌ ${r40.error}`);
    } else {
      console.log(`  DisplayHelper 注册方法数: ${r40.registeredMethods.length}`);
      console.log(`  方法列表: ${r40.registeredMethods.join(', ')}`);

      if (r40.potentialRawCalls.length > 0) {
        // 提示级：文件内含中文 switch 但整体未引用 DisplayHelper。
        // 可能是业务 switch（关键词解析/档位映射/首字符分类等非枚举显示逻辑），
        // 无法静态判定是否真绕过枚举显示，故降级为人工审查清单，不计违规。
        console.log(`\n  ℹ 潜在原始 switch 调用（需人工审查，是否为枚举→中文显示绕过）:`);
        for (const c of r40.potentialRawCalls) {
          console.log(`    ${c.file} (${c.count} 处)`);
        }
      } else {
        console.log('  ✅ 所有枚举显示调用均通过 DisplayHelper');
      }
    }
    console.log();
  }

  console.log('='.repeat(50));
  console.log(`  违规总数: ${totalViolations}`);
  console.log('='.repeat(50));

  if (totalViolations > 0) {
    process.exit(Math.min(totalViolations, 127));
  } else {
    console.log('✅ 枚举一致性检查通过。');
  }
}

main();
