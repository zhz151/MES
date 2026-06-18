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
function openPrintWindow(html, title) {
    var printWindow = window.open('', '_blank');
    printWindow.document.write(
        '<html><head>' +
        '<title>' + (title || '打印') + '</title>' +
        '<style>' +
        '@page{size:landscape;margin:15mm;}' +
        'body{font-family:\"Helvetica Neue\",Helvetica,Arial,sans-serif;padding:30px;}' +
        'h2{text-align:center;margin-bottom:20px;font-size:18px;}' +
        'table{width:100%;border-collapse:collapse;font-size:12px;}' +
        'th,td{border:1px solid #333;padding:5px 6px;text-align:left;}' +
        'th{background-color:#e0e0e0;font-weight:600;}' +
        '.mud-table-cell{border:1px solid #333;padding:5px 6px;}' +
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

// 打印 DOM 表格内容（所见即所得）
window.printTable = function (containerSelector, title) {
    var container = document.querySelector(containerSelector);
    if (!container) return;

    var table = container.querySelector('table');
    if (!table) return;

    openPrintWindow(table.outerHTML, title);
};

// 打印原始 HTML 表格内容（打印全部）
window.printRawHtml = function (htmlContent, title) {
    if (!htmlContent) return;
    openPrintWindow(htmlContent, title);
};

// 打开 Base64 PDF（通过 Blob URL 避免浏览器安全限制）
window.openPdf = function (base64) {
    try {
        const byteCharacters = atob(base64);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: 'application/pdf' });
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
    } catch (e) {
        console.error('打印失败:', e);
    }
};
