// 问题照片附件辅助脚本：客户端压缩 + Blob URL 预览 + 释放
window.MES = window.MES || {};

(function () {
    var objectUrls = [];

    // 客户端压缩：按最长边限制尺寸并转 JPEG，返回 base64 字符串（不含 data: 前缀）
    // 返回 { data: base64, fileName: string, contentType: string } ；失败返回 null
    window.MES.compressImage = function (file, maxSize, quality) {
        return new Promise(function (resolve) {
            if (!file || !file.type || file.type.indexOf('image/') !== 0) {
                resolve(null);
                return;
            }
            maxSize = maxSize || 1600;
            quality = quality || 0.8;

            var reader = new FileReader();
            reader.onload = function (e) {
                var img = new Image();
                img.onload = function () {
                    var w = img.width, h = img.height;
                    var scale = Math.min(1, maxSize / Math.max(w, h));
                    var cw = Math.round(w * scale), ch = Math.round(h * scale);
                    var canvas = document.createElement('canvas');
                    canvas.width = cw; canvas.height = ch;
                    var ctx = canvas.getContext('2d');
                    // 白底填充，避免 PNG 透明区转 JPEG 变黑
                    ctx.fillStyle = '#ffffff';
                    ctx.fillRect(0, 0, cw, ch);
                    ctx.drawImage(img, 0, 0, cw, ch);

                    var dataUrl = canvas.toDataURL('image/jpeg', quality);
                    var base64 = dataUrl.split(',')[1];
                    var name = (file.name || 'photo').replace(/\.[^.]+$/, '') + '.jpg';
                    resolve({ data: base64, fileName: name, contentType: 'image/jpeg' });
                };
                img.onerror = function () { resolve(null); };
                img.src = e.target.result;
            };
            reader.onerror = function () { resolve(null); };
            reader.readAsDataURL(file);
        });
    };

    // 触发隐藏的 <input type="file"> 选择框
    window.MES.clickElement = function (el) {
        if (el) el.click();
    };

    // 读取 <input type="file"> 选中的图片并逐张压缩，返回 [{data, fileName, contentType}]；读完清空 input
    window.MES.readCompressedFiles = async function (inputElement, maxSize, quality) {
        var out = [];
        if (!inputElement || !inputElement.files) return out;
        for (var i = 0; i < inputElement.files.length; i++) {
            var r = await window.MES.compressImage(inputElement.files[i], maxSize, quality);
            if (r) out.push(r);
        }
        inputElement.value = '';
        return out;
    };

    // base64 → Blob URL（用于 <img src> 预览，支持缩略图与原图复用同一 URL）
    window.MES.base64ToObjectUrl = function (base64, contentType) {
        try {
            var byteCharacters = atob(base64);
            var byteNumbers = new Array(byteCharacters.length);
            for (var i = 0; i < byteCharacters.length; i++) {
                byteNumbers[i] = byteCharacters.charCodeAt(i);
            }
            var blob = new Blob([new Uint8Array(byteNumbers)], { type: contentType || 'image/jpeg' });
            var url = URL.createObjectURL(blob);
            objectUrls.push(url);
            return url;
        } catch (e) {
            console.error('生成图片预览失败:', e);
            return null;
        }
    };

    // 释放所有已创建的 Blob URL（页面离开时调用，防内存泄漏）
    window.MES.revokeAllObjectUrls = function () {
        for (var i = 0; i < objectUrls.length; i++) {
            try { URL.revokeObjectURL(objectUrls[i]); } catch (e) { }
        }
        objectUrls = [];
    };
})();
