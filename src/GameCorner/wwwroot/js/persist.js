window.hexiconStore = {
    get: (k) => {
        try { return localStorage.getItem(k); } catch { return null; }
    },
    set: (k, v) => {
        try { localStorage.setItem(k, v); return true; } catch { return false; }
    },
    remove: (k) => {
        try { localStorage.removeItem(k); } catch { }
    }
};