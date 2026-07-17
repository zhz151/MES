/**
 * TC15 认证鉴权测试
 *
 * 验证：
 *   1. 无 Token 请求受保护端点 → 401
 *   2. 无效 Token 请求 → 401
 *   3. 有效 Token 请求 → 200
 *   4. 登录端点无需认证 → 可匿名访问
 *
 * 使用: node playwright-tests/api-auth-check.mjs
 * 前提: MES.Api 运行在 http://localhost:7000
 */

const BASE_URL = 'http://localhost:7000';
const AUTH = { email: 'admin@mes.com', password: 'Admin@123' };

// 选取 5 个典型受保护端点做测试
const PROTECTED_ENDPOINTS = [
  { name: 'Order 列表',       url: `${BASE_URL}/api/order/list?PageSize=1` },
  { name: 'WorkOrder 列表',   url: `${BASE_URL}/api/workorder/list?PageSize=1` },
  { name: 'Material 列表',    url: `${BASE_URL}/api/material/list?PageSize=1` },
  { name: 'Equipment 列表',   url: `${BASE_URL}/api/equipment/list?PageSize=1` },
  { name: 'Certificate 列表', url: `${BASE_URL}/api/certificate/all?PageSize=1` },
];

async function fetchUrl(url, token) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const res = await fetch(url, { headers });
  return { status: res.status, ok: res.ok };
}

let _validToken = null;

async function login() {
  const res = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(AUTH),
  });
  const data = await res.json();
  _validToken = data.data?.token || data.token;
  if (!_validToken) throw new Error('登录失败');
}

async function main() {
  console.log('============================================');
  console.log('  TC15 认证鉴权测试');
  console.log(`  目标: ${BASE_URL}`);
  console.log('============================================\n');

  // 获取有效 Token
  await login();
  console.log('  ✓ 获取有效 Token 成功\n');

  // ============================================================
  // Test 1: 无 Token 请求 → 401
  // ============================================================
  console.log('▶ Test 1: 无 Token 请求受保护端点 → 401');
  let t1Pass = 0;
  for (const ep of PROTECTED_ENDPOINTS) {
    const { status } = await fetchUrl(ep.url, null);
    const pass = status === 401;
    if (pass) t1Pass++;
    console.log(`  ${pass ? '✓' : '✗'} ${ep.name}: HTTP ${status}${pass ? '' : ' (期望 401)'}`);
  }
  console.log(`  → ${t1Pass}/${PROTECTED_ENDPOINTS.length} 通过\n`);

  // ============================================================
  // Test 2: 无效 Token 请求 → 401
  // ============================================================
  console.log('▶ Test 2: 无效 Token 请求 → 401');
  let t2Pass = 0;
  for (const ep of PROTECTED_ENDPOINTS) {
    const { status } = await fetchUrl(ep.url, 'Bearer invalid_token_xxx');
    const pass = status === 401;
    if (pass) t2Pass++;
    console.log(`  ${pass ? '✓' : '✗'} ${ep.name}: HTTP ${status}${pass ? '' : ' (期望 401)'}`);
  }
  console.log(`  → ${t2Pass}/${PROTECTED_ENDPOINTS.length} 通过\n`);

  // ============================================================
  // Test 3: 有效 Token → 200
  // ============================================================
  console.log('▶ Test 3: 有效 Token 请求 → 200');
  let t3Pass = 0;
  for (const ep of PROTECTED_ENDPOINTS) {
    const { status } = await fetchUrl(ep.url, _validToken);
    const pass = status === 200;
    if (pass) t3Pass++;
    console.log(`  ${pass ? '✓' : '✗'} ${ep.name}: HTTP ${status}${pass ? '' : ' (期望 200)'}`);
  }
  console.log(`  → ${t3Pass}/${PROTECTED_ENDPOINTS.length} 通过\n`);

  // ============================================================
  // Test 4: 登录端点无需认证
  // ============================================================
  console.log('▶ Test 4: 登录端点可匿名访问');
  const loginRes = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(AUTH),
  });
  const loginPass = loginRes.status === 200;
  console.log(`  ${loginPass ? '✓' : '✗'} POST /api/auth/login: HTTP ${loginRes.status}${loginPass ? '' : ' (期望 200)'}`);

  // ============================================================
  // 汇总
  // ============================================================
  console.log('\n--- 汇总 ---');
  const totalTests = 4;
  const passed = [t1Pass === PROTECTED_ENDPOINTS.length, t2Pass === PROTECTED_ENDPOINTS.length, t3Pass === PROTECTED_ENDPOINTS.length, loginPass];
  const passCount = passed.filter(Boolean).length;

  console.log(`  Test 1 (无 Token 401): ${t1Pass}/${PROTECTED_ENDPOINTS.length} ${t1Pass === PROTECTED_ENDPOINTS.length ? '✓' : '✗'}`);
  console.log(`  Test 2 (无效 Token 401): ${t2Pass}/${PROTECTED_ENDPOINTS.length} ${t2Pass === PROTECTED_ENDPOINTS.length ? '✓' : '✗'}`);
  console.log(`  Test 3 (有效 Token 200): ${t3Pass}/${PROTECTED_ENDPOINTS.length} ${t3Pass === PROTECTED_ENDPOINTS.length ? '✓' : '✗'}`);
  console.log(`  Test 4 (匿名登录): ${loginPass ? '✓' : '✗'}`);

  if (passCount === totalTests) {
    console.log('\n✅ 所有认证鉴权测试通过');
  } else {
    console.log(`\n✗ ${totalTests - passCount} 个测试失败`);
    process.exit(1);
  }
}

main().catch(e => {
  console.error('未捕获错误:', e);
  process.exit(1);
});
