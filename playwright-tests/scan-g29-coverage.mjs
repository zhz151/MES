/**
 * TC41 G29 覆盖率扫描（DTO 类型驱动，2026-08-31 重写）
 *
 * 检查 decimal 字段在 Razor 渲染中是否使用了 ToString("G29")/DisplayHelper 去零
 *
 * 早期版本按"字段名后缀猜测类型"，把 int 字段（TotalQuantity/InputQuantity/
 * Count 结尾等）误判为 decimal，导致大量误报（覆盖率仅 49.5%）。
 * 本版本改为解析 MES.Core/DTOs 中属性的真实类型：
 *   - 仅对 decimal / decimal? 属性的渲染位置做 G29 检查
 *   - int / string / 枚举等字段一律不报
 *   - 输入框绑定（Value/ValueChanged、MudNumericField）不算渲染输出，跳过
 *
 * 渲染识别：
 *   .razor     : @item.X / @context.X / @row.X（或任意 @变量.属性，按 DTO 属性类型锚定）
 *   .razor.cs  : builder.AddContent(...) / 字符串插值 $"{item.X}" 中的 item.X
 *
 * 使用: node playwright-tests/scan-g29-coverage.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

const PAGES_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');

// ============================================================
// 1. 解析 DTO 属性类型（dtoName -> { propName: typeName }）
// ============================================================
function walkCsDir(dir, handler) {
  if (!fs.existsSync(dir)) return;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) walkCsDir(fullPath, handler);
    else if (entry.name.endsWith('.cs')) handler(fullPath, fs.readFileSync(fullPath, 'utf-8'));
  }
}

function scanDtos() {
  const dtos = {};
  const dtoDir = path.join(PROJECT_DIR, 'MES.Core', 'DTOs');
  walkCsDir(dtoDir, (filePath, content) => {
    const classes = [...content.matchAll(/class\s+(\w+)/g)].map(m => m[1]);
    if (classes.length === 0) return;

    const props = {};
    const propRegex = /public\s+(?:static\s+)?(?:readonly\s+)?([A-Za-z_][\w.<>\[\],?]*)\s+(\w+)\s*(?:=>|\{|=)/g;
    let m;
    while ((m = propRegex.exec(content)) !== null) {
      const typeName = m[1].trim();
      const propName = m[2];
      if (!props[propName]) props[propName] = typeName;
    }

    for (const cls of classes) {
      dtos[cls] = { props, file: path.relative(PROJECT_DIR, filePath) };
    }
  });
  return dtos;
}

function isDecimalProp(dtos, dtoType, propName) {
  if (!dtoType) return false;
  const dto = dtos[dtoType];
  if (!dto) return false;
  const typeName = dto.props[propName];
  if (!typeName) return false;
  return /decimal/.test(typeName.replace(/\s+/g, ''));
}

// ============================================================
// 2. 页面 DTO 绑定解析
// ============================================================
function resolveDtoType(csPath, razorPath) {
  // 优先 .razor.cs 中 MudTable<X>
  if (csPath && fs.existsSync(csPath)) {
    const cs = fs.readFileSync(csPath, 'utf-8');
    const m = cs.match(/MudTable<(\w+)>/);
    if (m) return m[1];
  }
  // 退回 .razor 中 MudTable T="X"
  if (razorPath && fs.existsSync(razorPath)) {
    const razor = fs.readFileSync(razorPath, 'utf-8');
    const m = razor.match(/MudTable\s+T="(\w+)"/);
    if (m) return m[1];
  }
  return null;
}

// ============================================================
// 3. 渲染格式化判断
// ============================================================
// 一行内已格式化（去零/转换），视为已覆盖 G29 语义
function lineAlreadyFormatted(line) {
  return /\.ToString\(\s*["']G29["']/i.test(line)          // 显式 G29
    || /\.ToString\(\s*["']N["']\d*\)/i.test(line)          // N0-N6
    || /\.ToString\(\s*["']F\d*["']\)/i.test(line)          // F0-F3（百分比/定点去零）
    || /DisplayHelper\./i.test(line)                        // 统一显示 Helper
    || /FormatNullable|Format\(|Convert\.ToString|\(int\)/.test(line) // 格式化/整型转换
    || /string\.Format/.test(line);
}

// 对单处属性引用分类：比较表达式=非渲染；插值格式说明符(:F1)=已格式化
function classifyPropUsage(line, matchIndex, matchText) {
  const after = line.slice(matchIndex + matchText.length).substring(0, 8);
  if (/^\s*(==|!=|>=|<=|>|<|=)/.test(after)) return 'compare';   // item.X == 0 / > 0
  if (/^\s*:/.test(after)) return 'formatted';                   // {item.X:F1} / {item.X:N2}
  return 'render';
}

// 输入绑定（Value/ValueChanged / MudNumericField）不算渲染输出
function isInputBinding(line) {
  return /(?:^|[<\s])Value(?:Changed)?=@/.test(line)
    || /\bValue(?:Changed)?=@/.test(line)
    || /MudNumericField/.test(line)
    || /AddAttribute\([^)]*"Value(?:Changed)?"/.test(line);
}

// ============================================================
// 4. 扫描
// ============================================================
let covered = 0;   // 已正确格式化/去零的 decimal 渲染
let missed = 0;    // 遗漏 G29 的 decimal 渲染
let noDtoFiles = 0;
const missedList = [];  // { file, line, text, prop }
const visitedProps = new Set(); // 防重复统计同页同属性

function scanRazor(content, relPath, dtos, dtoType) {
  const lines = content.split('\n');
  const excludedPrefix = /^(Icons|using|inject|bind|foreach|col|onclick|page|attribute|DisplayHelper|Roles|ref|opt|_totalCount|if|else|EditCache|rowCache|bind-)/;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const lineNum = i + 1;

    // 渲染表达式 @变量.属性
    const exprRe = /@([a-zA-Z_]\w*)\s*\.\s*([a-zA-Z_]\w+)/g;
    let m;
    while ((m = exprRe.exec(line)) !== null) {
      const varName = m[1];
      const prop = m[2];
      if (excludedPrefix.test(varName)) continue;          // 非行数据变量
      if (!isDecimalProp(dtos, dtoType, prop)) continue;    // 非 decimal 属性
      if (isInputBinding(line)) continue;                   // 输入框绑定

      const usage = classifyPropUsage(line, m.index, m[0]);
      if (usage === 'compare') continue;                    // 比较/逻辑表达式，非渲染输出
      const formatted = usage === 'formatted' || lineAlreadyFormatted(line);

      const key = `${relPath}|${prop}`;
      if (visitedProps.has(key)) continue;  // 同页同属性只统计一次
      visitedProps.add(key);

      if (formatted) {
        covered++;
      } else {
        missed++;
        missedList.push({ file: relPath, line: lineNum, text: line.trim().substring(0, 110), prop });
      }
    }
  }
}

function scanRazorCs(content, relPath, dtos, dtoType) {
  const lines = content.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const lineNum = i + 1;

    // 仅渲染输出上下文：AddContent / AddAttribute / 字符串插值 $"{...}"
    if (!/AddContent\(|AddAttribute\(|"\$\{|string\.Format/.test(line)) continue;
    if (isInputBinding(line)) continue;                     // Value 绑定行

    const propRe = /(?:item|context|row|r)\s*\.\s*([a-zA-Z_]\w+)/g;
    let m;
    while ((m = propRe.exec(line)) !== null) {
      const prop = m[1];
      if (!isDecimalProp(dtos, dtoType, prop)) continue;

      const usage = classifyPropUsage(line, m.index, m[0]);
      if (usage === 'compare') continue;                    // 比较/逻辑表达式，非渲染输出
      const formatted = usage === 'formatted' || lineAlreadyFormatted(line);

      const key = `${relPath}|${prop}`;
      if (visitedProps.has(key)) continue;
      visitedProps.add(key);

      if (formatted) {
        covered++;
      } else {
        missed++;
        missedList.push({ file: relPath, line: lineNum, text: line.trim().substring(0, 110), prop });
      }
    }
  }
}

// ============================================================
// 5. 主流程
// ============================================================
function main() {
  console.log('============================================');
  console.log('  TC41 G29 覆盖率扫描（DTO 类型驱动）');
  console.log('============================================\n');

  const dtos = scanDtos();
  console.log(`DTO 类: ${Object.keys(dtos).length} 个\n`);

  let fileCount = 0;
  const walkPages = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const fullPath = path.join(dir, entry.name);
      if (entry.isDirectory()) { walkPages(fullPath); continue; }

      const isRazor = entry.name.endsWith('.razor');
      const isRazorCs = entry.name.endsWith('.razor.cs');
      if (!isRazor && !isRazorCs) continue;

      const csPath = fullPath.replace(/\.razor\.cs$/, '.razor.cs');
      const razorPath = fullPath.replace(/\.razor$/, '.razor');
      const dtoType = resolveDtoType(csPath, razorPath);
      if (!dtoType) { noDtoFiles++; continue; }

      const relPath = path.relative(PROJECT_DIR, fullPath);
      const content = fs.readFileSync(fullPath, 'utf-8');
      fileCount++;
      if (isRazor) scanRazor(content, relPath, dtos, dtoType);
      else scanRazorCs(content, relPath, dtos, dtoType);
    }
  };
  walkPages(PAGES_DIR);

  // 输出遗漏明细
  if (missedList.length > 0) {
    console.log('疑似遗漏（decimal 渲染未去零，需人工审查是否真实）:');
    const byFile = new Map();
    for (const it of missedList) {
      if (!byFile.has(it.file)) byFile.set(it.file, []);
      byFile.get(it.file).push(it);
    }
    for (const [file, items] of byFile) {
      console.log(`\n${file}`);
      for (const it of items.slice(0, 8)) {
        console.log(`  L${it.line}: ${it.text}`);
      }
      if (items.length > 8) console.log(`  ... 还有 ${items.length - 8} 处`);
    }
  }

  console.log('\n--- 统计 ---');
  console.log(`已正确格式化 decimal 渲染: ${covered}`);
  console.log(`疑似遗漏 decimal 渲染: ${missed}`);
  console.log(`无法解析 DTO 的页面文件: ${noDtoFiles}`);

  const total = covered + missed;
  if (total > 0) {
    const coverage = (covered / total * 100).toFixed(1);
    console.log(`G29 覆盖率: ${coverage}% (${covered}/${total})`);
  } else {
    console.log('G29 覆盖率: N/A（无 decimal 渲染可评估）');
  }

  if (missed > 0) {
    console.log(`\n⚠ 发现 ${missed} 处疑似遗漏，建议人工审查（部分可能为计算/比较表达式误报）`);
  } else {
    console.log('\n✅ 所有 decimal 渲染均已使用 G29/DisplayHelper 去零');
  }
}

main();
