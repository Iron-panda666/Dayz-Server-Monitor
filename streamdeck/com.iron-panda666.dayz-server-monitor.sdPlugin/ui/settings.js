let socket, context, actionUuid, settings = {}, initialized = false, pinned = false;
const ids = ['view', 'label', 'dataDirectory', 'endpoint', 'autoWarnings', 'clockFormat', 'sunrise', 'sunset'];
function setMonitors(monitors = []) {
  const select = document.getElementById('endpoint'); select.replaceChildren(new Option('Follow desktop selection', ''));
  for (const item of monitors) if (typeof item.endpoint === 'string' && typeof item.serverName === 'string') select.add(new Option(item.serverName + ' · ' + item.endpoint, item.endpoint));
  if (settings.endpoint && ![...select.options].some(o => o.value === settings.endpoint)) select.add(new Option(settings.endpoint + ' (not currently listed)', settings.endpoint));
  select.value = settings.endpoint ?? '';
}
function reloadMonitors() { if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ event: 'sendToPlugin', action: actionUuid, context, payload: { command: 'list-monitors' } })); }
function apply(value) {
  settings = { ...(value ?? {}) }; initialized = false;
  const legacy = ["game", "restart", "players", "history", "rtt", "logout"];
  if (!(settings.settingsVersion >= 2) && Array.isArray(settings.pages) && settings.pages.length === 6 && legacy.every(p => settings.pages.includes(p))) settings.pages = [...settings.pages, "session"];
  settings.settingsVersion = 2;
  document.getElementById('view').value = settings.view ?? 'restart';
  document.getElementById('label').value = settings.label ?? '';
  document.getElementById('dataDirectory').value = settings.dataDirectory ?? '';
  setMonitors([...document.getElementById('endpoint').options].filter(o => o.value).map(o => ({endpoint: o.value, serverName: o.text.split(' · ')[0]})));
  document.getElementById('autoWarnings').checked = settings.autoWarnings !== false;
  document.getElementById('clockFormat').value = settings.clockFormat ?? 'app';
  document.getElementById('sunrise').value = settings.sunrise ?? 6;
  document.getElementById('sunset').value = settings.sunset ?? 18;
  for (const checkbox of document.querySelectorAll('#pages input')) checkbox.checked = !Array.isArray(settings.pages) || settings.pages.includes(checkbox.value);
  document.getElementById('pages').hidden = pinned;
  initialized = true;
}
function save() {
  if (!initialized || socket?.readyState !== WebSocket.OPEN) return;
  const sunrise = Number(document.getElementById('sunrise').value), sunset = Number(document.getElementById('sunset').value);
  const pages = [...document.querySelectorAll('#pages input:checked')].map(input => input.value);
  const valid = Number.isInteger(sunrise) && Number.isInteger(sunset) && sunrise >= 0 && sunset <= 23 && sunrise < sunset && pages.length > 0;
  document.getElementById('validation').textContent = valid ? '' : 'Select at least one view. Daytime start must be before nighttime start (0–23).';
  if (!valid) return;
  settings = { ...settings, clockFormat: document.getElementById('clockFormat').value, view: document.getElementById('view').value, label: document.getElementById('label').value,
    dataDirectory: document.getElementById('dataDirectory').value.trim(), endpoint: document.getElementById('endpoint').value, autoWarnings: document.getElementById('autoWarnings').checked, sunrise, sunset, pages };
  socket.send(JSON.stringify({ event: 'setSettings', context, payload: settings }));
}
window.connectElgatoStreamDeckSocket = function(port, uuid, registerEvent, info, actionInfo) {
  context = uuid;
  const action = JSON.parse(actionInfo); actionUuid = action.action; pinned = action.action.endsWith('.pinned');
  socket = new WebSocket('ws://127.0.0.1:' + Number(port));
  socket.onopen = () => { socket.send(JSON.stringify({ event: registerEvent, uuid })); apply(action.payload.settings); reloadMonitors(); };
  socket.onmessage = event => { const message = JSON.parse(event.data); if (message.event === 'didReceiveSettings') apply(message.payload.settings); else if (message.event === 'sendToPropertyInspector') setMonitors(message.payload.monitors); };
};
document.getElementById('reloadMonitors').addEventListener('click', reloadMonitors);
for (const id of ids) document.getElementById(id).addEventListener('change', save);
for (const checkbox of document.querySelectorAll('#pages input')) checkbox.addEventListener('change', save);
