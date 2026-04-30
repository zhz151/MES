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
