// 生产环境 Service Worker — 静态资源缓存优先，API 网络优先

const STATIC_CACHE = 'mes-static-v1';
const API_CACHE = 'mes-api-v1';

// 由 MSBuild 自动生成的资源哈希清单
// 内容格式: self.assetsManifest = { assets: [{ hash, url }, ...] }
// 行末分号会被 MSBuild 注入覆盖
self.assetsManifest = { assets: [] };

// 安装事件：预缓存所有静态资源
self.addEventListener('install', async event => {
    const assets = self.assetsManifest?.assets || [];
    const urls = assets.map(a => a.url);

    const cache = await caches.open(STATIC_CACHE);
    await cache.addAll(urls);

    self.skipWaiting();
});

// 激活事件：清理旧版本缓存
self.addEventListener('activate', async event => {
    const keys = await caches.keys();
    await Promise.all(
        keys.map(key => {
            if (key !== STATIC_CACHE && key !== API_CACHE) {
                return caches.delete(key);
            }
        })
    );
    await clients.claim();
});

// 拦截请求
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);

    // Blazor 框架资源 / 静态文件：缓存优先
    if (url.pathname.startsWith('/_framework/') ||
        url.pathname.startsWith('/css/') ||
        url.pathname.startsWith('/js/') ||
        url.pathname === '/') {
        event.respondWith(cacheFirst(event.request));
        return;
    }

    // API 请求：网络优先，离线时用缓存
    if (url.pathname.startsWith('/api/')) {
        event.respondWith(networkFirst(event.request));
        return;
    }

    // manifest.json: 缓存优先
    if (url.pathname === '/manifest.json') {
        event.respondWith(cacheFirst(event.request));
        return;
    }
});

async function cacheFirst(request) {
    const cached = await caches.match(request);
    if (cached) return cached;

    try {
        const response = await fetch(request);
        if (response.ok) {
            const cache = await caches.open(STATIC_CACHE);
            cache.put(request, response.clone());
        }
        return response;
    } catch {
        return new Response('Offline', { status: 503 });
    }
}

async function networkFirst(request) {
    try {
        const response = await fetch(request);
        if (response.ok) {
            const cache = await caches.open(API_CACHE);
            cache.put(request, response.clone());
        }
        return response;
    } catch {
        const cached = await caches.match(request);
        if (cached) return cached;
        return new Response(
            JSON.stringify({ success: false, message: '网络连接已断开' }),
            { status: 503, headers: { 'Content-Type': 'application/json' } }
        );
    }
}
