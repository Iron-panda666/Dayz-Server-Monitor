import fs from 'node:fs/promises';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { Resvg } from '@resvg/resvg-js';
import gifenc from 'gifenc';
import { PNG } from 'pngjs';
import { renderKey } from '../src/render.mjs';

const { GIFEncoder, quantize, applyPalette } = gifenc;
const root = path.resolve(import.meta.dirname, '../..');
const framesDir = path.join(root, 'artifacts/countdown-frames');
const output = path.join(root, 'docs/images');
await fs.mkdir(framesDir, { recursive: true });
const restart = Date.parse('2026-11-01T18:00:00Z');
const phases = [901,900,840,780,720,660,600,540,480,420,360,300,270,240,210,180,150,120,90,60,45,31]
  .map(seconds => ({ seconds, delay: 250 }));
for (let seconds = 30; seconds >= 1; seconds--) phases.push({ seconds, delay: 250 });
phases.push({ seconds: 0, delay: 700 });
for (const seconds of [-1,-15,-30,-60,-90,-120,-150,-180,-210,-240,-270,-299]) phases.push({ seconds, delay: 250 });
phases.push({ seconds: 14100, delay: 1800, rollover: true });
const frames = phases.map(p => ({ ...p,
  now: new Date(p.rollover ? restart : restart - (p.seconds + 300) * 1000).toISOString(),
  nextRestart: new Date(p.rollover ? restart + 4 * 3600000 : restart).toISOString()
}));
const manifest = path.join(framesDir, 'frames.json');
await fs.writeFile(manifest, JSON.stringify(frames, null, 2));
const dotnet = process.env.DOTNET_EXE || path.join(root, '.tools/dotnet/dotnet.exe');
const dll = path.join(root, 'tests/MagDeer.ThemeTests/bin/Release/net10.0-windows/MagDeer.ThemeTests.dll');
const capture = spawnSync(dotnet, [dll, '--countdown-frames', manifest], { cwd: root, windowsHide: true, encoding: 'utf8' });
if (capture.status !== 0) throw new Error(capture.stderr || capture.stdout);
console.log(capture.stdout.trim());
const appGif = GIFEncoder(), deckGif = GIFEncoder();
function add(gif, rgba, width, height, delay) {
  const palette = quantize(rgba, 256);
  gif.writeFrame(applyPalette(rgba, palette), width, height, { palette, delay, repeat: 0 });
}
for (let i = 0; i < frames.length; i++) {
  const frame = frames[i];
  const png = PNG.sync.read(await fs.readFile(path.join(framesDir, `app-${String(i).padStart(3, '0')}.png`)));
  add(appGif, png.data, png.width, png.height, frame.delay);
  const state = { now: frame.now, nextRestartAt: frame.nextRestart, serverName: 'MAG MiddleAgedGamers #2 PVE DEER',
    kickBeforeRestart: true, countdownToKick: true, kickWarningMinutes: 5, logoutWarningMinutes: 10,
    scheduleConfirmed: false, visualAlerts: true, lastQueryAt: frame.now, querySuccess: true,
    refreshRemainingSeconds: 0, queryIntervalSeconds: 300 };
  const key = renderKey(state, 'restart');
  const inner = key.replace(/^<svg[^>]*>/, '').replace(/<\/svg>$/, '');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="460" height="490"><rect width="460" height="490" fill="#141d27"/><g font-family="Segoe UI,Arial,sans-serif" text-anchor="middle"><text x="230" y="30" fill="#eef5fa" font-size="18">STREAM DECK - ACCELERATED DEMO</text><text x="230" y="469" fill="#b8cbd4" font-size="14">Configured schedule - sample data</text></g><g transform="translate(50,65) scale(5)">${inner}</g></svg>`;
  const image = new Resvg(svg, { font: { loadSystemFonts: true } }).render();
  add(deckGif, image.pixels, image.width, image.height, frame.delay);
  if ([0,22,23,52,53,64,65].includes(i)) await fs.writeFile(path.join(framesDir, `deck-${i}.png`), image.asPng());
}
appGif.finish(); deckGif.finish();
for (const [name, gif] of [['countdown-app.gif', appGif], ['countdown-stream-deck.gif', deckGif]]) {
  await fs.writeFile(path.join(output, name), gif.bytes());
  console.log(`${name}: ${gif.bytes().length} bytes, ${frames.length} frames`);
}
console.log(`Loop duration: ${frames.reduce((sum, f) => sum + f.delay, 0)} ms`);
