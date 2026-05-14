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

// 隐藏表格中的"操作"列
function hideActionColumn(table) {
    var headerRow = table.querySelector('thead tr');
    if (!headerRow) return;
    var ths = headerRow.querySelectorAll('th');
    for (var i = 0; i < ths.length; i++) {
        if (ths[i].textContent.trim() === '操作') {
            table.querySelectorAll('tr').forEach(function(row) {
                var cells = row.querySelectorAll('td, th');
                if (cells[i]) cells[i].style.display = 'none';
            });
            break;
        }
    }
}

// 在新窗口中打开打印 HTML
function openPrintWindow(html, title) {
    var printWindow = window.open('', '_blank');
    printWindow.document.write(
        '<html><head>' +
        '<title>' + (title || '打印') + '</title>' +
        '<style>' +
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

// 打印 DOM 表格内容（选中记录/当前页）
window.printTable = function (containerSelector, title) {
    var container = document.querySelector(containerSelector);
    if (!container) return;

    var table = container.querySelector('table');
    if (!table) return;

    var clonedTable = table.cloneNode(true);
    // 移除表格内的方向键编辑控件
    clonedTable.querySelectorAll('.compact-input, .mud-input').forEach(function(el) { el.remove(); });
    // 隐藏复选框列
    clonedTable.querySelectorAll('input[type="checkbox"]').forEach(function(el) {
        var td = el.closest('td, th');
        if (td) td.style.display = 'none';
    });
    // 隐藏操作列
    hideActionColumn(clonedTable);

    openPrintWindow(clonedTable.outerHTML, title);
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
