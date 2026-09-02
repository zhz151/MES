// 表格箭头导航：上下左右键在数据行单元格之间移动焦点
// 使用 capture 阶段捕获，避免被 MudBlazor 组件拦截事件冒泡
window.enableTableArrowNav = function (containerSelector) {
    const container = document.querySelector(containerSelector);
    if (!container) {
        console.debug('[ArrowNav] Container not found (retrying in 500ms):', containerSelector);
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

// 初始化分组标题栏：测量 MudTable 实际列宽并同步滚动
// 每次 OnAfterRenderAsync 均会调用，因此需防重复注册 scroll 监听
window.initGroupHeaders = function (tableSelector) {
    var wrapper = document.querySelector(tableSelector);
    if (!wrapper) return false;

    var headerScroll = wrapper.querySelector('.col-group-header-scroll');
    var headerBar = headerScroll ? headerScroll.querySelector('.col-group-header-bar') : null;
    if (!headerScroll || !headerBar) return false;

    var groupItems = headerBar.querySelectorAll('.col-group-header-item');
    if (groupItems.length === 0) return false;

    // 同步逻辑函数：测量 <th> 实际宽度并应用到分组标题栏
    function syncGroupWidths() {
        var tableContainer = wrapper.querySelector('.mud-table-container');
        if (!tableContainer) return;

        var thead = tableContainer.querySelector('thead');
        if (!thead) return;
        var headerRow = thead.querySelector('tr');
        if (!headerRow) return;
        var thCells = headerRow.querySelectorAll('th');
        if (thCells.length === 0) return;

        function getGroupKey(className) {
            var match = className.match(/\bcol-g(\d+)\b/);
            return match ? parseInt(match[1]) : 0;
        }

        var itemIndex = 0;
        var barTotalWidth = 0;
        var groupItemWidth = 0;
        var currentGk = null;
        var liveGroupItems = headerBar.querySelectorAll('.col-group-header-item');

        thCells.forEach(function (th) {
            var gk = getGroupKey(th.className);
            if (gk !== currentGk && currentGk !== null) {
                if (itemIndex < liveGroupItems.length) {
                    liveGroupItems[itemIndex].style.width = groupItemWidth + 'px';
                    barTotalWidth += groupItemWidth;
                    itemIndex++;
                }
                groupItemWidth = 0;
            }
            currentGk = gk;
            groupItemWidth += th.offsetWidth;
        });
        if (itemIndex < liveGroupItems.length) {
            liveGroupItems[itemIndex].style.width = groupItemWidth + 'px';
            barTotalWidth += groupItemWidth;
        }
        headerBar.style.width = barTotalWidth + 'px';
    }

    // 防重复注册 scroll + observer（仅首次调用注册一次）
    if (!wrapper.dataset.woeScrollInited) {
        wrapper.dataset.woeScrollInited = 'true';

        // 同步水平滚动：使用 transform 定位组标题栏
        // 不用 scrollLeft 的原因是：
        //   1. overflow:visible 时 scrollLeft 无效（非 scroll container）
        //   2. 竖向滚动条会使表格视口变窄，scrollLeft 同步后标题会偏移
        //  transform 直接根据表格 scrollLeft 平移组标题栏，无视视口宽度差
        var tableContainer = wrapper.querySelector('.mud-table-container');
        if (tableContainer) {
            tableContainer.addEventListener('scroll', function () {
                headerBar.style.transform = 'translateX(-' + tableContainer.scrollLeft + 'px)';
            });
        }

        // 初始同步（requestAnimationFrame 确保布局已就绪）
        requestAnimationFrame(syncGroupWidths);

        // ResizeObserver 监听 <th> + 容器宽度变化 → 自动同步分组标题栏
        // 监听容器可以捕获滚动条出现/消失导致的布局偏移
        var ro = new ResizeObserver(function () {
            requestAnimationFrame(syncGroupWidths);
        });
        if (tableContainer) {
            ro.observe(tableContainer);
        }
        var thead = wrapper.querySelector('.mud-table-container thead');
        if (thead) {
            var headerRow = thead.querySelector('tr');
            if (headerRow) {
                headerRow.querySelectorAll('th').forEach(function (th) { ro.observe(th); });
            }
            // MutationObserver 监听 thead 增删列（列显隐/排序变化）
            var mo = new MutationObserver(function () {
                // 新列出现时重新注册 ResizeObserver
                headerRow.querySelectorAll('th').forEach(function (th) { ro.observe(th); });
                requestAnimationFrame(syncGroupWidths);
            });
            mo.observe(thead, { childList: true, subtree: true, attributes: true, attributeFilter: ['style', 'class'] });
            wrapper._groupMo = mo;
        }
        wrapper._groupRo = ro;
    } else {
        // 非首次调用：重新同步一次
        requestAnimationFrame(syncGroupWidths);
    }

    return true;
};

// 打印前同步分组标题栏：按 th 内联 width（col.Width，与打印 table-layout:fixed 列宽同基准）重算，
// 解决「屏幕列被拉伸（表格 width:100%）、打印表格压缩回 col.Width」导致的组标题与字段错位。
// 打印结束后（afterprint）自动恢复屏幕测量对齐。
window.syncGroupHeadersForPrint = function (tableSelector) {
    var wrapper = document.querySelector(tableSelector);
    if (!wrapper) return false;

    var headerScroll = wrapper.querySelector('.col-group-header-scroll');
    var headerBar = headerScroll ? headerScroll.querySelector('.col-group-header-bar') : null;
    if (!headerScroll || !headerBar) return false;

    var tableContainer = wrapper.querySelector('.mud-table-container');
    if (!tableContainer) return false;
    var headerRow = tableContainer.querySelector('thead tr');
    if (!headerRow) return false;
    var thCells = headerRow.querySelectorAll('th');
    if (thCells.length === 0) return false;

    var groupItems = headerBar.querySelectorAll('.col-group-header-item');
    if (groupItems.length === 0) return false;

    function getGroupKey(className) {
        var m = className.match(/\bcol-g(\d+)\b/);
        return m ? parseInt(m[1]) : 0;
    }
    // 优先解析 th 内联 width（col.Width，打印固定布局列宽来源）；解析失败回退 offsetWidth
    function colWidthPx(th) {
        if (th.style && th.style.width) {
            var v = parseFloat(th.style.width);
            if (!isNaN(v) && v > 0) return v;
        }
        return th.offsetWidth;
    }

    var itemIndex = 0;
    var barTotalWidth = 0;
    var groupItemWidth = 0;
    var currentGk = null;
    thCells.forEach(function (th) {
        var gk = getGroupKey(th.className);
        if (gk !== currentGk && currentGk !== null) {
            if (itemIndex < groupItems.length) {
                groupItems[itemIndex].style.width = groupItemWidth + 'px';
                barTotalWidth += groupItemWidth;
                itemIndex++;
            }
            groupItemWidth = 0;
        }
        currentGk = gk;
        groupItemWidth += colWidthPx(th);
    });
    if (itemIndex < groupItems.length) {
        groupItems[itemIndex].style.width = groupItemWidth + 'px';
        barTotalWidth += groupItemWidth;
    }
    headerBar.style.width = barTotalWidth + 'px';
    headerBar.style.transform = ''; // 打印不横向滚动

    // 打印结束恢复屏幕测量对齐（一次性监听）
    window.addEventListener('afterprint', function onAfter() {
        window.initGroupHeaders(tableSelector);
        window.removeEventListener('afterprint', onAfter);
    });

    return true;
};

// === 考勤表键盘导航（2026-09-01）===
// 定义在本文件而非独立 attendance-nav.js：本文件被所有页面必加载，若浏览器缓存了不含新脚本引用的旧 index.html，
// 独立脚本不会被加载导致 enableAttendanceKeyNav undefined（红屏）。聚焦全选用 focusin 事件委托，不随 Blazor 重渲染失效。
(function () {
    window.initAttendanceCellSelect = function () {
        var grid = document.querySelector('.attendance-grid');
        if (!grid || grid.__cellSelectInited) return true;
        grid.__cellSelectInited = true;
        grid.addEventListener('focusin', function (e) {
            var t = e.target;
            if (t && t.classList && t.classList.contains('attendance-cell-input')) {
                requestAnimationFrame(function () { t.select(); });
            }
        });
        return true;
    };

    window.enableAttendanceKeyNav = function () {
        window.initAttendanceCellSelect();
        if (window.enableTableArrowNav) {
            window.enableTableArrowNav('.attendance-scroll');
        }
        return true;
    };
})();
