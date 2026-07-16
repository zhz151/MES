/**
 * 静态扫描：检测 MudIconButton 删除按钮是否缺少 ConfirmDialog
 *
 * 规范项 A12: 所有删除操作必须弹出确认对话框
 * 规则：对数据库发起 DELETE 请求的按钮，必须在处理函数中使用 ConfirmDialog
 * 例外：仅操作内存列表（未保存行）的删除按钮不需要 ConfirmDialog
 *
 * 使用:
 *   node playwright-tests/scan-delete-confirm.mjs
 *
 * 返回码:
 *   0 = 所有删除按钮都有 ConfirmDialog
 *   1 = 发现缺少 ConfirmDialog 的删除按钮
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');
const PAGES_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');
const SHARED_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Shared');

// ============================================================
// 删除相关图标模式
// ============================================================
const DELETE_ICON_PATTERNS = [
  /Icons\.Material\.Filled\.Delete\b/,
  /Icons\.Material\.Filled\.DeleteForever\b/,
  /Icons\.Material\.Outlined\.Delete\b/,
  /Icons\.Material\.Sharp\.Delete\b/,
  /Delete\s*\)/,
];

// ============================================================
// 扫描单个文件
// ============================================================
function scanFile(filePath) {
  const content = fs.readFileSync(filePath, 'utf-8');
  const lines = content.split('\n');
  const results = [];

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    // 找 MudIconButton 行
    if (!line.includes('MudIconButton')) continue;

    // 检查是否有删除图标
    const hasDeleteIcon = DELETE_ICON_PATTERNS.some(p => p.test(line));
    if (!hasDeleteIcon) continue;

    // 提取 OnClick 处理函数名
    const onClickMatch = line.match(/OnClick="@?(\w+(?:\.\w+)?(?:\([^)]*\))?)"/);
    if (!onClickMatch) continue;
    const handlerName = onClickMatch[1].split('(')[0];

    // 检查附近（上下 30 行）是否有 ConfirmDialog
    const start = Math.max(0, i - 30);
    const end = Math.min(lines.length, i + 30);
    const context = lines.slice(start, end).join('\n');
    const hasConfirmDialog = context.includes('ConfirmDialog') ||
                             context.includes('确认删除') ||
                             context.includes('确认作废');

    if (!hasConfirmDialog) {
      results.push({
        line: i + 1,
        handler: handlerName,
        snippet: line.trim().substring(0, 120),
        contextLines: context.split('\n').length,
      });
    }
  }

  return results;
}

// ============================================================
// 扫描 .razor.cs 文件确认 handler 中是否有 ConfirmDialog
// ============================================================
function checkCodeBehind(filePath, handlers) {
  const csPath = filePath + '.cs';
  if (!fs.existsSync(csPath)) return handlers;

  const content = fs.readFileSync(csPath, 'utf-8');
  const stillMissing = [];

  for (const h of handlers) {
    // 在 .cs 文件中找 handler 方法
    const handlerPattern = new RegExp(`(async\\s+)?Task\\s+${h.handler}\\b|async\\s+void\\s+${h.handler}\\b|void\\s+${h.handler}\\b`);
    const handlerMatch = content.match(handlerPattern);
    if (!handlerMatch) {
      // handler 可能在别的文件中，不报错
      stillMissing.push(h);
      continue;
    }

    // 检查 handler 方法体内是否有 ConfirmDialog
    const handlerIndex = handlerMatch.index;
    const methodBody = content.substring(handlerIndex, handlerIndex + 2000);
    if (!methodBody.includes('ConfirmDialog') &&
        !methodBody.includes('确认删除') &&
        !methodBody.includes('确认作废') &&
        !methodBody.includes('删除确认')) {
      stillMissing.push(h);
    }
  }

  return stillMissing;
}

// ============================================================
// 递归扫描目录
// ============================================================
function scanDir(dirPath) {
  const results = [];
  if (!fs.existsSync(dirPath)) return results;
  const entries = fs.readdirSync(dirPath, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dirPath, entry.name);
    if (entry.isDirectory()) {
      results.push(...scanDir(fullPath));
    } else if (entry.name.endsWith('.razor')) {
      const issues = scanFile(fullPath);
      if (issues.length > 0) {
        // 尝试检查 .cs 文件
        const csPath = path.join(path.dirname(fullPath), entry.name + '.cs');
        const remaining = fs.existsSync(csPath) ? checkCodeBehind(fullPath, issues) : issues;
        if (remaining.length > 0) {
          results.push({
            file: path.relative(PROJECT_DIR, fullPath),
            issues: remaining,
          });
        }
      }
    }
  }
  return results;
}

// ============================================================
// 主流程
// ============================================================
function main() {
  console.log('============================================');
  console.log('  删除按钮 ConfirmDialog 覆盖扫描');
  console.log('============================================\n');

  const allResults = [
    ...scanDir(PAGES_DIR),
    ...scanDir(SHARED_DIR),
  ];

  let totalMissing = 0;

  if (allResults.length === 0) {
    console.log('✅ 所有删除按钮均已配置 ConfirmDialog，无遗漏。\n');
    process.exit(0);
  }

  for (const result of allResults) {
    console.log(`\n  ${result.file}`);
    for (const issue of result.issues) {
      console.log(`    L${issue.line}: ${issue.snippet}`);
      console.log(`    → 处理函数: ${issue.handler}`);
      console.log(`    → 附近 ${issue.contextLines} 行内未发现 ConfirmDialog`);
      totalMissing++;
    }
  }

  console.log(`\n--- 汇总 ---`);
  console.log(`  缺少 ConfirmDialog: ${totalMissing}`);

  if (totalMissing > 0) {
    console.log(`\n⚠️  发现 ${totalMissing} 处删除按钮可能缺少确认对话框`);
    console.log(`  请检查:\n`);
    console.log(`  1. 该按钮是否触发服务端 DELETE 请求（是则需要加 ConfirmDialog）`);
    console.log(`  2. 如仅为内存列表删除（未保存的行），则可忽略`);
    console.log(`  3. Handler 可能在 .razor.cs 中使用了 ConfirmDialog 但未检测到\n`);
    process.exit(1);
  }
}

main();
