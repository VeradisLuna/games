window.setInputValue = (el, val) => { if (el) el.value = val; };

window.lunaFocus = {
    focus: function (id) {
        const el = document.getElementById(id);
        if (el) {
            el.focus();
            // optional: keep caret sane and avoid scroll jumps
            el.setSelectionRange?.(1, 1);
            el.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
        }
    }
};

window.mini = {
    selectAll(el) {
        if (!el) return;
        // Select the single character so the next keystroke replaces it
        if (typeof el.select === "function") el.select();
        if (typeof el.setSelectionRange === "function") el.setSelectionRange(0, 1);
    }
};

window.miniTabs = {
    enable: function (id, dotNetRef) {
        const container = document.getElementById(id);
        if (!container) return;

        const handler = (e) => {
            if (e.key === 'Tab') {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnTab', !!e.shiftKey);
            }
        };
        container._miniTabsHandler = handler;
        container.addEventListener('keydown', handler, true);
    },
    disable: function (id) {
        const container = document.getElementById(id);
        if (container && container._miniTabsHandler) {
            container.removeEventListener('keydown', container._miniTabsHandler, true);
            delete container._miniTabsHandler;
        }
    }
};

window.miniDevice = (function () {
    function isMobileLike() {
        const vv = window.visualViewport;
        const width = vv ? vv.width : window.innerWidth;

        const touch = ('ontouchstart' in window) || (navigator.maxTouchPoints > 0);
        const ua = navigator.userAgent || "";
        const uaMobile = /Android|iPhone|iPad|iPod|Mobile/i.test(ua);

        const smallViewport = width < 768;

        return smallViewport && (touch || uaMobile);
    }

    function subscribe(dotNetRef) {
        const fire = () => dotNetRef.invokeMethodAsync('SetOnscreenKeys', isMobileLike());
        window.addEventListener('resize', fire);
        window.addEventListener('orientationchange', fire);
        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', fire);
            window.visualViewport.addEventListener('scroll', fire);
        }
        fire();
        window._miniDeviceHandlers = { fire };
    }

    function unsubscribe() {
        const h = window._miniDeviceHandlers;
        if (!h) return;
        window.removeEventListener('resize', h.fire);
        window.removeEventListener('orientationchange', h.fire);
        if (window.visualViewport) {
            window.visualViewport.removeEventListener('resize', h.fire);
            window.visualViewport.removeEventListener('scroll', h.fire);
        }
        delete window._miniDeviceHandlers;
    }

    return { isMobileLike, subscribe, unsubscribe };
})();

// Compute the biggest cell size that fits width AND height constraints.
// Assumes:
//   - grid element:  .mini-grid  (inside .mini-board)
//   - clue bar:      .current-clue-bar (fixed on mobile)
//   - custom keys:   .mini-keys (fixed on mobile)
//   - CSS var --keys-height is optional; we also measure live heights.
window.miniFit = (function () {
    const SEL_GRID = '.mini-grid';
    const SEL_BOARD = '.mini-board';
    const SEL_BAR = '.current-clue-bar';
    const SEL_KEYS = '.mini-keys';

    // tune these two to taste
    const MAX_CELL = 64;   // biggest cell you’ll allow
    const MIN_CELL = 36;   // smallest cell you’ll allow
    const PAD = 12;        // extra breathing room

    function elementsReady() {
        return document.querySelector(SEL_GRID) && document.querySelector(SEL_BOARD);
    }

    function runWhenReady(fn) {
        if (elementsReady()) { fn(); return; }
        const mo = new MutationObserver(() => {
            if (elementsReady()) { mo.disconnect(); fn(); }
        });
        mo.observe(document.body, { childList: true, subtree: true });
    }

    function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }
    function isShown(el) { return !!el && el.getClientRects().length > 0; }

    function fit() {
        console.log("fitting grid!");
        const grid = document.querySelector(SEL_GRID);
        const board = document.querySelector(SEL_BOARD);
        if (!grid || !board) return;

        console.log("still fitting grid!");
        const vv = window.visualViewport;
        const rectGrid = board.getBoundingClientRect(); // use the board to get top

        // WIDTH: container width (board’s parent content box)
        const container = board.parentElement; // board is usually inside the left col
        const usableW = (container?.clientWidth ?? board.clientWidth);

        // HEIGHT: from grid top to just above bar/keyboard (visual viewport aware)
        const viewBottom = vv ? (vv.height - vv.offsetTop) : window.innerHeight;

        const bar = document.querySelector(SEL_BAR);
        const keys = document.querySelector(SEL_KEYS);
        const barH = isShown(bar) ? bar.getBoundingClientRect().height : 0;
        const keysH = isShown(keys) ? keys.getBoundingClientRect().height : 0;

        const bottomLimit = viewBottom - barH - keysH - PAD; // where we must stay above
        const usableH = bottomLimit - rectGrid.top;

        console.log("usable width - " + usableW + ", usable height - " + usableH);

        if (usableW <= 0 || usableH <= 0) return;

        // 5x5 board → 5 cells + 4 gaps vertically/horizontally
        const ROWS = 5, COLS = 5;
        const gapsW = (COLS - 1) * getGapPx();
        const gapsH = (ROWS - 1) * getGapPx();

        const cellByW = Math.floor((usableW - gapsW) / COLS);
        const cellByH = Math.floor((usableH - gapsH) / ROWS);

        const chosen = clamp(Math.min(cellByW, cellByH), MIN_CELL, MAX_CELL);

        console.log("chosen = " + chosen + ", cellByW = " + cellByW + ", cellByH = " + cellByH);

        document.documentElement.style.setProperty('--cell', chosen + 'px');
    }

    function getGapPx() {
        const val = getComputedStyle(document.documentElement).getPropertyValue('--gap').trim();
        const n = parseInt(val, 10);
        return Number.isFinite(n) ? n : 8;
    }

    function enable() {
        runWhenReady(() => {
            fit();

            window.addEventListener('resize', fit, { passive: true });
            window.addEventListener('orientationchange', () => setTimeout(fit, 150), { passive: true });
            if (window.visualViewport) {
                window.visualViewport.addEventListener('resize', fit);
                window.visualViewport.addEventListener('scroll', fit);
            }
            // react when bar/keys heights change dynamically
            const roTargets = [SEL_BAR, SEL_KEYS].map(sel => document.querySelector(sel)).filter(Boolean);
            if (roTargets.length) {
                const ro = new ResizeObserver(fit);
                roTargets.forEach(t => ro.observe(t));
                // stash for disable
                window._miniFitRO = ro;
            }
        });
    }

    function disable() {
        window.removeEventListener('resize', fit);
        if (window.visualViewport) {
            window.visualViewport.removeEventListener('resize', fit);
            window.visualViewport.removeEventListener('scroll', fit);
        }
        if (window._miniFitRO) { window._miniFitRO.disconnect(); window._miniFitRO = null; }
        document.documentElement.style.removeProperty('--cell');
    }

    return { enable, fit, disable };
})();

