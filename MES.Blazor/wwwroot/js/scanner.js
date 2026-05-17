// 扫码执行 - 摄像头扫描工具
// 使用 jsQR 从视频帧中解码二维码

let scanVideoRef = null;
let scanCanvasRef = null;
let scanCanvasCtx = null;
let scanStream = null;
let scanAnimationId = null;
let scanCallbackRef = null;
let isScanning = false;

// 加载 jsQR 库
function loadJsQr() {
    return new Promise((resolve, reject) => {
        if (window.jsQR) {
            resolve();
            return;
        }
        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/jsqr@1.4.0/dist/jsQR.min.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Failed to load jsQR library'));
        document.head.appendChild(script);
    });
}

// 启动摄像头扫描
window.startScanner = async function (videoElementId, canvasElementId, dotnetRef, callbackMethod) {
    try {
        await loadJsQr();
    } catch (e) {
        console.error('Failed to load jsQR:', e);
        return { success: false, error: '无法加载二维码解码库，请检查网络连接' };
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
function scanFrame() {
    if (!isScanning || !scanVideoRef || !scanCanvasCtx || !scanCanvasRef) return;

    const video = scanVideoRef;
    const canvas = scanCanvasRef;

    if (video.readyState === video.HAVE_ENOUGH_DATA) {
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        scanCanvasCtx.drawImage(video, 0, 0, canvas.width, canvas.height);

        const imageData = scanCanvasCtx.getImageData(0, 0, canvas.width, canvas.height);
        const code = window.jsQR(imageData.data, imageData.width, imageData.height, {
            inversionAttempts: 'dontInvert'
        });

        if (code && code.data) {
            // 发现二维码
            stopScanner();
            if (scanCallbackRef) {
                scanCallbackRef.invokeMethodAsync(callbackMethod, code.data);
            }
            return;
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
