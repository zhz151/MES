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

// 格式化打印日期（yyyy-MM-dd，与后端 PDF 页脚一致）
function formatPrintDate(d) {
    var y = d.getFullYear();
    var m = ('0' + (d.getMonth() + 1)).slice(-2);
    var day = ('0' + d.getDate()).slice(-2);
    return y + '-' + m + '-' + day;
}

// 打印二维码标签（工位/设备/员工通用；二维码放大便于张贴，无标题/打印日期）
// 2026-08-29 改为本地生成：POST 后端批量二维码端点（api/scan/qr-codes，QRCoder 生成 PNG），不再依赖外部在线二维码服务
window.MES = window.MES || {};
window.MES.printQrCodes = function (codes) {
    if (!codes || codes.length === 0) return;

    // 构造 API 基地址：优先 MES_API_URL（解决开发环境 Blazor 与 API 端口不一致），兜底当前 origin
    var origin = window.location.origin;
    var apiBase = window.MES_API_URL || origin;
    var apiUrl = apiBase + '/api/scan/qr-codes';

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
        body: JSON.stringify({ Codes: codes })
    })
    .then(function (r) {
        if (!r.ok) {
            // 后端 BusinessException 返回 400 + ApiResponse<object>.message，解析出友好提示
            return r.json().then(function (envelope) {
                var msg = (envelope && envelope.message) ? envelope.message : ('HTTP ' + r.status);
                throw new Error(msg);
            }, function () {
                throw new Error('HTTP ' + r.status);
            });
        }
        return r.json().then(function (envelope) {
            if (!envelope || !envelope.success || !envelope.data || envelope.data.length === 0) {
                throw new Error(envelope && envelope.message || '响应格式异常');
            }
            var rows = envelope.data.map(function (base64, index) {
                var code = codes[index] || '';
                return '<div style="display:inline-block; text-align:center; margin:10px; padding:10px; border:1px dashed #999;">' +
                    '<img src="data:image/png;base64,' + base64 + '" alt="' + code + '" style="width:480px;height:480px;" />' +
                    '<div style="margin-top:8px; font-size:14px; font-weight:bold;">' + code + '</div>' +
                    '</div>';
            }).join('');

            var html = '<html><head>' +
                '<style>' +
                '@page{size:landscape;margin:10mm;}' +
                'body{text-align:center;font-family:sans-serif;}' +
                '</style></head><body>' +
                '<div>' + rows + '</div>' +
                '<script>window.onload=function(){window.print();}<' + '/script>' +
                '</body></html>';

            var w = window.open('', '_blank');
            if (!w) {
                showPrintNotice('浏览器阻止了弹窗，请允许后重试', 'error');
                return;
            }
            w.document.write(html);
            w.document.close();
        });
    })
    .catch(function (e) {
        console.error('二维码生成失败:', e);
        var msg = (e && e.message) ? e.message : '未知错误';
        showPrintNotice('二维码生成失败：' + msg, 'error');
    });
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
        'h2{text-align:center;margin:6px 0 12px;font-size:22px;}' +
        'table{width:100%;border-collapse:collapse;font-size:17px;table-layout:auto;}' +
        'th,td{border:1px solid #333;padding:4px 6px;text-align:left;white-space:nowrap;word-break:normal;}' +
        'th{background-color:#e0e0e0;font-weight:600;}' +
        '.mud-table-cell{border:1px solid #333;padding:4px 6px;}' +
        '.mud-table-cell--right{text-align:right;}' +
        '.col-header-cell .mud-icon-root,.col-header-cell .th-label svg{display:none;}' +
        'tr{page-break-inside:avoid;}' +
        '@media print{body{-webkit-print-color-adjust:exact;print-color-adjust:exact;}}' +
        '[style*=\"display:none\"],[style*=\"display: none\"]{display:none!important;}' +
        '</style></head><body>' +
        '<h2>' + (title || '') + '</h2>' +
        html +
        '<div style="margin-top:10px;padding-top:6px;border-top:1px solid #999;text-align:left;font-size:14px;">打印日期：' + formatPrintDate(new Date()) + '</div>' +
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

