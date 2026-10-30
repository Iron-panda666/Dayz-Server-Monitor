import { build } from 'esbuild';
import { Resvg } from '@resvg/resvg-js';
import fs from 'node:fs/promises';
import path from 'node:path';
import { renderKey } from '../src/render.mjs';

const root = path.resolve(import.meta.dirname, '../..'), plugin = path.join(root, 'streamdeck/com.iron-panda666.dayz-server-monitor.sdPlugin');
await fs.mkdir(path.join(plugin, 'bin'), { recursive: true });
await build({ entryPoints: [path.join(root, 'streamdeck/src/plugin.mjs')], outfile: path.join(plugin, 'bin/plugin.js'),
  platform: 'node', target: 'node24', format: 'esm', bundle: true, sourcemap: false, legalComments: 'eof',
  external: ['bufferutil', 'utf-8-validate'], banner: { js: "import {createRequire} from 'node:module';const require=createRequire(import.meta.url);" } });
const originalIcon = await fs.readFile(path.join(root, 'src/MagDeer.App/Assets/app.svg'), 'utf8');
for (const size of [256, 512]) {
  const image = new Resvg(originalIcon, { fitTo: { mode: 'width', value: size } }).render().asPng();
  await fs.writeFile(path.join(plugin, 'imgs', size === 256 ? 'plugin.png' : 'plugin@2x.png'), image);
}
await fs.copyFile(path.join(root, 'LICENSE'), path.join(plugin, 'LICENSE'));
const packages = ['@elgato/streamdeck', '@elgato/schemas', '@elgato/utils', 'ws', 'zod'];
let notices = '# Bundled runtime dependencies\n\nThe original plugin code and T/R icon use MIT. These components retain their own notices.\n';
for (const name of packages) {
  const directory = path.join(root, 'streamdeck/node_modules', name);
  const meta = JSON.parse(await fs.readFile(path.join(directory, 'package.json'), 'utf8'));
  const files = await fs.readdir(directory);
  const license = files.find(name => /^licen[cs]e(?:\.(?:md|txt))?$/i.test(name));
  if (!license) throw new Error('Missing dependency license: ' + name);
  notices += `\n## ${name} ${meta.version} (${meta.license})\n\n` + await fs.readFile(path.join(directory, license), 'utf8') + '\n';
}
await fs.writeFile(path.join(plugin, 'THIRD_PARTY_NOTICES.md'), notices);
await fs.writeFile(path.join(root, 'docs/licenses/STREAMDECK-RUNTIME-NOTICES.md'), notices);
const at = '2026-10-30T00:00:00Z';
const demo = { protocolVersion: 1, now: at, serverName: 'MAG MiddleAgedGamers #2 PVE DEER', endpoint: 'example:2303', status: 'Online',
  nextRestartAt: '2026-10-30T02:30:00Z', restartExpected: false, scheduleConfirmed: false, logoutWarningMinutes: 10, kickWarningMinutes: 5,
  lastQueryAt: at, lastSuccessAt: at, querySuccess: true, players: 39, capacity: 121, queue: 0, rttMs: 87,
  gameClock: { hour: 19, minute: 42, dayMultiplier: 4, nightMultiplier: 4 }, refreshRemainingSeconds: 0, queryIntervalSeconds: 300,
  history: Array.from({ length: 13 }, (_, i) => ({ at: new Date(Date.parse(at) - (12 - i) * 300000).toISOString(), players: i === 7 ? null : [22, 25, 27, 23, 29, 33, 31, 0, 35, 38, 34, 40, 39][i], capacity: 121 })) };
const names = ['game', 'restart', 'players', 'history', 'rtt', 'logout'];
let children = '';
for (let i = 0; i < names.length; i++) {
  const key = renderKey(demo, names[i], { autoWarnings: false });
  children += `<g transform="translate(${22 + i % 3 * 168},${48 + Math.floor(i / 3) * 170})">${key}<text x="72" y="161" text-anchor="middle" fill="#c7d3e0" font-size="12">${names[i].toUpperCase()}</text></g>`;
}
const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="548" height="400"><rect width="548" height="400" fill="#202833"/><text x="22" y="27" fill="#f5f8fc" font-size="15" font-family="Segoe UI,Arial">Dayz Server Monitor · MK.2 key designs · DEMO</text>${children}</svg>`;
await fs.writeFile(path.join(root, 'docs/images/stream-deck-keys.png'), new Resvg(svg, { font: { loadSystemFonts: true } }).render().asPng());
console.log('Built official-SDK plugin, runtime notices, icons and English demo keys. No desktop windows opened.');
