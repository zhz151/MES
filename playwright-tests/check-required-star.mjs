// MES Required Star Checker (v3)
// 扫描所有 Blazor .razor 文件，检查必填字段表头是否带红色 *
// 仅报告: DTO 中标注了 [Required] 但表头缺少 <span class="required-star">*</span> 的字段
// 用法: node playwright-tests/check-required-star.mjs

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, '..');

// ======================================================================
// 表头中文名 → 可能的 DTO 属性名映射
// ======================================================================
const HEADER_TO_PROPERTY = {
  '客户编码': 'CustomerCode',
  '业务员': 'Salesman',
  '客户单位': 'CustomerUnit',
  '最终用户': 'EndCustomer',
  '订单号': 'OrderNumber',
  '签订日期': 'SignDate',
  '项次号': 'Sequence',
  '交货日期': 'DeliveryDate',
  '结算方式': 'SettlementMethod',
  '钢管制造类别': 'PipeManufacturingType',
  '标准号': 'StandardNo',
  '交货状态': 'DeliveryState',
  '标准牌号': 'StandardGrade',
  '外径': 'OuterDiameter',
  '壁厚': 'WallThickness',
  '外径上限': 'OuterDiameterPositive',
  '外径下限': 'OuterDiameterNegative',
  '壁厚上限': 'WallThicknessPositive',
  '壁厚下限': 'WallThicknessNegative',
  '长度状态': 'LengthStatus',
  '合同重量': 'ContractWeight',
  '理论重量': 'TheoreticalWeight',
  '数量': 'Quantity',
  '状态': 'Status',
  '客户': 'CustomerId',
  '编号': 'Code',
  '编码': 'Code',
  '名称': 'Name',
  '仓库名称': 'Name',
  '仓库编码': 'Code',
  '排序': 'SortOrder',
  '生产令号': 'ProductionMainNo',
  '子令号': 'ProductionSubNo',
  '工单号': 'WorkOrderNo',
  '批次号': 'BatchNo',
  '牌号': 'PlantGrade',
  '制造规格': 'Specification',
  '米数': 'Meters',
  '入库单号': 'InboundNo',
  '出库单号': 'OutboundNo',
  '入库类型': 'InboundType',
  '入库日期': 'InboundDate',
  '出库类型': 'OutboundType',
  '出库日期': 'OutboundDate',
  '工序名称': 'ProcessName',
};

function guessPropertyName(headerText) {
  // 精确匹配
  if (HEADER_TO_PROPERTY[headerText]) return HEADER_TO_PROPERTY[headerText];
  // 包含匹配（用于"外径公差"→OuterDiameter 等变体）
  for (const [key, val] of Object.entries(HEADER_TO_PROPERTY)) {
    if (headerText.includes(key)) return val;
  }
  return null;
}

// ======================================================================
// 1. 收集所有 DTO 中 [Required] 字段
// ======================================================================
function collectRequiredFields() {
  const result = {};
  function walk(dir) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.isFile() && entry.name.endsWith('Request.cs')) {
        const content = fs.readFileSync(full, 'utf-8');
        const lines = content.split('\n');
        const fields = [];
        for (let i = 0; i < lines.length; i++) {
          const t = lines[i].trim();
          if (/^\[Required/.test(t)) {
            // 往后找属性定义
            for (let j = i + 1; j < Math.min(i + 5, lines.length); j++) {
              const m = lines[j].trim().match(/public\s+\S+\??\s+(\w+)\s*\{/);
              if (m) { fields.push(m[1]); break; }
              if (lines[j].trim().startsWith('[')) continue;
              break;
            }
          }
        }
        if (fields.length > 0) {
          result[entry.name.replace('.cs', '')] = fields;
        }
      }
    }
  }
  walk(path.join(rootDir, 'MES.Core', 'DTOs'));
  return result;
}

// ======================================================================
// 2. 扫描 Razor 文件中的 MudTh 表头
// ======================================================================
function scanRazorFiles(dir) {
  const headers = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory() && !entry.name.startsWith('.') && entry.name !== 'bin' && entry.name !== 'obj') {
      headers.push(...scanRazorFiles(full));
    } else if (entry.isFile() && entry.name.endsWith('.razor')) {
      const content = fs.readFileSync(full, 'utf-8');
      const lines = content.split('\n');
      for (let i = 0; i < lines.length; i++) {
        const match = lines[i].match(/<MudTh[^>]*>(.+)<\/MudTh>/);
        if (match) {
          const inner = match[1];
          const cleanText = inner.replace(/<[^>]+>/g, '').trim();
          const hasStar = inner.includes('required-star');
          if (cleanText && !['操作', '行号', '序号', '#', ''].includes(cleanText)) {
            headers.push({
              file: path.relative(rootDir, full),
              line: i + 1,
              text: cleanText,
              hasStar,
            });
          }
        }
      }
    }
  }
  return headers;
}

// ======================================================================
// 3. 主逻辑
// ======================================================================

console.log('='.repeat(70));
console.log(' MES Required Star Checker');
console.log(' 仅报告: DTO 中 [Required] 但表头缺 * 的字段');
console.log('='.repeat(70));

const dtos = collectRequiredFields();
const allRequiredFields = new Set();
for (const fields of Object.values(dtos)) {
  for (const f of fields) allRequiredFields.add(f);
}

console.log(`\n📋 DTO 中共 ${allRequiredFields.size} 个 [Required] 字段:`);
console.log(`   [${[...allRequiredFields].sort().join(', ')}]`);

const headers = scanRazorFiles(path.join(rootDir, 'MES.Blazor', 'Pages'));

// 只关注 DTO 中标记了 Required 的表头
const relevantHeaders = headers.filter(h => {
  const prop = guessPropertyName(h.text);
  return prop && allRequiredFields.has(prop);
});

const missing = relevantHeaders.filter(h => !h.hasStar);
const ok = relevantHeaders.filter(h => h.hasStar);

console.log(`\n📊 必填字段表头统计:`);
console.log(`   应标注的必填表头: ${relevantHeaders.length}`);
console.log(`   ✅ 已标注 *: ${ok.length}`);
console.log(`   ❌ 缺 *: ${missing.length}`);

if (missing.length > 0) {
  console.log(`\n❌ 以下必填字段表头缺少红色 *:`);
  console.log('-'.repeat(70));
  const byFile = {};
  for (const h of missing) {
    if (!byFile[h.file]) byFile[h.file] = [];
    byFile[h.file].push(h);
  }
  for (const [file, hs] of Object.entries(byFile)) {
    console.log(`\n  📄 ${file}:`);
    for (const h of hs) {
      const prop = guessPropertyName(h.text);
      console.log(`    L${String(h.line).padStart(4)} | ${h.text}  (DTO: ${prop})`);
    }
  }
  console.log('\n💡 修复: 在 <MudTh> 内添加 <span class="required-star">*</span>');
} else {
  console.log(`\n✅ 所有必填字段表头均已正确标注红色 *！`);
}

console.log('\n' + '='.repeat(70));
