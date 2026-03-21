(() => {
  if (!window.chrome || !window.chrome.webview) {
    console.warn('WebView bridge is unavailable.');
    return;
  }

  const pending = new Map();
  let seq = 1;

  window.chrome.webview.addEventListener('message', (event) => {
    const msg = event.data;
    if (!msg || msg.kind !== 'bridge-response' || !msg.id) return;
    const ticket = pending.get(msg.id);
    if (!ticket) return;
    pending.delete(msg.id);
    if (msg.ok) ticket.resolve(msg.result);
    else ticket.reject(new Error(msg.error || 'Native bridge error'));
  });

  const invoke = (method, args = {}) => new Promise((resolve, reject) => {
    const id = `req_${Date.now()}_${seq++}`;
    pending.set(id, { resolve, reject });
    window.chrome.webview.postMessage({ id, method, args });
  });

  const blobToBase64 = (blob) => new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error || new Error('Failed to read blob'));
    reader.onload = () => {
      const text = String(reader.result || '');
      const comma = text.indexOf(',');
      resolve(comma >= 0 ? text.slice(comma + 1) : text);
    };
    reader.readAsDataURL(blob);
  });

  const fileToBase64 = async (file) => blobToBase64(file);

  window.nativeHost = {
    invoke,
    getInfo: () => invoke('app.getInfo')
  };

  const localStore = {
    getItem: async (key) => (await invoke('localStore.getItem', { key }))?.value ?? null,
    setItem: async (key, value) => invoke('localStore.setItem', { key, value: String(value) }),
    removeItem: async (key) => invoke('localStore.removeItem', { key }),
    clear: async () => invoke('localStore.clear'),
    keys: async () => (await invoke('localStore.keys'))?.keys ?? []
  };

  const blobStore = {
    putBase64: async (key, base64, opts = {}) => invoke('blobStore.putBase64', {
      key,
      base64,
      fileName: opts.fileName ?? null,
      mimeType: opts.mimeType ?? 'application/octet-stream'
    }),
    putBlob: async (key, blob, opts = {}) => invoke('blobStore.putBase64', {
      key,
      base64: await blobToBase64(blob),
      fileName: opts.fileName ?? null,
      mimeType: (opts.mimeType ?? blob.type) || 'application/octet-stream'
    }),
    putFile: async (key, file, opts = {}) => invoke('blobStore.putBase64', {
      key,
      base64: await fileToBase64(file),
      fileName: opts.fileName ?? file.name,
      mimeType: (opts.mimeType ?? file.type) || 'application/octet-stream'
    }),
    getBase64: async (key) => invoke('blobStore.getBase64', { key }),
    remove: async (key) => invoke('blobStore.remove', { key }),
    list: async () => (await invoke('blobStore.list'))?.items ?? []
  };

  window.nativeFiles = {
    pickFile: (opts = {}) => invoke('dialog.pickFile', opts),
    pickFiles: (opts = {}) => invoke('dialog.pickFiles', opts),
    pickFolder: (opts = {}) => invoke('dialog.pickFolder', opts),
    saveBase64: (opts) => invoke('download.saveBase64', opts),
    saveText: (opts) => invoke('download.saveText', opts),
    saveJson: (opts) => invoke('download.saveJson', opts),
    saveBlob: async (blob, fileName = 'download.bin', opts = {}) => invoke('download.saveBase64', {
      fileName,
      base64: await blobToBase64(blob),
      defaultExtension: opts.defaultExtension,
      filter: opts.filter,
      mimeType: (opts.mimeType ?? blob.type) || 'application/octet-stream'
    })
  };

  window.nativeSettings = {
    get: (key, fallback = null) => invoke('settings.get', { key, fallback }),
    set: (key, value) => invoke('settings.set', { key, value })
  };

  window.nativeFs = {
    getContext: () => invoke('fs.getContext'),
    getRoots: () => invoke('fs.getRoots'),
    changeDirectory: (path, cwd = null) => invoke('fs.changeDirectory', { path, cwd }),
    list: (path = '.', cwd = null) => invoke('fs.list', { path, cwd }),
    stat: (path, cwd = null, allowMissing = false) => invoke('fs.stat', { path, cwd, allowMissing }),
    readText: (path, cwd = null) => invoke('fs.readText', { path, cwd }),
    writeText: (path, text, cwd = null) => invoke('fs.writeText', { path, text, cwd }),
    readBase64: (path, cwd = null) => invoke('fs.readBase64', { path, cwd }),
    writeBase64: (path, base64, cwd = null, mimeType = 'application/octet-stream') => invoke('fs.writeBase64', { path, base64, cwd, mimeType }),
    createDirectory: (path, cwd = null) => invoke('fs.createDirectory', { path, cwd }),
    createEmptyFile: (path, cwd = null) => invoke('fs.createEmptyFile', { path, cwd }),
    deletePath: (path, cwd = null, recursive = false) => invoke('fs.deletePath', { path, cwd, recursive }),
    copyPath: (sourcePath, destinationPath, cwd = null) => invoke('fs.copyPath', { sourcePath, destinationPath, cwd }),
    movePath: (sourcePath, destinationPath, cwd = null) => invoke('fs.movePath', { sourcePath, destinationPath, cwd }),
    find: (pattern, cwd = null) => invoke('fs.find', { pattern, cwd }),
    grep: (pattern, path, cwd = null) => invoke('fs.grep', { pattern, path, cwd }),
    directorySummary: (path = '.', cwd = null) => invoke('fs.directorySummary', { path, cwd }),
    largestFiles: (path = '.', cwd = null, limit = 12) => invoke('fs.largestFiles', { path, cwd, limit })
  };

  window.localStore = localStore;
  window.sqliteLocalStore = localStore;
  window.nativeBlobStore = blobStore;
  window.appDownloadBlob = window.nativeFiles.saveBlob;
})();
