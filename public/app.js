/* Catch the Troublemaker — browser client.
 * The server owns the simulation; this file only renders snapshots and sends intent. */

const I18N = {
  en: {
    brand: 'Catch the Troublemaker', contrast: 'High contrast',
    eyebrow: 'A shared-map party game · 2–6 players',
    heroTitle: 'One kid. Fifty coins. Two very tired parents.',
    heroLede: 'Two families share one random map. Parents can only catch their own child. Troublemakers just want to fill their pockets and disappear.',
    yourName: 'Your name', namePlaceholder: 'e.g. Marta', codePlaceholder: 'ABCDE',
    createRoom: 'Create room', joinRoom: 'Join room',
    rulesTitle: 'The rules',
    rulesBody: 'A troublemaker wins the moment they hold 50 coins. Parents win by grounding both troublemakers — or by running out the clock.',
    catchTitle: 'Family only',
    catchBody: 'Dad A and Mum A can catch Troublemaker A only. Wrong-family tags do nothing. Each kid has three lives and drops every coin when caught.',
    balanceTitle: 'Keeping it fair',
    balanceBody: 'Kids run faster; parents sprint harder and can confiscate coins. Empty seats become bots, and nobody starts next to their own parents.',
    waitingRoom: 'Waiting room', lobbyTitle: 'Pick your seat at the table',
    roomCode: 'Room code', copy: 'Copy', copied: 'Copied!',
    lobbyHint: 'Share the code. Unclaimed roles are played by bots, so two people are enough to start.',
    players: 'Players', chooseRole: 'Choose your role',
    chooseFamilies: 'Choose the two families',
    familyHint: 'Host picks two different families. Click a chosen one to remove it.',
    familyA: 'A', familyB: 'B', needFamilies: 'Choose two different families before starting.',
    startGame: 'Start game', leave: 'Leave room', free: 'Free · bot',
    gameTitle: 'Game in progress', youAre: 'You are', spectator: 'Spectator',
    teamA: 'Family A', teamB: 'Family B', timeLeft: 'Time left', sprint: 'Sprint',
    sprinting: 'SPRINTING!', ready: 'Ready',
    moveHint: 'move', sprintHint: 'sprint', touchHint: 'Touch controls below on mobile',
    sprintShort: 'SPRINT', canvasAria: 'Game map showing both families, coins and obstacles',
    gameOver: 'Game over', playAgain: 'Play again', backHome: 'Back home',
    parentsWin: 'The parents win!', kidsWin: 'gets away with it!',
    reasonGoal: 'Fifty coins, straight into the pockets.',
    reasonCaught: 'Both troublemakers were grounded.',
    reasonTimeout: 'Time ran out before the coin goal.',
    statCoins: 'Coins collected', statCatches: 'Catches', statConfiscated: 'Confiscated',
    statTime: 'Duration', statLives: 'lives', statCoinsShort: 'coins',
    plusCoin: '+1 coin', caughtToast: 'Caught! Coins dropped', confiscated: 'Coin confiscated',
    footer: 'Built as an AI-assisted project · Node.js + Server-Sent Events · no external dependencies',
  },
  ca: {
    brand: 'Catch the Troublemaker', contrast: 'Alt contrast',
    eyebrow: 'Un joc de mapa compartit · 2–6 jugadors',
    heroTitle: 'Un nen. Cinquanta monedes. Dos pares molt cansats.',
    heroLede: 'Dues famílies comparteixen un mapa aleatori. Els pares només poden agafar el seu fill. Els entremaliats només volen omplir-se les butxaques i desaparèixer.',
    yourName: 'El teu nom', namePlaceholder: 'p. ex. Marta', codePlaceholder: 'ABCDE',
    createRoom: 'Crear sala', joinRoom: 'Unir-se',
    rulesTitle: 'Les regles',
    rulesBody: 'Un entremaliat guanya en arribar a 50 monedes. Els pares guanyen si atrapen els dos entremaliats o si s’acaba el temps.',
    catchTitle: 'Només la família',
    catchBody: 'El Pare A i la Mare A només poden agafar l’Entremaliat A. Les etiquetes d’altra família no serveixen. Cada nen té tres vides i perd totes les monedes en ser atrapat.',
    balanceTitle: 'Equilibri',
    balanceBody: 'Els nens corren més; els pares esprinten més i poden confiscar monedes. Els llocs buits els juguen bots i ningú comença al costat dels seus pares.',
    waitingRoom: 'Sala d’espera', lobbyTitle: 'Tria el teu lloc a la taula',
    roomCode: 'Codi de sala', copy: 'Copiar', copied: 'Copiat!',
    lobbyHint: 'Comparteix el codi. Els rols lliures els juguen bots, així que dues persones són suficients per començar.',
    players: 'Jugadors', chooseRole: 'Tria el teu rol',
    chooseFamilies: 'Tria les dues famílies',
    familyHint: 'L’amfitrió tria dues famílies diferents. Prem una de triada per treure-la.',
    familyA: 'A', familyB: 'B', needFamilies: 'Tria dues famílies diferents abans de començar.',
    startGame: 'Començar', leave: 'Sortir', free: 'Lliure · bot',
    gameTitle: 'Partida en curs', youAre: 'Ets', spectator: 'Espectador',
    teamA: 'Família A', teamB: 'Família B', timeLeft: 'Temps restant', sprint: 'Esprint',
    sprinting: 'ESPRINTANT!', ready: 'A punt',
    moveHint: 'moure’s', sprintHint: 'esprintar', touchHint: 'Controls tàctils a sota al mòbil',
    sprintShort: 'ESPRINT', canvasAria: 'Mapa de la partida amb les dues famílies, monedes i obstacles',
    gameOver: 'Fi de la partida', playAgain: 'Jugar de nou', backHome: 'Tornar a l’inici',
    parentsWin: 'Els pares guanyen!', kidsWin: 'se’n surt!',
    reasonGoal: 'Cinquanta monedes directes a les butxaques.',
    reasonCaught: ' els dos entremaliats han estat posats a terra.',
    reasonTimeout: 'El temps s’ha acabat abans de la meta.',
    statCoins: 'Monedes recollides', statCatches: 'Captures', statConfiscated: 'Confiscades',
    statTime: 'Durada', statLives: 'vides', statCoinsShort: 'monedes',
    plusCoin: '+1 moneda', caughtToast: 'Atrapat! Monedes perdudes', confiscated: 'Moneda confiscada',
    footer: 'Projecte desenvolupat amb IA · Node.js + Server-Sent Events · sense dependències externes',
  },
};

