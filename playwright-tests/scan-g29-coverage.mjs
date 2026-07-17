/**
 * TC41 G29 覆盖率扫描
 *
 * 检查 decimal 字段在 Razor 渲染中是否使用了 ToString("G29") 去零
 *
 * 检查内容：
 *   1. 扫描所有 .razor 和 .razor.cs 文件中 decimal 类型的渲染
 *   2. 标记可能遗漏 G29 的位置
 *   3. 仅做 warning（有些 decimal 字段确实需要保留零）
 *
 * 使用: node playwright-tests/scan-g29-coverage.mjs
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

const PAGE_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');

// G29 渲染模式 — 各种写法
const G29_PATTERNS = [
  /\.ToString\(["']G29["']\)/g,
  /\.ToString\(["']N["']\d*\)/g,  // N2/N3 等也是常见的 decimal 格式化
];

// 可能的遗漏模式 — 直接渲染 decimal 但没加 G29
const SUSPICIOUS_PATTERNS = [
  // 在 Razor 中直接 @item.SomeDecimalField
  /@\w+\.(Weight|Quantity|Amount|Price|Cost|Rate|Ratio|Percentage|Meters|Length|Count|Total|Unit|Area|Volume|Temperature|Hours|Minutes)\b(?!\s*\.ToString)/g,
  // 在 RenderCell 中直接 item.SomeDecimalField
  /item\.\w*(Weight|Quantity|Amount|Price|Cost|Rate|Ratio|Percentage|Meters)\b(?!\s*\.ToString)/g,
];

// 已知的 decimal 后缀关键词
const DECIMAL_SUFFIXES = [
  'Weight', 'Quantity', 'Amount', 'Price', 'Cost', 'Rate', 'Ratio',
  'Percentage', 'Meters', 'Length', 'Area', 'Volume', 'Temperature',
  'Hours', 'Minutes', 'Total', 'Count', 'Unit',
];

// 允许不使用 G29 的场景（如输入框绑定、计算表达式等）
const ALLOWED_CONTEXTS = [
  /@bind-Value/,
  /MudNumericField/,
  /\.ToString\(/,
  /Format\(/,
  /\.Value\s*=/,
];

let totalIssues = 0;
let totalDecimalFieldRenders = 0;
let g29Count = 0;

function scanFile(filePath) {
  const content = fs.readFileSync(filePath, 'utf-8');
  const relPath = path.relative(PROJECT_DIR, filePath);
  const fileIssues = [];

  // 统计 G29 使用
  let g29InFile = 0;
  for (const p of G29_PATTERNS) {
    const matches = content.match(p);
    if (matches) g29InFile += matches.length;
  }
  if (g29InFile > 0) {
    g29Count += g29InFile;
  }

  // 在每个 RenderCell 或 Razor 模板中检查 decimal 字段渲染
  const lines = content.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const lineNum = i + 1;

    // 跳过注释行
    if (line.trimStart().startsWith('//') || line.trimStart().startsWith('/*')) continue;

    // 跳过允许的上下文
    if (ALLOWED_CONTEXTS.some(p => p.test(line))) continue;

    // 检查 decimal 后缀关键词
    for (const suffix of DECIMAL_SUFFIXES) {
      // 匹配 item.FieldName 或 item.FieldName.Value 但没后缀
      const regex = new RegExp(`(item\\s*\\.\\s*|\\@\\s*)\\w*${suffix}\\b(?!\\s*\\.ToString|\\s*\\.Value|\\s*\\()`, 'g');
      const matches = line.match(regex);
      if (matches) {
        // 确认这行确实是渲染输出（在 @ 上下文中或 builder.AddContent 中）
        if (line.includes('@') || line.includes('AddContent') || line.includes('builder.')) {
          totalDecimalFieldRenders++;
          fileIssues.push({
            line: lineNum,
            text: line.trim(),
            field: suffix,
          });
        }
      }
    }
  }

  return { relPath, fileIssues, g29InFile };
}

function scanDir(dir) {
  const files = fs.readdirSync(dir, { withFileTypes: true });
  const results = [];

  for (const file of files) {
    const fullPath = path.join(dir, file.name);
    if (file.isDirectory()) {
      results.push(...scanDir(fullPath));
    } else if (file.name.endsWith('.razor') || file.name.endsWith('.razor.cs')) {
      const result = scanFile(fullPath);
      if (result.fileIssues.length > 0 || result.g29InFile > 0) {
        results.push(result);
      }
      totalIssues += result.fileIssues.length;
    }
  }

  return results;
}

function main() {
  console.log('============================================');
  console.log('  TC41 G29 覆盖率扫描');
  console.log('============================================\n');

  const results = scanDir(PAGE_DIR);

  console.log(`扫描文件: ${results.length} 个包含 decimal 渲染或 G29 的文件\n`);

  // 按文件输出 warning
  let warningCount = 0;
  for (const r of results) {
    if (r.fileIssues.length === 0) continue;
    warningCount += r.fileIssues.length;
    console.log(`\n${r.relPath}`);
    for (const issue of r.fileIssues.slice(0, 5)) {
      console.log(`  L${issue.line}: ${issue.text.substring(0, 100)}`);
    }
    if (r.fileIssues.length > 5) {
      console.log(`  ... 还有 ${r.fileIssues.length - 5} 处`);
    }
  }

  console.log('\n--- 统计 ---');
  console.log(`G29 使用次数: ${g29Count}`);
  console.log(`疑似遗漏: ${warningCount}`);
  console.log(`总计 decimal 渲染: ${totalDecimalFieldRenders}`);

  // G29 覆盖率
  const totalRenders = g29Count + totalDecimalFieldRenders;
  if (totalRenders > 0) {
    const coverage = (g29Count / totalRenders * 100).toFixed(1);
    console.log(`G29 覆盖率: ${coverage}%`);
  }

  // 注意：这是 warning 级别检查，不完全精确
  // decimal 字段在绑定到 MudNumericField 时不需要 G29
  // 只在最终显示（RenderCell/表格列）时需要
  if (warningCount > 0) {
    console.log(`\n⚠ 发现 ${warningCount} 处可能的 G29 遗漏，建议人工审查`);
    console.log('  注意：MudNumericField 绑定等场景不需要 G29，以上可能包含误报');
  } else {
    console.log('\n✅ 未发现明显的 G29 遗漏');
  }
}

main();
