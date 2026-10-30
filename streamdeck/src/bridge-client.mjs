import net from 'node:net';
import fs from 'node:fs/promises';
import path from 'node:path';
import { performance } from 'node:perf_hooks';

export const discoveryDirectory = () => process.env.DAYZ_MONITOR_DISCOVERY_DIR || path.join(process.env.LOCALAPPDATA ?? '', 'DayzServerMonitor', 'bridges');
export function validDiscovery(value) {
  return value?.protocolVersion === 1 && /^DayzServerMonitor-[A-F0-9]{20}$/.test(value.pipeName)
    && typeof value.dataRoot === 'string' && path.isAbsolute(value.dataRoot);
}
export function validSnapshot(value) {
  return value?.protocolVersion === 1 && Number.isFinite(Date.parse(value.now)) && Date.parse(value.now) > Date.UTC(2020, 0, 1)
    && typeof value.serverName === 'string' && value.serverName.length <= 180
    && Array.isArray(value.history) && value.history.length <= 120;
}

export class BridgeClient {
  constructor(dataDirectory = '', directory = discoveryDirectory(), endpoint = '') {
    this.dataDirectory = dataDirectory; this.directory = directory;
    this.endpoint = endpoint; this.monitors = [];
    this.socket = null; this.pending = null; this.buffer = ''; this.state = null;
    this.receivedAt = 0; this.progressAt = 0; this.tail = Promise.resolve(); this.stopped = false;
    this.connectionIssue = null;
  }
  snapshot() { return this.state && performance.now() - this.receivedAt < 3500 && performance.now() - this.progressAt < 3500 ? this.state : null; }
  async connect() {
    if (this.stopped) throw new Error('stopped');
    if (this.socket && !this.socket.destroyed) return;
    let names;
    try { names = await fs.readdir(this.directory); }
    catch (error) { this.connectionIssue = ['EPERM', 'EACCES'].includes(error.code) ? 'access-denied' : 'monitor-off'; throw error; }
    const candidates = [];
    for (const name of names.filter(n => /^[A-F0-9]{20}\.json$/.test(n))) {
      try {
        const file = path.join(this.directory, name), stat = await fs.stat(file);
        if (stat.size > 4096) continue;
        const value = JSON.parse(await fs.readFile(file, 'utf8'));
        if (validDiscovery(value) && (!this.dataDirectory || path.resolve(value.dataRoot).toUpperCase() === path.resolve(this.dataDirectory).toUpperCase()))
          candidates.push({ ...value, modified: stat.mtimeMs });
      } catch { /* In-progress or stale discovery file. */ }
    }
    let denied = false;
    for (const candidate of candidates.sort((a, b) => b.modified - a.modified).slice(0, 8)) {
      if (this.stopped) throw new Error('stopped');
      try {
        const socket = await new Promise((resolve, reject) => {
          const socket = net.createConnection('\\\\.\\pipe\\' + candidate.pipeName);
          const timeout = setTimeout(() => { socket.destroy(); reject(new Error('connect timeout')); }, 500);
          socket.once('error', error => { clearTimeout(timeout); reject(error); });
          socket.once('connect', () => { clearTimeout(timeout); resolve(socket); });
        });
        if (this.stopped) { socket.destroy(); throw new Error('stopped'); }
        this.socket = socket; this.buffer = '';
        socket.setEncoding('utf8');
        socket.on('data', data => { if (this.socket === socket) this.onData(data); });
        socket.on('error', () => { if (this.socket === socket) this.disconnect(); });
        socket.on('close', () => { if (this.socket === socket) this.disconnect(); });
        return;
      } catch (error) { if (['EPERM', 'EACCES'].includes(error.code)) denied = true; }
    }
    this.connectionIssue = denied ? 'access-denied' : 'monitor-off';
    throw new Error('monitor unavailable');
  }
  onData(data) {
    this.buffer += data;
    if (Buffer.byteLength(this.buffer, 'utf8') > 128 * 1024) { this.disconnect(); return; }
    let end;
    while ((end = this.buffer.indexOf('\n')) >= 0) {
      const line = this.buffer.slice(0, end); this.buffer = this.buffer.slice(end + 1);
      const pending = this.pending;
      if (!pending) { this.disconnect(); return; }
      this.pending = null; clearTimeout(pending.timeout);
      try {
        const message = JSON.parse(line);
        if (message.error === 'server-not-monitored' || this.endpoint && message.state?.endpoint !== this.endpoint) {
          this.connectionIssue = 'server-not-monitored'; throw new Error('Selected server is not monitored by this app');
        }
        if (!message.ok || !validSnapshot(message.state)) throw new Error('invalid monitor reply');
        if (!this.state || this.state.now !== message.state.now) this.progressAt = performance.now();
        this.state = message.state; this.receivedAt = performance.now(); this.connectionIssue = null; pending.resolve(message);
        if (Array.isArray(message.monitors)) this.monitors = message.monitors.filter(s => typeof s.endpoint === 'string' && typeof s.serverName === 'string').slice(0, 50);
      } catch (error) { pending.reject(error); this.disconnect(); }
    }
  }
  request(command = 'snapshot') {
    if (!['snapshot', 'refresh'].includes(command)) return Promise.reject(new Error('unsupported command'));
    const task = this.tail.catch(() => {}).then(async () => {
      await this.connect();
      return new Promise((resolve, reject) => {
        const timeout = setTimeout(() => this.disconnect(), 2500);
        this.pending = { resolve, reject, timeout };
        this.socket.write(JSON.stringify({ command, endpoint: this.endpoint }) + '\n');
      });
    });
    this.tail = task;
    return task;
  }
  disconnect() {
    const socket = this.socket; this.socket = null; this.buffer = ''; this.state = null;
    socket?.destroy();
    if (this.pending) { const p = this.pending; this.pending = null; clearTimeout(p.timeout); p.reject(new Error('monitor disconnected')); }
  }
  stop() { this.stopped = true; this.disconnect(); }
}
