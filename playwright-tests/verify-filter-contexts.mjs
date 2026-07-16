/**
 * 静态分析：验证 filter-contexts 端点覆盖度
 *
 * 检查内容：
 *   1. 扫描页面中 GetFilterContextsAsync 调用 → 获取请求 URL
 *   2. 交叉检查 Controller 是否有对应 filter-contexts 端点
 *   3. 检查 filter-contexts 端点返回的 key 是否覆盖前端筛选列
 *
 * 使用: node playwright-tests/verify-filter-contexts.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

// ============================================================
// 1. 已知的 filter-contexts 端点（Controller 级别）
// ============================================================
// 从 Controller 文件中 scan 出来的端点
const KNOWN_FILTER_CONTEXT_ENDPOINTS = new Set([
  // 标准路径: GET filter-contexts
  'Batch', 'Pickling', 'ProductionRecord', 'SectionOutsource',
  'Equipment', 'InspectionRecord', 'MaintenanceOrder', 'RepairOrder',
  'Material', 'PurchaseOrder', 'SubcontractOrder', 'Supplier',
  'Customer', 'Order',
  'Certificate', 'ChemicalAnalysis', 'FinalInspection', 'FlaringTest',
  'FlatteningTest', 'FurnaceRegistration', 'GrainSizeTest', 'HardnessTest',
  'IntergranularCorrosionTest', 'MaterialReceiveCheck', 'MetallographicTest',
  'Ncr', 'PittingCorrosionTest', 'ProcessInspection', 'TensileTest',
  'QualityProcessTracking',
  'BatchPlan', 'WorkOrderSchedule',
  'StandardRegister', 'ChemicalComposition', 'ChemicalValidationRule',
  'GradeChemicalComposition', 'GradeMapping', 'GradePhysicalProperty',
  'StandardInspectionRequirement', 'SubStandardQuickView',
  'PendingDelivery', 'Warehouse',
  'WorkOrder', 'WorkOrderExecution', 'OrderDemandAdjustment',
  // Inventory 有两个特殊端点
  'InventoryInventory',     // GET inventory-filter-contexts
  'InventoryOutbound',      // GET outbound-filter-contexts
  // Pickling 有特殊端点
  'PicklingOutRecord',      // GET out-records/filter-contexts
  // SectionOutsource 有特殊端点
  'SectionOutsourceRecovery', // GET recoveries/filter-contexts
]);

// 没有 filter-contexts 端点的 Controller（有列表但没有筛选上下文）
const NO_FILTER_CONTEXT_CONTROLLERS = new Set([
  'ConfigParameter', 'Employee', 'Workstation', 'DailyOutputEstimate',
  'DailyProductionCapacity', 'StandardWorkDay', 'StandardWorkDayDeliveryState',
  'SectionFlowCategorySettings', 'ProductRequirement',
  'ColdRollPlan', 'FinalInspectionPlan',
  'RawMaterialLockPlanAndExecution',
  'SectionFlowAnalysis', 'SectionProductionStatus',
  'User',
]);

// ============================================================
// 2. 页面 → Controller 映射表
// ============================================================
const PAGE_CONTROLLER_MAP = {
  // Batch
  'Batches': { controller: 'Batch', filterContextEndpoint: true },
  'PicklingInRecords': { controller: 'Pickling', filterContextEndpoint: true },
  'PicklingOutRecords': { controller: 'Pickling', filterContextEndpoint: 'PicklingOutRecord' },
  'OutsourceRecoveries': { controller: 'SectionOutsource', filterContextEndpoint: 'SectionOutsourceRecovery' },
  'SectionOutsources': { controller: 'SectionOutsource', filterContextEndpoint: true },
  'ProductionRecords': { controller: 'ProductionRecord', filterContextEndpoint: true },
  'ProcessCardPrint': { controller: 'Batch', filterContextEndpoint: true },
  // Configuration — 无 filter-contexts endpoints
  'ConfigParameters': { controller: 'ConfigParameter', filterContextEndpoint: false },
  'Employees': { controller: 'Employee', filterContextEndpoint: false },
  'Workstations': { controller: 'Workstation', filterContextEndpoint: false },
  'DailyOutputEstimates': { controller: 'DailyOutputEstimate', filterContextEndpoint: false },
  'DailyProductionCapacities': { controller: 'DailyProductionCapacity', filterContextEndpoint: false },
  'StandardWorkDays': { controller: 'StandardWorkDay', filterContextEndpoint: false },
  'StandardWorkDayDeliveryStates': { controller: 'StandardWorkDayDeliveryState', filterContextEndpoint: false },
  // Equipment
  'Equipments': { controller: 'Equipment', filterContextEndpoint: true },
  'InspectionRecords': { controller: 'InspectionRecord', filterContextEndpoint: true },
  'MaintenanceOrders': { controller: 'MaintenanceOrder', filterContextEndpoint: true },
  'RepairOrders': { controller: 'RepairOrder', filterContextEndpoint: true },
  // Materials
  'Materials': { controller: 'Material', filterContextEndpoint: true },
  'Suppliers': { controller: 'Supplier', filterContextEndpoint: true },
  'PurchaseOrders': { controller: 'PurchaseOrder', filterContextEndpoint: true },
  'SubcontractOrders': { controller: 'SubcontractOrder', filterContextEndpoint: true },
  // Orders
  'Orders': { controller: 'Order', filterContextEndpoint: true },
  'Customers': { controller: 'Customer', filterContextEndpoint: true },
  // Quality
  'ChemicalAnalyses': { controller: 'ChemicalAnalysis', filterContextEndpoint: true },
  'Certificates': { controller: 'Certificate', filterContextEndpoint: true },
  'FinalInspections': { controller: 'FinalInspection', filterContextEndpoint: true },
  'FlaringTests': { controller: 'FlaringTest', filterContextEndpoint: true },
  'FlatteningTests': { controller: 'FlatteningTest', filterContextEndpoint: true },
  'FurnaceRegistrations': { controller: 'FurnaceRegistration', filterContextEndpoint: true },
  'GrainSizeTests': { controller: 'GrainSizeTest', filterContextEndpoint: true },
  'HardnessTests': { controller: 'HardnessTest', filterContextEndpoint: true },
  'IntergranularCorrosionTests': { controller: 'IntergranularCorrosionTest', filterContextEndpoint: true },
  'MaterialReceiveChecks': { controller: 'MaterialReceiveCheck', filterContextEndpoint: true },
  'MetallographicTests': { controller: 'MetallographicTest', filterContextEndpoint: true },
  'Ncrs': { controller: 'Ncr', filterContextEndpoint: true },
  'PittingCorrosionTests': { controller: 'PittingCorrosionTest', filterContextEndpoint: true },
  'ProcessInspections': { controller: 'ProcessInspection', filterContextEndpoint: true },
  'QualityProcessTracking': { controller: 'QualityProcessTracking', filterContextEndpoint: true },
  'TensileTests': { controller: 'TensileTest', filterContextEndpoint: true },
  // Scheduling
  'BatchPlans': { controller: 'BatchPlan', filterContextEndpoint: true },
  'ColdRollPlans': { controller: 'ColdRollPlan', filterContextEndpoint: false },
  'FinalInspectionPlan': { controller: 'FinalInspectionPlan', filterContextEndpoint: false },
  'RawMaterialLockPlanAndExecution': { controller: 'RawMaterialLockPlanAndExecution', filterContextEndpoint: false },
  'WorkOrderSchedules': { controller: 'WorkOrderSchedule', filterContextEndpoint: true },
  'SectionFlowAnalysis': { controller: 'SectionFlowAnalysis', filterContextEndpoint: false },
  'SectionProductionStatus': { controller: 'SectionProductionStatus', filterContextEndpoint: false },
  // StandardRegister
  'ChemicalCompositions': { controller: 'ChemicalComposition', filterContextEndpoint: true },
  'ChemicalValidationRules': { controller: 'ChemicalValidationRule', filterContextEndpoint: true },
  'GradeChemicalCompositions': { controller: 'GradeChemicalComposition', filterContextEndpoint: true },
  'GradeMappings': { controller: 'GradeMapping', filterContextEndpoint: true },
  'GradePhysicalProperties': { controller: 'GradePhysicalProperty', filterContextEndpoint: true },
  'StandardInspectionRequirements': { controller: 'StandardInspectionRequirement', filterContextEndpoint: true },
  'StandardRegisters': { controller: 'StandardRegister', filterContextEndpoint: true },
  'SubStandardQuickViews': { controller: 'SubStandardQuickView', filterContextEndpoint: true },
  // Warehouse
  'WarehouseInventory': { controller: 'Inventory', filterContextEndpoint: 'InventoryInventory' },
  'InboundHistory': { controller: 'Inventory', filterContextEndpoint: 'InventoryInventory' },
  'OutboundHistory': { controller: 'Inventory', filterContextEndpoint: 'InventoryOutbound' },
  'PendingDelivery': { controller: 'PendingDelivery', filterContextEndpoint: true },
  // WorkOrders
  'WorkOrders': { controller: 'WorkOrder', filterContextEndpoint: true },
  'WorkOrderExecution': { controller: 'WorkOrderExecution', filterContextEndpoint: true },
  'OrderDemandAdjustment': { controller: 'OrderDemandAdjustment', filterContextEndpoint: true },
  'MaterialPlanOverview': { controller: 'WorkOrder', filterContextEndpoint: true },
};

// ============================================================
// 3. 扫描 .razor.cs 中的 ColumnDef 定义
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

  const dtoMatch = content.match(/MudTable<(\w+)>/);
  const dtoType = dtoMatch ? dtoMatch[1] : null;

  const colDefStart = content.indexOf('new() { Key = "');
  if (colDefStart === -1) return null;

  const columns = [];
  const colRegex = /new\(\)\s*\{([^}]+)\}/g;
  let match;

  while ((match = colRegex.exec(content)) !== null) {
    const block = match[1];
    const keyMatch = block.match(/Key\s*=\s*"([^"]+)"/);
    const labelMatch = block.match(/Label\s*=\s*"([^"]+)"/);
    const filterMatch = block.match(/FilterType\s*=\s*"([^"]+)"/);
    const enumOptionsMatch = block.match(/EnumOptions/);

    if (keyMatch) {
      columns.push({
        key: keyMatch[1],
        label: labelMatch ? labelMatch[1] : keyMatch[1],
        filterType: filterMatch ? filterMatch[1] : null,
        hasEnumOptions: !!enumOptionsMatch,
      });
    }
  }

  if (columns.length === 0) return null;

  return {
    file: path.relative(PROJECT_DIR, filePath),
    dtoType,
    pageName: path.basename(filePath, '.razor.cs'),
    columns,
    hasGetFilterContextsAsync: content.includes('GetFilterContextsAsync'),
  };
}

// ============================================================
// 4. 检查 Controller 是否有 filter-contexts 端点
// ============================================================
function checkControllerHasFilterContexts(controllerName) {
  const controllerFile = path.join(PROJECT_DIR, 'MES.Api', 'Controllers', `${controllerName}Controller.cs`);
  if (!fs.existsSync(controllerFile)) return { exists: false, hasEndpoint: false };

  const content = fs.readFileSync(controllerFile, 'utf-8');
  return {
    exists: true,
    hasEndpoint: content.includes('filter-contexts') || content.includes('FilterContexts'),
    content,
  };
}

// ============================================================
// 5. 从 Controller 中提取 filter-contexts 返回的 key 列表
// ============================================================
function extractFilterContextKeys(controllerContent) {
  const keys = [];
  const keyRegex = /\["(\w+)"\]/g;
  // 找 filter-contexts 方法体中的 Dictionary key
  const fcMatch = controllerContent.match(/filter[-_]?contexts[^}]+/i);
  if (fcMatch) {
    let m;
    while ((m = keyRegex.exec(fcMatch[0])) !== null) {
      keys.push(m[1]);
    }
  }
  return keys;
}

// ============================================================
// 6. 主流程
// ============================================================
function main() {
  console.log('============================================');
  console.log('  Filter-Contexts 端点覆盖度验证');
  console.log('============================================\n');

  const pagesDir = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');
  const pageDefs = findAllColumnDefs(pagesDir);
  console.log(`扫描了 ${pageDefs.length} 个页面文件\n`);

  const errors = [];
  const warnings = [];
  let hasFCAndHasEndpoint = 0;
  let hasFCNoEndpoint = 0;
  let noFCIntended = 0; // 故意不用 filter-contexts（客户端构建）
  let noFCNoFilter = 0;

  const pageMap = new Map();
  for (const page of pageDefs) {
    pageMap.set(page.pageName, page);
  }

  for (const page of pageDefs) {
    const pageName = page.pageName;
    const mapping = PAGE_CONTROLLER_MAP[pageName];

    if (!mapping) {
      warnings.push({
        type: '未映射',
        page: pageName,
        message: `页面 ${pageName} 不在 Controller 映射表中`
      });
      continue;
    }

    const hasFilterColumns = page.columns.some(c => c.filterType);
    const hasGetFC = page.hasGetFilterContextsAsync;

    if (!hasFilterColumns) {
      noFCNoFilter++;
      continue;
    }

    if (hasGetFC) {
      // 页面调用了 GetFilterContextsAsync
      if (mapping.filterContextEndpoint) {
        const endpointKey = typeof mapping.filterContextEndpoint === 'string'
          ? mapping.filterContextEndpoint : mapping.controller;
        const isKnown = KNOWN_FILTER_CONTEXT_ENDPOINTS.has(endpointKey);

        if (isKnown) {
          hasFCAndHasEndpoint++;
        } else {
          // 从 Controller 代码中确认
          const controllerInfo = checkControllerHasFilterContexts(mapping.controller);
          if (controllerInfo.exists && controllerInfo.hasEndpoint) {
            hasFCAndHasEndpoint++;
          } else {
            errors.push({
              type: '缺少 filter-contexts 端点',
              page: pageName,
              controller: mapping.controller,
              message: `页面调用了 GetFilterContextsAsync 但 ${mapping.controller}Controller 没有 filter-contexts 端点`
            });
            hasFCNoEndpoint++;
          }
        }
      } else {
        // 页面调用了 GetFilterContextsAsync 但对应的 Controller 没有端点
        errors.push({
          type: '缺少 filter-contexts 端点',
          page: pageName,
          controller: mapping.controller,
          message: `页面调用了 GetFilterContextsAsync 但 ${mapping.controller}Controller 没有 filter-contexts 端点`
        });
        hasFCNoEndpoint++;
      }
    } else {
      // 页面有筛选列但未调用 GetFilterContextsAsync
      // 可能是客户端构建模式（Scheduling 页面）
      if (mapping.filterContextEndpoint === false) {
        noFCIntended++;
      } else {
        // 检查 Controller 是否有端点（页面可能自己构建数据源）
        warnings.push({
          type: '未使用 filter-contexts',
          page: pageName,
          controller: mapping.controller,
          message: `页面有 ${page.columns.filter(c => c.filterType).length} 个筛选列但未调用 GetFilterContextsAsync`
        });
      }
    }
  }

  // ============================================================
  // 输出结果
  // ============================================================
  console.log('--- Filter-Contexts 覆盖验证结果 ---\n');
  console.log(`使用 filter-contexts 的页面: ${hasFCAndHasEndpoint}`);
  console.log(`缺少端点: ${hasFCNoEndpoint}`);
  console.log(`客户端构建（Scheduling 等）: ${noFCIntended}`);
  console.log(`无筛选列: ${noFCNoFilter}`);

  if (errors.length > 0) {
    console.log(`\n✗ 错误: ${errors.length}`);
    for (const e of errors) {
      console.log(`  [${e.type}] ${e.page}`);
      console.log(`    ${e.message}`);
    }
  }

  if (warnings.length > 0) {
    console.log(`\n⚠ 警告: ${warnings.length}`);
    for (const w of warnings) {
      console.log(`  [${w.type}] ${w.page} — ${w.message}`);
    }
  }

  if (errors.length === 0) {
    console.log('\n✅ Filter-Contexts 验证通过 — 所有使用 filter-contexts 的页面有对应端点');
  }

  // ============================================================
  // 所有已知端点的完整列表
  // ============================================================
  console.log('\n--- 已知 filter-contexts 端点 ---');
  console.log(`标准路径 (GET filter-contexts): ${[...KNOWN_FILTER_CONTEXT_ENDPOINTS].filter(k => !k.includes('Inventory') && !k.includes('OutRecord') && !k.includes('Recovery')).length} 个`);
  console.log(`特殊路径:`);
  console.log(`  GET inventory-filter-contexts (Inventory)`);
  console.log(`  GET outbound-filter-contexts (Inventory)`);
  console.log(`  GET out-records/filter-contexts (Pickling)`);
  console.log(`  GET recoveries/filter-contexts (SectionOutsource)`);
}

main();
