// 开发环境 Service Worker — 自动注册并跳过等待
// 生产构建会生成 service-worker.published.js 并使用缓存策略

self.addEventListener('install', async event => {
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(clients.claim());
});

// 开发模式: 所有请求直接网络获取，不做缓存
self.addEventListener('fetch', event => {
    event.respondWith(fetch(event.request));
});
