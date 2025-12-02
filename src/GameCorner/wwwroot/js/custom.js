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

window.letterheadFit = (function () {
    const SEL_GRID = '.lh-grid';
    const SEL_BOARD = '.lh-board';
    const SEL_KEYS = '.mini-keys';
    const MAX_CELL = 64;
    const MIN_CELL = 36;
    const PAD = 12;

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

    function getGapPx() {
        const val = getComputedStyle(document.documentElement)
            .getPropertyValue('--lh-gap').trim();
        const n = parseInt(val, 10);
        return Number.isFinite(n) ? n : 8;
    }

    function fit() {
        const grid = document.querySelector(SEL_GRID);
        const board = document.querySelector(SEL_BOARD);
        if (!grid || !board) return;

        const vv = window.visualViewport;
        const vw = vv ? vv.width : window.innerWidth;
        const boardRect = board.getBoundingClientRect();

        // width: content width of the board's parent
        const container = board.parentElement;
        //const usableW = (container?.clientWidth ?? board.clientWidth);
        const usableW = Math.min(vw, (container?.clientWidth ?? vw));

        // height: from board top to just above on-screen keys
        const viewBottom = vv ? (vv.height - vv.offsetTop) : window.innerHeight;
        const keys = document.querySelector(SEL_KEYS);
        const keysH = isShown(keys) ? keys.getBoundingClientRect().height : 0;
        const bottomLimit = viewBottom - keysH - PAD;
        const usableH = bottomLimit - boardRect.top;

        if (usableW <= 0 || usableH <= 0) return;

        // Letterhead is 6 rows × 5 columns
        const ROWS = 6, COLS = 5;
        const gap = getGapPx();
        const gapsW = (COLS - 1) * gap;
        const gapsH = (ROWS - 1) * gap;

        const cellByW = Math.floor((usableW - gapsW) / COLS);
        const cellByH = Math.floor((usableH - gapsH) / ROWS);
        const chosen = clamp(Math.min(cellByW, cellByH), MIN_CELL, MAX_CELL);

        document.documentElement.style.setProperty('--lh-cell', chosen + 'px');
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
            const roTargets = [SEL_KEYS].map(sel => document.querySelector(sel)).filter(Boolean);
            if (roTargets.length) {
                const ro = new ResizeObserver(fit);
                roTargets.forEach(t => ro.observe(t));
                window._letterheadFitRO = ro;
            }
        });
    }

    function disable() {
        window.removeEventListener('resize', fit);
        if (window.visualViewport) {
            window.visualViewport.removeEventListener('resize', fit);
            window.visualViewport.removeEventListener('scroll', fit);
        }
        if (window._letterheadFitRO) { window._letterheadFitRO.disconnect(); window._letterheadFitRO = null; }
        document.documentElement.style.removeProperty('--lh-cell');
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

(function () {
    'use strict';

    let snowRunning = false;
    let snowStopFn = null;

    // Respect reduced motion
    function canCelebrate() {
        return !window.matchMedia || !matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    // Random helpers
    const R = Math.random, cos = Math.cos, sin = Math.sin, PI = Math.PI, PI2 = PI * 2;

    // Color themes (unchanged)
    const colorThemes = [
        () => rgb(200 * R() | 0, 200 * R() | 0, 200 * R() | 0),
        () => { const k = 200 * R() | 0; return rgb(200, k, k); },
        () => { const k = 200 * R() | 0; return rgb(k, 200, k); },
        () => { const k = 200 * R() | 0; return rgb(k, k, 200); },
        () => rgb(200, 100, 200 * R() | 0),
        () => rgb(200 * R() | 0, 200, 200),
        () => { const k = 256 * R() | 0; return rgb(k, k, k); },
        () => (R() < .5 ? colorThemes[1] : colorThemes[2])(),
        () => (R() < .5 ? colorThemes[3] : colorThemes[5])(),
        () => (R() < .5 ? colorThemes[2] : colorThemes[4])(),
    ];
    function rgb(r, g, b) { return `rgb(${r},${g},${b})`; }

    // Cosine interpolation
    function interp(a, b, t) {
        return (1 - cos(PI * t)) / 2 * (b - a) + a;
    }

    // 1D Poisson disc over [0,1]
    function createPoisson(eccentricity) {
        const radius = 1 / eccentricity, r2 = radius * 2;
        let domain = [radius, 1 - radius];
        let measure = 1 - r2;
        const spline = [0, 1];
        while (measure) {
            let dart = measure * R(), i, l, a, b, c, d, interval;
            for (i = 0, l = domain.length, measure = 0; i < l; i += 2) {
                a = domain[i]; b = domain[i + 1]; interval = b - a;
                if (dart < measure + interval) { spline.push(dart += a - measure); break; }
                measure += interval;
            }
            c = dart - radius; d = dart + radius;

            for (i = domain.length - 1; i > 0; i -= 2) {
                l = i - 1; a = domain[l]; b = domain[i];
                if (a >= c && a < d) {
                    if (b > d) domain[l] = d; else domain.splice(l, 2);
                } else if (a < c && b > c) {
                    if (b <= d) domain[i] = c; else domain.splice(i, 0, c, d);
                }
            }
            for (i = 0, l = domain.length, measure = 0; i < l; i += 2) {
                measure += domain[i + 1] - domain[i];
            }
        }
        return spline.sort();
    }

    function Confetto(theme, opts) {
        const {
            sizeMin, sizeMax, dxThetaMin, dxThetaMax,
            dyMin, dyMax, dThetaMin, dThetaMax,
            deviation, eccentricity
        } = opts;

        const isSnow = opts.kind === 'snow';

        this.frame = 0;
        this.outer = document.createElement('div');
        this.inner = document.createElement('div');
        this.outer.appendChild(this.inner);

        const o = this.outer.style, i = this.inner.style;
        o.position = 'absolute';

        const size = (sizeMin + sizeMax * R());
        o.width = size + 'px';
        o.height = size + 'px';
        i.width = '100%';
        i.height = '100%';

        if (isSnow) {
            i.backgroundColor = 'white';
            i.borderRadius = '50%';
            o.opacity = 0.7 + 0.3 * R();
            o.boxShadow = '0 0 6px rgba(255,255,255,0.8)';
        } else {
            i.backgroundColor = theme();
        }

        if (isSnow) {
            // Simple 2D-ish rotation
            this.axis = 'rotate3D(0,0,1,';
            this.theta = 360 * R();
            this.dTheta = (dThetaMin != null ? dThetaMin : 0.01) + (dThetaMax != null ? dThetaMax : 0.02) * R();
            i.transform = this.axis + this.theta + 'deg)';
        } else {
            // Original 3D-style rotation
            o.perspective = '50px';
            o.transform = 'rotate(' + (360 * R()) + 'deg)';
            this.axis = 'rotate3D(' + cos(360 * R()) + ',' + cos(360 * R()) + ',0,';
            this.theta = 360 * R();
            this.dTheta = dThetaMin + dThetaMax * R();
            i.transform = this.axis + this.theta + 'deg)';
        }

        this.x = window.innerWidth * R();
        this.y = -deviation;
        this.dx = sin(dxThetaMin + dxThetaMax * R());
        this.dy = dyMin + dyMax * R();
        o.left = this.x + 'px';
        o.top = this.y + 'px';

        this.splineX = createPoisson(eccentricity);
        this.splineY = [];
        for (let k = 1, l = this.splineX.length - 1; k < l; ++k) {
            this.splineY[k] = deviation * R();
        }
        this.splineY[0] = this.splineY[this.splineX.length - 1] = deviation * R();

        // Snow sway seed
        this.seed = R() * PI2;

        this.update = (height, delta) => {
            this.frame += delta;
            this.x += this.dx * delta;
            this.y += this.dy * delta;
            this.theta += this.dTheta * delta;

            if (isSnow) {
                // Gentle horizontal sway for snow
                const t = this.frame * 0.0005;
                const sway = deviation * 0.5 * sin(t + this.seed);
                o.left = (this.x + sway) + 'px';
                o.top = this.y + 'px';
                i.transform = this.axis + this.theta + 'deg)';
            } else {
                // Original confetti path
                const sx = this.splineX, sy = this.splineY;
                let phi = this.frame % 7777 / 7777, a = 0, b = 1;
                while (phi >= sx[b]) a = b++;
                const rho = interp(sy[a], sy[b], (phi - sx[a]) / (sx[b] - sx[a]));
                phi *= PI2;

                o.left = this.x + rho * cos(phi) + 'px';
                o.top = this.y + rho * sin(phi) + 'px';
                i.transform = this.axis + this.theta + 'deg)';
            }

            // Remove when far below viewport
            return this.y > height + deviation;
        };
    }

    // Confetti defaults (kept as-is)
    function makeDefaults(over) {
        return Object.assign({
            particles: 80,
            spread: 40,
            sizeMin: 3,
            sizeMax: 9,          // original scalar
            eccentricity: 10,
            deviation: 100,
            dxThetaMin: -.1,
            dxThetaMax: .2,
            dyMin: .13,
            dyMax: .18,
            dThetaMin: .4,
            dThetaMax: .3,
            zIndex: 9999,
            themeIndex: 0
        }, over || {});
    }

    // Snow defaults (new)
    function makeSnowDefaults(over) {
        return Object.assign({
            kind: 'snow',
            particles: 200,     // max flakes on screen
            spread: 200,        // ms range between new flakes
            sizeMin: 2,
            sizeMax: 5,
            eccentricity: 5,
            deviation: 40,
            dxThetaMin: -.05,
            dxThetaMax: .05,
            dyMin: .03,
            dyMax: .06,
            dThetaMin: 0.01,
            dThetaMax: 0.02,
            zIndex: 9999
        }, over || {});
    }

    // Original "burst" confetti
    function poof(userOpts) {
        if (!canCelebrate()) return;

        const opts = makeDefaults(userOpts);
        const theme = colorThemes[opts.themeIndex % colorThemes.length];

        // Container
        const container = document.createElement('div');
        const cs = container.style;
        cs.position = 'fixed';
        cs.top = '0'; cs.left = '0';
        cs.width = '100%'; cs.height = '0';
        cs.overflow = 'visible';
        cs.zIndex = String(opts.zIndex);
        cs.pointerEvents = 'none';

        document.body.appendChild(container);

        // Build confetti gradually for a nicer wave
        const confetti = [];
        let timer = null, frame = null;

        (function addConfetto() {
            const c = new Confetto(theme, opts);
            confetti.push(c);
            container.appendChild(c.outer);
            if (confetti.length < opts.particles) {
                timer = setTimeout(addConfetto, opts.spread * R());
            } else {
                timer = null;
            }
        })();

        // Animate
        let prev;
        function loop(ts) {
            const delta = prev ? ts - prev : 0; prev = ts;
            const height = window.innerHeight;

            for (let i = confetti.length - 1; i >= 0; --i) {
                if (confetti[i].update(height, delta)) {
                    container.removeChild(confetti[i].outer);
                    confetti.splice(i, 1);
                }
            }

            if (timer || confetti.length) {
                frame = requestAnimationFrame(loop);
            } else {
                cancelAnimationFrame(frame);
                document.body.removeChild(container);
            }
        }
        frame = requestAnimationFrame(loop);
    }

    // Continuous snowfall
    function snow(userOpts) {
        if (!canCelebrate()) return;

        // Prevent multiple snow instances
        if (snowRunning) {
            return;
        }
        snowRunning = true;

        const opts = makeSnowDefaults(userOpts || {});
        opts.kind = 'snow'; // force kind

        const container = document.createElement('div');
        const cs = container.style;
        cs.position = 'fixed';
        cs.top = '0'; cs.left = '0';
        cs.width = '100%'; cs.height = '0';
        cs.overflow = 'visible';
        cs.zIndex = String(opts.zIndex);
        cs.pointerEvents = 'none';
        document.body.appendChild(container);

        const confetti = [];
        let frame = null;
        let running = true;

        function addConfetto() {
            if (!running) return;
            if (confetti.length < opts.particles) {
                const c = new Confetto(() => 'white', opts);
                confetti.push(c);
                container.appendChild(c.outer);
            }
            setTimeout(addConfetto, opts.spread * R());
        }
        addConfetto();

        let prev;
        function loop(ts) {
            const delta = prev ? ts - prev : 0; prev = ts;
            const height = window.innerHeight;

            for (let i = confetti.length - 1; i >= 0; --i) {
                if (confetti[i].update(height, delta)) {
                    container.removeChild(confetti[i].outer);
                    confetti.splice(i, 1);
                }
            }

            if (running) {
                frame = requestAnimationFrame(loop);
            } else {
                cancelAnimationFrame(frame);
                document.body.removeChild(container);
            }
        }
        frame = requestAnimationFrame(loop);

        function stop() {
            running = false;
            snowRunning = false;
        }

        snowStopFn = stop;

        return {
            stop
        };
    }

    // Public API
    window.miniConfetti = {
        poof,
        snow,
        stopSnow: function () {
            if (typeof snowStopFn === 'function') {
                snowStopFn();
            }
        }
    };
})();

window.setWinterTheme = function (enabled) {
    document.body.classList.toggle("winter-theme", enabled === true);
};

// Letterhead: flip and shake row animations
window.letterhead = (function () {
    function revealRow(r) {
        const row = document.getElementById(`row-${r}`);
        if (!row) return; // defensive
        const cells = row.querySelectorAll(".lh-cell");
        cells.forEach((el, i) => {
            // staggered flip
            el.style.animation = `lh-flip 420ms ease ${i * 80}ms both`;
        });
    }

    function shakeRow(r) {
        const row = document.getElementById(`row-${r}`);
        if (!row) return;
        row.classList.remove("shake"); // restart if already applied
        void row.offsetWidth;          // reflow
        row.classList.add("shake");
        setTimeout(() => row.classList.remove("shake"), 260);
    }

    return { revealRow, shakeRow };
})();

window.sharePuzzle = {
    copy: async function (text) {
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
                return true;
            } else {
                // Fallback
                const textarea = document.createElement("textarea");
                textarea.value = text;
                textarea.style.position = "fixed";
                textarea.style.left = "-9999px";
                document.body.appendChild(textarea);
                textarea.focus();
                textarea.select();
                const ok = document.execCommand("copy");
                document.body.removeChild(textarea);
                return ok;
            }
        } catch (e) {
            console.error("copy failed", e);
            return false;
        }
    },

    share: async function (text, title) {
        if (!navigator.share) {
            return false; // signal to Blazor to fall back to copy
        }

        try {
            await navigator.share({
                title: title,
                text: text
            });
            return true;
        } catch (e) {
            console.error("share failed", e);
            return false;
        }
    }
};