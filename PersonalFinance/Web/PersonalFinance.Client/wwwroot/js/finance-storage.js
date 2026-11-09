window.financeStorage = {
  get: key => localStorage.getItem(key),
  set: (key, value) => localStorage.setItem(key, value),
  remove: key => localStorage.removeItem(key),
  subject(token) {
    try {
      const body = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const claims = JSON.parse(atob(body));
      return claims.sub || claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || '';
    } catch { return ''; }
  }
};

const financeQueueDatabase = new Promise((resolve, reject) => {
  const request = indexedDB.open('personal-finance-commands', 2);
  request.onupgradeneeded = () => {
    if (!request.result.objectStoreNames.contains('commands')) request.result.createObjectStore('commands', { keyPath: 'id' });
    if (!request.result.objectStoreNames.contains('leases')) request.result.createObjectStore('leases', { keyPath: 'key' });
    if (!request.result.objectStoreNames.contains('meta')) request.result.createObjectStore('meta', { keyPath: 'key' });
  };
  request.onsuccess = () => resolve(request.result);
  request.onerror = () => reject(request.error);
});

async function financeTransaction(storeName, mode, action) {
  const db = await financeQueueDatabase;
  return new Promise((resolve, reject) => {
    const transaction = db.transaction(storeName, mode);
    const store = transaction.objectStore(storeName);
    let result;
    action(store, value => { result = value; });
    transaction.oncomplete = () => resolve(result);
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error);
  });
}

window.financeQueue = {
  online: () => navigator.onLine,
  active: () => navigator.onLine && !document.hidden,
  legacyCount() { try { return JSON.parse(localStorage.getItem('finance-pending') || '[]').length; } catch { return 0; } },
  async add(owner, command) {
    const db = await financeQueueDatabase;
    await new Promise((resolve, reject) => {
      const transaction = db.transaction(['commands', 'meta'], 'readwrite');
      const sequence = transaction.objectStore('meta');
      sequence.get('next').onsuccess = event => {
        const next = (event.target.result?.value || Date.now() * 1000) + 1;
        sequence.put({ key: 'next', value: next });
        transaction.objectStore('commands').add({
          id: command.operationId, owner, command, sequence: next,
          created: Date.now(), status: 'queued', error: null, result: null
        });
      };
      transaction.oncomplete = resolve;
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    });
  },
  async list(owner) {
    const all = await financeTransaction('commands', 'readonly', (store, set) => {
      store.getAll().onsuccess = event => set(event.target.result);
    });
    return all.filter(x => x.owner === owner && x.status !== 'applied')
      .sort((a, b) => (a.sequence ?? a.created) - (b.sequence ?? b.created)).map(x => JSON.stringify(x));
  },
  async ready(owner) {
    const all = await financeTransaction('commands', 'readonly', (store, set) => {
      store.getAll().onsuccess = event => set(event.target.result);
    });
    return all.filter(x => x.owner === owner && x.status === 'queued')
      .sort((a, b) => (a.sequence ?? a.created) - (b.sequence ?? b.created)).map(x => JSON.stringify(x));
  },
  async get(owner, id) {
    const item = await financeTransaction('commands', 'readonly', (store, set) => {
      store.get(id).onsuccess = event => set(event.target.result);
    });
    return item?.owner === owner ? JSON.stringify(item) : null;
  },
  async mark(owner, id, status, error, result) {
    await financeTransaction('commands', 'readwrite', store => {
      const request = store.get(id);
      request.onsuccess = () => {
        const item = request.result;
        if (!item || item.owner !== owner) return;
        item.status = status; item.error = error; item.result = result;
        store.put(item);
      };
    });
  },
  async remove(owner, id) {
    await financeTransaction('commands', 'readwrite', store => {
      const request = store.get(id);
      request.onsuccess = () => { if (request.result?.owner === owner) store.delete(id); };
    });
  },
  async rebase(owner, id, revisions) {
    const map = new Map(Object.entries(revisions || {}).map(([key, value]) => [key.toLowerCase(), value]));
    if (!map.size) return;
    await financeTransaction('commands', 'readwrite', store => {
      store.get(id).onsuccess = event => {
        const current = event.target.result;
        if (current?.owner !== owner) return;
        store.getAll().onsuccess = all => {
          for (const item of all.target.result) {
            if (item.owner !== owner || item.status !== 'queued' || item.id === id ||
                (item.sequence ?? item.created) <= (current.sequence ?? current.created)) continue;
            const revision = map.get((item.command.clientId || '').toLowerCase());
            if (revision === undefined) continue;
            item.command.expectedRevision = revision;
            store.put(item);
          }
        };
      };
    });
  },
  async acquire(owner, holder) {
    return await financeTransaction('leases', 'readwrite', (store, set) => {
      const request = store.get(owner);
      request.onsuccess = () => {
        const lease = request.result;
        if (lease && lease.expires > Date.now() && lease.holder !== holder) { set(false); return; }
        store.put({ key: owner, holder, expires: Date.now() + 180000 }); set(true);
      };
    });
  },
  async release(owner, holder) {
    await financeTransaction('leases', 'readwrite', store => {
      const request = store.get(owner);
      request.onsuccess = () => { if (request.result?.holder === holder) store.delete(owner); };
    });
  },
  async renew(owner, holder) {
    return await financeTransaction('leases', 'readwrite', (store, set) => {
      const request = store.get(owner);
      request.onsuccess = () => {
        if (request.result?.holder !== holder) { set(false); return; }
        store.put({ key: owner, holder, expires: Date.now() + 180000 }); set(true);
      };
    });
  },
  watch(reference) {
    this.unwatch();
    this.listener = () => { if (this.active()) reference.invokeMethodAsync('ResumeAsync').catch(() => {}); };
    document.addEventListener('visibilitychange', this.listener);
    window.addEventListener('online', this.listener);
  },
  unwatch() {
    if (!this.listener) return;
    document.removeEventListener('visibilitychange', this.listener);
    window.removeEventListener('online', this.listener);
    this.listener = null;
  }
};

window.financeBackup = {
  async download(apiBaseUrl, token) {
    const response = await fetch(new URL('api/backup', apiBaseUrl), { headers: { Authorization: 'Bearer ' + token } });
    if (!response.ok) throw new Error('Falha ao exportar');
    const url = URL.createObjectURL(await response.blob());
    const a = document.createElement('a'); a.href = url; a.download = 'financeiro.db'; a.click();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
  }
};
