/**
 * 静态分析：验证 ColumnDef FilterType 与 DTO 属性类型的一致性
 *
 * 检查内容：
 *   1. FilterType="string" 但 DTO 属性是枚举类型 → 应改为 FilterType="enum"
 *   2. FilterType="enum" 但 DTO 属性不是枚举类型 → 应改为 FilterType="string"
 *   3. FilterType="enum" 的列缺少 EnumOptions → 应补充
 *
 * 使用: node playwright-tests/check-filter-types.mjs
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

// ============================================================
// 1. 已知枚举类型列表（从 EnumHelper 中提取）
// ============================================================
const ENUM_TYPES = new Set([
  'MaterialCategory', 'LengthStatus', 'DeliveryState', 'SettlementMethod',
  'SalesOrderStatus', 'PurchaseOrderStatus', 'WorkOrderStatus', 'BatchStatus',
  'ManufacturingItem', 'ProductionType', 'PipeManufacturingType',
  'MaterialPlanStatus', 'InventoryPlanStatus',
  'RawMaterialType', 'FinishedProductType', 'RequirementType',
  'OutboundType', 'CustomerStatus', 'ReworkType',
  'SubcontractOrderStatus', 'SubcontractProcessStatus', 'SectionOutsourceStatus',
  'RepairPriority', 'LifecycleStatus', 'UsageType', 'RunningStatus',
  'RepairOrderStatus', 'EquipmentTaskStatus', 'TaskOrderStatus',
  'InspectionItem', 'DisposalMethod', 'NcrStatus', 'PicklingStatus',
  'ResponsibilityCategory', 'SeverityLevel', 'VerifyResult', 'PipeCategory',
  'SectionStatus',
  'NcrStatus', 'DisposalMethod',
]);

// ============================================================
// 2. 扫描 .razor.cs 中的 ColumnDef 定义
// ============================================================
function findAllColumnDefs(pageDir) {
  const files = fs.readdirSync(pageDir, { withFileTypes: true });
  let results = [];

  for (const file of files) {
    const fullPath = path.join(pageDir, file.name);
    if (file.isDirectory()) {
      results = results.concat(findAllColumnDefs(fullPath));
    } else if (file.name.endsWith('.razor.cs')) {
      const pageDefs = extractColumnDefsFromFile(fullPath);
      if (pageDefs) results.push(pageDefs);
    }
  }
  return results;
}

function extractColumnDefsFromFile(filePath) {
  const content = fs.readFileSync(filePath, 'utf-8');
  const lines = content.split('\n');

  // 找到 DTO 类型：MudTable<DtoType>
  const dtoMatch = content.match(/MudTable<(\w+)>/);
  const dtoType = dtoMatch ? dtoMatch[1] : null;

  // 找到 GetAllColumnDefs / _allColumns 定义
  const colDefStart = content.indexOf('new() { Key = "');
  if (colDefStart === -1) return null;

  // 找到文件中的 ColumnDef 列表
  const columns = [];
  let inColumnDef = false;
  let nextIsKey = false;

  // 用正则提取所有 new() { ... }
  const colRegex = /new\(\)\s*\{([^}]+)\}/g;
  let match;

  while ((match = colRegex.exec(content)) !== null) {
    const block = match[1];

    const keyMatch = block.match(/Key\s*=\s*"([^"]+)"/);
    const labelMatch = block.match(/Label\s*=\s*"([^"]+)"/);
    const filterMatch = block.match(/FilterType\s*=\s*"([^"]+)"/);
    const enumOptionsMatch = block.match(/EnumOptions/);
    const sortKeyMatch = block.match(/SortKey\s*=\s*"([^"]+)"/);

    if (keyMatch) {
      columns.push({
        key: keyMatch[1],
        label: labelMatch ? labelMatch[1] : keyMatch[1],
        filterType: filterMatch ? filterMatch[1] : 'none',
        sortKey: sortKeyMatch ? sortKeyMatch[1] : null,
        hasEnumOptions: !!enumOptionsMatch,
      });
    }
  }

  if (columns.length === 0) return null;

  return {
    file: path.relative(PROJECT_DIR, filePath),
    dtoType,
    columns,
  };
}

// ============================================================
// 3. 扫描 DTO 文件，获取属性类型
// ============================================================
function findAllDtoTypes(dtoDir) {
  const results = {};

  function scan(dir) {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        scan(fullPath);
      } else if (entry.name.endsWith('.cs')) {
        const content = fs.readFileSync(fullPath, 'utf-8');
        const dtoName = path.basename(entry.name, '.cs');

        // 找到所有 public 属性
        const propRegex = /public\s+(\w+\??)\s+(\w+)\s*\{/g;
        let m;
        const props = {};
        while ((m = propRegex.exec(content)) !== null) {
          props[m[2]] = m[1];
        }

        results[dtoName] = props;
      }
    }
  }

  scan(dtoDir);
  return results;
}

// ============================================================
// 4. 判断字符串是否表示枚举类型
// ============================================================
function isEnumType(typeName) {
  if (!typeName) return false;
  const clean = typeName.replace('?', '').replace('[]', '');
  return ENUM_TYPES.has(clean);
}

// ============================================================
// 5. 主流程
// ============================================================
function main() {
  console.log('========================================');
  console.log('  FilterType vs DTO 类型交叉验证');
  console.log('========================================\n');

  // 扫描 DTO 属性
  const dtoDir = path.join(PROJECT_DIR, 'MES.Core', 'DTOs');
  if (!fs.existsSync(dtoDir)) {
    console.log(`DTO 目录不存在: ${dtoDir}`);
    console.log('尝试查找 DTOs 目录...');
    // 查找任何 DTOs 目录
    return;
  }
  const dtoTypes = findAllDtoTypes(dtoDir);
  console.log(`找到 ${Object.keys(dtoTypes).length} 个 DTO 类型\n`);

  // 扫描 Blazor 页面
  const pagesDir = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');
  const pageDefs = findAllColumnDefs(pagesDir);
  console.log(`扫描了 ${pageDefs.length} 个页面文件\n`);

  // 检查
  const issues = [];

  for (const page of pageDefs) {
    for (const col of page.columns) {
      // 如果列没有 FilterType，跳过
      if (col.filterType === 'none') continue;

      // 尝试找到 DTO 属性
      const dtoProps = page.dtoType ? dtoTypes[page.dtoType] : null;
      const propType = dtoProps ? dtoProps[col.key] : null;

      if (col.filterType === 'string') {
        if (propType && isEnumType(propType)) {
          issues.push({
            file: page.file,
            dto: page.dtoType,
            key: col.key,
            label: col.label,
            issue: `FilterType="string" 但 DTO.${col.key} 是枚举类型 (${propType})`,
            severity: 'error',
            fix: `改为 FilterType="enum" + 添加 EnumOptions`,
          });
        }
      } else if (col.filterType === 'enum') {
        if (propType && !isEnumType(propType) && propType !== 'bool' && propType !== 'bool?') {
          issues.push({
            file: page.file,
            dto: page.dtoType,
            key: col.key,
            label: col.label,
            issue: `FilterType="enum" 但 DTO.${col.key} 不是枚举类型 (${propType})`,
            severity: 'warning',
            fix: `需要确认 FilterType 是否正确`,
          });
        }
        if (!col.hasEnumOptions && propType && isEnumType(propType)) {
          issues.push({
            file: page.file,
            dto: page.dtoType,
            key: col.key,
            label: col.label,
            issue: `FilterType="enum" 但缺少 EnumOptions 配置`,
            severity: 'warning',
            fix: `添加 EnumOptions = GetXxxOptions()`,
          });
        }
      }
    }
  }

  // 输出结果
  if (issues.length === 0) {
    console.log('✅ 未发现问题 — 所有 FilterType 与 DTO 属性类型一致');
  } else {
    const errors = issues.filter(i => i.severity === 'error');
    const warnings = issues.filter(i => i.severity === 'warning');

    console.log(`发现 ${issues.length} 个问题（${errors.length} 错误, ${warnings.length} 警告）:\n`);

    for (const issue of issues) {
      const icon = issue.severity === 'error' ? '✗' : '⚠';
      console.log(`  ${icon} [${issue.severity}] ${issue.file}`);
      console.log(`     列: ${issue.key} ("${issue.label}")`);
      console.log(`     DTO: ${issue.dto}.${issue.key} (${dtoTypes[issue.dto]?.[issue.key] || '?'})`);
      console.log(`     问题: ${issue.issue}`);
      console.log(`     修复: ${issue.fix}`);
      console.log();
    }

    console.log(`--- 统计 ---`);
    console.log(`错误: ${errors.length}`);
    console.log(`警告: ${warnings.length}`);
  }

  // 输出 FilterType="string" 的枚举信息（供确认）
  console.log('\n--- 所有 FilterType="string" 的列（含 DTO 类型，确认是否枚举） ---');
  for (const page of pageDefs) {
    const stringCols = page.columns.filter(c => c.filterType === 'string');
    if (stringCols.length > 0) {
      console.log(`\n  ${page.file} (DTO: ${page.dtoType || '?'}):`);
      for (const col of stringCols) {
        const propType = page.dtoType ? (dtoTypes[page.dtoType]?.[col.key] || '?') : '?';
        const isEnum = isEnumType(propType);
        const mark = isEnum ? ' ← 枚举!' : '';
        console.log(`    ${col.key.padEnd(25)} ${propType.padEnd(15)}${mark}`);
      }
    }
  }
}

main();
