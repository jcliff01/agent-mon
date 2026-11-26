'use strict';
// Agent Mon dashboard.
//
// Two ways to feed it:
//  1. Opened from the Agent Mon server (/dashboard/): enter the read-only key, pick an agent, and it
//     polls the server every 5 seconds. The key is kept in sessionStorage for this tab only.
//  2. Embedded by an authenticated integration (for example a Finesse gadget) that calls
//     window.AgentMon.render(samples). That hides the connect form.
//
// Never put the collector's write key (AGENTMON_API_KEY) in a page.
(function () {
  const MAX_AGE_MS = 15000;
  const POLL_MS = 5000;
  const HIGH_LATENCY_MS = 250;
  const KEY_STORE = 'agentmon-read-key';
  const AGENT_STORE = 'agentmon-agent';
  const defaultTargets = Object.freeze(['Nest', 'Finesse']);

  let latestSamples = [];
  let pollTimer = null;
  let embedded = false;

  const $ = (id) => document.getElementById(id);

  function element(tag, cls, text) {
    const el = document.createElement(tag);
    if (cls) el.className = cls;
    if (text !== undefined) el.textContent = String(text);
    return el;
  }

  function normalize(raw) {
    if (!Array.isArray(raw)) return [];
    return raw
      .filter((s) => s && typeof s.target === 'string' &&
        typeof s.timestamp === 'string' && Number.isFinite(Date.parse(s.timestamp)) &&
        typeof s.reachable === 'boolean' &&
        (s.latencyMs == null || (typeof s.latencyMs === 'number' && Number.isFinite(s.latencyMs) && s.latencyMs >= 0)))
      .sort((a, b) => Date.parse(a.timestamp) - Date.parse(b.timestamp))
      .slice(-120);
  }

  function describe(sample, now) {
    const fresh = sample && Math.abs(now - Date.parse(sample.timestamp)) <= MAX_AGE_MS;
    if (!fresh) return { fresh, status: 'No recent data', color: 'muted' };
    if (!sample.reachable) return { fresh, status: sample.error ? 'Unreachable (' + sample.error + ')' : 'Unreachable', color: 'bad' };
    if (sample.latencyMs > HIGH_LATENCY_MS) return { fresh, status: 'Slow', color: 'warn' };
    return { fresh, status: 'Connected', color: 'good' };
  }

  function draw() {
    const container = $('targets');
    container.replaceChildren();
    const now = Date.now();
    let healthy = 0, problems = 0;
    const names = latestSamples.length ? [...new Set(latestSamples.map((s) => s.target))] : defaultTargets;
    for (const name of names) {
      const sample = latestSamples.filter((s) => s.target === name).at(-1);
      const { fresh, status, color } = describe(sample, now);
      if (fresh) { if (sample.reachable) healthy++; else problems++; }
      const card = element('article', 'target');
      card.append(
        element('h3', '', name),
        element('div', 'latency ' + color, fresh && sample.latencyMs != null ? Math.round(sample.latencyMs) + ' ms' : '—'),
        element('p', color, status));
      container.append(card);
    }

    const overall = $('overall');
    overall.textContent = problems ? 'Connection issue' : healthy === names.length ? 'Monitoring live' : 'Waiting for measurements';
    overall.className = problems ? 'bad' : healthy === names.length ? 'good' : 'muted';

    const table = $('samples');
    table.replaceChildren();
    for (const s of latestSamples.slice(-20).reverse()) {
      const row = element('tr');
      for (const value of [
        new Date(s.timestamp).toLocaleTimeString(),
        s.target,
        s.reachable ? 'Reachable' : 'Unreachable' + (s.error ? ' (' + s.error + ')' : ''),
        s.latencyMs == null ? '—' : Math.round(s.latencyMs) + ' ms'
      ]) {
        row.append(element('td', '', value));
      }
      table.append(row);
    }
    const last = latestSamples.at(-1);
    $('freshness').textContent = last ? 'Last measurement: ' + new Date(last.timestamp).toLocaleTimeString() : 'No measurements received';
  }

  function render(samples) {
    if (!embedded) {
      embedded = true;
      stopPolling();
      $('connect').hidden = true;
    }
    latestSamples = normalize(samples);
    draw();
  }

  // ---------- connecting to the Agent Mon server ----------
  function store(key, value) {
    try { value == null ? sessionStorage.removeItem(key) : sessionStorage.setItem(key, value); } catch (e) { /* storage blocked */ }
  }
  function recall(key) {
    try { return sessionStorage.getItem(key); } catch (e) { return null; }
  }

  async function api(path, key) {
    const response = await fetch(path, { headers: { 'X-AgentMon-Key': key }, cache: 'no-store' });
    if (response.status === 401) throw new Error('That key was refused.');
    if (!response.ok) throw new Error('The server answered ' + response.status + '.');
    return response.json();
  }

  function setStatus(text) { $('connect-status').textContent = text; }

  async function loadAgents(key) {
    const agents = await api('../api/v1/agents', key);
    const select = $('agent');
    select.replaceChildren();
    if (!agents.length) {
      select.append(new Option('No agents have reported yet', ''));
      select.disabled = true;
      return false;
    }
    for (const a of agents) select.append(new Option(a.agentId, a.agentId));
    const remembered = recall(AGENT_STORE);
    if (remembered && agents.some((a) => a.agentId === remembered)) select.value = remembered;
    select.disabled = false;
    return true;
  }

  async function poll() {
    const key = recall(KEY_STORE);
    const agent = $('agent').value;
    if (!key || !agent || embedded) return;
    try {
      const samples = await api('../api/v1/agents/' + encodeURIComponent(agent) + '/samples', key);
      if (embedded) return;
      latestSamples = normalize(samples);
      draw();
      setStatus('Updated ' + new Date().toLocaleTimeString());
    } catch (error) {
      setStatus(error.message);
    }
  }

  function startPolling() {
    stopPolling();
    poll();
    pollTimer = setInterval(poll, POLL_MS);
    $('disconnect-btn').hidden = false;
  }

  function stopPolling() {
    if (pollTimer) clearInterval(pollTimer);
    pollTimer = null;
  }

  async function connect(event) {
    event.preventDefault();
    const key = $('key').value.trim() || recall(KEY_STORE);
    if (!key) return;
    setStatus('Connecting…');
    try {
      const hasAgents = await loadAgents(key);
      store(KEY_STORE, key);
      $('key').value = '';
      $('key').placeholder = 'Saved for this tab';
      $('key').required = false;
      if (hasAgents) {
        store(AGENT_STORE, $('agent').value);
        startPolling();
      } else {
        setStatus('Connected. No agents have reported yet; try again once a collector is running.');
      }
    } catch (error) {
      store(KEY_STORE, null);
      setStatus(error.message);
    }
  }

  function disconnect() {
    stopPolling();
    store(KEY_STORE, null);
    store(AGENT_STORE, null);
    latestSamples = [];
    $('agent').replaceChildren(new Option('Enter the key first', ''));
    $('agent').disabled = true;
    $('key').placeholder = '';
    $('key').required = true;
    $('disconnect-btn').hidden = true;
    setStatus('Disconnected.');
    draw();
  }

  $('connect').addEventListener('submit', connect);
  $('disconnect-btn').addEventListener('click', disconnect);
  $('agent').addEventListener('change', () => {
    store(AGENT_STORE, $('agent').value);
    startPolling();
  });

  // Reconnect automatically after a page reload in the same tab.
  if (recall(KEY_STORE)) {
    loadAgents(recall(KEY_STORE))
      .then((hasAgents) => {
        $('key').placeholder = 'Saved for this tab';
        $('key').required = false;
        if (hasAgents) startPolling();
      })
      .catch((error) => setStatus(error.message));
  }

  window.AgentMon = Object.freeze({ render });
  draw();
  // Refresh how old the measurements look, without fetching anything.
  setInterval(draw, 1000);
})();
