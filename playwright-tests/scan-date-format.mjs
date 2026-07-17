/**
 * TC42 日期格式一致性扫描
 *
 * 检查 Razor 文件中日期格式化是否统一使用 yyyy-MM-dd
 *
 * 检查内容：
 *   1. 扫描所有 .razor 文件中日期格式字符串
 *   2. 标记非标准格式（如 dd/MM/yyyy, MM/dd/yyyy, yyyy/MM/dd 等）
 *   3. 检查 Placeholder="yyyy-MM-dd" 使用
 *
 * 使用: node playwright-tests/scan-date-format.mjs
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

const PAGE_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');

// 允许的日期格式
const ALLOWED_FORMATS = [
  'yyyy-MM-dd',           // 标准日期
  'yyyy-MM-dd HH:mm',     // 标准日期时间
  'yyyy-MM-dd HH:mm:ss',  // 标准日期时间秒
  'MM-dd',                // 月-日（用于图表等）
  'yyyy-MM',              // 年-月（用于图表等）
  'HH:mm',                // 仅时间
  'yyyy年MM月dd日',       // 中文日期（打印场景等）
  'yyyy年M月d日',         // 中文日期（去零）
];

// 禁止/可疑的格式
const SUSPICIOUS_PATTERNS = [
  { pattern: /dd\/MM\/yyyy/, format: 'dd/MM/yyyy', severity: 'error', note: '非标准日期格式（日/月/年）' },
  { pattern: /MM\/dd\/yyyy/, format: 'MM/dd/yyyy', severity: 'error', note: '非标准日期格式（月/日/年）' },
  { pattern: /yyyy\/MM\/dd/, format: 'yyyy/MM/dd', severity: 'warning', note: '建议改用 yyyy-MM-dd' },
  { pattern: /dd\.MM\.yyyy/, format: 'dd.MM.yyyy', severity: 'warning', note: '建议改用 yyyy-MM-dd' },
  { pattern: /yyyy\.MM\.dd/, format: 'yyyy.MM.dd', severity: 'warning', note: '建议改用 yyyy-MM-dd' },
  { pattern: /dd-MM-yyyy/, format: 'dd-MM-yyyy', severity: 'warning', note: '建议改用 yyyy-MM-dd' },
  { pattern: /yyyyMMdd/, format: 'yyyyMMdd', severity: 'info', note: '紧凑格式，如非必要建议用 yyyy-MM-dd' },
  { pattern: /yyyy年MM月dd日/, format: 'yyyy年MM月dd日', severity: 'info', note: '中文日期格式，特定场景允许' },
];

// 检查 Placeholder 中是否使用 yyyy-MM-dd
const PLACEHOLDER_DATE_PATTERN = /Placeholder\s*=\s*"([^"]*(?:yyyy|MM|dd)[^"]*)"/g;

// 检查 ToString 中的日期格式
const TOSTRING_DATE_PATTERN = /\.ToString\(["']([^"']*yyyy[^"']*)["']\)/g;

let totalErrors = 0;
let totalWarnings = 0;
let standardFormatCount = 0;

function scanFile(filePath) {
  const content = fs.readFileSync(filePath, 'utf-8');
  const relPath = path.relative(PROJECT_DIR, filePath);
  const lines = content.split('\n');
  const issues = [];

  // 1. 检查 Placeholder 日期格式
  for (const match of content.matchAll(PLACEHOLDER_DATE_PATTERN)) {
    const format = match[1];
    if (format === 'yyyy-MM-dd' || format === 'yyyy-MM-dd HH:mm' || format === 'yyyy-MM-dd HH:mm:ss') {
      standardFormatCount++;
    }
  }

  // 2. 检查 ToString 日期格式
  for (const match of content.matchAll(TOSTRING_DATE_PATTERN)) {
    const format = match[1];
    const matchStart = match.index;
    const lineNum = content.substring(0, matchStart).split('\n').length;

    if (ALLOWED_FORMATS.includes(format)) {
      standardFormatCount++;
      continue;
    }

    // 检查是否在可疑格式列表中
    let found = false;
    for (const sp of SUSPICIOUS_PATTERNS) {
      if (sp.pattern.test(format)) {
        issues.push({
          line: lineNum,
          format,
          severity: sp.severity,
          note: sp.note,
        });
        if (sp.severity === 'error') totalErrors++;
        else if (sp.severity === 'warning') totalWarnings++;
        found = true;
        break;
      }
    }

    if (!found) {
      // 未知格式 — warning
      issues.push({
        line: lineNum,
        format,
        severity: 'warning',
        note: '未知日期格式，确认是否为统一标准格式',
      });
      totalWarnings++;
    }
  }

  return { relPath, issues };
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
      if (result.issues.length > 0) {
        results.push(result);
      }
    }
  }

  return results;
}

function main() {
  console.log('============================================');
  console.log('  TC42 日期格式一致性扫描');
  console.log('============================================\n');

  const startTime = Date.now();
  const results = scanDir(PAGE_DIR);
  const elapsed = Date.now() - startTime;

  console.log(`扫描完成: ${elapsed}ms\n`);

  if (results.length === 0) {
    console.log('✅ 未发现日期格式问题');
    console.log(`  标准格式使用: ${standardFormatCount} 处`);
    return;
  }

  // 按严重程度输出
  const errors = [];
  const warnings = [];
  const infos = [];

  for (const r of results) {
    for (const issue of r.issues) {
      const entry = { ...issue, file: r.relPath };
      if (issue.severity === 'error') errors.push(entry);
      else if (issue.severity === 'warning') warnings.push(entry);
      else infos.push(entry);
    }
  }

  if (errors.length > 0) {
    console.log(`\n✗ 错误 (${errors.length}):`);
    for (const e of errors) {
      console.log(`  [${e.file}:L${e.line}] "${e.format}" — ${e.note}`);
    }
  }

  if (warnings.length > 0) {
    console.log(`\n⚠ 警告 (${warnings.length}):`);
    for (const w of warnings) {
      console.log(`  [${w.file}:L${w.line}] "${w.format}" — ${w.note}`);
    }
  }

  if (infos.length > 0) {
    console.log(`\nℹ 提示 (${infos.length}):`);
    for (const i of infos) {
      console.log(`  [${i.file}:L${i.line}] "${i.format}" — ${i.note}`);
    }
  }

  console.log('\n--- 统计 ---');
  console.log(`标准格式使用: ${standardFormatCount} 处`);
  console.log(`错误: ${errors.length}`);
  console.log(`警告: ${warnings.length}`);
  console.log(`提示: ${infos.length}`);

  if (errors.length > 0 || warnings.length > 0) {
    console.log('\n⚠ 存在日期格式问题，建议统一为 yyyy-MM-dd');
    if (errors.length > 0) process.exit(1);
  } else {
    console.log('\n✅ 日期格式一致');
  }
}

main();
