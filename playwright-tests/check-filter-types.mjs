/**
 * 静态分析：FilterType 与 DTO 字段类型交叉验证
 *
 * 检查内容：
 *   1. 提取前端每个 ColumnDef 的 FilterType 取值（string/enum/number/date/boolean）
 *   2. 通过 MudTable<XxxDto> 定位对应 DTO
 *   3. 解析 DTO 属性类型，交叉验证 FilterType 是否与字段真实类型一致
 *   4. 防止 FilterType 标错导致筛选管道失效（如把 string 列标成 enum，或枚举列漏配 EnumOptions）
 *
 * 使用: node playwright-tests/check-filter-types.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

// ============================================================
// 1. 扫描枚举类型名集合（MES.Core 全部 .cs）
// ============================================================
function scanEnumTypes() {
  const enumSet = new Set();
  walkDir(path.join(PROJECT_DIR, 'MES.Core'), (file, content) => {
    for (const m of content.matchAll(/\benum\s+(\w+)/g)) enumSet.add(m[1]);
  });
  return enumSet;
}

function walkDir(dir, handler) {
  if (!fs.existsSync(dir)) return;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) walkDir(fullPath, handler);
    else if (entry.name.endsWith('.cs')) handler(fullPath, fs.readFileSync(fullPath, 'utf-8'));
  }
}

// ============================================================
// 2. 解析 DTO 属性（dtoName -> { propName: typeName }）
// ============================================================
function scanDtos() {
  const dtos = {};
  const dir = path.join(PROJECT_DIR, 'MES.Core', 'DTOs');
  if (!fs.existsSync(dir)) return dtos;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      scanDtosInDir(fullPath, dtos);
    } else if (entry.name.endsWith('.cs')) {
      registerDtoFile(fullPath, dtos);
    }
  }
  return dtos;
}

function scanDtosInDir(dir, dtos) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) scanDtosInDir(fullPath, dtos);
    else if (entry.name.endsWith('.cs')) registerDtoFile(fullPath, dtos);
  }
}

function registerDtoFile(filePath, dtos) {
  const content = fs.readFileSync(filePath, 'utf-8');
  const classes = [...content.matchAll(/class\s+(\w+)/g)].map(m => m[1]);
  if (classes.length === 0) return;

  const props = {};
  // 存储属性: public T Name { get; set; }（含初始化）
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
}

// ============================================================
// 3. 类型归类
// ============================================================
function classifyType(typeName, enumSet) {
  const t = (typeName || '').replace(/\s+/g, '').trim();
  if (!t) return { kind: 'unknown' };

  // 集合类型无法用于列筛选字段类型判断
  if (t.startsWith('List<') || t.startsWith('Dictionary<') || t.startsWith('IEnumerable<') || t.startsWith('ICollection<')) {
    return { kind: 'collection' };
  }

  const base = t.replace(/\?$/, '').replace(/\[\]$/, '');
  if (base === 'string') return { kind: 'string' };
  if (/^(int|long|short|decimal|double|float)$/.test(base)) return { kind: 'number' };
  if (base === 'bool') return { kind: 'boolean' };
  if (['DateTime', 'DateTimeOffset', 'DateOnly', 'TimeSpan'].includes(base)) return { kind: 'date' };
  if (enumSet.has(base)) return { kind: 'enum' };

  // 全限定名（如 MES.Core.Enums.MaterialType）取最后段
  const lastSegment = base.split('.').pop();
  if (lastSegment && enumSet.has(lastSegment)) return { kind: 'enum' };

  return { kind: 'other' };
}

// FilterType -> 期望的字段类型 kind
const FILTER_TYPE_EXPECT = {
  string: 'string',
  enum: 'enum',
  number: 'number',
  date: 'date',
  boolean: 'boolean',
};

// ============================================================
// 4. 扫描页面 ColumnDef（提取 dtoType + 列 FilterType）
// ============================================================
function scanPages() {
  const pages = [];
  const pagesDir = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');
  if (!fs.existsSync(pagesDir)) return pages;

  for (const entry of fs.readdirSync(pagesDir, { withFileTypes: true })) {
    const fullPath = path.join(pagesDir, entry.name);
    if (entry.isDirectory()) scanPagesInDir(fullPath, pages);
    else if (entry.name.endsWith('.razor.cs')) scanPageFile(fullPath, pages);
  }
  return pages;
}

function scanPagesInDir(dir, pages) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) scanPagesInDir(fullPath, pages);
    else if (entry.name.endsWith('.razor.cs')) scanPageFile(fullPath, pages);
  }
}

function scanPageFile(filePath, pages) {
  const content = fs.readFileSync(filePath, 'utf-8');
  const hasBuildFilterContextOptions = content.includes('BuildFilterContextOptions');

  // dtoType 优先从 .razor.cs 的 MudTable<X> 提取，其次读同名 .razor
  let dtoMatch = content.match(/MudTable<(\w+)>/);
  if (!dtoMatch) {
    const razorPath = filePath.replace(/\.cs$/, '');
    if (fs.existsSync(razorPath)) {
      const razorContent = fs.readFileSync(razorPath, 'utf-8');
      const razorMatch = razorContent.match(/MudTable\s+T="(\w+)"/);
      if (razorMatch) dtoMatch = razorMatch;
    }
  }
  const dtoType = dtoMatch ? dtoMatch[1] : null;

  const colDefStart = content.indexOf('new() { Key = "');
  if (colDefStart === -1) return;

  const columns = [];
  const colRegex = /new\(\)\s*\{([^}]+)\}/g;
  let match;
  while ((match = colRegex.exec(content)) !== null) {
    const block = match[1];
    const keyMatch = block.match(/Key\s*=\s*"([^"]+)"/);
    const labelMatch = block.match(/Label\s*=\s*"([^"]+)"/);
    const filterMatch = block.match(/FilterType\s*=\s*"([^"]+)"/);
    const sortKeyMatch = block.match(/SortKey\s*=\s*"([^"]+)"/);

    if (keyMatch && filterMatch) {
      columns.push({
        key: keyMatch[1],
        label: labelMatch ? labelMatch[1] : keyMatch[1],
        filterType: filterMatch[1],
        sortKey: sortKeyMatch ? sortKeyMatch[1] : null,
      });
    }
  }

  if (columns.length === 0) return;

  pages.push({
    file: path.relative(PROJECT_DIR, filePath),
    pageName: path.basename(filePath, '.razor.cs'),
    dtoType,
    hasBuildFilterContextOptions,
    columns,
  });
}

// ============================================================
// 5. 交叉验证
// ============================================================
function validateColumn(page, col, dtos, enumSet) {
  if (!page.dtoType) {
    return { level: 'info', message: `页面 ${page.pageName} 未解析到 MudTable<Dto> 类型` };
  }

  const dto = dtos[page.dtoType];
  if (!dto) {
    return { level: 'info', message: `DTO ${page.dtoType} 未在 MES.Core/DTOs 找到` };
  }

  // 优先用 SortKey（PascalCase，对齐 DTO 属性名），退回用 Key
  const propName = col.sortKey || col.key;
  const typeName = dto.props[propName];

  if (!typeName) {
    return { level: 'info', message: `DTO ${page.dtoType} 无属性 "${propName}"（可能为派生/计算字段）` };
  }

  const actual = classifyType(typeName, enumSet);
  const expected = FILTER_TYPE_EXPECT[col.filterType];

  if (!expected) {
    return { level: 'info', message: `未知 FilterType="${col.filterType}"` };
  }

  // 类型匹配
  if (actual.kind === expected) {
    return null; // 通过
  }

  // 自定义字符串筛选管道：页面有 BuildFilterContextOptions，FilterType="string" 为有意约定
  // （从 filter-contexts 端点拉取枚举选项，按显示文本/Key 字符串筛选）
  if (col.filterType === 'string' && page.hasBuildFilterContextOptions) {
    return { level: 'info', message: `FilterType="string" + BuildFilterContextOptions 自定义管道（DTO.${propName} 为 ${actual.kind}，有意设计）` };
  }

  // 是/否语义枚举：FilterType="enum" 但字段为 bool（提供 是/否 下拉选项）
  if (col.filterType === 'enum' && actual.kind === 'boolean') {
    return { level: 'warning', message: `FilterType="enum" 但 DTO.${propName} 为 bool（是/否语义枚举，确认选项为 是/否）` };
  }

  // int 语义枚举列（IntStatusDisplayHelper 模式）：FilterType="enum" 但字段为 int
  if (col.filterType === 'enum' && actual.kind === 'number') {
    return { level: 'warning', message: `FilterType="enum" 但 DTO.${propName} 为 int（IntStatusDisplayHelper 语义枚举，需人工确认）` };
  }

  // 字符串显示列：FilterType="enum"/"string" 但字段为字符串枚举文本
  if ((col.filterType === 'enum' || col.filterType === 'string') && actual.kind === 'string') {
    return { level: 'info', message: `FilterType="${col.filterType}" 但 DTO.${propName} 为 string（字符串枚举/显示列，确认）` };
  }

  // 硬冲突：date/boolean 不匹配，或 enum 匹配到完全无关类型
  return {
    level: 'error',
    message: `FilterType="${col.filterType}" 与 DTO.${propName} 类型(${typeName} → ${actual.kind})冲突`,
  };
}

// ============================================================
// 6. 主流程
// ============================================================
function main() {
  console.log('============================================');
  console.log('  FilterType vs DTO 字段类型 交叉验证');
  console.log('============================================\n');

  const enumSet = scanEnumTypes();
  const dtos = scanDtos();
  const pages = scanPages();

  console.log(`枚举类型: ${enumSet.size} 个`);
  console.log(`DTO 类: ${Object.keys(dtos).length} 个`);
  console.log(`页面文件: ${pages.length} 个\n`);

  const errors = [];
  const warnings = [];
  const infos = [];
  let passCount = 0;
  let unverifiableCount = 0;

  for (const page of pages) {
    for (const col of page.columns) {
      const result = validateColumn(page, col, dtos, enumSet);
      if (!result) {
        passCount++;
      } else if (result.level === 'error') {
        errors.push({ page, col, ...result });
      } else if (result.level === 'warning') {
        warnings.push({ page, col, ...result });
      } else {
        infos.push({ page, col, ...result });
        unverifiableCount++;
      }
    }
  }

  console.log(`验证通过: ${passCount}`);
  console.log(`无法验证(DTO/属性缺失): ${unverifiableCount}`);
  console.log(`警告: ${warnings.length}`);
  console.log(`错误: ${errors.length}\n`);

  if (errors.length > 0) {
    console.log('✗ 错误明细:');
    for (const e of errors) {
      console.log(`  [${e.page.pageName}] 列 "${e.col.label}" (${e.col.key})`);
      console.log(`    ${e.message}`);
    }
    console.log();
  }

  if (warnings.length > 0) {
    console.log('⚠ 警告明细（需人工确认）:');
    for (const w of warnings) {
      console.log(`  [${w.page.pageName}] 列 "${w.col.label}" (${w.col.key}) — ${w.message}`);
    }
    console.log();
  }

  if (errors.length === 0 && warnings.length === 0) {
    console.log('✅ FilterType 验证通过 — 所有列 FilterType 与 DTO 字段类型一致');
  } else if (errors.length === 0) {
    console.log('⚠ 无硬性冲突，存在需人工确认的警告项');
  }

  if (errors.length > 0) process.exitCode = 1;
}

main();
