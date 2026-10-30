import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { WebSocketServer } from 'ws';
import { BridgeClient } from '../src/bridge-client.mjs';

const root = path.resolve(import.meta.dirname, '../..');
async function until(predicate, timeout = 6000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await predicate()) return; await new Promise(resolve => setTimeout(resolve, 30)); }
  throw new Error('Timed out waiting for integration result');
}
function text(image = '') {
  if (image.startsWith('data:')) return image.includes(';base64,') ? Buffer.from(image.split(',')[1], 'base64').toString() : decodeURIComponent(image.slice(image.indexOf(',') + 1));
  return image;
}
test('real official-SDK plugin exchanges state with the C# pipe fixture and shares refresh protection', { timeout: 25000 }, async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'DayzDeckIntegration-'));
  const dotnet = process.env.DOTNET_EXE || path.join(root, '.tools/dotnet/dotnet.exe');
  const dll = path.join(root, 'tests/MagDeer.Tests/bin/Release/net8.0/MagDeer.Tests.dll');
  const fixture = spawn(dotnet, [dll, '--deck-fixture', directory], { cwd: root, stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
  let output = '', errors = '', plugin;
  fixture.stdout.on('data', data => output += data); fixture.stderr.on('data', data => errors += data);
  const server = new WebSocketServer({ host: '127.0.0.1', port: 0 });
  try {
    await until(() => output.includes('DECK_FIXTURE_READY') || (fixture.exitCode !== null && Promise.reject(new Error(errors || output))));
    const client = new BridgeClient('', directory);
    const reply = await client.request(); assert.equal(reply.state.players, 39); assert.equal(reply.monitors.length, 2); client.stop();
    if (!server.address()) await once(server, 'listening');
    const info = { application: { font: 'Segoe UI', language: 'en', platform: 'windows', platformVersion: '10', version: '7.1.0' },
      plugin: { uuid: 'com.iron-panda666.dayz-server-monitor', version: '1.9.2.0' },
      devices: [{ id: 'mock-mk2', name: 'MK.2 console fixture', type: 0, size: { columns: 5, rows: 3 } }], colors: {} };
    const connected = once(server, 'connection');
    plugin = spawn(process.execPath, ['bin/plugin.js', '-port', String(server.address().port), '-pluginUUID', 'fixture-plugin', '-registerEvent', 'registerPlugin', '-info', JSON.stringify(info)],
      { cwd: path.join(root, 'streamdeck/com.iron-panda666.dayz-server-monitor.sdPlugin'), env: { ...process.env, DAYZ_MONITOR_DISCOVERY_DIR: directory }, stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
    let pluginErrors = ''; plugin.stderr.on('data', data => pluginErrors += data);
    const [socket] = await Promise.race([connected, new Promise((_, reject) => setTimeout(() => reject(new Error('Plugin failed to register: ' + pluginErrors)), 6000))]);
    const images = new Map(), settings = [], inspectorReplies = [];
    const invalidImages = [];
    socket.on('message', data => {
      const message = JSON.parse(data);
      if (message.event === 'setImage') {
        if (!message.payload.image.startsWith('data:image/svg+xml;base64,')) { invalidImages.push(message.payload.image); return; }
        images.set(message.context, text(message.payload.image));
      }
      if (message.event === 'setSettings') settings.push(message);
      if (message.event === 'sendToPropertyInspector') inspectorReplies.push(message.payload);
    });
    const event = (name, context, payload = {}) => socket.send(JSON.stringify({ event: name,
      action: 'com.iron-panda666.dayz-server-monitor.' + (context === 'pinned' ? 'pinned' : 'cycle'), context, device: 'mock-mk2', payload }));
    const appear = context => event('willAppear', context, { controller: 'Keypad', coordinates: { column: 0, row: 0 }, isInMultiAction: false, state: 0,
      settings: { view: 'players', pages: ['players', 'rtt'], autoWarnings: false } });
    appear('cycle'); appear('pinned');
    event('willAppear', 'second', { controller: 'Keypad', coordinates: { column: 1, row: 0 }, isInMultiAction: false, state: 0,
      settings: { view: 'players', pages: ['players'], autoWarnings: false, endpoint: '127.0.0.1:2403' } });
    await until(() => images.get('cycle')?.includes('39/121') && images.get('pinned')?.includes('39/121'));
    await until(() => images.get('second')?.includes('77/121'));
    event('propertyInspectorDidAppear', 'cycle');
    event('sendToPlugin', 'cycle', { command: 'list-monitors' });
    await until(() => inspectorReplies.some(reply => reply.monitors?.some(m => m.endpoint === '127.0.0.1:2403')));
    event('keyDown', 'cycle'); event('keyUp', 'cycle');
    await until(() => settings.some(s => s.payload.view === 'rtt') && images.get('cycle')?.includes('UDP RTT'));
    event('keyDown', 'cycle'); await new Promise(resolve => setTimeout(resolve, 1150)); event('keyUp', 'cycle');
    await until(() => images.get('pinned')?.includes('40/121'));
    assert.ok(images.get('cycle').includes('UDP RTT'), 'Long hold also changed view');
    event('keyDown', 'pinned'); await new Promise(resolve => setTimeout(resolve, 1150)); event('keyUp', 'pinned');
    assert.ok(images.get('pinned').includes('40/121'), 'A second key bypassed the shared cooldown');
    assert.ok(!images.get('pinned').includes('41/121'));
    assert.ok(images.get('second').includes('77/121'), 'Refreshing the first server affected the second server');
    event('keyDown', 'second'); await new Promise(resolve => setTimeout(resolve, 1150)); event('keyUp', 'second');
    await until(() => images.get('second')?.includes('78/121'));
    assert.ok(images.get('pinned').includes('40/121'), 'Refreshing the second server affected the desktop server');
    event('didReceiveSettings', 'second', { settings: { view: 'players', endpoint: 'removed:2403', autoWarnings: false } });
    await until(() => images.get('second')?.includes('CHECK SELECTION'));
    event('didReceiveSettings', 'cycle', { settings: { view: 'session', pages: ['session'], autoWarnings: false, settingsVersion: 2 } });
    await until(() => images.get('cycle')?.includes('>00:30<') && images.get('cycle')?.includes('GAME RUNTIME'));
    const ended = once(fixture, 'exit'); fixture.stdin.end('stop\n'); await ended;
    await until(() => images.get('cycle')?.includes('>OFF<') && images.get('pinned')?.includes('>OFF<'));
    assert.equal((await fs.readdir(directory)).filter(n => n.endsWith('.json')).length, 0);
    assert.equal(invalidImages.length, 0, 'Raw SVG was sent instead of an image data URL');
  } finally {
    // Stop only the console children created by this test, never the user's applications.
    if (plugin && plugin.exitCode === null) { const ended = once(plugin, 'exit'); plugin.kill(); await ended; }
    if (fixture.exitCode === null) { const ended = once(fixture, 'exit'); fixture.stdin.end('stop\n'); await Promise.race([ended, new Promise(resolve => setTimeout(resolve, 2000))]); if (fixture.exitCode === null) fixture.kill(); }
    for (const socket of server.clients) socket.terminate(); await new Promise(resolve => server.close(resolve));
    await fs.rm(directory, { recursive: true, force: true });
  }
});
