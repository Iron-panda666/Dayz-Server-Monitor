import streamDeck, { SingletonAction, action } from '@elgato/streamdeck';
import { BridgeClient } from './bridge-client.mjs';
import { normalizeSettings, renderKey, keyImageDataUrl } from './render.mjs';
import { KeyGesture } from './gesture.mjs';
import path from 'node:path';

const entries = new Map(), connections = new Map();
let polling = false;
const connectionKey = directory => directory ? path.resolve(directory).toUpperCase() : '';
function acquire(directory) {
  const key = connectionKey(directory);
  let shared = connections.get(key);
  if (!shared) { shared = { client: new BridgeClient(directory), users: 0, refreshing: false, feedback: '', feedbackUntil: 0 }; connections.set(key, shared); }
  shared.users++; return shared;
}
function release(directory) {
  const key = connectionKey(directory), shared = connections.get(key);
  if (shared && --shared.users === 0) { shared.client.stop(); connections.delete(key); }
}
function paint(entry) {
  const shared = entry.shared;
  const feedback = performance.now() < shared.feedbackUntil ? shared.feedback : '';
  const image = keyImageDataUrl(renderKey(shared.client.snapshot(), entry.settings.view, entry.settings, feedback, shared.client.connectionIssue));
  if (image === entry.image) return;
  entry.image = image;
  entry.action.setImage(image).catch(error => { entry.image = null; streamDeck.logger.error(error); });
}
async function refresh(entry) {
  const shared = entry.shared;
  if (shared.refreshing) return;
  const state = shared.client.snapshot();
  if (!state) { shared.feedback = 'START THE APP'; shared.feedbackUntil = performance.now() + 2500; paint(entry); return; }
  if (state.polling || state.refreshRemainingSeconds > 0) {
    shared.feedback = state.polling ? 'QUERY RUNNING' : `WAIT ${state.refreshRemainingSeconds}s`;
    shared.feedbackUntil = performance.now() + 1500; paint(entry); return;
  }
  shared.refreshing = true;
  try {
    const reply = await shared.client.request('refresh');
    shared.feedback = reply.refresh?.accepted ? 'QUERY REQUESTED' : reply.refresh?.remainingSeconds > 0 ? `WAIT ${reply.refresh.remainingSeconds}s` : 'BUSY';
    shared.feedbackUntil = performance.now() + 1500;
  } catch { shared.feedback = 'MONITOR OFF'; shared.feedbackUntil = performance.now() + 2000; }
  finally { shared.refreshing = false; for (const item of entries.values()) if (item.shared === shared) paint(item); }
}
function detach(id) {
  const entry = entries.get(id);
  if (entry) { entry.gesture.cancel(); release(entry.settings.dataDirectory); entries.delete(id); }
}
function attach(ev, pinned) {
  if (!ev.action.isKey()) return;
  detach(ev.action.id);
  const settings = normalizeSettings(ev.payload.settings);
  const entry = { action: ev.action, settings, pinned, shared: acquire(settings.dataDirectory), image: null };
  entry.gesture = new KeyGesture(() => {
    if (entry.pinned) return;
    const index = entry.settings.pages.indexOf(entry.settings.view);
    entry.settings.view = entry.settings.pages[(index + 1) % entry.settings.pages.length];
    entry.action.setSettings(entry.settings).catch(error => streamDeck.logger.error(error));
    paint(entry);
  }, () => { void refresh(entry); });
  entries.set(ev.action.id, entry);
  ev.action.setTitle('').catch(error => streamDeck.logger.error(error));
  paint(entry); void tick();
}
async function tick() {
  if (polling || !entries.size) return;
  polling = true;
  try {
    await Promise.all([...connections.values()].map(async shared => {
      if (!shared.refreshing) try { await shared.client.request(); } catch { /* App is absent or closing. */ }
    }));
    for (const entry of entries.values()) paint(entry);
  } finally { polling = false; }
}
class MonitorAction extends SingletonAction {
  constructor(pinned) { super(); this.pinned = pinned; }
  onWillAppear(ev) { attach(ev, this.pinned); }
  onWillDisappear(ev) { detach(ev.action.id); }
  onDidReceiveSettings(ev) {
    const entry = entries.get(ev.action.id), settings = normalizeSettings(ev.payload.settings);
    if (!entry || entry.settings.dataDirectory !== settings.dataDirectory) { attach(ev, this.pinned); return; }
    entry.gesture.cancel(); entry.settings = settings; paint(entry);
  }
  onKeyDown(ev) { entries.get(ev.action.id)?.gesture.press(); }
  onKeyUp(ev) { entries.get(ev.action.id)?.gesture.release(); }
}
class CycleAction extends MonitorAction { constructor() { super(false); } }
class PinnedAction extends MonitorAction { constructor() { super(true); } }
const Cycle = action({ UUID: 'com.iron-panda666.dayz-server-monitor.cycle' })(CycleAction);
const Pinned = action({ UUID: 'com.iron-panda666.dayz-server-monitor.pinned' })(PinnedAction);
streamDeck.actions.registerAction(new Cycle());
streamDeck.actions.registerAction(new Pinned());
streamDeck.system.onSystemDidWakeUp(() => {
  for (const entry of entries.values()) entry.gesture.cancel();
  for (const shared of connections.values()) shared.client.disconnect();
});
const timer = setInterval(() => { void tick(); }, 1000);
process.once('exit', () => { clearInterval(timer); for (const shared of connections.values()) shared.client.stop(); });
streamDeck.connect().catch(error => { streamDeck.logger.error(error); process.exitCode = 1; clearInterval(timer); });