const FALLBACK_ROLES = {
  fatherA: { short: 'FA', shape: 'square', color: '#ff8a5b', name: { en: 'Dad A', ca: 'Pare A' } },
  motherA: { short: 'MA', shape: 'triangle', color: '#ffd166', name: { en: 'Mum A', ca: 'Mare A' } },
  troublemakerA: { short: 'TA', shape: 'star', color: '#ef476f', name: { en: 'Troublemaker A', ca: 'Entremaliat A' } },
  fatherB: { short: 'FB', shape: 'square', color: '#4cc9f0', name: { en: 'Dad B', ca: 'Pare B' } },
  motherB: { short: 'MB', shape: 'triangle', color: '#8ac4ff', name: { en: 'Mum B', ca: 'Mare B' } },
  troublemakerB: { short: 'TB', shape: 'star', color: '#9b5de5', name: { en: 'Troublemaker B', ca: 'Entremaliat B' } },
};
const FALLBACK_FAMILIES = ['ClassicCaillou', 'EmilyAnderson', 'JonathanJohnson', 'LittleBill']
  .map((id) => ({
    id,
    name: id.replace(/([a-z0-9])([A-Z])/g, '$1 $2'),
    sprites: {
      troublemaker: `/characters/Troublemakers/${id}/Troublemaker.png`,
      father: `/characters/Troublemakers/${id}/Father.png`,
      mother: `/characters/Troublemakers/${id}/Mother.png`,
    },
  }));
const TEAM_NAMES = {
  parents: { en: 'Parents', ca: 'Pares' },
  troublemakers: { en: 'Troublemakers', ca: 'Entremaliats' },
};

const $ = (id) => document.getElementById(id);
const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (ch) => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
}[ch]));

const state = {
  lang: localStorage.getItem('ctt-lang') === 'ca' ? 'ca' : 'en',
  code: localStorage.getItem('ctt-code') || null,
  playerId: localStorage.getItem('ctt-playerId') || null,
  lobby: null,
  map: null,
  snapshot: null,
  prevSnapshot: null,
  config: { sprint: { cooldownMs: 6500 } },
  roles: FALLBACK_ROLES,
  families: FALLBACK_FAMILIES,
  screen: 'home',
  isHost: false,
  role: null,
  source: null,
  pollTimer: null,
  keys: new Set(),
  touch: {},
  lastInputSig: '',
  lastInputAt: 0,
  renderPositions: new Map(),
  particles: [],
  seenEvents: new Set(),
  lastResult: null,
};

const t = (key) => I18N[state.lang][key] ?? I18N.en[key] ?? key;
const roleMeta = (role) => state.roles[role] || FALLBACK_ROLES[role] || FALLBACK_ROLES.fatherA;
const roleName = (role) => roleMeta(role).name?.[state.lang] || role;
const teamName = (team) => TEAM_NAMES[team]?.[state.lang] || team;
const glyphFor = (shape) => ({ square: '■', triangle: '▲', star: '★' }[shape] || '●');

