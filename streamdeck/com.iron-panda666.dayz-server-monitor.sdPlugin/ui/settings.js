let socket, context, settings = {}, initialized = false, pinned = false;
const ids = ['view', 'label', 'dataDirectory', 'autoWarnings', 'sunrise', 'sunset'];
function apply(value) {
  settings = value ?? {}; initialized = false;
  document.getElementById('view').value = settings.view ?? 'restart';
  document.getElementById('label').value = settings.label ?? '';
  document.getElementById('dataDirectory').value = settings.dataDirectory ?? '';
  document.getElementById('autoWarnings').checked = settings.autoWarnings !== false;
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
  settings = { ...settings, view: document.getElementById('view').value, label: document.getElementById('label').value,
    dataDirectory: document.getElementById('dataDirectory').value.trim(), autoWarnings: document.getElementById('autoWarnings').checked, sunrise, sunset, pages };
  socket.send(JSON.stringify({ event: 'setSettings', context, payload: settings }));
}
window.connectElgatoStreamDeckSocket = function(port, uuid, registerEvent, info, actionInfo) {
  context = uuid;
  const action = JSON.parse(actionInfo); pinned = action.action.endsWith('.pinned');
  socket = new WebSocket('ws://127.0.0.1:' + Number(port));
  socket.onopen = () => { socket.send(JSON.stringify({ event: registerEvent, uuid })); apply(action.payload.settings); };
  socket.onmessage = event => { const message = JSON.parse(event.data); if (message.event === 'didReceiveSettings') apply(message.payload.settings); };
};
for (const id of ids) document.getElementById(id).addEventListener('change', save);
for (const checkbox of document.querySelectorAll('#pages input')) checkbox.addEventListener('change', save);
