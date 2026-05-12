// 表格箭头导航：上下左右键在数据行单元格之间移动焦点
// 使用 capture 阶段捕获，避免被 MudBlazor 组件拦截事件冒泡
window.enableTableArrowNav = function (containerSelector) {
    const container = document.querySelector(containerSelector);
    if (!container) {
        console.warn('[ArrowNav] Container not found:', containerSelector);
        setTimeout(function () {
            const retryContainer = document.querySelector(containerSelector);
            if (retryContainer && retryContainer.dataset.arrowNavEnabled !== 'true') {
                console.log('[ArrowNav] Retry setting up on:', containerSelector);
                window.enableTableArrowNav(containerSelector);
            }
        }, 500);
        return false;
    }

    if (container.dataset.arrowNavEnabled === 'true') return true;

    // 状态机：_mudSelectActive = true 表示 MudSelect 处于"下拉选择模式"
    // Enter 切换开关，Escape 退出，左右键离开时也会退出
    var _mudSelectActive = false;

    // 鼠标点击 MudSelect（输入框）时进入下拉模式
    // 鼠标点击选项（弹窗内）时退出下拉模式
    document.addEventListener('mousedown', function (e) {
        if (e.target.closest('.mud-popover')) {
            _mudSelectActive = false;
        } else if (e.target.closest('.mud-select')) {
            _mudSelectActive = true;
        }
    });

    // === MudSelect 弹出菜单选项导航辅助函数 ===
    // MudSelect 的弹出层由 MudBlazor 渲染在 document 根级（portal），
    // 不在表格容器内，因此从 document 级别查找
    function getPopoverItems() {
        var popover = document.querySelector('.mud-popover-open .mud-list');
        if (!popover) return null;
        var items = popover.querySelectorAll('.mud-list-item-clickable');
        return items.length > 0 ? Array.from(items) : null;
    }

    function getHighlightedIndex(items) {
        for (var i = 0; i < items.length; i++) {
            if (items[i].classList.contains('mud-selected-item')) return i;
        }
        return -1;
    }

    function highlightItem(items, index) {
        for (var i = 0; i < items.length; i++) {
            items[i].classList.remove('mud-selected-item');
        }
        if (index >= 0 && index < items.length) {
            items[index].classList.add('mud-selected-item');
            items[index].scrollIntoView({ block: 'nearest' });
        }
    }

    var handler = function (e) {
        var active = document.activeElement;
        if (!active || !active.closest('td')) return;

        var key = e.key;
        var activeMudSelect = active.closest('.mud-select');

        // === Enter 处理 ===
        if (key === 'Enter') {
            // MudCheckBox / MudSwitch 切换
            var activeCheckbox = active.closest('.mud-checkbox, .mud-switch');
            if (activeCheckbox && !activeMudSelect) {
                var nativeInput = activeCheckbox.querySelector('input[type="checkbox"]');
                if (nativeInput) {
                    nativeInput.click();
                    e.preventDefault();
                    e.stopPropagation();
                    return;
                }
            }
            // === MudSelect：已在下拉模式 → 点击当前高亮项确认选择 ===
            // JS 侧记录的高亮索引可能与 Blazor 不同（因为我们在 JS 侧导航），
            // 因此需要点击对应的 DOM 元素来触发 Blazor 的 MudSelectItem.OnClick 处理
            if (activeMudSelect && _mudSelectActive) {
                e.preventDefault();
                e.stopPropagation();
                var confirmItems = getPopoverItems();
                if (confirmItems) {
                    var hi = getHighlightedIndex(confirmItems);
                    if (hi >= 0 && confirmItems[hi]) {
                        confirmItems[hi].click(); // 触发 Blazor 选择逻辑
                    }
                }
                _mudSelectActive = false;
                return;
            }
            // === MudSelect：第一次 Enter → 打开下拉 ===
            if (activeMudSelect) {
                _mudSelectActive = true;
                return; // 不拦截，让 MudSelect 处理打开
            }
        }

        // === Escape 退出下拉模式 ===
        if (key === 'Escape' && activeMudSelect) {
            _mudSelectActive = false;
            return; // 不拦截，让 MudSelect 处理 Escape
        }

        // 只处理方向键
        if (!['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(key)) return;

        // === 下拉模式：JS 侧直接操作 DOM 高亮 ===
        // 绕过 Blazor WASM 互调（JS→.NET→StateHasChanged→Render→DOM），
        // 直接在 JS 侧添加/移除 .mud-selected-item，实现即时响应
        if (_mudSelectActive && activeMudSelect && (key === 'ArrowUp' || key === 'ArrowDown')) {
            var popupItems = getPopoverItems();
            if (popupItems) {
                e.preventDefault();
                e.stopPropagation();
                var curIdx = getHighlightedIndex(popupItems);
                var newIdx = key === 'ArrowDown'
                    ? Math.min(curIdx + 1, popupItems.length - 1)
                    : Math.max(curIdx - 1, 0);
                if (newIdx !== curIdx || curIdx < 0) {
                    highlightItem(popupItems, newIdx);
                }
            }
            return;
        }

        // === 左右键离开 MudSelect 时退出下拉模式 ===
        if ((key === 'ArrowLeft' || key === 'ArrowRight') && activeMudSelect) {
            _mudSelectActive = false;
        }

        var currentTd = active.closest('td');
        var currentTr = currentTd.closest('tr');
        var tbody = currentTr.closest('tbody');
        if (!tbody) return;

        var rows = Array.from(tbody.querySelectorAll('tr'));
        var rowIndex = rows.indexOf(currentTr);
        var cells = Array.from(currentTr.querySelectorAll('td'));
        var cellIndex = cells.indexOf(currentTd);
        if (rowIndex < 0 || cellIndex < 0) return;

        var targetTd = null;
        var findInputCell = function (tds, start, step) {
            for (var i = start; i >= 0 && i < tds.length; i += step) {
                if (tds[i].querySelector('input, select, textarea, .mud-input'))
                    return tds[i];
            }
            return null;
        };

        switch (key) {
            case 'ArrowDown':
                if (rowIndex < rows.length - 1)
                    targetTd = rows[rowIndex + 1].querySelectorAll('td')[cellIndex];
                break;
            case 'ArrowUp':
                if (rowIndex > 0)
                    targetTd = rows[rowIndex - 1].querySelectorAll('td')[cellIndex];
                break;
            case 'ArrowRight':
                targetTd = findInputCell(cells, cellIndex + 1, 1);
                break;
            case 'ArrowLeft':
                targetTd = findInputCell(cells, cellIndex - 1, -1);
                break;
        }

        if (targetTd) {
            e.preventDefault();
            if (key === 'ArrowUp' || key === 'ArrowDown') e.stopPropagation();

            // 离开 MudSelect：blur 触发其关闭逻辑
            if (activeMudSelect) {
                var mudInput = activeMudSelect.querySelector('input:not([type=hidden])');
                if (mudInput && document.activeElement === mudInput) {
                    mudInput.blur();
                }
            }

            // 进入目标单元格
            var isTargetMudSelect = !!targetTd.querySelector('.mud-select');
            var targetInput = null;

            if (isTargetMudSelect) {
                targetInput = targetTd.querySelector('input:not([type=hidden])');
                if (targetInput) {
                    targetInput.focus();
                } else {
                    var mudDiv = targetTd.querySelector('.mud-input');
                    if (mudDiv) {
                        if (!mudDiv.getAttribute('tabindex')) {
                            mudDiv.setAttribute('tabindex', '-1');
                        }
                        mudDiv.focus();
                    }
                }
            } else {
                targetInput = targetTd.querySelector('select, input:not([type=hidden]), textarea');
                if (targetInput) {
                    targetInput.focus();
                    if (targetInput instanceof HTMLInputElement && targetInput.type !== 'checkbox' && targetInput.type !== 'number') {
                        targetInput.setSelectionRange(targetInput.value.length, targetInput.value.length);
                    }
                } else {
                    var mudInput = targetTd.querySelector('.mud-input');
                    if (mudInput) mudInput.click();
                    else {
                        var hiddenInput = targetTd.querySelector('input[type=hidden]');
                        if (hiddenInput) {
                            var mudSelect = hiddenInput.closest('.mud-select');
                            if (mudSelect) mudSelect.querySelector('.mud-input')?.click();
                        }
                    }
                }
            }
        } else if (key === 'ArrowUp' || key === 'ArrowDown') {
            // 没有目标单元格（已在首行/末行）：阻止事件传播到 MudSelect
            e.preventDefault();
            e.stopPropagation();
        }
    };

    container.addEventListener('keydown', handler, { capture: true });
    container.dataset.arrowNavEnabled = 'true';
    console.log('[ArrowNav] Setup complete on:', containerSelector);
    return true;
};