// ===== PDF 打印（fetch + Blob URL + iframe 同页覆盖层——标准做法）=====
// C# 传入 API 地址和 JSON 请求体，JS 直接 fetch 获取二进制 PDF

window.openPdfFromApi = function (apiUrl, jsonBody, skipColumnCheck) {
    // 列数前置校验：仅适用于「列表打印」（TablePrintHelper 单层平铺，列过多时各列被压到单字符放不下 → QuestPDF 布局冲突）。
    // 阈值与后端 TablePrintHelper.MaxPrintColumns(35) 同步。下列两类请求必须跳过校验（由调用方传第三参 skipColumnCheck=true）：
    //   1) 富布局/单据打印：请求也携带 Columns（如工艺卡的 ProcessCardColumnDef），但那些列只是"区块内字段布局参数"，分块/分行渲染不受 35 列限制；
    //   2) 不携带 Columns 的请求（单据/批量计划打印，json 为 "{}"）本就走不到此分支。
    if (!skipColumnCheck) {
        var MAX_PRINT_COLUMNS = 35;
        try {
            var reqBody = JSON.parse(jsonBody);
            var cols = reqBody.Columns || reqBody.columns;
            if (cols && Array.isArray(cols) && cols.length > MAX_PRINT_COLUMNS) {
                showPrintNotice('当前可见列过多（' + cols.length + ' 列，打印上限 ' + MAX_PRINT_COLUMNS + ' 列），请通过列显隐精简后再打印', 'warning');
                return;
            }
        } catch (e) { }
    }

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
        if (!r.ok) {
            // 后端 BusinessException 返回 400 + ApiResponse<object>.message，解析出友好提示（如"打印列数过多…"）
            return r.json().then(function (envelope) {
                var msg = (envelope && envelope.message) ? envelope.message : ('HTTP ' + r.status);
                throw new Error(msg);
            }, function () {
                throw new Error('HTTP ' + r.status);
            });
        }
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
        var msg = (e && e.message) ? e.message : '未知错误';
        showPrintNotice('打印失败：' + msg + '\n请按 F12 查看详细错误', 'error');
    });
};

// 页面内警示覆盖层（列过多无法打印 / 打印失败等业务警示），替代原生 alert，样式醒目且可关闭
function showPrintNotice(message, level) {
    var existing = document.getElementById('print-notice-overlay');
    if (existing) existing.remove();

    var color = level === 'error' ? '#d32f2f' : '#ed6c02';
    var overlay = document.createElement('div');
    overlay.id = 'print-notice-overlay';
    overlay.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;z-index:99999;background:rgba(0,0,0,0.45);display:flex;align-items:center;justify-content:center;';

    var box = document.createElement('div');
    box.style.cssText = 'background:#fff;border-radius:8px;padding:24px 28px;max-width:520px;box-shadow:0 8px 24px rgba(0,0,0,0.25);text-align:center;font-family:sans-serif;';

    var title = document.createElement('div');
    title.textContent = level === 'error' ? '打印失败' : '无法打印';
    title.style.cssText = 'font-size:18px;font-weight:bold;color:' + color + ';margin-bottom:12px;';

    var msg = document.createElement('div');
    msg.textContent = message || '';
    msg.style.cssText = 'font-size:14px;color:#333;line-height:1.7;margin-bottom:18px;white-space:pre-line;word-break:break-all;';

    var btn = document.createElement('button');
    btn.textContent = '我知道了';
    btn.style.cssText = 'padding:8px 28px;cursor:pointer;font-size:14px;background:' + color + ';color:#fff;border:none;border-radius:4px;';
    btn.onclick = function () { overlay.remove(); };

    box.appendChild(title);
    box.appendChild(msg);
    box.appendChild(btn);
    overlay.appendChild(box);
    document.body.appendChild(overlay);
}

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
