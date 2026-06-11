// PWA 安装提示 — 监听 beforeinstallprompt 事件
window.pwaInstallPrompt = null;

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    window.pwaInstallPrompt = e;
});

window.getPwaInstallPrompt = function () {
    return window.pwaInstallPrompt != null;
};

window.triggerPwaInstall = function () {
    if (window.pwaInstallPrompt) {
        window.pwaInstallPrompt.prompt();
        return window.pwaInstallPrompt.userChoice.then(function (result) {
            window.pwaInstallPrompt = null;
            return result.outcome === 'accepted';
        });
    }
    return Promise.resolve(false);
};
