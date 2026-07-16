/**
 * 静态分析：验证前端 SortKey 在后端有对应的排序处理
 *
 * 检查内容：
 *   1. 提取所有 .razor.cs 中的 ColumnDef SortKey
 *   2. 映射到后端 Controller/Service
 *   3. 使用 ApplySort（反射通用）的服务 → 自动通过
 *   4. Hand-rolled switch 的服务 → 从代码读取分支并交叉验证
 *
 * 使用: node playwright-tests/verify-sort-keys.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

// ============================================================
// 1. Hand-rolled switch 服务的精确分支（从代码验证）
// ============================================================
// 每个分支列表是在小写比较下的值（除非标记为 caseSensitive）
const HAND_ROLLED_SORT_SERVICES = {
  'OrderService.cs': {
    serviceFile: 'MES.Services/Order/OrderService.cs',
    caseSensitive: false,
    switchBranches: [
      'ordernumber', 'signdate', 'status', 'salesman', 'customername',
      'endcustomer', 'deliverystart', 'deliveryend', 'hasdelaypenalty',
      'totalcontractweight', 'itemcount', 'lastchangedate', 'schedulestage',
      'urgencylevel', 'estimatedcompletiondate', 'hastechnicalrequirement'
    ]
  },
  'ProductionRecordService.cs': {
    serviceFile: 'MES.Services/Batch/ProductionRecordService.cs',
    caseSensitive: false,
    switchBranches: [
      'execdate', 'batchno', 'processname', 'manufacturingspec',
      'sectionname', 'sequencenumber', 'equipmentname', 'operator',
      'shift', 'quantity', 'weight', 'solutiontemperature', 'soaktime',
      'productstatus', 'cuttingmultiple', 'finishedcutlength', 'postcutquantity',
      'facecutcount', 'tagno', 'plantgrade', 'remark', 'createdtime',
      'updatedtime', 'datasource'
    ]
  },
  'WorkOrderExecutionService.cs': {
    serviceFile: 'MES.Services/WorkOrder/WorkOrderExecutionService.cs',
    caseSensitive: false,
    switchBranches: [
      'workorderno', 'salesman', 'customername', 'signdate', 'deliverydate',
      'salesorderno', 'productionmainno', 'plantgrade', 'specification',
      'totalquantity', 'totalweight', 'inputquantity', 'inputweight',
      'inputoutputratio', 'inputstatus', 'delaypenalty',
      'settlementmethod', 'productionsubno', 'materialname', 'deliverystate',
      'lengthstatus', 'minlength', 'maxlength', 'totalitemcount', 'totalmeters',
      'latestplandate', 'materialplanrate', 'mainnomaterialplanrate',
      'materialplanstatus', 'mainnomaterialplanstatus', 'processcycle',
      'materialplancoveredcount', 'materialplanproportion', 'latestrequireddate',
      'pendingroughtubeqty', 'pendingroughtubeweight', 'pendingoutsourcefinishqty',
      'pendingoutsourcefinishweight', 'theoreticalfinishqty', 'theoreticalfinishweight',
      'reworkinputenddate', 'reworkbatchcount', 'reworkinputquantity',
      'reworkinputweight', 'reworktheoreticaloutputqty', 'reworktheoreticaloutputweight',
      'inputstartdate', 'inputenddate', 'totalbatchcount', 'theoreticaloutputqty',
      'theoreticaloutputweight', 'mainnoinputratio', 'mainnoinputstatus',
      'validbatchcount', 'validinputquantity', 'validinputweight', 'validoutputqty',
      'validoutputweight', 'flowoutputratio', 'mainnoflowoutputratio',
      'flowtotalbatchcount', 'flowincompletebatchcount', 'flowmaxremainingworkdays',
      'flowstatus', 'mainnoflowstatus', 'defectiverawqty', 'defectiverawweight',
      'defectiveoutputqty', 'defectiveoutputweight', 'defectiveratio',
      'inspectiondefectqty', 'inspectiondefectweight', 'inspectiondefectratio',
      'inspectionstartdate', 'inspectionenddate', 'generaldefectweight',
      'generaldefectratio', 'seriousdefectweight', 'seriousdefectratio',
      'scrapweight', 'scrapratio', 'warehousingstartdate', 'warehousingenddate',
      'warehousingtotalqty', 'warehousingtotalweight', 'wowarehousingstatus',
      'mainnowarehousingstatus', 'orderwarehousingstatus', 'schedulestage',
      'totalremainingworkdays', 'urgencylevel', 'estimatedprocesscompletiondate',
      'daysdifffromdelivery', 'capacityworkdays', 'rawmateriallockremark',
      'pendingsectionroughtube', 'pendingsectionwarehousefix',
      'pendingsection60roll', 'pendingsection50roll', 'pendingsection30roll',
      'pendingsection20roll', 'pendingsectionthreeroll',
      'pendingsectiondrawbench', 'deformedprocesscompleted',
      'productionattentionprocess', 'isurging', 'isbatchdelivery', 'ispaused',
      'adjustmentremark', 'productionflowproperty',
      'maxbatchremainingworkdays', 'mainnoattentionprocess'
    ]
  },
  'InventoryService.cs': {
    serviceFile: 'MES.Services/Warehouse/InventoryService.cs',
    caseSensitive: false,
    switchBranches: [
      // 库存查询 (35)
      'batchno', 'materialtype', 'inbounddate', 'remainingweight',
      'plantgrade', 'specification', 'heatno', 'remainingquantity',
      'initialquantity', 'initialweight', 'unitweight', 'inboundsource',
      'productionbatchno', 'salesorderno', 'surfacecondition', 'lengthstatus',
      'workorderno', 'sourcename', 'sourceorderno', 'minlength', 'maxlength',
      'meters', 'actualspecification', 'actualouterdiameter', 'actualwallthickness',
      'locationarea', 'locationrack', 'tagno', 'defectreason', 'liabilitytype',
      'originalsupplier', 'defectremark', 'orderitemids', 'remark',
      'islinkedtoworkorder'
    ]
  },
  'InventoryOutboundService.cs': {
    serviceFile: 'MES.Services/Warehouse/InventoryService.cs',
    caseSensitive: false,
    switchBranches: [
      // 出库查询 (10)
      'batchno', 'outbounddate', 'outboundtype', 'outboundquantity',
      'outboundweight', 'outboundmeters', 'targetcompany', 'createdby', 'sourceorderno', 'remark'
    ]
  },
  'MaterialReceiveCheckService.cs': {
    serviceFile: 'MES.Services/Quality/MaterialReceiveCheckService.cs',
    caseSensitive: false,
    switchBranches: [
      'batchno', 'receivedate', 'checker', 'createdtime', 'updatedtime',
      'shift', 'remark', 'manufacturingitem', 'plantgrade', 'specification',
      'tagno', 'workorderno', 'salesorderno', 'furnaceno', 'sourceunit',
      'productiontype', 'datasource', 'productioncutquantity', 'productionweight',
      'lengthstatus', 'isforcecompleted', 'salesman', 'deliverystate'
    ]
  },
  'CertificateService.cs': {
    serviceFile: 'MES.Services/Quality/CertificateService.cs',
    caseSensitive: false,
    switchBranches: [
      'certificateno', 'customername', 'issuedate',
      'productstandard', 'productname', 'deliverystatus'
    ]
  },
};

// ============================================================
// 2. 已知使用 ApplySort（反射通用）的页面 → 自动 PASS
// ============================================================
const APPLYSORT_PAGES = new Set([
  'Batches', 'SectionOutsources', 'PicklingInRecords', 'PicklingOutRecords', 'OutsourceRecoveries',
  'ConfigParameters', 'Employees', 'Workstations', 'DailyOutputEstimates', 'DailyProductionCapacities',
  'StandardWorkDays', 'StandardWorkDayDeliveryStates',
  'Equipments', 'InspectionRecords', 'MaintenanceOrders', 'RepairOrders',
  'Materials', 'Suppliers', 'PurchaseOrders', 'SubcontractOrders',
  'Customers',
  'ChemicalAnalyses', 'FlaringTests', 'FlatteningTests', 'FinalInspections', 'FurnaceRegistrations',
  'GrainSizeTests', 'HardnessTests', 'IntergranularCorrosionTests', 'MetallographicTests',
  'Ncrs', 'PittingCorrosionTests', 'ProcessInspections', 'TensileTests',
  'QualityProcessTracking',
  'ChemicalCompositions', 'ChemicalValidationRules', 'GradeChemicalCompositions', 'GradeMappings',
  'GradePhysicalProperties', 'StandardInspectionRequirements', 'SubStandardQuickViews',
  'StandardRegisters',
  'BatchPlans', 'WorkOrderSchedules', 'RawMaterialLockPlanAndExecution',
  'WarehouseInventory', 'PendingDelivery',
  'WorkOrders', 'OrderDemandAdjustment', 'MaterialPlanOverview',
]);

// ============================================================
// 3. Hand-rolled 排序的页面 → 需要交叉验证
// ============================================================
const HAND_ROLLED_SORT_PAGES = {
  'Orders': 'OrderService.cs',
  'ProductionRecords': 'ProductionRecordService.cs',
  'WorkOrderExecution': 'WorkOrderExecutionService.cs',
  'InboundHistory': 'InventoryService.cs',        // 入库/库存
  'OutboundHistory': 'InventoryOutboundService.cs', // 出库
  'MaterialReceiveChecks': 'MaterialReceiveCheckService.cs',
  'Certificates': 'CertificateService.cs',
};

// ============================================================
// 4. 扫描 .razor.cs 中的 ColumnDef 定义
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
  const colDefStart = content.indexOf('new() { Key = "');
  if (colDefStart === -1) return null;

  const columns = [];
  const colRegex = /new\(\)\s*\{([^}]+)\}/g;
  let match;
  while ((match = colRegex.exec(content)) !== null) {
    const block = match[1];
    const keyMatch = block.match(/Key\s*=\s*"([^"]+)"/);
    const labelMatch = block.match(/Label\s*=\s*"([^"]+)"/);
    const sortKeyMatch = block.match(/SortKey\s*=\s*"([^"]+)"/);
    if (keyMatch) {
      columns.push({
        key: keyMatch[1],
        label: labelMatch ? labelMatch[1] : keyMatch[1],
        sortKey: sortKeyMatch ? sortKeyMatch[1] : null,
      });
    }
  }
  if (columns.length === 0) return null;
  return {
    file: path.relative(PROJECT_DIR, filePath),
    pageName: path.basename(filePath, '.razor.cs'),
    columns,
  };
}

// ============================================================
// 5. 检查 Hand-rolled switch 是否覆盖 SortKey
// ============================================================
function checkSortCoverage(pageName, columns, serviceKey) {
  const serviceInfo = HAND_ROLLED_SORT_SERVICES[serviceKey];
  if (!serviceInfo) return [];

  const issues = [];

  for (const col of columns) {
    if (!col.sortKey) continue;

    const sortKeyLower = col.sortKey.toLowerCase();
    let found = false;

    if (serviceInfo.caseSensitive) {
      // 大小写敏感：精确匹配
      found = serviceInfo.switchBranches.includes(col.sortKey);
    } else {
      // 大小写不敏感：小写匹配
      found = serviceInfo.switchBranches.includes(sortKeyLower);
    }

    if (!found) {
      issues.push({
        page: pageName,
        column: col.key,
        label: col.label,
        sortKey: col.sortKey,
        service: serviceKey,
        severity: 'error',
        message: `SortKey="${col.sortKey}" 在 ${serviceKey} 的手工 switch 中找不到对应分支` +
          (serviceInfo.caseSensitive ? '（大小写敏感！）' : '')
      });
    }
  }
  return issues;
}

// ============================================================
// 6. 从 Service 文件读取实际 switch 分支（验证用）
// ============================================================
function readSwitchBranchesFromService(serviceRelPath) {
  const fullPath = path.join(PROJECT_DIR, serviceRelPath);
  if (!fs.existsSync(fullPath)) return null;

  const content = fs.readFileSync(fullPath, 'utf-8');
  const branches = [];
  // 匹配 switch 表达式中的 "xxx" => 模式
  const branchRegex = /["']([a-zA-Z]+)["']\s*=>/g;
  let m;
  while ((m = branchRegex.exec(content)) !== null) {
    branches.push(m[1]);
  }
  return branches.length > 0 ? [...new Set(branches)] : null;
}

// ============================================================
// 7. 主流程
// ============================================================
function main() {
  console.log('============================================');
  console.log('  SortKey vs 后端排序处理 交叉验证');
  console.log('============================================\n');

  const pagesDir = path.join(PROJECT_DIR, 'MES.Blazor', 'Pages');
  const pageDefs = findAllColumnDefs(pagesDir);
  console.log(`扫描了 ${pageDefs.length} 个页面文件\n`);

  const errors = [];
  const infoMessages = [];
  const autoPassPages = [];

  for (const page of pageDefs) {
    const pageName = page.pageName;

    if (HAND_ROLLED_SORT_PAGES[pageName]) {
      const serviceKey = HAND_ROLLED_SORT_PAGES[pageName];
      const colIssues = checkSortCoverage(pageName, page.columns, serviceKey);
      const serviceInfo = HAND_ROLLED_SORT_SERVICES[serviceKey];
      const sortKeyCount = page.columns.filter(c => c.sortKey).length;

      for (const issue of colIssues) {
        errors.push(issue);
      }

      const icon = colIssues.length > 0 ? '✗' : '✓';
      console.log(`  ${icon} ${pageName.padEnd(32)} Hand-rolled: ${serviceKey.replace('.cs', '')} (${serviceInfo.switchBranches.length} 分支, ${sortKeyCount} SortKeys)`);
      if (serviceInfo.caseSensitive) {
        console.log(`      ⚠ 大小写敏感 — 前端必须发送精确大小写的 SortKey`);
      }
    } else if (APPLYSORT_PAGES.has(pageName)) {
      autoPassPages.push(page);
      console.log(`  ✓ ${pageName.padEnd(32)} 通用 ApplySort（自动通过）`);
    } else {
      infoMessages.push({ page: pageName, msg: '未映射到后端服务' });
      console.log(`  ? ${pageName.padEnd(32)} 未映射`);
    }
  }

  // ============================================================
  // 输出结果
  // ============================================================
  console.log('\n--- 统计 ---');
  console.log(`ApplySort 通用服务: ${autoPassPages.length} 页`);
  console.log(`Hand-rolled switch: ${Object.keys(HAND_ROLLED_SORT_PAGES).length} 页`);
  console.log(`未映射: ${infoMessages.filter(m => m.msg === '未映射到后端服务').length} 页`);

  if (errors.length > 0) {
    console.log(`\n✗ 错误: ${errors.length}`);
    for (const e of errors) {
      console.log(`  [${e.page}] ${e.message}`);
      console.log(`    列: "${e.label}" (${e.column}), SortKey="${e.sortKey}"`);
    }
  }

  if (infoMessages.length > 0) {
    console.log(`\nℹ 提示: ${infoMessages.length}`);
    for (const i of infoMessages) {
      if (i.msg !== '未映射到后端服务') {
        console.log(`  [${i.page}] ${i.msg}`);
      }
    }
  }

  if (errors.length === 0) {
    console.log('\n✅ SortKey 验证通过 — 所有 SortKey 在后端有对应的排序处理');
  }

  // 详细信息（对比实际 vs 预期分支）
  console.log('\n--- Hand-rolled switch 分支验证 ---');
  for (const [serviceKey, info] of Object.entries(HAND_ROLLED_SORT_SERVICES)) {
    const actualBranches = readSwitchBranchesFromService(info.serviceFile);
    const expectedSet = new Set(info.switchBranches.map(b => b.toLowerCase()));

    console.log(`\n  ${serviceKey.replace('.cs', '')}:${info.caseSensitive ? ' [大小写敏感]' : ''}`);
    console.log(`    期望: ${info.switchBranches.length} 分支`);
    if (actualBranches) {
      const actualLower = new Set(actualBranches.map(b => b.toLowerCase()));
      const missingFromCode = info.switchBranches.filter(b => !actualLower.has(b.toLowerCase()));
      const extraInCode = actualBranches.filter(b => !expectedSet.has(b.toLowerCase()));
      console.log(`    实际: ${actualBranches.length} 分支（读取自代码）`);
      if (missingFromCode.length > 0) {
        console.log(`    ⚠ 脚本中的分支在代码中未找到: ${missingFromCode.join(', ')}`);
      }
      if (extraInCode.length > 0) {
        console.log(`    ℹ 代码中有但脚本未列出: ${extraInCode.join(', ')}`);
      }
    } else {
      console.log(`    实际: 未能解析分支`);
    }
  }
}

main();