window.miniClueBar = (function () {
    const SEL = '.current-clue-bar';
    const BASE = 8; // base bottom padding in px

    function setBottom(px) {
        const el = document.querySelector(SEL);
        if (!el) return;
        el.style.bottom = `calc(var(--keys-height) + ${px}px + env(safe-area-inset-bottom))`;
    }

    function update() {
        const vv = window.visualViewport;
        if (!vv) return;

        // keyboard overlap = layout viewport bottom - (visual viewport bottom)
        const overlap = window.innerHeight - (vv.height + vv.offsetTop);
        const bump = Math.max(BASE, BASE + overlap); // never less than BASE
        setBottom(bump);
    }

    function enable() {
        update();
        window.addEventListener('resize', update);
        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', update);
            window.visualViewport.addEventListener('scroll', update);
        }
        window.addEventListener('orientationchange', () => setTimeout(update, 150));
    }

    function disable() {
        window.removeEventListener('resize', update);
        if (window.visualViewport) {
            window.visualViewport.removeEventListener('resize', update);
            window.visualViewport.removeEventListener('scroll', update);
        }
    }

    return { enable, disable, update };
})();

window.miniLayout = (function () {
    const KEY_SEL = '.mini-keys';

    let ro, activeEl;

    function visible(el) { return !!(el && el.offsetParent !== null); }

    function update() {
        const k = document.querySelector(KEY_SEL);
        if (!k) {
            console.log('[miniLayout] update: no .mini-keys');
            return;
        }

        const rect = k.getBoundingClientRect();
        const style = window.getComputedStyle(k);
        const hidden = (style.display === 'none') || (style.visibility === 'hidden');
        const h = hidden ? 0 : Math.max(0, Math.round(rect.height));

        document.documentElement.style.setProperty('--keys-height', h + 'px');
        console.log('[miniLayout] set --keys-height =', h, '(rect.height=', rect.height, ')');
    }

    function observe() {
        const k = document.querySelector(KEY_SEL);
        console.log("[miniLayout] observe found:", k);
        if (!k) return;
        if (ro) ro.disconnect();
        ro = new ResizeObserver(update);
        ro.observe(k);
        activeEl = k;
    }

    function enable() {
        update();
        observe();
        window.addEventListener('resize', update);
        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', update);
            window.visualViewport.addEventListener('scroll', update);
        }
        window.addEventListener('orientationchange', () => setTimeout(update, 150));
    }

    function refresh() {
        console.log("we hit refresh!");
        update();
        observe();
    }

    function disable() {
        window.removeEventListener('resize', update);
        if (window.visualViewport) {
            window.visualViewport.removeEventListener('resize', update);
            window.visualViewport.removeEventListener('scroll', update);
        }
        if (ro && activeEl) ro.disconnect();
        document.documentElement.style.removeProperty('--keys-height');
    }

    return { enable, refresh, disable };
})();