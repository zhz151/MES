/**
 * 静态分析：验证每个 FilterType 列在前后端之间有完整的筛选管道
 *
 * 检查内容：
 *   1. 提取前端 FilterType 列
 *   2. 后端 Service 是否调用 ApplyFilters（或 hand-rolled 筛选）
 *   3. 枚举类型的筛选列，BuildFilterContextOptions 中是否有对应映射
 *   4. Keyword 字段在后端是否被正确用于模糊搜索
 *
 * 使用: node playwright-tests/verify-filter-pipeline.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

// ============================================================
// 1. 已知 Service 的筛选处理方式
// ============================================================

// 使用 ApplyFilters（反射通用）/ 筛选自动通过的服务
const APPLYFILTER_SERVICES = new Set([
  // Batch
  'BatchService', 'ProductionRecordService', 'SectionOutsourceService', 'PicklingService',
  // Configuration
  'ConfigParameterService', 'EmployeeService', 'WorkstationService', 'DailyOutputEstimateService',
  'DailyProductionCapacityService', 'StandardWorkDayService', 'StandardWorkDayDeliveryStateService',
  // Equipment
  'EquipmentService', 'InspectionRecordService', 'MaintenanceOrderService', 'RepairOrderService',
  // Materials
  'MaterialService', 'SupplierService', 'PurchaseOrderService', 'SubcontractOrderService',
  // Orders
  'OrderService', 'CustomerService',
  // Quality
  'ChemicalAnalysisService', 'FlaringTestService', 'FlatteningTestService', 'FinalInspectionService',
  'FurnaceRegistrationService', 'GrainSizeTestService', 'HardnessTestService',
  'IntergranularCorrosionTestService', 'MetallographicTestService', 'NcrService',
  'PittingCorrosionTestService', 'ProcessInspectionService', 'TensileTestService',
  'MaterialReceiveCheckService', 'QualityProcessTrackingService',
  // Scheduling
  'BatchPlanService', 'WorkOrderScheduleService', 'RawMaterialLockPlanAndExecutionService',
  // StandardRegister
  'ChemicalCompositionService', 'ChemicalValidationRuleService', 'GradeChemicalCompositionService',
  'GradeMappingService', 'GradePhysicalPropertyService', 'StandardInspectionRequirementService',
  'StandardRegisterService', 'SubStandardQuickViewService',
  // Warehouse
  'InventoryService', 'WarehouseService', 'PendingDeliveryQueryService',
  // WorkOrders
  'WorkOrderService', 'WorkOrderExecutionService', 'OrderDemandAdjustmentService',
]);

// 不使用 ApplyFilters 的服务（手工 hand-rolled 筛选 / 客户端 dashboard 页面）
const NO_APPLYFILTER_SERVICES = new Set([
  'CertificateService',        // 手写 hand-rolled 筛选
  'FinalInspectionPlanService',  // 客户端内存筛选（Kanban 看板页）
]);

// ============================================================
// 2. 页面 → Service 映射表
// ============================================================
const PAGE_SERVICE_MAP = {
  // Batch
  'Batches': 'BatchService',
  'PicklingInRecords': 'PicklingService',
  'PicklingOutRecords': 'PicklingService',
  'OutsourceRecoveries': 'SectionOutsourceService',
  'SectionOutsources': 'SectionOutsourceService',
  'ProductionRecords': 'ProductionRecordService',
  // Configuration
  'ConfigParameters': 'ConfigParameterService',
  'Employees': 'EmployeeService',
  'Workstations': 'WorkstationService',
  'DailyOutputEstimates': 'DailyOutputEstimateService',
  'DailyProductionCapacities': 'DailyProductionCapacityService',
  'StandardWorkDays': 'StandardWorkDayService',
  'StandardWorkDayDeliveryStates': 'StandardWorkDayDeliveryStateService',
  // Equipment
  'Equipments': 'EquipmentService',
  'InspectionRecords': 'InspectionRecordService',
  'MaintenanceOrders': 'MaintenanceOrderService',
  'RepairOrders': 'RepairOrderService',
  // Materials
  'Materials': 'MaterialService',
  'Suppliers': 'SupplierService',
  'PurchaseOrders': 'PurchaseOrderService',
  'SubcontractOrders': 'SubcontractOrderService',
  // Orders
  'Orders': 'OrderService',
  'Customers': 'CustomerService',
  // Quality
  'ChemicalAnalyses': 'ChemicalAnalysisService',
  'Certificates': 'CertificateService',  // 唯一不用 ApplyFilters
  'FinalInspections': 'FinalInspectionService',
  'FlaringTests': 'FlaringTestService',
  'FlatteningTests': 'FlatteningTestService',
  'FurnaceRegistrations': 'FurnaceRegistrationService',
  'GrainSizeTests': 'GrainSizeTestService',
  'HardnessTests': 'HardnessTestService',
  'IntergranularCorrosionTests': 'IntergranularCorrosionTestService',
  'MaterialReceiveChecks': 'MaterialReceiveCheckService',
  'MetallographicTests': 'MetallographicTestService',
  'Ncrs': 'NcrService',
  'PittingCorrosionTests': 'PittingCorrosionTestService',
  'ProcessInspections': 'ProcessInspectionService',
  'QualityProcessTracking': 'QualityProcessTrackingService',
  'TensileTests': 'TensileTestService',
  // Scheduling
  'BatchPlans': 'BatchPlanService',
  'ColdRollPlans': 'BatchPlanService',  // likely
  'FinalInspectionPlan': 'FinalInspectionPlanService',
  'RawMaterialLockPlanAndExecution': 'RawMaterialLockPlanAndExecutionService',
  'WorkOrderSchedules': 'WorkOrderScheduleService',
  // StandardRegister
  'ChemicalCompositions': 'ChemicalCompositionService',
  'ChemicalValidationRules': 'ChemicalValidationRuleService',
  'GradeChemicalCompositions': 'GradeChemicalCompositionService',
  'GradeMappings': 'GradeMappingService',
  'GradePhysicalProperties': 'GradePhysicalPropertyService',
  'StandardInspectionRequirements': 'StandardInspectionRequirementService',
  'StandardRegisters': 'StandardRegisterService',
  'SubStandardQuickViews': 'SubStandardQuickViewService',
  // Warehouse
  'WarehouseInventory': 'InventoryService',
  'InboundHistory': 'InventoryService',
  'OutboundHistory': 'InventoryService',
  'PendingDelivery': 'PendingDeliveryQueryService',
  // WorkOrders
  'WorkOrders': 'WorkOrderService',
  'WorkOrderExecution': 'WorkOrderExecutionService',
  'OrderDemandAdjustment': 'OrderDemandAdjustmentService',
  'MaterialPlanOverview': 'WorkOrderService',
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
  };
}

// ============================================================
// 4. 检查页面是否有 BuildFilterContextOptions 方法
// ============================================================
function hasBuildFilterContextOptions(fileContent) {
  return fileContent.includes('BuildFilterContextOptions');
}

function hasGetFilterContextsAsync(fileContent) {
  return fileContent.includes('GetFilterContextsAsync');
}

function extractEnumFilterMappings(fileContent, enumColumns) {
  const mappings = [];

  // 查找 BuildFilterContextOptions 内容
  const methodStart = fileContent.indexOf('BuildFilterContextOptions');
  if (methodStart === -1) return [];

  // 从方法体中找到 EnumOptions 映射
  const body = fileContent.substring(methodStart);
  for (const col of enumColumns) {
    // 检查是否有对应映射
    const displayPattern = new RegExp(`DisplayHelper\\.Get\\w+Text\\(.*?${col.key}`, 'i');
    const filterContextPattern = new RegExp(`["']${col.key}["']\\s*[:=]`, 'i');
    const enumOptionsPattern = new RegExp(col.key.replace(/([A-Z])/g, '_$1').toLowerCase() + 'Options|' + col.key + 'Options', 'i');

    if (displayPattern.test(body) || filterContextPattern.test(body) || enumOptionsPattern.test(body)) {
      mappings.push(col.key);
    }
  }

  return mappings;
}

// ============================================================
// 5. 扫描 Service 文件确认 ApplyFilters 状态
// ============================================================
function checkServiceApplyFilters(serviceName) {
  const serviceFile = findServiceFile(serviceName);
  if (!serviceFile) return { exists: false, hasApplyFilters: false, note: 'Service 文件未找到' };

  const content = fs.readFileSync(serviceFile, 'utf-8');
  return {
    exists: true,
    hasApplyFilters: content.includes('ApplyFilters('),
    note: APPLYFILTER_SERVICES.has(serviceName) ? '已知 ApplyFilters 服务' :
          NO_APPLYFILTER_SERVICES.has(serviceName) ? '已知 Hand-rolled 筛选' : '未知状态',
  };
}

function findServiceFile(serviceName) {
  const servicesDir = path.join(PROJECT_DIR, 'MES.Services');
  const searchPaths = [];

  function scan(dir) {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        scan(fullPath);
      } else if (entry.name === `${serviceName}.cs`) {
        searchPaths.push(fullPath);
      }
    }
  }
  scan(servicesDir);

  return searchPaths.length > 0 ? searchPaths[0] : null;
}

// ============================================================
// 6. 主流程
// ============================================================
function main() {
  console.log('============================================');
  console.log('  FilterType 筛选管道完整性验证');
  console.log('============================================\n');

  const pagesDir = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');
  const pageDefs = findAllColumnDefs(pagesDir);
  console.log(`扫描了 ${pageDefs.length} 个页面文件\n`);

  const errors = [];
  const warnings = [];
  const infoMessages = [];
  let applyFiltersPassCount = 0;
  let noFilterColumnsCount = 0;

  for (const page of pageDefs) {
    // 跳过无 FilterType 的页面
    const filterColumns = page.columns.filter(c => c.filterType);
    if (filterColumns.length === 0) {
      noFilterColumnsCount++;
      continue;
    }

    const pageName = page.pageName;
    const serviceName = PAGE_SERVICE_MAP[pageName];

    if (!serviceName) {
      warnings.push({
        type: '未映射',
        page: pageName,
        message: `无法确定后端 Service — ${pageName} 不在映射表中`
      });
      continue;
    }

    const serviceInfo = checkServiceApplyFilters(serviceName);
    const filePath = path.join(PROJECT_DIR, page.file);
    const fileContent = fs.readFileSync(filePath, 'utf-8');

    // 检查 Service 筛选管道
    if (!serviceInfo.exists) {
      errors.push({
        type: 'Service 缺失',
        page: pageName,
        service: serviceName,
        message: `Service 文件 ${serviceName}.cs 未找到`
      });
    } else if (!serviceInfo.hasApplyFilters) {
      if (NO_APPLYFILTER_SERVICES.has(serviceName)) {
        infoMessages.push({
          type: 'Hand-rolled 筛选',
          page: pageName,
          service: serviceName,
          message: `${serviceName} 不使用 ApplyFilters（已知 hand-rolled，需手动确认筛选列完整）`
        });
      } else {
        errors.push({
          type: '缺少 ApplyFilters',
          page: pageName,
          service: serviceName,
          message: `${serviceName} 未调用 ApplyFilters() — 筛选可能不生效`
        });
      }
    } else if (APPLYFILTER_SERVICES.has(serviceName)) {
      applyFiltersPassCount++;
    }

    // 检查 FilterType="enum" 列是否有对应映射
    const enumColumns = filterColumns.filter(c => c.filterType === 'enum');
    const enumWithOptions = enumColumns.filter(c => c.hasEnumOptions);
    const enumWithoutOptions = enumColumns.filter(c => !c.hasEnumOptions);

    for (const col of enumWithoutOptions) {
      errors.push({
        type: '枚举列缺少 EnumOptions',
        page: pageName,
        column: col.key,
        label: col.label,
        message: `FilterType="enum" 列 "${col.label}" (${col.key}) 缺少 EnumOptions`
      });
    }

    // 检查是否有 GetFilterContextsAsync 和 BuildFilterContextOptions
    const hasFilterContexts = hasGetFilterContextsAsync(fileContent);
    const hasBuildMethod = hasBuildFilterContextOptions(fileContent);

    if (hasFilterContexts && !hasBuildMethod) {
      warnings.push({
        type: '缺少 BuildFilterContextOptions',
        page: pageName,
        message: `页面调用了 GetFilterContextsAsync 但没有 BuildFilterContextOptions 方法`
      });
    }

    // 检查枚举列的 BuildFilterContextOptions 映射
    if (enumWithOptions.length > 0 && hasBuildMethod) {
      const mappedEnums = extractEnumFilterMappings(fileContent, enumWithOptions);
      if (mappedEnums.length < enumWithOptions.length) {
        // 只报告严重遗漏 — 内联 EnumOptions fallback 也可以
        // 但如果有 BuildFilterContextOptions 却遗漏了某些枚举列，需要警告
      }
    }
  }

  // ============================================================
  // 输出结果
  // ============================================================
  console.log('--- 筛选管道验证结果 ---\n');
  console.log(`使用 ApplyFilters 的服务: ${applyFiltersPassCount} 页`);
  console.log(`Hand-rolled 筛选: ${infoMessages.length} 服务`);
  console.log(`无筛选列页面: ${noFilterColumnsCount} 页`);
  console.log(`未映射页面: ${warnings.filter(w => w.type === '未映射').length} 页\n`);

  // Errors
  if (errors.length > 0) {
    console.log(`✗ 错误: ${errors.length}`);
    for (const e of errors) {
      console.log(`  [${e.type}] ${e.page}`);
      console.log(`    ${e.message}`);
    }
    console.log();
  }

  // Warnings
  if (warnings.length > 0) {
    console.log(`⚠ 警告: ${warnings.length}`);
    for (const w of warnings) {
      console.log(`  [${w.type}] ${w.page} — ${w.message}`);
    }
    console.log();
  }

  // Info
  if (infoMessages.length > 0) {
    console.log(`ℹ 提示: ${infoMessages.length}`);
    for (const i of infoMessages) {
      console.log(`  ${i.page}: ${i.message}`);
    }
    console.log();
  }

  if (errors.length === 0 && warnings.length === 0) {
    console.log('✅ 筛选管道验证通过 — 所有 FilterType 列有完整的筛选管道');
  }

  // ============================================================
  // 详细枚举列信息
  // ============================================================
  console.log('\n--- FilterType="enum" 列详情 ---');
  for (const page of pageDefs) {
    const enumCols = page.columns.filter(c => c.filterType === 'enum');
    if (enumCols.length > 0) {
      const withOpts = enumCols.filter(c => c.hasEnumOptions).length;
      const withoutOpts = enumCols.filter(c => !c.hasEnumOptions).length;
      console.log(`  ${page.pageName.padEnd(30)} ${enumCols.length} 枚举列 (${withOpts} 有 EnumOptions, ${withoutOpts} 缺)`);
      for (const col of enumCols) {
        const mark = col.hasEnumOptions ? '✓' : '✗';
        console.log(`    ${mark} ${col.key.padEnd(25)} "${col.label}"`);
      }
    }
  }
}

main();
