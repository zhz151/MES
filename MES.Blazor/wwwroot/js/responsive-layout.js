// 响应式布局检测 — 监听窗口大小变化，通知 Blazor 切换桌面/移动端布局
let dotNetRef = null;
let callbackMethod = null;
const MOBILE_BREAKPOINT = 960; // MudBlazor "sm" breakpoint

window.checkMobile = function () {
    return window.innerWidth < MOBILE_BREAKPOINT;
};

window.listenResize = function (dotnetRef, method) {
    dotNetRef = dotnetRef;
    callbackMethod = method;
    window.addEventListener('resize', onResizeHandler);
};

window.stopListening = function () {
    window.removeEventListener('resize', onResizeHandler);
    dotNetRef = null;
    callbackMethod = null;
};

function onResizeHandler() {
    if (dotNetRef && callbackMethod) {
        const isMobile = window.innerWidth < MOBILE_BREAKPOINT;
        dotNetRef.invokeMethodAsync(callbackMethod, isMobile);
    }
}