function applyI18n() {
  document.documentElement.lang = state.lang;
  document.querySelectorAll('[data-i18n]').forEach((el) => { el.textContent = t(el.dataset.i18n); });
  document.querySelectorAll('[data-i18n-placeholder]').forEach((el) => { el.placeholder = t(el.dataset.i18nPlaceholder); });
  document.querySelectorAll('[data-i18n-aria]').forEach((el) => { el.setAttribute('aria-label', t(el.dataset.i18nAria)); });
  $('btn-lang').textContent = state.lang === 'en' ? 'CA / EN' : 'EN / CA';
}

function showScreen(name) {
  state.screen = name;
  document.querySelectorAll('.screen').forEach((s) => s.classList.toggle('active', s.id === `screen-${name}`));
}

async function api(method, route, body) {
  const res = await fetch(`/api${route}`, {
    method,
    headers: body ? { 'Content-Type': 'application/json' } : {},
    body: body ? JSON.stringify(body) : undefined,
  });
  let data = {};
  try { data = await res.json(); } catch { /* empty response */ }
  if (!res.ok) {
    const error = new Error(data.error || `HTTP ${res.status}`);
    error.status = res.status;
    throw error;
  }
  return data;
}

/* ---------------- Lobby flow ---------------- */

async function createRoom() {
  const name = $('player-name').value.trim() || 'Player';
  try {
    const data = await api('POST', '/rooms', { name });
    state.code = data.code;
    state.playerId = data.playerId;
    state.lobby = data.lobby;
    localStorage.setItem('ctt-code', state.code);
    localStorage.setItem('ctt-playerId', state.playerId);
    localStorage.setItem('ctt-name', name);
    $('home-error').textContent = '';
    showScreen('lobby');
    renderLobby();
    connectStream();
  } catch (err) {
    $('home-error').textContent = err.message;
  }
}

async function joinRoom() {
  const code = $('join-code').value.trim().toUpperCase();
  const name = $('player-name').value.trim() || 'Player';
  if (code.length !== 5) {
    $('home-error').textContent = state.lang === 'ca' ? 'El codi té 5 caràcters.' : 'Room codes have 5 characters.';
    return;
  }
  try {
    const data = await api('POST', `/rooms/${code}/join`, { name, playerId: state.playerId });
    state.code = code;
    state.playerId = data.playerId;
    state.lobby = data.lobby;
    localStorage.setItem('ctt-code', code);
    localStorage.setItem('ctt-playerId', data.playerId);
    localStorage.setItem('ctt-name', name);
    $('home-error').textContent = '';
    showScreen('lobby');
    renderLobby();
    connectStream();
  } catch (err) {
    $('home-error').textContent = err.message;
  }
}

async function resumeRoom() {
  if (!state.code || !state.playerId) return false;
  const name = localStorage.getItem('ctt-name') || 'Player';
  try {
    const data = await api('POST', `/rooms/${state.code}/join`, { name, playerId: state.playerId });
    state.lobby = data.lobby;
    showScreen('lobby');
    renderLobby();
    connectStream();
    return true;
  } catch (err) {
    // A finished/deleted room produces a 404/409 on join: try a read-only state
    // request so a player can still watch or re-enter an ongoing match.
    if (err.status === 404 || err.status === 409) {
      try {
        const data = await api('GET', `/rooms/${state.code}/state`);
        if (!data.lobby?.players.some((p) => p.id === state.playerId)) {
          localStorage.removeItem('ctt-code');
          localStorage.removeItem('ctt-playerId');
          state.code = null;
          state.playerId = null;
          return false;
        }
        state.lobby = data.lobby;
        if (data.map) { state.map = data.map; state.snapshot = data; }
        connectStream();
        if (data.status === 'playing') enterGame();
        else if (data.result) showResult(data.result);
        else { showScreen('lobby'); renderLobby(); }
        return true;
      } catch { /* room really is gone */ }
    }
    localStorage.removeItem('ctt-code');
    state.code = null;
    return false;
  }
}

function renderLobby() {
  const lobby = state.lobby;
  if (!lobby) return;
  state.isHost = lobby.hostId === state.playerId;
  state.role = lobby.players.find((p) => p.id === state.playerId)?.role || null;
  $('room-code').textContent = lobby.code;

  const chosenFamilies = lobby.families || [];
  const startBtn = $('btn-start');
  startBtn.disabled = !state.isHost || chosenFamilies.length !== 2;
  startBtn.title = !state.isHost
    ? (state.lang === 'ca' ? 'Només l’amfitrió' : 'Host only')
    : chosenFamilies.length !== 2 ? t('needFamilies') : '';
  renderFamilyGrid();

  $('player-list').innerHTML = lobby.players.map((p) => `
    <li>
      <span class="role-glyph" style="background:${esc(roleMeta(p.role).color)}">${esc(glyphFor(roleMeta(p.role).shape))}</span>
      <span class="who">${esc(p.name)}</span>
      ${p.id === lobby.hostId ? `<span class="host-badge">${state.lang === 'ca' ? 'AMFITRIÓ' : 'HOST'}</span>` : ''}
      <span class="tag">${esc(roleName(p.role))}</span>
    </li>`).join('');

  $('role-grid').innerHTML = lobby.roles.map((slot) => {
    const owner = slot.takenBy ? lobby.players.find((p) => p.id === slot.takenBy) : null;
    const selected = slot.takenBy === state.playerId;
    return `
      <button class="role-btn ${selected ? 'selected' : ''} ${owner && !selected ? 'taken' : ''}"
              data-role="${esc(slot.role)}" aria-pressed="${selected}">
        <span class="role-glyph" style="background:${esc(slot.color)}">${esc(glyphFor(roleMeta(slot.role).shape))}</span>
        <span class="role-meta">
          <strong>${esc(roleName(slot.role))}</strong>
          <small>${esc(teamName(slot.team))} · ${owner ? esc(owner.name) : esc(t('free'))}</small>
        </span>
      </button>`;
  }).join('');
}

