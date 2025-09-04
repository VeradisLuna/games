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