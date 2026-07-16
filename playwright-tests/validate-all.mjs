/**
 * 统一入口：运行所有 Tier 1 静态分析脚本
 * Tier 1: 无需启动服务，纯文本扫描
 * Tier 2: API 集成测试（需要 Api 运行）
 * Tier 3: Playwright E2E（需要 Blazor 运行）
 *
 * 使用:
 *   node playwright-tests/validate-all.mjs              # 运行 Tier 1
 *   node playwright-tests/validate-all.mjs --all        # 尝试运行所有
 *   node playwright-tests/validate-all.mjs --tier=2     # 只运行特定层级
 */
import { execSync } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = path.resolve(__dirname, '..');

const args = process.argv.slice(2);
const runAll = args.includes('--all');
const tierFilter = args.find(a => a.startsWith('--tier='));

const SCRIPTS = {
  tier1: [
    { name: 'check-filter-types', file: 'check-filter-types.mjs', description: 'FilterType vs DTO 类型交叉验证' },
    { name: 'verify-sort-keys', file: 'verify-sort-keys.mjs', description: 'SortKey vs 后端排序处理交叉验证' },
    { name: 'verify-filter-pipeline', file: 'verify-filter-pipeline.mjs', description: 'FilterType 筛选管道完整性验证' },
    { name: 'verify-filter-contexts', file: 'verify-filter-contexts.mjs', description: 'Filter-Contexts 端点覆盖度验证' },
    { name: 'scan-mudselect-string', file: 'scan-mudselect-string.mjs', description: 'MudSelect T="string" 绑定枚举字段回归检测' },
    { name: 'scan-delete-confirm', file: 'scan-delete-confirm.mjs', description: '删除按钮 ConfirmDialog 覆盖度回归检测' },
    { name: 'scan-spec-compliance', file: 'scan-spec-compliance.mjs', description: 'TC30-37 规范符合度全面扫描' },
    { name: 'scan-enum-consistency', file: 'scan-enum-consistency.mjs', description: 'TC06+TC40 枚举显示回潮与完整性扫描' },
  ],
  tier2: [
    { name: 'api-verify', file: 'api-verify-sort-filter-search.mjs', description: 'API 集成测试（需后端运行）' },
  ],
  tier3: [
    { name: 'interact', file: 'interact-sort-filter-search.mjs', description: 'Playwright E2E 交互测试（需 Blazor 运行）' },
  ],
};

function runScript(script) {
  const scriptPath = path.join(__dirname, script.file);
  console.log('\n' + '='.repeat(60));
  console.log(`  运行: ${script.name}`);
  console.log(`  描述: ${script.description}`);
  console.log('='.repeat(60) + '\n');

  try {
    const result = execSync(`node "${scriptPath}"`, {
      cwd: PROJECT_DIR,
      encoding: 'utf-8',
    });
    console.log(result);
    return { success: true, output: result };
  } catch (err) {
    console.log(err.stdout || '');
    console.error(err.stderr || '');
    return { success: false, error: err.message };
  }
}

function main() {
  console.log('========================================');
  console.log('  MES 全面验证套件');
  console.log('========================================\n');

  const results = { passed: 0, failed: 0, skipped: 0 };

  // 选择要运行的层级
  const tiersToRun = [];
  if (tierFilter) {
    tiersToRun.push(tierFilter.split('=')[1]);
  } else if (runAll) {
    tiersToRun.push('1', '2', '3');
  } else {
    tiersToRun.push('1'); // 默认只运行 Tier 1
  }

  for (const tier of tiersToRun) {
    const scripts = SCRIPTS[`tier${tier}`];
    if (!scripts) {
      console.log(`警告: 未知层级 tier${tier}`);
      continue;
    }

    console.log(`\n>>> Tier ${tier}: 运行 ${scripts.length} 个脚本 <<<\n`);

    for (const script of scripts) {
      // 检查脚本文件是否存在
      const scriptPath = path.join(__dirname, script.file);
      if (!fs.existsSync(scriptPath)) {
        console.log(`  跳过: ${script.file} 尚未创建`);
        results.skipped++;
        continue;
      }

      const result = runScript(script);
      if (result.success) {
        results.passed++;
      } else {
        results.failed++;
        console.error(`  ✗ ${script.name} 失败: ${result.error}`);
      }
    }
  }

  console.log('\n' + '='.repeat(60));
  console.log('  验证完成');
  console.log('='.repeat(60));
  console.log(`  通过: ${results.passed}`);
  console.log(`  失败: ${results.failed}`);
  console.log(`  跳过: ${results.skipped}`);
  console.log(`  总计: ${results.passed + results.failed + results.skipped}`);
  console.log('');

  if (results.failed > 0) {
    process.exit(1);
  }
}

main();