function renderFamilyGrid() {
  const grid = $('family-grid');
  if (!grid || !state.lobby) return;
  const chosen = state.lobby.families || [];
  grid.innerHTML = state.families.map((family) => {
    const slot = chosen.indexOf(family.id);
    const canPick = state.isHost && (slot >= 0 || chosen.length < 2);
    const badge = slot === 0 ? t('familyA') : slot === 1 ? t('familyB') : '';
    return `
      <button class="family-btn ${slot >= 0 ? 'selected' : ''}"
              data-family="${esc(family.id)}" aria-pressed="${slot >= 0}"
              ${canPick ? '' : 'disabled'}>
        <span class="family-slot">${esc(badge)}</span>
        <span class="family-sprites">
          <img src="${esc(family.sprites.troublemaker)}" alt="" />
          <img src="${esc(family.sprites.father)}" alt="" />
          <img src="${esc(family.sprites.mother)}" alt="" />
        </span>
        <strong>${esc(family.name)}</strong>
      </button>`;
  }).join('');
}

async function toggleFamily(id) {
  const chosen = state.lobby?.families || [];
  const next = [];
  let wasSelected = false;
  for (const family of chosen) {
    if (family === id) { wasSelected = true; continue; }
    next.push(family);
  }
  if (!wasSelected && next.length < 2) next.push(id);
  try {
    const data = await api('POST', `/rooms/${state.code}/families`, {
      playerId: state.playerId, families: next,
    });
    state.lobby = data.lobby;
    renderLobby();
  } catch (err) {
    $('lobby-error').textContent = err.message;
  }
}

async function startGame() {
  try {
    await api('POST', `/rooms/${state.code}/start`, { playerId: state.playerId });
    const data = await api('GET', `/rooms/${state.code}/state`);
    state.map = data.map;
    state.snapshot = data;
    enterGame();
  } catch (err) {
    $('lobby-error').textContent = err.message;
  }
}

/* ---------------- Async transport ---------------- */

function onPacket(type, data) {
  if (type === 'hello') {
    if (data.lobby) { state.lobby = data.lobby; renderLobby(); }
    if (data.map) { state.map = data.map; state.snapshot = data; }
    if (data.status === 'playing') enterGame();
    else if (data.status === 'finished' && data.result) showResult(data.result);
  } else if (type === 'lobby') {
    state.lobby = data;
    renderLobby();
  } else if (type === 'start') {
    state.map = data.map;
    state.snapshot = data;
    enterGame();
  } else if (type === 'state') {
    // The /state poll also carries the lobby while the match has not started.
    // Without this, a broken SSE stream leaves the player counter frozen.
    if (data.lobby) {
      state.lobby = data.lobby;
      if (state.screen === 'lobby') renderLobby();
    }
    if (data.players) {
      state.prevSnapshot = state.snapshot;
      state.snapshot = data;
      if (data.map) state.map = data.map;
      processEvents(data.events || []);
      updateHUD(data);
    }
    if (data.status === 'finished' && data.result && state.screen !== 'result') showResult(data.result);
  } else if (type === 'result') {
    showResult(data.result);
  }
}

function startPolling() {
  if (state.pollTimer) return;
  const poll = async () => {
    try {
      const data = await api('GET', `/rooms/${state.code}/state`);
      onPacket('state', data);
      const result = await api('GET', `/rooms/${state.code}/result`);
      if (result.finished) onPacket('result', result);
    } catch { /* server may be restarting */ }
  };
  poll();
  state.pollTimer = setInterval(poll, 250);
}

