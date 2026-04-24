window.idbInterop = (() => {
    const DB_NAME = 'alidade-db';
    const DB_VERSION = 1;
    let _db = null;

    function open() {
        if (_db) return Promise.resolve(_db);
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, DB_VERSION);
            req.onupgradeneeded = e => {
                const db = e.target.result;
                if (!db.objectStoreNames.contains('keyvalue')) {
                    db.createObjectStore('keyvalue');
                }
                if (!db.objectStoreNames.contains('tiles')) {
                    const ts = db.createObjectStore('tiles');
                    ts.createIndex('timestamp', 'timestamp');
                }
            };
            req.onsuccess = e => { _db = e.target.result; resolve(_db); };
            req.onerror = e => reject(e.target.error);
        });
    }

    function tx(store, mode, fn) {
        return open().then(db => new Promise((resolve, reject) => {
            const t = db.transaction(store, mode);
            const s = t.objectStore(store);
            const req = fn(s);
            req.onsuccess = e => resolve(e.target.result);
            req.onerror = e => reject(e.target.error);
        }));
    }

    return {
        get: (key) => tx('keyvalue', 'readonly', s => s.get(key)),
        set: (key, value) => tx('keyvalue', 'readwrite', s => s.put(value, key)),
        delete: (key) => tx('keyvalue', 'readwrite', s => s.delete(key)),

        getTile: (key) => tx('tiles', 'readonly', s => s.get(key)).then(r => r ? r.data : null),
        setTile: (key, data) => tx('tiles', 'readwrite', s => s.put({ data, timestamp: Date.now() }, key)),

        // Delete tile entries older than maxAgeMs
        evictOldTiles: (maxAgeMs) => open().then(db => new Promise((resolve, reject) => {
            const cutoff = Date.now() - maxAgeMs;
            const t = db.transaction('tiles', 'readwrite');
            const idx = t.objectStore('tiles').index('timestamp');
            const range = IDBKeyRange.upperBound(cutoff);
            const req = idx.openCursor(range);
            req.onsuccess = e => {
                const cursor = e.target.result;
                if (cursor) { cursor.delete(); cursor.continue(); }
                else resolve();
            };
            req.onerror = e => reject(e.target.error);
        }))
    };
})();
