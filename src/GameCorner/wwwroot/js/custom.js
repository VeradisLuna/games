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

window.miniFit = (function () {
    const cfg = { cell: 56, min: 36, gap: 8, rows: 5 };
    const gridSel = ".mini-grid";
    const barSel = ".current-clue-bar";

    function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }

    function fit() {
        const vv = window.visualViewport;
        const grid = document.querySelector(gridSel);
        if (!vv || !grid) return;

        const rect = grid.getBoundingClientRect();

        // header (or top nav) to keep out of the way
        const header = document.querySelector(".mini-header");
        const headerBottom = header ? header.getBoundingClientRect().bottom : 0;

        // where the visible viewport ends (above the keyboard)
        const viewBottom = vv.height - vv.offsetTop;

        // account for the clue bar if present; otherwise assume ~2 line height
        const bar = document.querySelector(barSel);
        const barH = bar ? bar.getBoundingClientRect().height : 56;

        // little breathing room
        const pad = 12;

        const topLimit = Math.max(headerBottom + pad, rect.top);

        const keys = document.querySelector('.mini-keys');
        const keysH = (keys && keys.offsetParent !== null) ? keys.getBoundingClientRect().height : 0;

        const bottomLimit = viewBottom - barH - keysH - pad;

        const avail = bottomLimit - topLimit;
        if (avail <= 0) return;

        // rows * cell + gaps
        const rows = cfg.rows;
        const needed = rows + (rows - 1) * (cfg.gap / Math.max(1, avail)); // just to avoid NaN
        const size = Math.floor((avail - cfg.gap * (rows - 1)) / rows);

        const px = clamp(size, cfg.min, cfg.cell);
        document.documentElement.style.setProperty("--cell", px + "px");
    }

    function enable() {
        fit();
        window.addEventListener("resize", fit);
        if (window.visualViewport) {
            window.visualViewport.addEventListener("resize", fit);
            window.visualViewport.addEventListener("scroll", fit);
        }
        window.addEventListener("orientationchange", () => setTimeout(fit, 150));
    }

    function disable() {
        window.removeEventListener("resize", fit);
        if (window.visualViewport) {
            window.visualViewport.removeEventListener("resize", fit);
            window.visualViewport.removeEventListener("scroll", fit);
        }
        document.documentElement.style.removeProperty("--cell");
    }

    return { enable, disable, fit };
})();

window.miniClueBar = (function () {
    const SEL = '.current-clue-bar';
    const BASE = 8; // base bottom padding in px

    function setBottom(px) {
        const el = document.querySelector(SEL);
        if (!el) return;
        el.style.bottom = `calc(${px}px + env(safe-area-inset-bottom))`;
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
        if (!k) return console.log("[miniLayout] update: no .mini-keys");
        const rect = k.getBoundingClientRect();
        const vis = k.offsetParent !== null;
        const h = (k && visible(k)) ? rect.height : 0;
        console.log("[miniLayout] update rect.height=", rect.height, "visible?", vis);
        document.documentElement.style.setProperty('--keys-height', (h | 0) + 'px');
        console.log("[miniLayout] set --keys-height =", h);
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