function connectStream() {
  if (state.source) { state.source.close(); state.source = null; }
  if (state.pollTimer) { clearInterval(state.pollTimer); state.pollTimer = null; }
  if (typeof EventSource === 'undefined') { startPolling(); return; }

  const url = `/api/rooms/${state.code}/events?playerId=${encodeURIComponent(state.playerId)}`;
  const source = new EventSource(url);
  state.source = source;
  ['hello', 'lobby', 'start', 'state', 'result'].forEach((name) => {
    source.addEventListener(name, (event) => {
      try { onPacket(name, JSON.parse(event.data)); } catch { /* malformed packet */ }
    });
  });
  source.onerror = () => {
    if (source.readyState === EventSource.CLOSED) {
      source.close();
      if (state.source === source) state.source = null;
      startPolling(); // asynchronous HTTP fallback
    }
  };
}

/* ---------------- Input ---------------- */

function currentIntent() {
  const k = state.keys;
  let dx = 0; let dy = 0;
  if (k.has('a') || k.has('arrowleft') || state.touch.left) dx -= 1;
  if (k.has('d') || k.has('arrowright') || state.touch.right) dx += 1;
  if (k.has('w') || k.has('arrowup') || state.touch.up) dy -= 1;
  if (k.has('s') || k.has('arrowdown') || state.touch.down) dy += 1;
  const sprint = k.has(' ') || k.has('shift') || !!state.touch.sprint;
  return { dx, dy, sprint };
}

function bindInput() {
  window.addEventListener('keydown', (e) => {
    const key = e.key.toLowerCase();
    if (state.screen === 'game' && ['arrowup', 'arrowdown', 'arrowleft', 'arrowright', ' '].includes(key)) e.preventDefault();
    state.keys.add(key);
  });
  window.addEventListener('keyup', (e) => state.keys.delete(e.key.toLowerCase()));
  window.addEventListener('blur', () => { state.keys.clear(); state.touch = {}; });

  document.querySelectorAll('.pad[data-dir]').forEach((btn) => {
    const dir = btn.dataset.dir;
    const down = (e) => { e.preventDefault(); state.touch[dir] = true; btn.classList.add('held'); };
    const up = () => { state.touch[dir] = false; btn.classList.remove('held'); };
    btn.addEventListener('pointerdown', down);
    btn.addEventListener('pointerup', up);
    btn.addEventListener('pointercancel', up);
    btn.addEventListener('pointerleave', up);
  });
  const sprintBtn = $('btn-sprint');
  sprintBtn.addEventListener('pointerdown', (e) => {
    e.preventDefault(); state.touch.sprint = true; sprintBtn.classList.add('held');
  });
  ['pointerup', 'pointercancel', 'pointerleave'].forEach((evt) =>
    sprintBtn.addEventListener(evt, () => { state.touch.sprint = false; sprintBtn.classList.remove('held'); }));

  setInterval(() => {
    if (state.screen !== 'game' || !state.snapshot || !state.code || !state.playerId) return;
    const intent = currentIntent();
    const sig = `${intent.dx}|${intent.dy}|${intent.sprint}`;
    if (sig !== state.lastInputSig || Date.now() - state.lastInputAt > 1200) {
      state.lastInputSig = sig;
      state.lastInputAt = Date.now();
      api('POST', `/rooms/${state.code}/input`, { playerId: state.playerId, ...intent }).catch(() => {});
    }
  }, 60);
}

/* ---------------- Rendering ---------------- */

const canvas = $('game-canvas');
const ctx = canvas.getContext('2d');

function enterGame() {
  showScreen('game');
  state.renderPositions = new Map();
  state.particles = [];
  state.lastInputSig = '';
  if (state.snapshot) updateHUD(state.snapshot);
}

function addParticles(x, y, color, count) {
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  for (let i = 0; i < count; i++) {
    const angle = Math.random() * Math.PI * 2;
    const speed = 0.6 + Math.random() * 1.8;
    state.particles.push({
      x, y, vx: Math.cos(angle) * speed, vy: Math.sin(angle) * speed,
      life: 1, color,
    });
  }
}

function processEvents(events) {
  for (const ev of events) {
    const key = `${ev.type}:${ev.now}:${ev.id || ''}:${ev.by || ''}`;
    if (state.seenEvents.has(key)) continue;
    state.seenEvents.add(key);
    if (state.seenEvents.size > 200) state.seenEvents.clear();

    if (ev.type === 'coin') {
      addParticles(ev.x, ev.y, '#ffd166', 7);
      if (ev.id === state.playerId) toast(t('plusCoin'), 'coin');
    } else if (ev.type === 'catch') {
      addParticles(ev.x, ev.y, '#ef476f', 20);
      toast(`${roleName(roleOf(ev.id))}: ${t('caughtToast')} (${ev.dropped})`, 'catch');
    } else if (ev.type === 'confiscate') {
      addParticles(ev.x, ev.y, '#4cc9f0', 6);
    } else if (ev.type === 'finish') {
      addParticles(ev.x || 10, ev.y || 10, '#ffffff', 24);
    }
  }
}

function roleOf(playerId) {
  return state.snapshot?.players.find((p) => p.id === playerId)?.role || 'fatherA';
}

