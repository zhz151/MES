/**
 * 静态扫描：检测 MudSelect T="string" 绑定枚举字段的违规
 *
 * 规范项 A23: MudSelect 的 T 参数必须使用枚举类型（而非 string）
 * 当 DTO 字段为 string 但对应领域概念是枚举时，应使用计算属性模式：
 *   private MyEnum formField { get => ...; set => dto.Field = value.ToString(); }
 *   <MudSelect T="MyEnum" @bind-Value="formField">
 *
 * 使用:
 *   node playwright-tests/scan-mudselect-string.mjs
 *
 * 返回码:
 *   0 = 无违规
 *   1 = 发现潜在违规
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');
const PAGES_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');
const SHARED_DIR = path.join(PROJECT_DIR, 'MES.Blazor', 'Shared');

// ============================================================
// 已知的枚举类型列表（从 MES.Core/Enums 收集）
// 当 MudSelect T="string" 绑定的值看起来像这些枚举时，需要警告
// ============================================================
const KNOWN_ENUM_TYPES = new Set([
  'NcrStatus', 'PipeCategory', 'DisposalMethod', 'SeverityLevel',
  'VerifyResult', 'ProductionType',
  'ManufacturingItem', 'MaterialStatus', 'BatchStatus',
  'DeliveryState', 'SalesOrderStatus', 'WorkOrderStatus',
  'InspectionResult', 'TestResult', 'FurnaceStatus',
  'PurchaseOrderStatus', 'RepairOrderStatus', 'MaintenanceStatus',
  'EquipmentStatus', 'InventoryStatus', 'InboundSourceType',
  'ProcessInspectionStatus', 'FinalInspectionStatus',
  'MaterialReceiveStatus', 'CertificateStatus',
  'RawMaterialType', 'SteelGrade', 'StandardLevel',
  'ManufactureMethod', 'InspectionCategory', 'ReportType',
]);

// ============================================================
// MudSelectItem 的值看起来像枚举值（非中文，首字母大写英文）
// ============================================================
function looksLikeEnumValue(val) {
  if (!val || val.includes('>')) return false;
  // 纯英文、首字母大写、不含空格或包含下划线 → 疑似枚举值
  return /^[A-Z][a-zA-Z0-9]*$/.test(val) || /^[A-Z][a-zA-Z0-9_]*$/.test(val);
}

// ============================================================
// 扫描 .razor 文件中的 MudSelect T="string"
// ============================================================
function scanFile(filePath) {
  const content = fs.readFileSync(filePath, 'utf-8');
  const lines = content.split('\n');
  const results = [];

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    // 匹配 MudSelect T="string" 或 MudSelect T="string?"
    if (/\bMudSelect\s+T="string"\b/.test(line) || /\bMudSelect\s+T="string\?"/.test(line)) {
      // 收集 MudSelectItem Value 值
      const itemValues = [];
      let j = i + 1;
      while (j < lines.length && j < i + 30) {
        const itemLine = lines[j];
        if (itemLine.includes('</MudSelect>')) break;
        const match = itemLine.match(/MudSelectItem\s+Value="@\(?"([^"]+)"\)?"/);
        if (match) {
          itemValues.push(match[1]);
        } else {
          // 变量绑定: Value="@someVar" 或 Value="@someExpr"
          const varMatch = itemLine.match(/MudSelectItem\s+Value="@([^"]+)"@/);
          if (varMatch) itemValues.push(`<var>:${varMatch[1]}`);
        }
        j++;
      }

      // 检查 Value 值是否看起来像枚举
      const enumLikeValues = itemValues.filter(v => !v.startsWith('<var>') && looksLikeEnumValue(v));

      // 检查附近是否有已知枚举类型引用
      const contextBefore = lines.slice(Math.max(0, i - 10), i).join(' ');
      const bindTarget = line.match(/@bind-Value="(\w+)"/) || line.match(/Value="(\w+)"/);
      const fieldName = bindTarget ? bindTarget[1] : '';

      // 检查绑定字段是否是已知枚举类型
      let suspectEnumType = null;
      for (const enumType of KNOWN_ENUM_TYPES) {
        if (fieldName.toLowerCase().includes(enumType.toLowerCase()) ||
            contextBefore.includes(enumType)) {
          suspectEnumType = enumType;
          break;
        }
      }

      // 检查绑定的字段名是否暗示枚举（如 Status、Type 结尾）
      const fieldHintsEnum = /(Status|Type|Method|Category|Level|Result)$/.test(fieldName);

      if (suspectEnumType || (enumLikeValues.length > 0 && fieldHintsEnum)) {
        results.push({
          line: i + 1,
          column: 'MudSelect T="string"',
          field: fieldName || '(inline)',
          suspectEnumType,
          enumLikeValues,
          itemCount: itemValues.length,
          snippet: line.trim().substring(0, 120),
        });
      }
    }
  }

  return results;
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
      const fileResults = scanFile(fullPath);
      if (fileResults.length > 0) {
        results.push({ file: path.relative(PROJECT_DIR, fullPath), issues: fileResults });
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
  console.log('  MudSelect T="string" 绑定枚举字段扫描');
  console.log('============================================\n');

  const allResults = [
    ...scanDir(PAGES_DIR),
    ...scanDir(SHARED_DIR),
  ];

  let totalIssues = 0;

  for (const result of allResults) {
    console.log(`\n  ${result.file}`);
    for (const issue of result.issues) {
      const hint = issue.suspectEnumType
        ? ` → 疑似枚举类型: ${issue.suspectEnumType}`
        : issue.field
          ? ` → 字段名 "${issue.field}" 暗示枚举`
          : '';
      console.log(`    L${issue.line}: ${issue.snippet}`);
      if (issue.enumLikeValues.length > 0) {
        console.log(`        MudSelectItem 值: ${issue.enumLikeValues.join(', ')}${hint}`);
      }
      totalIssues++;
    }
  }

  console.log(`\n--- 汇总 ---`);
  console.log(`  扫描文件: ${allResults.length > 0 ? 'see above' : '全量扫描完成'}`);
  console.log(`  潜在违规: ${totalIssues}`);

  if (totalIssues > 0) {
    console.log(`\n⚠️  发现 ${totalIssues} 处潜在违规`);
    console.log(`  提示: 确认这些 MudSelect T="string" 是否绑定枚举字段。`);
    console.log(`  如是，应改用计算属性模式:\n`);
    console.log(`    private MyEnum formField {`);
    console.log(`        get => string.IsNullOrEmpty(dto.Field) ? default : Enum.Parse<MyEnum>(dto.Field);`);
    console.log(`        set => dto.Field = value.ToString();`);
    console.log(`    }`);
    console.log(`    <MudSelect T="MyEnum" @bind-Value="formField">`);
    console.log(`\n  如确为 string 字段（如 StandardNo、SectionName），则无违规。`);
    process.exit(1);
  } else {
    console.log(`\n✅ 无违规：所有 MudSelect T="string" 都是合法的 string 字段绑定`);
  }
}

main();
