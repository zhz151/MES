// 筛选弹窗定位函数
window.positionFilterPopup = function (btnId, popupId) {
    var btn = document.getElementById(btnId);
    var popup = document.getElementById(popupId);
    if (!btn || !popup) return;
    var rect = btn.getBoundingClientRect();
    var popupWidth = Math.max(260, Math.min(400, rect.width * 4));
    var left = Math.max(4, Math.min(rect.left, window.innerWidth - popupWidth - 4));
    popup.style.left = left + 'px';
    popup.style.width = popupWidth + 'px';

    // 先设为可见并清除高度限制，以便测量实际高度
    popup.style.visibility = 'visible';
    popup.style.maxHeight = '';
    popup.style.overflowY = '';
    var popupHeight = popup.offsetHeight;

    // 底部边界检查：如果弹窗超出视口底部，则向上弹出
    var spaceBelow = window.innerHeight - rect.bottom - 4;
    if (popupHeight > spaceBelow) {
        var spaceAbove = rect.top - 4;
        if (spaceAbove >= spaceBelow || spaceBelow < 100) {
            // 上方空间更大或下方空间不足 100px：向上弹出
            popup.style.top = Math.max(4, rect.top - popupHeight - 4) + 'px';
            // 重新检查顶部是否超出，若仍超出则限制弹窗高度
            var overflow = Math.abs(rect.top - popupHeight - 4 - 4);
            if (popup.offsetTop < 4) {
                popup.style.top = '4px';
                popup.style.maxHeight = (rect.top - 8) + 'px';
                popup.style.overflowY = 'auto';
            }
        } else {
            // 下方空间不足但也不适合向上：限制弹窗高度
            popup.style.top = (rect.bottom + 4) + 'px';
            popup.style.maxHeight = spaceBelow + 'px';
            popup.style.overflowY = 'auto';
        }
    } else {
        popup.style.top = (rect.bottom + 4) + 'px';
    }
};