function toast(text, cls = '') {
  const el = document.createElement('div');
  el.className = `toast ${cls}`;
  el.textContent = text.replace(/<[^>]*>/g, '');
  $('toasts').appendChild(el);
  setTimeout(() => el.remove(), 2600);
  while ($('toasts').children.length > 5) $('toasts').firstElementChild.remove();
}

function roundRect(c, x, y, w, h, r) {
  const radius = Math.min(r, w / 2, h / 2);
  c.beginPath();
  c.moveTo(x + radius, y);
  c.arcTo(x + w, y, x + w, y + h, radius);
  c.arcTo(x + w, y + h, x, y + h, radius);
  c.arcTo(x, y + h, x, y, radius);
  c.arcTo(x, y, x + w, y, radius);
  c.closePath();
}

function drawShape(c, shape, x, y, size) {
  c.beginPath();
  if (shape === 'square') {
    roundRect(c, x - size, y - size, size * 2, size * 2, size * 0.35);
  } else if (shape === 'triangle') {
    c.moveTo(x, y - size * 1.15);
    c.lineTo(x + size, y + size * 0.85);
    c.lineTo(x - size, y + size * 0.85);
    c.closePath();
  } else {
    for (let i = 0; i < 10; i++) {
      const angle = -Math.PI / 2 + (i * Math.PI) / 5;
      const radius = i % 2 === 0 ? size * 1.15 : size * 0.5;
      const px = x + Math.cos(angle) * radius;
      const py = y + Math.sin(angle) * radius;
      if (i === 0) c.moveTo(px, py); else c.lineTo(px, py);
    }
    c.closePath();
  }
  c.fill();
  c.stroke();
}

function renderPosition(player, scale) {
  let pos = state.renderPositions.get(player.id);
  if (!pos) { pos = { x: player.x, y: player.y }; state.renderPositions.set(player.id, pos); }
  const factor = player.id === state.playerId ? 0.45 : 0.3;
  pos.x += (player.x - pos.x) * factor;
  pos.y += (player.y - pos.y) * factor;
  return pos;
}

