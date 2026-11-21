import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const CWD = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const PORT = 43127;
const BASE = `http://127.0.0.1:${PORT}`;
let child;

async function waitForServer() {
  for (let i = 0; i < 60; i++) {
    try {
      const res = await fetch(`${BASE}/api/health`);
      if (res.ok) return;
    } catch { /* not listening yet */ }
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error('server did not start');
}

async function api(method, route, body) {
  const res = await fetch(`${BASE}${route}`, {
    method,
    headers: body ? { 'Content-Type': 'application/json' } : {},
    body: body ? JSON.stringify(body) : undefined,
  });
  return { status: res.status, data: await res.json() };
}

test.before(async () => {
  child = spawn(process.execPath, ['server/index.js'], {
    cwd: CWD,
    env: { ...process.env, PORT: String(PORT) },
    stdio: 'ignore',
  });
  await waitForServer();
});

test.after(() => child?.kill());

test('health and config endpoints describe the running game', async () => {
  const health = await api('GET', '/api/health');
  assert.equal(health.status, 200);
  assert.equal(health.data.ok, true);
  assert.equal(health.data.coinGoal, 50);

  const config = await api('GET', '/api/config');
  assert.equal(config.status, 200);
  assert.equal(config.data.roleOrder.length, 6);
});

test('create, join, swap roles and enforce host-only start', async () => {
  const created = await api('POST', '/api/rooms', { name: 'Marta' });
  assert.equal(created.status, 201);
  const { code, playerId: hostId } = created.data;
  assert.match(code, /^[A-Z2-9]{5}$/);

  const joined = await api('POST', `/api/rooms/${code}/join`, {
    name: 'Joan', preferredRole: 'troublemakerA',
  });
  assert.equal(joined.status, 200);
  const guestId = joined.data.playerId;
  assert.equal(
    joined.data.lobby.players.find((p) => p.id === guestId).role,
    'troublemakerA',
  );

  const swapped = await api('POST', `/api/rooms/${code}/role`, {
    playerId: hostId, role: 'troublemakerA',
  });
  assert.equal(swapped.status, 200);
  const roles = swapped.data.lobby.players;
  assert.equal(roles.find((p) => p.id === hostId).role, 'troublemakerA');
  assert.equal(roles.find((p) => p.id === guestId).role, null);

  const denied = await api('POST', `/api/rooms/${code}/start`, { playerId: guestId });
  assert.equal(denied.status, 403);

  const started = await api('POST', `/api/rooms/${code}/start`, { playerId: hostId });
  assert.equal(started.status, 200);
  assert.equal(started.data.status, 'playing');

  // Nobody is a spectator even if a player never picked a role.
  const afterStart = await api('GET', `/api/rooms/${code}/state`);
  assert.ok(afterStart.data.lobby.players.every((p) => p.role));
});

test('state and result endpoints expose the authoritative match', async () => {
  const created = await api('POST', '/api/rooms', { name: 'Host' });
  const { code, playerId } = created.data;
  await api('POST', `/api/rooms/${code}/join`, { name: 'Guest', preferredRole: 'fatherB' });
  await api('POST', `/api/rooms/${code}/start`, { playerId });

  const state = await api('GET', `/api/rooms/${code}/state`);
  assert.equal(state.data.status, 'playing');
  assert.equal(state.data.players.length, 6);
  assert.equal(typeof state.data.map.tiles, 'string');
  assert.ok(state.data.coins.length > 0);

  const input = await api('POST', `/api/rooms/${code}/input`, {
    playerId, dx: 1, dy: 0, sprint: false,
  });
  assert.equal(input.status, 200);

  const result = await api('GET', `/api/rooms/${code}/result`);
  assert.equal(result.data.finished, false);
  assert.equal(result.data.status, 'playing');
});

test('static client is served from the same server', async () => {
  const res = await fetch(`${BASE}/`);
  assert.equal(res.status, 200);
  assert.match(await res.text(), /Catch the Troublemaker/i);
});
