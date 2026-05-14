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

// 打印表格内容（通过新窗口输出表格 HTML）
window.printTable = function (containerSelector, title) {
    const container = document.querySelector(containerSelector);
    if (!container) return;

    const table = container.querySelector('table');
    if (!table) return;

    const clonedTable = table.cloneNode(true);
    // 移除表格内的方向键编辑控件（隐藏输入框）
    clonedTable.querySelectorAll('.compact-input, .mud-input').forEach(el => el.remove());
    // 移除复选框列
    clonedTable.querySelectorAll('input[type="checkbox"]').forEach(el => {
        const td = el.closest('td, th');
        if (td) td.style.display = 'none';
    });

    const printWindow = window.open('', '_blank');
    printWindow.document.write('\
        <html>\
        <head>\
            <title>' + (title || '打印') + '</title>\
            <style>\
                body { font-family: "Helvetica Neue", Helvetica, Arial, sans-serif; padding: 30px; }\
                h2 { text-align: center; margin-bottom: 20px; font-size: 18px; }\
                table { width: 100%; border-collapse: collapse; font-size: 12px; }\
                th, td { border: 1px solid #333; padding: 5px 6px; text-align: left; }\
                th { background-color: #e0e0e0; font-weight: 600; }\
                .mud-table-cell { border: 1px solid #333; padding: 5px 6px; }\
                @media print { body { -webkit-print-color-adjust: exact; print-color-adjust: exact; } }\
            </style>\
        </head>\
        <body>\
            <h2>' + (title || '') + '</h2>\
            ' + clonedTable.outerHTML + '\
            <script>window.print();window.close();<' + '/script>\
        </body>\
        </html>\
    ');
    printWindow.document.close();
};

// 打开 Base64 PDF（通过 Blob URL 避免浏览器安全限制）
window.openPdf = function (base64) {
    try {
        // 将 Base64 转换为二进制
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
