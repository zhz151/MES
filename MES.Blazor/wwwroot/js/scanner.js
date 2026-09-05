// 扫码执行 - 摄像头扫描工具
//
// 解码引擎（v3，2026-09-05）：
//   本系统标签全部为【二维码】（工位/员工/批次/设备 printQrCodes 打 QR），因此解码以
//   纯本地 jsQR 为主路径 —— 不依赖浏览器原生 BarcodeDetector（夸克/微信 X5-XWeb/无 GMS
//   安卓常「有 API 无底座」，detect 恒空导致扫码卡死，极难预判）。jsQR 加载失败时才退
//   用原生 BarcodeDetector 兜底。
//
//   屏显诊断浮层：默认开启（右下角小黑条），实时显示 引擎 / jsQR 就绪 / 元数据 / 命中 /
//   错误 等状态，便于真机排障。稳定后可把 debugUI 置 false 关闭。
//
//   性能：解码前降采样到最长边 720、帧率门限约 90ms，避免全帧解码拖垮低端机。

let scanVideoRef = null;
let scanCanvasRef = null;
let scanCanvasCtx = null;
let scanStream = null;
let scanAnimationId = null;
let scanCallbackRef = null;
let scanCallbackMethod = null;  // C# 回调方法名（startScanner 局部变量需提升为模块级，供 handleHit 命中时使用）
let isScanning = false;
let useJsQr = false;           // jsQR 主引擎
let useNativeDetector = false; // 原生 BarcodeDetector（仅 jsQR 不可用时的兜底）
let jsqrAvailable = false;
let lastDecodeTime = 0;        // jsQR 帧率门限用
let nativeEmptySince = 0;      // 原生连续无结果起始（仅兜底路径用）

const debugUI = true;                       // 屏显诊断开关
const JSQR_INTERVAL_MS = 90;                // jsQR 解码帧率门限
const JSQR_MAX_DIMENSION = 720;             // jsQR 解码前降采样最长边
const NATIVE_FALLBACK_MS = 1500;            // 原生兜底时连续无结果提示间隔
const SCANNER_DIR = (function () {          // 推导 jsqr 同目录（不依赖 base href）
    try {
        const sc = document.currentScript;
        if (sc && sc.src) return sc.src.replace(/[^/]*$/, '');
    } catch (e) { }
    return 'js/';
})();

// ========== 屏显诊断浮层 ==========
let statusEl = null;
let lastStatusWrite = 0;

function getStatusEl() {
    if (statusEl && document.body.contains(statusEl)) return statusEl;
    statusEl = document.createElement('div');
    statusEl.id = '__scanStatus__';
    statusEl.style.cssText = 'position:fixed;right:8px;bottom:8px;z-index:999999;' +
        'background:rgba(0,0,0,0.75);color:#0f0;font-size:11px;line-height:1.4;' +
        'padding:4px 8px;border-radius:4px;max-width:85vw;' +
        'font-family:monospace;white-space:pre-wrap;pointer-events:none;';
    document.body.appendChild(statusEl);
    return statusEl;
}

function setStatus(parts) {
    if (!debugUI) return;
    try {
        const el = getStatusEl();
        const now = performance.now();
        const text = parts.join('\n');
        if (now - lastStatusWrite >= 300 || !el._last || el._last !== text) {
            el.textContent = text;
            el._last = text;
            lastStatusWrite = now;
        }
    } catch (e) { }
}

function removeStatus() {
    if (statusEl && statusEl.parentNode) statusEl.parentNode.removeChild(statusEl);
    statusEl = null;
    lastStatusWrite = 0;
}

// ========== 原生 BarcodeDetector 探测（仅 jsQR 失败时兜底用）==========
async function checkBarcodeDetector() {
    if (!('BarcodeDetector' in window)) return false;
    try {
        if (typeof BarcodeDetector.getSupportedFormats === 'function') {
            const formats = await BarcodeDetector.getSupportedFormats();
            return Array.isArray(formats) && formats.includes('qr_code');
        }
    } catch (e) { }
    return true;
}

