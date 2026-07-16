// 下载文件（Base64 → Blob URL → 触发下载）
window.downloadFile = function (base64, fileName) {
    try {
        const byteCharacters = atob(base64);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (e) {
        console.error('下载失败:', e);
    }
};

// 打印设备二维码标签
window.MES = window.MES || {};
window.MES.printQrCodes = function (codes) {
    if (!codes || codes.length === 0) return;

    var rows = codes.map(function (code) {
        var encoded = encodeURIComponent(code);
        return '<div style="display:inline-block; text-align:center; margin:10px; padding:10px; border:1px dashed #999;">' +
            '<img src="https://api.qrserver.com/v1/create-qr-code/?size=120x120&data=' + encoded + '" alt="' + code + '" style="width:120px;height:120px;" />' +
            '<div style="margin-top:6px; font-size:12px; font-weight:bold;">' + code + '</div>' +
            '</div>';
    }).join('');

    var html = '<html><head><title>设备二维码</title>' +
        '<style>' +
        '@page{size:landscape;margin:10mm;}' +
        'body{text-align:center;font-family:sans-serif;}' +
        '</style></head><body>' +
        '<h2 style="font-size:16px;margin-bottom:20px;">设备二维码标签</h2>' +
        '<div>' + rows + '</div>' +
        '<script>window.onload=function(){window.print();}<' + '/script>' +
        '</body></html>';

    var w = window.open('', '_blank');
    w.document.write(html);
    w.document.close();
};

// 在新窗口中打开打印 HTML
function openPrintWindow(html, title, pageOrientation) {
    var orientation = pageOrientation || 'landscape';
    var margin = orientation === 'portrait' ? '12mm 15mm' : '5mm 8mm';
    var printWindow = window.open('', '_blank');
    printWindow.document.write(
        '<html><head>' +
        '<title>' + (title || '打印') + '</title>' +
        '<style>' +
        '@page{size:' + orientation + ';margin:' + margin + ';}' +
        'body{font-family:\"Helvetica Neue\",Helvetica,Arial,sans-serif;padding:0;margin:0;}' +
        'h2{text-align:center;margin:6px 0 12px;font-size:16px;}' +
        'table{width:100%;border-collapse:collapse;font-size:11px;table-layout:auto;}' +
        'th,td{border:1px solid #333;padding:3px 5px;text-align:left;word-break:break-all;}' +
        'th{background-color:#e0e0e0;font-weight:600;}' +
        '.mud-table-cell{border:1px solid #333;padding:3px 5px;}' +
        '.mud-table-cell--right{text-align:right;}' +
        '.col-header-cell .mud-icon-root,.col-header-cell .th-label svg{display:none;}' +
        'tr{page-break-inside:avoid;}' +
        '@media print{body{-webkit-print-color-adjust:exact;print-color-adjust:exact;}}' +
        '[style*=\"display:none\"],[style*=\"display: none\"]{display:none!important;}' +
        '</style></head><body>' +
        '<h2>' + (title || '') + '</h2>' +
        html +
        '<script>window.print();window.close();<' + '/script>' +
        '</body></html>'
    );
    printWindow.document.close();
}

// 获取 DOM 表格的 HTML（供 C# 调用 printRawHtml 使用）
window.getTableHtml = function (containerSelector) {
    var container = document.querySelector(containerSelector);
    if (!container) return '';
    var table = container.querySelector('table');
    return table ? table.outerHTML : '';
};

// 打印原始 HTML 表格内容（打印全部）
window.printRawHtml = function (htmlContent, title, pageOrientation) {
    if (!htmlContent) return;
    openPrintWindow(htmlContent, title, pageOrientation);
};

// ===== PDF 打印（Base64 兼容版——旧页面用，Blob URL + iframe 覆盖层）=====

window.openPdf = function (base64) {
    try {
        var byteChars = atob(base64);
        var byteNums = new Array(byteChars.length);
        for (var i = 0; i < byteChars.length; i++) {
            byteNums[i] = byteChars.charCodeAt(i);
        }
        var byteArr = new Uint8Array(byteNums);
        var blob = new Blob([byteArr], { type: 'application/pdf' });
        var url = URL.createObjectURL(blob);
        showPdfOverlay(url);
    } catch (e) {
        console.error('PDF打开失败:', e);
        alert('PDF打开失败: ' + e.message);
    }
};

// ===== PDF 打印（fetch + Blob URL + iframe 同页覆盖层——标准做法）=====
// C# 传入 API 地址和 JSON 请求体，JS 直接 fetch 获取二进制 PDF

window.openPdfFromApi = function (apiUrl, jsonBody) {
    // 修正 API 基地址：若 Blazor 端口与 API 端口不同，替换 origin
    var apiBase = window.MES_API_URL;
    if (apiBase) {
        var currentOrigin = window.location.origin;
        if (apiUrl.indexOf(currentOrigin + '/') === 0) {
            apiUrl = apiBase + apiUrl.substring(currentOrigin.length);
        }
    }

    // 读取 JWT 令牌（与 AuthHttpClient 共用 localStorage，Blazored.LocalStorage 存的是 JSON 格式需 parse）
    var raw = localStorage.getItem('authToken');
    var token = null;
    if (raw) {
        try { token = JSON.parse(raw); } catch (e) { token = raw; }
    }
    var headers = { 'Content-Type': 'application/json' };
    if (token) {
        headers['Authorization'] = 'Bearer ' + token;
    }

    fetch(apiUrl, {
        method: 'POST',
        headers: headers,
        body: jsonBody
    })
    .then(function (r) {
        if (!r.ok) throw new Error('HTTP ' + r.status);
        var contentType = r.headers.get('content-type') || '';
        if (contentType.indexOf('application/pdf') !== -1) {
            // 端点直接返回 PDF 文件（如 TablePrintHelper）
            return r.arrayBuffer().then(function (buffer) {
                var blob = new Blob([buffer], { type: 'application/pdf' });
                var url = URL.createObjectURL(blob);
                showPdfOverlay(url);
            });
        } else {
            // 端点返回 JSON ApiResponse<string> 包裹的 Base64
            return r.json().then(function (envelope) {
                if (!envelope || !envelope.success || !envelope.data) {
                    throw new Error(envelope && envelope.message || '响应格式异常');
                }
                var base64 = envelope.data;
                var byteChars = atob(base64);
                var byteNums = new Array(byteChars.length);
                for (var i = 0; i < byteChars.length; i++) {
                    byteNums[i] = byteChars.charCodeAt(i);
                }
                var byteArr = new Uint8Array(byteNums);
                var blob = new Blob([byteArr], { type: 'application/pdf' });
                var url = URL.createObjectURL(blob);
                showPdfOverlay(url);
            });
        }
    })
    .catch(function (e) {
        console.error('PDF加载失败:', e);
        alert('PDF加载失败: ' + e.message + '\n请按 F12 查看详细错误');
    });
};

function showPdfOverlay(url) {
    var existing = document.getElementById('pdf-overlay');
    if (existing) existing.remove();

    // 覆盖层
    var overlay = document.createElement('div');
    overlay.id = 'pdf-overlay';
    overlay.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;z-index:99999;background:#fff;display:flex;flex-direction:column;';

    // 工具栏
    var toolbar = document.createElement('div');
    toolbar.style.cssText = 'background:#f0f0f0;padding:6px 16px;display:flex;align-items:center;gap:8px;border-bottom:1px solid #ccc;flex-shrink:0;';

    var closeBtn = document.createElement('button');
    closeBtn.textContent = '✕ 关闭';
    closeBtn.style.cssText = 'padding:4px 16px;cursor:pointer;font-size:13px;background:#f44336;color:#fff;border:none;border-radius:3px;';
    closeBtn.onclick = function () {
        overlay.remove();
        URL.revokeObjectURL(url);
    };

    var printBtn = document.createElement('button');
    printBtn.textContent = '打印';
    printBtn.style.cssText = 'padding:4px 16px;cursor:pointer;font-size:13px;background:#1565C0;color:#fff;border:none;border-radius:3px;';
    printBtn.onclick = function () {
        var iframe = document.getElementById('pdf-viewer-frame');
        if (iframe) try { iframe.contentWindow.print(); } catch (ex) { }
    };

    toolbar.appendChild(closeBtn);
    toolbar.appendChild(document.createTextNode(' '));
    toolbar.appendChild(printBtn);

    // iframe
    var iframe = document.createElement('iframe');
    iframe.id = 'pdf-viewer-frame';
    iframe.src = url;
    iframe.style.cssText = 'flex:1;border:none;width:100%;';

    overlay.appendChild(toolbar);
    overlay.appendChild(iframe);
    document.body.appendChild(overlay);
}

// 从 Blazor 启动时注入 API 基地址（解决开发环境端口不一致问题）
window.MES_setApiUrl = function (url) {
    window.MES_API_URL = url;
};