function draw(now) {
  requestAnimationFrame(draw);
  if (state.screen !== 'game' || !state.snapshot || !state.map) return;

  const { cols, rows, tiles } = state.map;
  const W = canvas.width;
  const H = canvas.height;
  const scale = Math.min(W / cols, H / rows);
  const ox = (W - cols * scale) / 2;
  const oy = (H - rows * scale) / 2;

  ctx.clearRect(0, 0, W, H);
  const bg = ctx.createLinearGradient(0, 0, W, H);
  bg.addColorStop(0, '#131a3c');
  bg.addColorStop(1, '#0b0f27');
  ctx.fillStyle = bg;
  ctx.fillRect(0, 0, W, H);

  // Tiles: subtle dots on free space, rounded blocks on walls.
  ctx.fillStyle = 'rgba(255,255,255,0.055)';
  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < cols; c++) {
      if (tiles[r * cols + c] === '1' || tiles[r * cols + c] === 1) continue;
      ctx.beginPath();
      ctx.arc(ox + (c + 0.5) * scale, oy + (r + 0.5) * scale, Math.max(1, scale * 0.045), 0, Math.PI * 2);
      ctx.fill();
    }
  }
  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < cols; c++) {
      if (tiles[r * cols + c] !== '1' && tiles[r * cols + c] !== 1) continue;
      const x = ox + c * scale;
      const y = oy + r * scale;
      roundRect(ctx, x + scale * 0.06, y + scale * 0.06, scale * 0.88, scale * 0.88, scale * 0.18);
      ctx.fillStyle = '#2b3768';
      ctx.fill();
      ctx.strokeStyle = 'rgba(150,175,255,0.18)';
      ctx.lineWidth = 1;
      ctx.stroke();
    }
  }

  // Coins.
  for (const coin of state.snapshot.coins) {
    const bob = Math.sin(now / 380 + coin.id * 0.7) * scale * 0.06;
    const x = ox + coin.x * scale;
    const y = oy + coin.y * scale + bob;
    const radius = scale * 0.15;
    const glow = ctx.createRadialGradient(x, y, 0, x, y, radius * 2.2);
    glow.addColorStop(0, 'rgba(255,230,140,0.55)');
    glow.addColorStop(1, 'rgba(255,209,102,0)');
    ctx.fillStyle = glow;
    ctx.fillRect(x - radius * 2.2, y - radius * 2.2, radius * 4.4, radius * 4.4);
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, Math.PI * 2);
    ctx.fillStyle = '#ffd166';
    ctx.fill();
    ctx.strokeStyle = '#a86b00';
    ctx.lineWidth = Math.max(1, scale * 0.025);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(x - radius * 0.3, y - radius * 0.3, radius * 0.22, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(255,255,255,0.85)';
    ctx.fill();
  }

  // Players.
  for (const p of state.snapshot.players) {
    if (p.eliminated) continue;
    const pos = renderPosition(p, scale);
    const x = ox + pos.x * scale;
    const y = oy + pos.y * scale;
    const size = scale * 0.34;
    ctx.save();
    ctx.globalAlpha = p.grace ? 0.42 + 0.35 * Math.sin(now / 90) : 1;
    ctx.fillStyle = p.color;
    ctx.strokeStyle = p.id === state.playerId ? '#ffffff' : 'rgba(8,10,28,0.85)';
    ctx.lineWidth = p.id === state.playerId ? Math.max(2, scale * 0.1) : Math.max(1, scale * 0.04);
    if (p.sprinting) {
      ctx.shadowColor = p.color;
      ctx.shadowBlur = scale * 0.8;
    }
    drawShape(ctx, p.shape, x, y, size);
    ctx.restore();

    ctx.font = `800 ${Math.max(9, scale * 0.36)}px system-ui, sans-serif`;
    ctx.textAlign = 'center';
    ctx.fillStyle = 'rgba(8,10,28,0.85)';
    ctx.fillText(roleMeta(p.role).short, x, y + scale * 0.1);
    ctx.fillStyle = p.color;
    ctx.fillText(roleMeta(p.role).short, x, y + scale * 0.08);

    if (p.team === 'troublemakers') {
      const label = `${p.coins}/${state.snapshot.goal}`;
      ctx.font = `800 ${Math.max(9, scale * 0.32)}px system-ui, sans-serif`;
      const width = ctx.measureText(label).width + scale * 0.3;
      roundRect(ctx, x - width / 2, y - scale * 0.95, width, scale * 0.42, scale * 0.12);
      ctx.fillStyle = 'rgba(10,13,34,0.88)';
      ctx.fill();
      ctx.fillStyle = '#ffd166';
      ctx.fillText(label, x, y - scale * 0.64);
    }
  }

  // Particles.
  for (let i = state.particles.length - 1; i >= 0; i--) {
    const p = state.particles[i];
    p.x += p.vx * 0.016;
    p.y += p.vy * 0.016;
    p.life -= 0.02;
    if (p.life <= 0) { state.particles.splice(i, 1); continue; }
    ctx.globalAlpha = p.life;
    ctx.fillStyle = p.color;
    ctx.beginPath();
    ctx.arc(ox + p.x * scale, oy + p.y * scale, scale * 0.08 * p.life, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.globalAlpha = 1;
}

function updateHUD(snap) {
  const me = snap.players.find((p) => p.id === state.playerId);
  if (!me) {
    $('hud-role').textContent = t('spectator');
    $('hud-lives').textContent = '';
    return;
  }
  const meta = roleMeta(me.role);
  $('hud-role').textContent = roleName(me.role);
  $('hud-role').style.background = meta.color;
  $('hud-role').style.color = '#10132a';

  const child = snap.players.find((p) => p.team === 'troublemakers' && p.family === me.family);
  if (me.team === 'troublemakers') {
    $('hud-lives').textContent = `${me.coins}/${snap.goal} ${t('statCoinsShort')} · ${me.lives} ${t('statLives')}`;
  } else if (child) {
    $('hud-lives').textContent = `${roleName(child.role)}: ${child.coins}/${snap.goal} ${t('statCoinsShort')} · ${child.lives} ${t('statLives')}`;
  }

  const mins = Math.floor(snap.remainingMs / 60000);
  const secs = Math.floor((snap.remainingMs % 60000) / 1000);
  $('hud-timer').textContent = `${mins}:${String(secs).padStart(2, '0')}`;

  const ta = snap.players.find((p) => p.role === 'troublemakerA');
  const tb = snap.players.find((p) => p.role === 'troublemakerB');
  $('score-a').textContent = ta ? ta.coins : 0;
  $('score-b').textContent = tb ? tb.coins : 0;
  $('bar-a').style.width = `${Math.min(100, ((ta?.coins || 0) / snap.goal) * 100)}%`;
  $('bar-b').style.width = `${Math.min(100, ((tb?.coins || 0) / snap.goal) * 100)}%`;

  const cooldown = state.config.sprint?.cooldownMs || 6500;
  $('sprint-fill').style.width = `${Math.max(0, 100 - (me.sprintIn / cooldown) * 100)}%`;
  $('sprint-label').textContent = me.sprinting ? t('sprinting') : (me.sprintIn > 0 ? `${t('sprint')} ${(me.sprintIn / 1000).toFixed(1)}s` : t('ready'));
}

function showResult(result) {
  if (!result) return;
  state.lastResult = result;
  showScreen('result');
  const card = document.querySelector('.result-card');
  card.classList.remove('win-parents', 'win-troublemakers');
  card.classList.add(result.winnerTeam === 'parents' ? 'win-parents' : 'win-troublemakers');

  const isParents = result.winnerTeam === 'parents';
  $('result-title').textContent = isParents
    ? t('parentsWin')
    : `${result.winnerName} ${t('kidsWin')}`;
  const reasonKey = {
    'reached the coin goal': 'reasonGoal',
    'both troublemakers were grounded': 'reasonCaught',
    'time ran out before the coin goal': 'reasonTimeout',
  }[result.reason];
  $('result-reason').textContent = reasonKey ? t(reasonKey) : result.reason;

  const seconds = Math.round(result.durationMs / 1000);
  $('result-stats').innerHTML = `
    <div class="stat"><strong>${seconds}s</strong><span>${t('statTime')}</span></div>
    <div class="stat"><strong>${result.stats.catches}</strong><span>${t('statCatches')}</span></div>
    <div class="stat"><strong>${result.stats.coinsCollected}</strong><span>${t('statCoins')}</span></div>
    <div class="stat"><strong>${result.stats.confiscated}</strong><span>${t('statConfiscated')}</span></div>`;
}

/* ---------------- Bootstrap ---------------- */

async function bootstrap() {
  applyI18n();
  $('player-name').value = localStorage.getItem('ctt-name') || '';

  try {
    const cfg = await api('GET', '/config');
    state.config = cfg.config;
    state.roles = cfg.roles;
    state.families = cfg.families || [];
  } catch { /* fall back to built-in values */ }

  $('btn-create').addEventListener('click', createRoom);
  $('btn-join').addEventListener('click', joinRoom);
  $('join-code').addEventListener('keydown', (e) => { if (e.key === 'Enter') joinRoom(); });

  $('btn-copy').addEventListener('click', async () => {
    if (!state.code) return;
    try { await navigator.clipboard.writeText(state.code); } catch { /* clipboard blocked */ }
    $('btn-copy').textContent = t('copied');
    setTimeout(() => { $('btn-copy').textContent = t('copy'); }, 1400);
  });

  $('role-grid').addEventListener('click', async (e) => {
    const btn = e.target.closest('.role-btn');
    if (!btn) return;
    try {
      const data = await api('POST', `/rooms/${state.code}/role`, { playerId: state.playerId, role: btn.dataset.role });
      state.lobby = data.lobby;
      renderLobby();
    } catch (err) { $('lobby-error').textContent = err.message; }
  });

  $('family-grid').addEventListener('click', (e) => {
    const btn = e.target.closest('.family-btn');
    if (btn && !btn.disabled) toggleFamily(btn.dataset.family);
  });

  $('btn-start').addEventListener('click', startGame);
  $('btn-leave').addEventListener('click', () => {
    state.source?.close(); state.source = null;
    if (state.pollTimer) { clearInterval(state.pollTimer); state.pollTimer = null; }
    localStorage.removeItem('ctt-code');
    state.code = null; state.lobby = null; state.snapshot = null;
    showScreen('home');
  });
  $('btn-again').addEventListener('click', async () => {
    try {
      await api('POST', `/rooms/${state.code}/restart`, { playerId: state.playerId });
      const data = await api('GET', `/rooms/${state.code}/state`);
      state.map = data.map;
      state.snapshot = data;
      state.lastResult = null;
      enterGame();
    } catch (err) { console.error(err); }
  });
  $('btn-home').addEventListener('click', () => {
    localStorage.removeItem('ctt-code');
    state.code = null; state.lobby = null; state.snapshot = null; state.map = null;
    showScreen('home');
  });

  $('btn-lang').addEventListener('click', () => {
    state.lang = state.lang === 'en' ? 'ca' : 'en';
    localStorage.setItem('ctt-lang', state.lang);
    applyI18n();
    if (state.lobby) renderLobby();
    if (state.snapshot) updateHUD(state.snapshot);
    if (state.lastResult) showResult(state.lastResult);
  });

  $('btn-contrast').addEventListener('click', (e) => {
    const on = document.body.classList.toggle('hc');
    e.currentTarget.setAttribute('aria-pressed', String(on));
    localStorage.setItem('ctt-contrast', String(on));
  });
  if (localStorage.getItem('ctt-contrast') === 'true') {
    document.body.classList.add('hc');
    $('btn-contrast').setAttribute('aria-pressed', 'true');
  }

  const setFont = (level) => {
    document.body.classList.remove('large', 'xlarge');
    if (level === 2) document.body.classList.add('xlarge');
    else if (level === 1) document.body.classList.add('large');
    localStorage.setItem('ctt-font', String(level));
  };
  let fontLevel = Number(localStorage.getItem('ctt-font') || 0);
  $('btn-font-plus').addEventListener('click', () => { fontLevel = Math.min(2, fontLevel + 1); setFont(fontLevel); });
  $('btn-font-minus').addEventListener('click', () => { fontLevel = Math.max(0, fontLevel - 1); setFont(fontLevel); });
  setFont(fontLevel);

  bindInput();
  requestAnimationFrame(draw);
  await resumeRoom();
}

bootstrap();
