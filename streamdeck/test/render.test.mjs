import test from 'node:test';
import assert from 'node:assert/strict';
import { Resvg } from '@resvg/resvg-js';
import { countdown, estimatedGameClock, normalizeSettings, renderKey, keyImageDataUrl, sessionDuration } from '../src/render.mjs';
import { KeyGesture } from '../src/gesture.mjs';
import { BridgeClient, validDiscovery, validSnapshot } from '../src/bridge-client.mjs';
import { OptionGroup } from '@elgato/utils';
import { JsonRpcRequest, JsonRpcResponse } from '@elgato/utils/rpc';
import { z } from 'zod/v4-mini';

test('SDK option and RPC schemas work with the patched Zod Mini', () => {
  const option = { type: 'option', label: 'MAG DEER', value: '217.182.198.57:2303' };
  const group = { type: 'option-group', label: 'Servers', options: [option] };
  assert.equal(OptionGroup.safeParse({ ...group, options: [group, option] }).success, true);
  assert.equal(OptionGroup.safeParse({ ...group, options: [{ ...option, value: {} }] }).success, false);
  assert.equal(JsonRpcRequest.safeParse({ jsonrpc: '2.0', method: 'snapshot', params: [], id: '1' }).success, true);
  assert.equal(JsonRpcResponse.safeParse({ jsonrpc: '2.0', result: { players: 39 }, id: '1' }).success, true);
  assert.equal(JsonRpcResponse.safeParse({ jsonrpc: '2.0', error: { code: -32600, message: 'Invalid request' } }).success, true);
  assert.equal(JsonRpcResponse.safeParse({ jsonrpc: '2.0', error: { code: 'invalid' } }).success, false);
});

test('oversized constrained arrays are rejected before validating their elements', () => {
  let parsed = 0;
  const item = z.string().check(z.refine(() => { parsed++; return false; }));
  const schema = z.array(item).check(z.maxLength(120));
  const result = schema.safeParse(Array(100000).fill('invalid'));
  assert.equal(result.success, false);
  assert.equal(parsed, 0, 'Oversized input was validated element by element');
  assert.equal(result.error.issues.length, 1);
  assert.equal(result.error.issues[0].code, 'too_big');
});

const state = { protocolVersion: 1, now: '2026-10-30T12:00:00Z', nextRestartAt: '2026-10-30T14:30:00Z',
  lastQueryAt: '2026-10-30T12:00:00Z', serverName: 'MAG #2', querySuccess: true, players: 39, capacity: 121, rttMs: 87,
  logoutWarningMinutes: 10, kickWarningMinutes: 5, queryIntervalSeconds: 300, scheduleConfirmed: false, history: [] };