// ========== jsQR 加载 ==========
function loadJsQr() {
    return new Promise((resolve, reject) => {
        if (typeof window.jsQR === 'function') { resolve(); return; }
        const script = document.createElement('script');
        // ?v=1 缓存失效：服务器曾部署残缺 jsqr 文件并被浏览器缓存，导致扫码解码失败
        script.src = SCANNER_DIR + 'jsqr-1.4.0.min.js?v=1';
        script.onload = () => {
            // 校验真正可用（防 SPA 回退/残缺文件把 HTML 当 JS 返回造成假成功）
            if (typeof window.jsQR === 'function') { resolve(); }
            else reject(new Error('jsqr loaded but window.jsQR missing'));
        };
        script.onerror = () => reject(new Error('Failed to load jsQR: ' + script.src));
        document.head.appendChild(script);
    });
}

// ========== 启动 ==========
window.startScanner = async function (videoElementId, canvasElementId, dotnetRef, callbackMethod) {
    // 重置运行态（支持同页反复启停）
    isScanning = false;
    useJsQr = false;
    useNativeDetector = false;
    jsqrAvailable = false;
    lastDecodeTime = 0;
    nativeEmptySince = 0;

    // jsQR 主路径：优先加载纯本地解码器
    try {
        await loadJsQr();
        jsqrAvailable = true;
        useJsQr = true;
    } catch (e) {
        jsqrAvailable = false;
        console.error('jsQR 加载失败，尝试原生兜底：', e);
    }
    if (!jsqrAvailable) {
        useNativeDetector = await checkBarcodeDetector();
    }
    if (!jsqrAvailable && !useNativeDetector) {
        setStatus(['二维码解码器初始化失败', 'jsQR 无法加载且原生不可用', '请检查网络后重试']);
        return { success: false, error: '当前浏览器没有可用的二维码解码能力' };
    }

    const video = document.getElementById(videoElementId);
    const canvas = document.getElementById(canvasElementId);
    if (!video || !canvas) {
        setStatus(['未找到 video/canvas 元素']);
        return { success: false, error: '未找到视频或画布元素' };
    }
    scanVideoRef = video;
    scanCanvasRef = canvas;
    scanCanvasCtx = canvas.getContext('2d');
    scanCallbackRef = dotnetRef;
    scanCallbackMethod = callbackMethod;
    isScanning = true;

    setStatus([
        useJsQr ? '引擎: jsQR' : '引擎: 原生BarcodeDetector',
        'jsQR 就绪: ' + (jsqrAvailable ? 'OK' : '失败'),
        '正在开启摄像头…'
    ]);

    try {
        scanStream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'environment', width: { ideal: 1280 }, height: { ideal: 720 } }
        });
        video.srcObject = scanStream;
        video.setAttribute('playsinline', 'true');
        video.muted = true;

        // 等待视频元数据（videoWidth/Height 就绪），防 0 尺寸导致解码永空
        if (video.readyState < 1) {
            await new Promise((resolve) => {
                const timer = setTimeout(() => { video.removeEventListener('loadedmetadata', h); resolve(); }, 3000);
                function h() { clearTimeout(timer); resolve(); }
                video.addEventListener('loadedmetadata', h, { once: true });
                if (video.readyState >= 1) { clearTimeout(timer); resolve(); }
            });
        }
        try { await video.play(); } catch (e) { }

        setStatus([
            useJsQr ? '引擎: jsQR' : '引擎: 原生BarcodeDetector',
            '扫码中… 请将二维码对准镜头'
        ]);
        scanAnimationId = requestAnimationFrame(scanFrame);
        return { success: true };
    } catch (e) {
        console.error('Camera access error:', e);
        isScanning = false;
        setStatus(['摄像头开启失败', (e && e.name) ? e.name : e.message]);
        return { success: false, error: '无法访问摄像头，请确保已授予摄像头权限' };
    }
};

