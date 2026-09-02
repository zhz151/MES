/**
 * 静态分析：MudSelect T="string" 绑定枚举字段回归检测
 *
 * 规范项 A23：下拉绑定枚举字段必须使用 T="枚举类型"（或计算属性模式），
 * 禁止 `MudSelect T="string"` 直接绑定 DTO 枚举类型字段。
 *
 * 检测逻辑：
 *   1. 收集 MES.Core 全部枚举类型名 + 各 DTO 中"枚举类型属性名"集合
 *   2. 扫描 MES.Blazor 全部 .razor 中的 `<MudSelect T="string">`（含 T="string?"）
 *   3. 提取绑定目标（@bind-Value / Value）
 *   4. 若绑定目标最后一段字段名 ∈ 枚举类型属性名集合 → 报告疑似违规
 *
 * 使用: node playwright-tests/scan-mudselect-string.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

// ============================================================
// 1. 枚举类型名集合 + DTO 枚举类型属性名集合
// ============================================================
function walkDir(dir, handler) {
  if (!fs.existsSync(dir)) return;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) walkDir(fullPath, handler);
    else if (entry.name.endsWith('.cs')) handler(fullPath, fs.readFileSync(fullPath, 'utf-8'));
  }
}

function buildEnumInfo() {
  const enumSet = new Set();
  walkDir(path.join(PROJECT_DIR, 'MES.Core'), (file, content) => {
    for (const m of content.matchAll(/\benum\s+(\w+)/g)) enumSet.add(m[1]);
  });

  // DTO 中"类型为枚举的属性名"
  const enumPropNames = new Set();
  const dtosDir = path.join(PROJECT_DIR, 'MES.Core', 'DTOs');
  if (fs.existsSync(dtosDir)) {
    walkDir(dtosDir, (file, content) => {
      const propRegex = /public\s+(?:static\s+)?(?:readonly\s+)?([A-Za-z_][\w.<>\[\],?]*)\s+(\w+)\s*(?:=>|\{|=)/g;
      let m;
      while ((m = propRegex.exec(content)) !== null) {
        const typeName = m[1].replace(/\s+/g, '').replace(/\?$/, '');
        const propName = m[2];
        // 枚举类型（含全限定名取最后段）
        if (enumSet.has(typeName)) {
          enumPropNames.add(propName);
        } else {
          const lastSegment = typeName.split('.').pop();
          if (lastSegment && enumSet.has(lastSegment)) enumPropNames.add(propName);
        }
      }
    });
  }

  return { enumSet, enumPropNames };
}

// ============================================================
// 2. 扫描 .razor 中 MudSelect T="string"
// ============================================================
function scanRazorFiles() {
  const razorFiles = [];
  walkRazor(path.join(PROJECT_DIR, 'MES.Blazor'), razorFiles);
  return razorFiles;
}

function walkRazor(dir, acc) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      // 跳过 node_modules / bin / obj
      if (entry.name === 'node_modules' || entry.name === 'bin' || entry.name === 'obj') continue;
      walkRazor(fullPath, acc);
    } else if (entry.name.endsWith('.razor')) {
      acc.push(fullPath);
    }
  }
}

function extractMudSelects(content) {
  const results = [];
  // 匹配 MudSelect 开标签（可跨行，直到第一个 >）
  const selectRegex = /<MudSelect\s+T="string\??"[^>]*>/gs;
  let match;
  while ((match = selectRegex.exec(content)) !== null) {
    const tag = match[0];
    const line = content.slice(0, match.index).split('\n').length;

    // 提取绑定目标：优先 @bind-Value，其次 Value（排除 ValueChanged）
    let bindMatch = tag.match(/@bind-Value(?::[a-zA-Z-]+)?="([^"]+)"/);
    let valueMatch = tag.match(/(?:^|\s)Value="([^"]+)"/);
    const binding = bindMatch ? bindMatch[1] : (valueMatch ? valueMatch[1] : null);

    if (!binding) continue;

    results.push({ line, tag: tag.trim(), binding });
  }
  return results;
}

// ============================================================
// 3. 判断绑定目标是否疑似绑定枚举字段
// ============================================================
function extractFieldName(binding) {
  const trimmed = binding.trim();
  // 排除模板上下文 / 字符串字面量 / 复杂表达式
  if (trimmed.includes('@context') || trimmed.includes('context.')) return null;
  if (trimmed.startsWith('"') || trimmed.startsWith("'")) return null;
  if (trimmed.includes(' => ')) return null;

  // 取路径最后一段字段名（pg.ProcessName → ProcessName）
  const segments = trimmed.split('.');
  const last = segments[segments.length - 1].trim();
  // 字段名应为合法标识符且 PascalCase（DTO 属性命名），排除含方法调用/索引
  if (!/^[A-Z_][A-Za-z0-9_]*$/.test(last)) return null;
  return last;
}

// ============================================================
// 4. 已知合法绑定白名单（属性名启发式无法分辨的本地编辑缓存场景）
// ============================================================
// 说明：页面本地 EditCache 类以 string 存储枚举英文 Key（如 DeliveryState），
//       T="string" 下拉合法，但属性名与 DTO 枚举属性同名导致启发式误报。
//       此处按绑定目标精确登记；新增同类合法场景时人工追加。
const KNOWN_LEGIT_BINDINGS = new Set([
  'rowCache.DeliveryState', // StandardWorkDayDeliveryStates EditCache.DeliveryState 为 string
]);

// ============================================================
// 5. 主流程
// ============================================================
function main() {
  console.log('============================================');
  console.log('  MudSelect T="string" 绑定枚举字段 回归检测');
  console.log('============================================\n');

  const { enumSet, enumPropNames } = buildEnumInfo();
  console.log(`枚举类型: ${enumSet.size} 个`);
  console.log(`DTO 枚举类型属性名: ${enumPropNames.size} 个\n`);

  const razorFiles = scanRazorFiles();
  console.log(`扫描 .razor 文件: ${razorFiles.length} 个\n`);

  const suspects = [];
  let stringSelectCount = 0;

  for (const file of razorFiles) {
    const content = fs.readFileSync(file, 'utf-8');
    const selects = extractMudSelects(content);

    for (const sel of selects) {
      stringSelectCount++;
      if (KNOWN_LEGIT_BINDINGS.has(sel.binding.trim().replace(/^@/, ''))) continue;
      const fieldName = extractFieldName(sel.binding);
      if (fieldName && enumPropNames.has(fieldName)) {
        suspects.push({
          file: path.relative(PROJECT_DIR, file),
          line: sel.line,
          binding: sel.binding,
          fieldName,
        });
      }
    }
  }

  console.log(`MudSelect T="string" 总数: ${stringSelectCount}`);
  console.log(`疑似绑定枚举字段: ${suspects.length}\n`);

  if (suspects.length > 0) {
    console.log('⚠ 疑似违规明细（需人工确认）:');
    for (const s of suspects) {
      console.log(`  ${s.file}:${s.line}`);
      console.log(`    @bind-Value="${s.binding}" → 字段 "${s.fieldName}" 在 DTO 中为枚举类型`);
    }
    console.log();
  }

  if (suspects.length === 0) {
    console.log('✅ 通过 — 所有 MudSelect T="string" 均未直接绑定 DTO 枚举字段');
    console.log('   （绑定枚举字段的下拉均使用 T=枚举类型 或 计算属性模式）');
  }

  if (suspects.length > 0) process.exitCode = 1;
}

main();