test('service keys distinguish provider report, web reachability and an unknown check without restart overrides', () => {
  const service = { state: 'Degraded', scope: 'ProviderStatus' };
  const snapshot = { ...state, serverName: 'Discord', service, nextRestartAt: state.now, restartExpected: true };
  assert.match(renderKey(snapshot, 'restart'), />ISSUE</); assert.match(renderKey(snapshot, 'players'), /PROVIDER REPORT/);
  assert.match(renderKey({ ...snapshot, service: { state: 'Reachable', scope: 'WebReachability' } }, 'restart'), /WEB CHECK ONLY/);
  assert.match(renderKey({ ...snapshot, service: { state: 'Unavailable', scope: 'WebReachability' } }, 'players'), />N\/A</);
  assert.match(renderKey(null, 'players', {}, '', 'server-not-monitored'), /CHECK SELECTION/);
});
test('a targeted key never displays an unrelated server returned by an older monitor', async () => {
  const client = new BridgeClient('', 'unused', '127.0.0.1:2403');
  const pending = new Promise((resolve, reject) => { client.pending = { resolve, reject, timeout: setTimeout(() => {}, 1000) }; });
  client.onData(JSON.stringify({ ok: true, state: { ...state, endpoint: '127.0.0.1:2303' } }) + '\n');
  await assert.rejects(pending); assert.equal(client.snapshot(), null); assert.equal(client.connectionIssue, 'server-not-monitored');
});
test('wire image is an encoded SVG data URL and access denial is distinct from a stopped monitor', () => {
  const svg = renderKey(state, 'players', { label: 'MAG #2 & CZ' });
  const url = keyImageDataUrl(svg);
  assert.ok(url.startsWith('data:image/svg+xml;base64,'));
  assert.equal(Buffer.from(url.split(',')[1], 'base64').toString('utf8'), svg);
  assert.match(renderKey(null, 'players', {}, '', 'access-denied'), />DENIED</);
  assert.ok(!renderKey(null, 'players', {}, '', 'access-denied').includes('START THE APP'));
});
test('short presses cycle once; a one-second hold refreshes once and never cycles on release', () => {
  let short = 0, long = 0, next = 0; const pending = new Map();
  const timers = { setTimeout(fn) { pending.set(++next, fn); return next; }, clearTimeout(id) { pending.delete(id); } };
  const gesture = new KeyGesture(() => short++, () => long++, timers);
  gesture.press(); gesture.press(); gesture.release(); assert.equal(short, 1); assert.equal(long, 0);
  gesture.press(); const [id, fire] = [...pending.entries()][0]; pending.delete(id); fire(); gesture.release(); assert.equal(long, 1); assert.equal(short, 1);
  gesture.press(); gesture.cancel(); gesture.release(); assert.equal(short, 1); assert.equal(long, 1); assert.equal(pending.size, 0);
});
test('game clock uses the product of night and base speed, with marked assumed day/night boundaries', () => {
  const snapshot = { ...state, gameClock: { hour: 19, minute: 0, dayMultiplier: 4, nightMultiplier: 4 }, now: '2026-10-30T12:01:00Z' };
  assert.deepEqual(estimatedGameClock(snapshot), { text: '~19:16', phase: 'NIGHT?', speed: 16, predicted: true });
  snapshot.gameClock.hour = 17; snapshot.gameClock.minute = 59;
  assert.equal(estimatedGameClock(snapshot).text, '~18:12');
  snapshot.gameClock = { hour: 23, minute: 59, dayMultiplier: 4, nightMultiplier: 4 };
  assert.equal(estimatedGameClock(snapshot).text, '~00:15');
  snapshot.gameClock.nightMultiplier = null;
  assert.equal(estimatedGameClock(snapshot).text, '23:59'); assert.equal(estimatedGameClock(snapshot).predicted, false);
  snapshot.gameClock.hour = 24; assert.equal(estimatedGameClock(snapshot), null);
});
test('rendering missing replies, absent monitor and missing game fields never invents zero metrics', () => {
  assert.match(renderKey(null, 'players'), />OFF</);
  assert.match(renderKey({ ...state, querySuccess: false, players: null }, 'players'), />N\/A</);
  assert.match(renderKey(state, 'game'), /NOT PUBLISHED/);
  assert.match(renderKey({ ...state, lastQueryAt: '2026-10-30T10:00:00Z' }, 'players'), /STALE/);
  assert.match(renderKey(state, 'restart'), /PLAN\?/);
  assert.match(renderKey({ ...state, refreshRemainingSeconds: 20 }, 'players'), /WAIT 20s/);
});
test('restart warning overrides are optional and use configured thresholds', () => {
  const urgent = { ...state, nextRestartAt: '2026-10-30T12:04:00Z' };
  assert.match(renderKey(urgent, 'players'), /RESTART/); assert.match(renderKey(urgent, 'players'), /#ffaaa0/);
  assert.match(renderKey(urgent, 'players', { autoWarnings: false }), /PLAYERS/);
  assert.equal(countdown(9000), '2h30m'); assert.equal(countdown(65), '01:05'); assert.equal(countdown(-5), '00:00');
});
test('SVG is escaped, settings are bounded and every face rasterizes at MK.2 resolution', () => {
  const settings = normalizeSettings({ label: '<script>&"', pages: ['bad'], view: 'bad', sunrise: 20, sunset: 1 });
  assert.equal(settings.view, 'restart'); assert.equal(settings.pages.length, 7); assert.equal(settings.sunrise, 6);
  const svg = renderKey(state, 'players', settings); assert.ok(!svg.includes('<script>')); assert.match(svg, /&lt;script&gt;/);
  for (const mode of settings.pages) {
    const image = new Resvg(renderKey(state, mode), { fitTo: { mode: 'width', value: 72 } }).render();
    assert.equal(image.width, 72); assert.equal(image.height, 72);
  }
});
test('player history preserves missing-sample gaps and places points by elapsed time', () => {
  const history = [
    { at: '2026-10-30T11:00:00Z', players: 10, capacity: 100 },
    { at: '2026-10-30T11:05:00Z', players: null, capacity: null },
    { at: '2026-10-30T11:55:00Z', players: 39, capacity: 100 }
  ];
  const svg = renderKey({ ...state, history }, 'history');
  assert.equal((svg.match(/<circle/g) ?? []).length, 2); assert.equal((svg.match(/<path/g) ?? []).length, 0);
  assert.match(svg, /cx="5.00"/); assert.match(svg, /cx="61.83"/);
});
test('discovery and wire validation reject arbitrary pipes and oversized history', () => {
  assert.ok(!validDiscovery({ protocolVersion: 1, pipeName: 'unrelated-service', dataRoot: 'C:\\example' }));
  assert.ok(validSnapshot(state)); assert.ok(!validSnapshot({ ...state, history: Array(121).fill(null) }));
  assert.ok(!validSnapshot({ ...state, now: 'invalid' }));
});
test('a responsive pipe with a frozen monitor clock is not shown as fresh', () => {
  const client = new BridgeClient();
  client.state = state; client.receivedAt = performance.now(); client.progressAt = performance.now();
  assert.equal(client.snapshot(), state);
  client.progressAt -= 4000; assert.equal(client.snapshot(), null);
  client.stop(); assert.equal(client.snapshot(), null);
});
test('full-server queue stays visible during refresh cooldown and missing queues are not zero', () => {
  const full = { ...state, players: 121, capacity: 121, queue: 21, refreshRemainingSeconds: 20 };
  const svg = renderKey(full, 'players');
  assert.match(svg, />121\/121</); assert.match(svg, /\+21 IN QUEUE/); assert.match(svg, /WAIT 20s/);
  assert.ok(!svg.includes('142/121'));
  assert.match(renderKey({ ...full, queue: 0 }, 'players'), /QUEUE 0/);
  assert.match(renderKey({ ...full, queue: null }, 'players'), /QUEUE N\/A/);
  assert.match(renderKey({ ...full, querySuccess: false }, 'players'), /QUEUE N\/A/);
  assert.match(renderKey({ ...full, lastQueryAt: '2026-10-30T10:00:00Z' }, 'players'), /QUEUE N\/A/);
  const bitmap = new Resvg(svg, { fitTo: { mode: 'width', value: 72 } }).render();
  assert.equal(bitmap.width, 72); assert.equal(bitmap.height, 72);
});

test('local session works without server query data and never treats unknown time as zero', () => {
  const local = { ...state, querySuccess: false, lastQueryAt: null, gameSession: { state: 'Running', elapsedSeconds: 5025, source: 'WindowsProcess' } };
  assert.match(renderKey(local, 'session'), />01:23</); assert.match(renderKey(local, 'session'), /DAYZ \/ LOCAL/);
  assert.match(renderKey({ ...local, gameSession: { state: 'NotRunning' } }, 'session'), /GAME CLOSED/);
  assert.match(renderKey({ ...local, gameSession: { state: 'Unavailable' } }, 'session'), />N\/A</);
  assert.match(renderKey({ ...local, gameSession: null }, 'session'), /APP 1.9/);
  assert.equal(sessionDuration(-1), 'N/A'); assert.equal(sessionDuration(NaN), 'N/A'); assert.equal(sessionDuration(90007), '25:00');
});
test('new session page joins legacy default cycling once while custom page selections stay intact', () => {
  const legacy = ['game', 'restart', 'players', 'history', 'rtt', 'logout'];
  const migrated = normalizeSettings({ pages: legacy }); assert.equal(migrated.pages.length, 7); assert.ok(migrated.pages.includes('session'));
  assert.deepEqual(normalizeSettings(migrated), migrated);
  assert.deepEqual(normalizeSettings({ pages: ['players', 'restart'] }).pages, ['players', 'restart']);
  assert.deepEqual(normalizeSettings({ pages: legacy, settingsVersion: 2 }).pages, legacy);
});
