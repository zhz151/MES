import { chromium } from 'playwright';

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });

await page.goto('http://localhost:5000/orders', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(5000);

console.log('Current URL:', page.url());
console.log('Title:', await page.title());

const html = await page.content();

// 检查关键元素
console.log('Has mud-table class:', html.includes('mud-table'));
console.log('Has login form (邮箱):', html.includes('邮箱'));
console.log('Has login form (密码):', html.includes('密码'));
console.log('Has MudCard:', html.includes('mud-card'));
console.log('Has [Authorize] redirect:', html.includes('Not authorized') || html.includes('authorized') || html.includes('login'));

// 打印 body 前 2000 字符
const bodyText = await page.evaluate(() => document.body?.innerText?.substring(0, 1000) || 'NO BODY');
console.log('--- Page text ---');
console.log(bodyText);

await page.screenshot({ path: 'report/debug-orders.png' });
await browser.close();
