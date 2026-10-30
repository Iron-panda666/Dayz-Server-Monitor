export const views = ['game', 'restart', 'players', 'history', 'rtt', 'logout', 'session'];
// Stream Deck's wire API needs an image data URL, not the raw SVG markup.
export const keyImageDataUrl = svg => 'data:image/svg+xml;base64,' + Buffer.from(svg, 'utf8').toString('base64');
export function normalizeSettings(value = {}) {
  const pages = Array.isArray(value.pages) ? [...new Set(value.pages.filter(p => views.includes(p)))] : [...views];
  if (!(value.settingsVersion >= 2) && pages.length === 6 && views.slice(0, 6).every(p => pages.includes(p))) pages.push('session');
  const number = (v, fallback) => Number.isFinite(Number(v)) ? Math.max(0, Math.min(23, Math.trunc(Number(v)))) : fallback;
  let sunrise = number(value.sunrise ?? 6, 6), sunset = number(value.sunset ?? 18, 18);
  if (sunrise >= sunset) { sunrise = 6; sunset = 18; }
  return {
    settingsVersion: 2, view: views.includes(value.view) ? value.view : 'restart', pages: pages.length ? pages : [...views],
    label: typeof value.label === 'string' ? value.label.slice(0, 12) : '',
    dataDirectory: typeof value.dataDirectory === 'string' ? value.dataDirectory.slice(0, 1024) : '',
    endpoint: typeof value.endpoint === 'string' && value.endpoint.length <= 80 ? value.endpoint.trim() : '',
    autoWarnings: value.autoWarnings !== false, sunrise, sunset,
    clockFormat: ['app', '12h', '24h'].includes(value.clockFormat) ? value.clockFormat : 'app'
  };
}
export const escapeXml = text => String(text).replace(/[<>&"']/g, c => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', '"': '&quot;', "'": '&apos;' })[c]);
export function countdown(seconds) {
  if (!Number.isFinite(seconds)) return 'N/A';
  seconds = Math.max(0, Math.ceil(seconds));
  return seconds >= 3600 ? `${Math.floor(seconds / 3600)}h${String(Math.floor(seconds / 60) % 60).padStart(2, '0')}m`
    : `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
}
export function sessionDuration(seconds) {
  if (!Number.isSafeInteger(seconds) || seconds < 0 || seconds > 315360000) return 'N/A';
  return seconds >= 360000 ? `${Math.floor(seconds / 86400)}d ${Math.floor(seconds / 3600) % 24}h`
    : `${String(Math.floor(seconds / 3600)).padStart(2, '0')}:${String(Math.floor(seconds / 60) % 60).padStart(2, '0')}`;
}
export function estimatedGameClock(state, settings = normalizeSettings()) {
  const game = state?.gameClock;
  if (!game || !Number.isInteger(game.hour) || game.hour < 0 || game.hour > 23 || !Number.isInteger(game.minute) || game.minute < 0 || game.minute > 59) return null;
  let seconds = game.hour * 3600 + game.minute * 60;
  const sunrise = settings.sunrise * 3600, sunset = settings.sunset * 3600;
  const day = game.dayMultiplier, night = game.nightMultiplier;
  const valid = n => Number.isFinite(n) && n >= .1 && n <= 64;
  const predicted = valid(day) && valid(night) && Number.isFinite(Date.parse(state.lastQueryAt));
  if (predicted) {
    let elapsed = Math.max(0, (Date.parse(state.now) - Date.parse(state.lastQueryAt)) / 1000);
    // Full-day cycles can be removed without stepping through every accelerated minute.
    const cycle = (sunset - sunrise) / day + (86400 - sunset + sunrise) / (day * night);
    elapsed %= cycle;
    for (let step = 0; elapsed > 0 && step < 4; step++) {
      const isDay = seconds >= sunrise && seconds < sunset;
      const speed = day * (isDay ? 1 : night);
      const boundary = isDay ? sunset : seconds < sunrise ? sunrise : sunrise + 86400;
      const realUntilBoundary = (boundary - seconds) / speed;
      if (elapsed < realUntilBoundary) { seconds = (seconds + elapsed * speed) % 86400; break; }
      seconds = boundary % 86400; elapsed -= realUntilBoundary;
    }
  }
  const isDay = seconds >= sunrise && seconds < sunset;
  const hour = Math.floor(seconds / 3600) % 24;
  const use24 = settings.clockFormat === '24h' || settings.clockFormat !== '12h' && state.use24HourClock !== false;
  const period = use24 ? '' : hour < 12 ? 'AM' : 'PM';
  const displayTime = `${predicted ? '~' : ''}${String(use24 ? hour : hour % 12 || 12).padStart(2, '0')}:${String(Math.floor(seconds / 60) % 60).padStart(2, '0')}`;
  return { text: displayTime + (period ? ' ' + period : ''), displayTime, period,
    phase: isDay ? 'DAY' : 'NIGHT', speed: valid(day) && (isDay || valid(night)) ? day * (isDay ? 1 : night) : null, predicted };
}
export function restartAppearance(seconds, now) {
  if (!Number.isFinite(seconds) || seconds > 900) return { color: '#90e8ff', opacity: 1 };
  if (seconds > 600) return { color: '#ffda58', opacity: 1 };
  if (seconds > 300) return { color: '#ff9a3d', opacity: 1 };
  const mix = Math.max(0, Math.min(1, (300 - seconds) / 270));
  const values = [255, Math.round(154 + (91 - 154) * mix), Math.round(61 + (82 - 61) * mix)];
  return { color: '#' + values.map(n => n.toString(16).padStart(2, '0')).join(''),
    opacity: seconds > 0 && seconds <= 30 && Math.floor(now / 1000) % 2 !== 0 ? .78 : 1 };
}
export function serverLabel(name = '') {
  const number = name.match(/#\d+/)?.[0] ?? '';
  return (/Sunnyvale/i.test(name) ? 'SV ' + number : /MiddleAgedGamers/i.test(name) ? 'MAG ' + number : name).trim().slice(0, 12);
}
export function renderKey(state, requestedView, rawSettings = {}, feedback = '', connectionIssue = null) {
  const settings = normalizeSettings(rawSettings), now = Date.parse(state?.now);
  const untilRestart = (Date.parse(state?.nextRestartAt) - now) / 1000;
  const targetKick = state?.countdownToKick === true && state?.kickBeforeRestart === true;
  const remaining = state?.restartExpected ? 0 : untilRestart - (targetKick ? state.kickWarningMinutes * 60 : 0);
  const warning = state && !state.service && (state.restartExpected || remaining <= state.logoutWarningMinutes * 60);
  const view = settings.autoWarnings && warning ? 'restart' : requestedView;
  const appearance = state?.visualAlerts === false ? { color: '#90e8ff', opacity: 1 } : restartAppearance(remaining, now);
  const accent = !state ? '#b8cbd4' : appearance.color;
  const stale = state && (!Number.isFinite(Date.parse(state.lastQueryAt)) || (now - Date.parse(state.lastQueryAt)) / 1000 > Math.max(30, (state.queryIntervalSeconds ?? 300) + 45));
  const available = state?.querySuccess && !stale;
  const label = settings.label || serverLabel(state?.serverName) || 'DAYZ MONITOR';
  let body = '';
  const text = (value, y, size = 8, color = '#b8cbd4') => `<text x="36" y="${y}" fill="${color}" font-size="${size}" text-anchor="middle">${escapeXml(value)}</text>`;
  const metric = (value, color = '#f4f8fb', size = 18, opacity = 1) => `<g opacity="${opacity}">${text(value, 46, size, color)}</g>`;
  body += text(view === 'session' ? 'DAYZ / LOCAL' : label, 10, 7);
  if (!state) {
    body += connectionIssue === 'server-not-monitored'
      ? text('SERVER', 28, 9) + metric('N/A', '#ffdb7e') + text('CHECK SELECTION', 62, 6)
      : connectionIssue === 'access-denied'
      ? text('ACCESS', 28, 9) + metric('DENIED', '#ffdb7e', 16) + text('CHECK ADMIN', 62, 7)
      : text('MONITOR', 28, 9) + metric('OFF', '#ffdb7e') + text('START THE APP', 62, 7);
  }
  else if (state.service) {
    const service = state.service;
    const known = !stale && ['Operational', 'Degraded', 'Reachable'].includes(service.state);
    const value = !known ? 'N/A' : service.state === 'Operational' ? 'OK' : service.state === 'Degraded' ? 'ISSUE' : 'WEB OK';
    body += text('SERVICE', 24) + metric(value, !known || service.state === 'Degraded' ? '#ffdb7e' : '#90e8ff', 18)
      + text(feedback || (stale ? 'STALE' : !known ? 'CHECK UNKNOWN' : service.scope === 'ProviderStatus' ? 'PROVIDER REPORT' : 'WEB CHECK ONLY'), 63, 6);
  }
  else {
    const age = Math.max(0, Math.floor((now - Date.parse(state.lastQueryAt)) / 1000));
    const ageLabel = Number.isFinite(age) ? age < 60 ? `${age}s OLD` : `${Math.floor(age / 60)}m OLD` : 'NO QUERY';
    const footer = feedback || (state.refreshRemainingSeconds > 0 ? `WAIT ${state.refreshRemainingSeconds}s` : stale ? 'STALE' : '');
    if (['game', 'players', 'history', 'rtt'].includes(view) && !available) {
      body += text(state.status === 'Offline' ? 'OFFLINE' : 'QUERY', 26, 9) + metric('N/A', '#ffdb7e')
        + (view === 'players' ? text('QUEUE N/A', 60, 8) + text(footer || ageLabel, 70, 6) : text(footer || ageLabel, 64, 7));
    } else if (view === 'game') {
      const game = estimatedGameClock(state, settings);
      body += text('GAME TIME', 24) + metric(game?.displayTime ?? 'N/A', '#f4f8fb', 17)
        + (game?.period ? text(game.period, 55, 7) : '')
        + text(footer || (game ? `${game.phase} ${game.speed == null ? 'N/A' : Number(game.speed.toFixed(2)) + 'x'} · EST` : 'NOT PUBLISHED'), game?.period ? 69 : 63, 6);
    } else if (view === 'restart') {
      body += text(targetKick ? 'PLAYER KICK' : 'RESTART', 24) + metric(state.restartExpected ? 'NOW' : countdown(remaining), accent, 18, appearance.opacity)
        + text(footer || (!state.scheduleConfirmed ? 'UNCONFIRMED' : 'CONFIRMED'), 63, 6);
    } else if (view === 'players') {
      const players = Number.isInteger(state.players) && Number.isInteger(state.capacity) ? `${state.players}/${state.capacity}` : 'N/A';
      const waiting = Number.isInteger(state.queue) && state.queue >= 0 && state.queue <= 10000 ? state.queue : null;
      body += text('PLAYERS', 24) + metric(players)
        + text(waiting === null ? 'QUEUE N/A' : waiting > 0 ? `+${waiting} IN QUEUE` : 'QUEUE 0', 60, 8, waiting > 0 ? '#ffdb7e' : '#b8cbd4')
        + text(footer || ageLabel, 70, 6);
    } else if (view === 'rtt') {
      body += text('UDP RTT', 24) + metric(Number.isFinite(state.rttMs) ? `${Math.round(state.rttMs)} ms` : 'N/A', '#f4f8fb', 17) + text(footer || ageLabel, 63, 7);
    } else if (view === 'session') {
      const session = state.gameSession, running = session?.state === 'Running';
      const duration = running ? sessionDuration(session.elapsedSeconds) : session?.state === 'NotRunning' || session?.state === 'Disabled' ? 'OFF' : 'N/A';
      const note = !session ? 'APP 1.9+' : running ? 'GAME RUNTIME' : session.state === 'NotRunning' ? 'GAME CLOSED'
        : session.state === 'Disabled' ? 'DISABLED IN APP' : session.state === 'MultipleInstances' ? 'MULTIPLE GAMES' : 'LOCAL ACCESS';
      body += text('SESSION', 24) + metric(duration, running ? '#90e8ff' : '#ffdb7e', 20) + text(feedback || note, 63, 7);
    } else if (view === 'logout') {
      const untilLogout = untilRestart - state.logoutWarningMinutes * 60;
      body += text(untilLogout <= 0 ? 'LOG OUT NOW' : 'LOG OUT IN', 24) + metric(state.restartExpected || untilLogout <= 0 ? 'NOW' : countdown(untilLogout), accent)
        + text(footer || (!state.scheduleConfirmed ? 'PLAN?' : 'BEFORE RESTART'), 63, 7);
    } else {
      body += text('PLAYERS / 1h', 23, 7);
      const history = state.history.filter(s => Number.isFinite(Date.parse(s.at)) && now - Date.parse(s.at) >= 0 && now - Date.parse(s.at) <= 3600000);
      const max = Math.max(1, ...history.map(s => Number.isFinite(s.capacity) ? s.capacity : 1));
      let previous = null, count = 0;
      for (const sample of history) {
        if (!Number.isFinite(sample.players)) { previous = null; continue; }
        const x = 5 + (Date.parse(sample.at) - now + 3600000) / 3600000 * 62, y = 56 - Math.max(0, Math.min(1, sample.players / max)) * 28;
        if (previous) body += `<path d="M${previous.x.toFixed(2)},${previous.y.toFixed(2)} L${x.toFixed(2)},${y.toFixed(2)}" fill="none" stroke="#90e8ff" stroke-width="1.5"/>`;
        body += `<circle cx="${x.toFixed(2)}" cy="${y.toFixed(2)}" r="1" fill="#90e8ff"/>`;
        previous = { x, y }; count++;
      }
      if (!count) body += text('NO HISTORY', 44, 9);
      body += text(footer || `${state.players ?? 'N/A'} NOW`, 68, 7);
    }
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 72 72"><rect width="72" height="72" rx="5" fill="#101820"/><g font-family="Segoe UI,Arial,sans-serif" font-weight="600">${body}</g></svg>`;
}