// ========== 命中处理 ==========
function handleHit(value) {
    // 关键：先取回调引用/方法名 —— window.stopScanner() 会把两者置 null，
    // 且 callbackMethod 若仍靠局部传参极易漏传（scanFrame 触发点拿不到 startScanner 的局部变量），
    // 漏传会导致 invokeMethodAsync(undefined,…) → C# 永不执行 → 识别后页面不推进。
    // 因此回调方法名在 startScanner 时提升为模块级 scanCallbackMethod，此处统一使用。
    const cb = scanCallbackRef;
    const method = scanCallbackMethod;
    isScanning = false;
    if (scanAnimationId) {
        cancelAnimationFrame(scanAnimationId);
        scanAnimationId = null;
    }
    setStatus(['已识别: ' + value]);
    setTimeout(removeStatus, 800);
    // 不在此停流：视频流保留，交由 C# 回调成功/失败路径处理（StopCamera 才真正释放），
    // 避免 C# 未收到时画面黑屏 + 页面停在相机态无法重扫的死局。
    if (cb && method) {
        try { cb.invokeMethodAsync(method, value); } catch (e) { console.error('回调失败:', e); }
    } else {
        console.error('回调失败: scanCallbackRef/Method 为空', !!cb, method);
    }
}

// ========== 逐帧扫描 ==========
async function scanFrame() {
    if (!isScanning || !scanVideoRef || !scanCanvasCtx || !scanCanvasRef) return;

    const video = scanVideoRef;
    const canvas = scanCanvasRef;
    const ctx = scanCanvasCtx;

    if (video.readyState === video.HAVE_ENOUGH_DATA) {
        const width = video.videoWidth;
        const height = video.videoHeight;

        if (width > 0 && height > 0) {
            if (useJsQr) {
                // ===== jsQR 主引擎 =====
                const now = performance.now();
                if (now - lastDecodeTime >= JSQR_INTERVAL_MS) {
                    lastDecodeTime = now;
                    try {
                        // 降采样到最长边，降低 CPU
                        let dw = width, dh = height;
                        const maxDim = dw > dh ? dw : dh;
                        if (maxDim > JSQR_MAX_DIMENSION) {
                            const scale = JSQR_MAX_DIMENSION / maxDim;
                            dw = Math.max(1, Math.round(dw * scale));
                            dh = Math.max(1, Math.round(dh * scale));
                        }
                        canvas.width = dw;
                        canvas.height = dh;
                        ctx.drawImage(video, 0, 0, dw, dh);
                        const imageData = ctx.getImageData(0, 0, dw, dh);
                        const code = window.jsQR(imageData.data, imageData.width, imageData.height, {
                            inversionAttempts: 'dontInvert'
                        });
                        if (code && code.data) {
                            handleHit(code.data);
                            return;
                        }
                    } catch (e) {
                        console.warn('jsQR 解码异常:', e);
                        setStatus(['jsQR 异常: ' + e.message, '仍在尝试…']);
                    }
                }
            } else if (useNativeDetector) {
                // ===== 原生 BarcodeDetector（兜底，jsQR 加载失败时）=====
                canvas.width = width;
                canvas.height = height;
                ctx.drawImage(video, 0, 0, width, height);
                try {
                    const detector = new BarcodeDetector({ formats: ['qr_code', 'code_128', 'ean_13', 'ean_8', 'code_39', 'code_93', 'upc_a', 'upc_e', 'itf', 'codabar', 'data_matrix', 'aztec', 'pdf417'] });
                    const barcodes = await detector.detect(canvas);
                    if (barcodes.length > 0 && barcodes[0].rawValue) {
                        handleHit(barcodes[0].rawValue);
                        return;
                    }
                } catch (e) { }
                const now = performance.now();
                if (!nativeEmptySince) nativeEmptySince = now;
                else if (now - nativeEmptySince >= NATIVE_FALLBACK_MS) {
                    setStatus(['原生 BarcodeDetector 持续无结果', '可能为伪支持（无解码底座）', '请尝试系统浏览器']);
                    nativeEmptySince = now;
                }
            }
        } else {
            setStatus(['等待视频尺寸就绪…']);
        }
    }

    scanAnimationId = requestAnimationFrame(scanFrame);
}

// ========== 停止并释放 ==========
window.stopScanner = function () {
    isScanning = false;

    if (scanAnimationId) {
        cancelAnimationFrame(scanAnimationId);
        scanAnimationId = null;
    }
    if (scanStream) {
        try { scanStream.getTracks().forEach(t => t.stop()); } catch (e) { }
        scanStream = null;
    }
    if (scanVideoRef) {
        try { scanVideoRef.srcObject = null; } catch (e) { }
    }
    scanVideoRef = null;
    scanCanvasRef = null;
    scanCanvasCtx = null;
    scanCallbackRef = null;
    scanCallbackMethod = null;
    return true;
};
