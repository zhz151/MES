// 扫码执行 - 摄像头扫描工具
// 支持二维码（QR Code）和条形码（Code128/EAN-13/UPC/Code39 等）
// 使用 BarcodeDetector API（浏览器原生），不支持时回退到 jsQR

let scanVideoRef = null;
let scanCanvasRef = null;
let scanCanvasCtx = null;
let scanStream = null;
let scanAnimationId = null;
let scanCallbackRef = null;
let isScanning = false;
let useNativeDetector = false;

// 检测 BarcodeDetector API 是否可用
function checkBarcodeDetector() {
    return 'BarcodeDetector' in window;
}

// 加载 jsQR 库（回退方案）
function loadJsQr() {
    return new Promise((resolve, reject) => {
        if (window.jsQR) {
            resolve();
            return;
        }
        const script = document.createElement('script');
        script.src = 'js/jsqr-1.4.0.min.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Failed to load jsQR library'));
        document.head.appendChild(script);
    });
}

// 获取支持的条码格式
function getSupportedFormats() {
    if (!useNativeDetector) return null;
    try {
        return BarcodeDetector.getSupportedFormats();
    } catch {
        return null;
    }
}

// 启动摄像头扫描
window.startScanner = async function (videoElementId, canvasElementId, dotnetRef, callbackMethod) {
    try {
        useNativeDetector = checkBarcodeDetector();

        if (!useNativeDetector) {
            // 没有原生 API，加载 jsQR 回退
            try {
                await loadJsQr();
            } catch (e) {
                console.error('Failed to load jsQR:', e);
                return { success: false, error: '无法加载解码库，请检查网络连接' };
            }
        }
    } catch (e) {
        return { success: false, error: '初始化解码器失败' };
    }

    const video = document.getElementById(videoElementId);
    const canvas = document.getElementById(canvasElementId);

    if (!video || !canvas) {
        return { success: false, error: '未找到视频或画布元素' };
    }

    scanVideoRef = video;
    scanCanvasRef = canvas;
    scanCanvasCtx = canvas.getContext('2d');
    scanCallbackRef = dotnetRef;
    isScanning = true;

    try {
        scanStream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'environment', width: { ideal: 640 }, height: { ideal: 480 } }
        });
        video.srcObject = scanStream;
        video.setAttribute('playsinline', 'true');
        await video.play();

        // 开始循环扫描
        scanAnimationId = requestAnimationFrame(scanFrame);

        return { success: true };
    } catch (e) {
        console.error('Camera access error:', e);
        isScanning = false;
        return { success: false, error: '无法访问摄像头，请确保已授予摄像头权限' };
    }
};

// 逐帧扫描
async function scanFrame() {
    if (!isScanning || !scanVideoRef || !scanCanvasCtx || !scanCanvasRef) return;

    const video = scanVideoRef;
    const canvas = scanCanvasRef;

    if (video.readyState === video.HAVE_ENOUGH_DATA) {
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        scanCanvasCtx.drawImage(video, 0, 0, canvas.width, canvas.height);

        if (useNativeDetector) {
            // 使用原生 BarcodeDetector API（支持二维码 + 条码）
            try {
                const detector = new BarcodeDetector({ formats: ['qr_code', 'code_128', 'ean_13', 'ean_8', 'code_39', 'code_93', 'upc_a', 'upc_e', 'itf', 'codabar', 'data_matrix', 'aztec', 'pdf417'] });
                const barcodes = await detector.detect(canvas);
                if (barcodes.length > 0 && barcodes[0].rawValue) {
                    stopScanner();
                    if (scanCallbackRef) {
                        scanCallbackRef.invokeMethodAsync(callbackMethod, barcodes[0].rawValue);
                    }
                    return;
                }
            } catch (e) {
                // BarcodeDetector 可能不支持某些格式，忽略
            }
        } else {
            // 回退到 jsQR（仅二维码）
            const imageData = scanCanvasCtx.getImageData(0, 0, canvas.width, canvas.height);
            const code = window.jsQR(imageData.data, imageData.width, imageData.height, {
                inversionAttempts: 'dontInvert'
            });

            if (code && code.data) {
                stopScanner();
                if (scanCallbackRef) {
                    scanCallbackRef.invokeMethodAsync(callbackMethod, code.data);
                }
                return;
            }
        }
    }

    scanAnimationId = requestAnimationFrame(scanFrame);
}

// 停止扫描并释放摄像头
window.stopScanner = function () {
    isScanning = false;

    if (scanAnimationId) {
        cancelAnimationFrame(scanAnimationId);
        scanAnimationId = null;
    }

    if (scanStream) {
        scanStream.getTracks().forEach(track => track.stop());
        scanStream = null;
    }

    if (scanVideoRef) {
        scanVideoRef.srcObject = null;
    }

    scanVideoRef = null;
    scanCanvasRef = null;
    scanCanvasCtx = null;
    scanCallbackRef = null;

    return true;
};

// 清理资源
window.disposeScanner = function () {
    stopScanner();
    return true;
};
