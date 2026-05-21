// 筛选弹窗定位函数
window.positionFilterPopup = function (btnId, popupId) {
    var btn = document.getElementById(btnId);
    var popup = document.getElementById(popupId);
    if (!btn || !popup) return;
    var rect = btn.getBoundingClientRect();
    var popupWidth = Math.max(260, Math.min(400, rect.width * 4));
    var left = Math.max(4, Math.min(rect.left, window.innerWidth - popupWidth - 4));
    popup.style.top = (rect.bottom + 4) + 'px';
    popup.style.left = left + 'px';
    popup.style.width = popupWidth + 'px';
    popup.style.visibility = 'visible';
};
