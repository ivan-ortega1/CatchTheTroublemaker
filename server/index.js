/**
 * HTTP server for Catch the Troublemaker.
 *
 * - REST endpoints create/join rooms, assign roles and start matches.
 * - Server-Sent Events push authoritative snapshots to every connected client.
 * - POST /input carries only player intent; the server simulates everything.
 *
 * No npm dependencies: it runs with plain `node server/index.js`.
 */

import http from 'node:http';
import fs from 'node:fs';
import os from 'node:os';
import dgram from 'node:dgram';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { CONFIG, ROLES, ROLE_ORDER } from './config.js';
import { Match } from './engine.js';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const PUBLIC_DIR = path.join(ROOT, 'public');
const DATA_DIR = path.join(ROOT, 'data');
const DATA_FILE = path.join(DATA_DIR, 'matches.jsonl');
const PORT = Number(process.env.PORT) || 3000;

fs.mkdirSync(DATA_DIR, { recursive: true });

const rooms = new Map();               // code -> room
const streams = new Map();             // code -> Set<{ playerId, res }>
const CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
};

function newCode() {
  let code;
  do {
    code = Array.from({ length: 5 }, () =>
      CODE_ALPHABET[Math.floor(Math.random() * CODE_ALPHABET.length)]).join('');
  } while (rooms.has(code));
  return code;
}

function newSeed() {
  return Math.floor(Math.random() * 0xffffffff) >>> 0;
}

function sendJSON(res, status, body) {
  const data = JSON.stringify(body);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(data),
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Headers': 'Content-Type',
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Cache-Control': 'no-store',
  });
  res.end(data);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let raw = '';
    req.on('data', (chunk) => {
      raw += chunk;
      if (raw.length > 8192) {
        reject(new Error('payload too large'));
        req.destroy();
      }
    });
    req.on('end', () => {
      try {
        resolve(raw ? JSON.parse(raw) : {});
      } catch {
        resolve({});
      }
    });
    req.on('error', reject);
  });
}

function serveStatic(res, pathname) {
  const relative = pathname === '/' ? 'index.html' : pathname.replace(/^\/+/, '');
  const target = path.resolve(PUBLIC_DIR, relative);
  if (!target.startsWith(PUBLIC_DIR + path.sep) && target !== path.join(PUBLIC_DIR, 'index.html')) {
    return sendJSON(res, 403, { error: 'forbidden' });
  }
  fs.readFile(target, (err, data) => {
    if (err) return sendJSON(res, 404, { error: 'not found' });
    res.writeHead(200, {
      'Content-Type': MIME[path.extname(target).toLowerCase()] || 'application/octet-stream',
      'Cache-Control': 'no-cache',
    });
    res.end(data);
  });
}

function roleSlots(room) {
  return ROLE_ORDER.map((role) => ({
    role,
    team: ROLES[role].team,
    family: ROLES[role].family,
    short: ROLES[role].short,
    name: ROLES[role].name,
    color: ROLES[role].color,
    takenBy: [...room.players.values()].find((p) => p.role === role)?.id || null,
  }));
}

function lobbyInfo(room) {
  return {
    code: room.code,
    status: room.status,
    hostId: room.hostId,
    seed: room.seed,
    players: [...room.players.values()].map((p) => ({
      id: p.id, name: p.name, role: p.role, isHost: p.id === room.hostId,
    })),
    roles: roleSlots(room),
  };
}

function streamSet(room) {
  if (!streams.has(room.code)) streams.set(room.code, new Set());
  return streams.get(room.code);
}

function broadcast(room, event, payload) {
  const packet = `event: ${event}\ndata: ${JSON.stringify(payload)}\n\n`;
  for (const client of streamSet(room)) {
    try { client.res.write(packet); } catch { /* closed connection */ }
  }
}

/** Only players with a live stream control a human slot; absent ones become bots. */
function syncPresence(room) {
  const active = new Set([...streamSet(room)].map((c) => c.playerId));
  for (const p of room.match.players) {
    if (p.id.startsWith('bot-')) continue;
    const seenAt = room.seen?.get(p.id) || 0;
    const recentInput = Date.now() - seenAt < 10000;
    p.connected = active.has(p.id) || recentInput;
    p.isBot = !p.connected;
  }
}

function persistResult(room) {
  const result = room.match?.result;
  if (!result || room.persisted) return;
  room.persisted = true;
  const line = JSON.stringify({
    roomId: room.code, seed: room.seed, ...result,
  }) + '\n';
  fs.appendFile(DATA_FILE, line, () => {});
}

function openStream(room, playerId, req, res) {
  res.writeHead(200, {
    'Content-Type': 'text/event-stream; charset=utf-8',
    'Cache-Control': 'no-cache, no-transform',
    Connection: 'keep-alive',
    'X-Accel-Buffering': 'no',
    'Access-Control-Allow-Origin': '*',
  });
  res.write(`retry: 2000\n\n`);

  const client = { playerId, res };
  room.seen.set(playerId, Date.now());
  const set = streamSet(room);
  const wasFirst = [...set].every((c) => c.playerId !== playerId);
  set.add(client);
  if (wasFirst && room.match && room.status === 'playing') {
    room.match.setDisconnected(playerId, false);
  }

  const hello = room.match
    ? { ...room.match.getSnapshot(Date.now(), true), lobby: lobbyInfo(room) }
    : { status: room.status, lobby: lobbyInfo(room) };
  res.write(`event: hello\ndata: ${JSON.stringify(hello)}\n\n`);

  const keepAlive = setInterval(() => {
    try { res.write(': keep-alive\n\n'); } catch { /* closed */ }
  }, 15000);

  req.on('close', () => {
    clearInterval(keepAlive);
    set.delete(client);
    const stillConnected = [...set].some((c) => c.playerId === playerId);
    if (!stillConnected && room.match && room.status === 'playing') {
      room.match.setDisconnected(playerId, true);
    }
    if (set.size === 0) streams.delete(room.code);
  });
}

async function handleApi(req, res, parts, url) {
  const method = req.method;
  const [root, resource, code, action] = parts;

  if (root !== 'api') return false;

  if (resource === 'health' && method === 'GET') {
    sendJSON(res, 200, {
      ok: true,
      uptimeMs: Math.round(process.uptime() * 1000),
      rooms: rooms.size,
      tickMs: CONFIG.tickMs,
      coinGoal: CONFIG.coinGoal,
    });
    return true;
  }

  if (resource === 'config' && method === 'GET') {
    sendJSON(res, 200, { config: CONFIG, roles: ROLES, roleOrder: ROLE_ORDER });
    return true;
  }

  // Create a room: the creator becomes the host / game master.
  if (resource === 'rooms' && !code && method === 'POST') {
    const body = await readBody(req);
    const roomCode = newCode();
    const hostId = `p-${Math.random().toString(36).slice(2, 10)}`;
    const room = {
      code: roomCode,
      seed: newSeed(),
      hostId,
      players: new Map([[hostId, {
        id: hostId,
        name: String(body.name || 'Host').slice(0, 24),
        role: null,
      }]]),
      match: null,
      status: 'lobby',
      createdAt: Date.now(),
      lastBroadcast: 0,
      persisted: false,
      seen: new Map(), // last input/stream time per player (Unity can play without SSE)
    };
    rooms.set(roomCode, room);
    sendJSON(res, 201, { code: roomCode, playerId: hostId, lobby: lobbyInfo(room) });
    return true;
  }

  const room = rooms.get(code);
  if (!room) {
    sendJSON(res, 404, { error: `Room ${code || ''} does not exist` });
    return true;
  }
  room.lastAccess = Date.now();

  if (!action && method === 'GET') {
    sendJSON(res, 200, lobbyInfo(room));
    return true;
  }

  if (action === 'join' && method === 'POST') {
    const body = await readBody(req);
    // Known players may rejoin mid-match (page reload / network drop).
    const returning = typeof body.playerId === 'string' && room.players.has(body.playerId);
    if (room.status !== 'lobby' && !returning) {
      sendJSON(res, 409, { error: 'This game has already started' });
      return true;
    }
    if (!returning && room.players.size >= ROLE_ORDER.length) {
      sendJSON(res, 409, { error: 'Room is full (6 roles)' });
      return true;
    }
    const playerId = returning
      ? body.playerId
      : `p-${Math.random().toString(36).slice(2, 10)}`;
    const existing = room.players.get(playerId);
    if (existing) {
      existing.name = String(body.name || existing.name).slice(0, 24);
    } else {
      const preferred = ROLES[body.preferredRole] ? body.preferredRole : null;
      const taken = new Set([...room.players.values()].map((p) => p.role));
      const role = (preferred && !taken.has(preferred))
        ? preferred
        : ROLE_ORDER.find((r) => !taken.has(r)) || null;
      room.players.set(playerId, {
        id: playerId, name: String(body.name || 'Player').slice(0, 24), role,
      });
    }
    if (room.status === 'lobby') broadcast(room, 'lobby', lobbyInfo(room));
    sendJSON(res, 200, { code, playerId, lobby: lobbyInfo(room) });
    return true;
  }

  if (action === 'role' && method === 'POST') {
    if (room.status !== 'lobby') {
      sendJSON(res, 409, { error: 'Roles are locked once the game starts' });
      return true;
    }
    const body = await readBody(req);
    const player = room.players.get(body.playerId);
    const role = ROLES[body.role] ? body.role : null;
    if (!player || !role) {
      sendJSON(res, 400, { error: 'Unknown player or role' });
      return true;
    }
    const owner = [...room.players.values()].find((p) => p.role === role && p.id !== player.id);
    if (owner) owner.role = player.role; // swap roles instead of blocking
    player.role = role;
    broadcast(room, 'lobby', lobbyInfo(room));
    sendJSON(res, 200, { ok: true, lobby: lobbyInfo(room) });
    return true;
  }

  if (action === 'start' && method === 'POST') {
    const body = await readBody(req);
    if (body.playerId !== room.hostId) {
      sendJSON(res, 403, { error: 'Only the host can start the game' });
      return true;
    }
    if (room.status !== 'lobby') {
      sendJSON(res, 409, { error: 'Game already started' });
      return true;
    }
    // Nobody is left as a spectator: give any undeclared player a free role.
    const participants = [...room.players.values()];
    const taken = new Set(participants.map((p) => p.role).filter(Boolean));
    for (const player of participants) {
      if (player.role) continue;
      const free = ROLE_ORDER.find((role) => !taken.has(role));
      if (free) { player.role = free; taken.add(free); }
    }
    room.seed = newSeed();
    room.match = new Match({
      id: room.code,
      seed: room.seed,
      humans: participants.filter((p) => p.role).map((p) => ({
        id: p.id, name: p.name, role: p.role,
      })),
      now: Date.now(),
    });
    room.status = 'playing';
    room.persisted = false;
    // Kickoff grace: a client that has not posted input (or opened SSE) yet must
    // not be replaced by a bot on the very first tick.
    for (const p of participants) room.seen.set(p.id, Date.now());
    syncPresence(room);
    broadcast(room, 'start', room.match.getSnapshot(Date.now(), true));
    sendJSON(res, 200, { ok: true, status: room.status });
    return true;
  }

  if (action === 'restart' && method === 'POST') {
    const body = await readBody(req);
    if (body.playerId !== room.hostId) {
      sendJSON(res, 403, { error: 'Only the host can restart' });
      return true;
    }
    if (!room.match) {
      sendJSON(res, 409, { error: 'No finished match to restart' });
      return true;
    }
    room.seed = newSeed();
    room.match = new Match({
      id: room.code,
      seed: room.seed,
      humans: [...room.players.values()].filter((p) => p.role).map((p) => ({
        id: p.id, name: p.name, role: p.role,
      })),
      now: Date.now(),
    });
    room.status = 'playing';
    room.persisted = false;
    // Same kickoff grace window on restart.
    for (const p of room.players.values()) room.seen.set(p.id, Date.now());
    syncPresence(room);
    broadcast(room, 'start', room.match.getSnapshot(Date.now(), true));
    sendJSON(res, 200, { ok: true, status: room.status });
    return true;
  }

  if (action === 'input' && method === 'POST') {
    if (!room.match || room.status !== 'playing') {
      sendJSON(res, 409, { error: 'No active match' });
      return true;
    }
    const body = await readBody(req);
    room.seen.set(body.playerId, Date.now());
    syncPresence(room); // a polling client becomes human again as soon as it sends input
    room.match.setInput(body.playerId, body, Date.now());
    sendJSON(res, 200, { ok: true });
    return true;
  }

  if (action === 'events' && method === 'GET') {
    if (!room.players.has(url.searchParams.get('playerId'))) {
      sendJSON(res, 401, { error: 'Unknown playerId for this room' });
      return true;
    }
    openStream(room, url.searchParams.get('playerId'), req, res);
    return true;
  }

  if (action === 'state' && method === 'GET') {
    if (!room.match) {
      sendJSON(res, 200, { status: room.status, lobby: lobbyInfo(room) });
      return true;
    }
    sendJSON(res, 200, {
      ...room.match.getSnapshot(Date.now(), true),
      lobby: lobbyInfo(room),
    });
    return true;
  }

  // Required deliverable: an endpoint that returns who won the match.
  if (action === 'result' && method === 'GET') {
    if (!room.match) {
      sendJSON(res, 200, { status: room.status, finished: false, result: null });
      return true;
    }
    sendJSON(res, 200, room.match.getResult());
    return true;
  }

  sendJSON(res, 404, { error: 'Unknown endpoint' });
  return true;
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  if (req.method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Headers': 'Content-Type',
      'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    });
    return res.end();
  }
  // Log commands (not the 15 Hz state/input traffic) with the caller's address.
  // This makes "cannot reach server" reports diagnosable: no line = network/firewall.
  if (!/\/input$|\/state$|\/events$/.test(url.pathname)) {
    console.log(`[http] ${req.method} ${url.pathname} <- ${req.socket.remoteAddress}`);
  }
  try {
    const parts = url.pathname.split('/').filter(Boolean);
    const handled = await handleApi(req, res, parts, url);
    if (!handled) serveStatic(res, url.pathname);
  } catch (err) {
    console.error('[server]', err);
    if (!res.headersSent) sendJSON(res, 500, { error: 'Internal server error' });
    else res.end();
  }
});

// Authoritative simulation loop.
setInterval(() => {
  const now = Date.now();
  for (const room of rooms.values()) {
    if (room.status !== 'playing' || !room.match) continue;
    syncPresence(room); // refresh input/SSE presence; stale clients fall back to bots
    room.match.tick(now, CONFIG.tickMs / 1000);

    if (room.match.status === 'finished') {
      room.status = 'finished';
      persistResult(room);
      broadcast(room, 'state', room.match.getSnapshot(now, true));
      broadcast(room, 'result', room.match.getResult());
      continue;
    }

    if (now - room.lastBroadcast >= 1000 / CONFIG.stateHz) {
      room.lastBroadcast = now;
      const snapshot = room.match.getSnapshot(now, false);
      snapshot.events = room.match.drainEvents();
      broadcast(room, 'state', snapshot);
    }
  }

  // Drop stale empty lobbies older than 6 hours.
  for (const [code, room] of rooms) {
    if (room.status === 'lobby' && now - (room.lastAccess || room.createdAt) > 6 * 3600_000) {
      rooms.delete(code);
      streams.delete(code);
    }
  }
}, CONFIG.tickMs);

const DISCOVERY_PORT = Number(process.env.DISCOVERY_PORT) || 3001;
const DISCOVERY_MAGIC = 'CTT_DISCOVER';
let advertisedIp = null;

/** Pick the local interface on the same subnet as the asking client. */
function lanAddressFor(peer) {
  for (const list of Object.values(os.networkInterfaces())) {
    for (const net of list || []) {
      if (net.family !== 'IPv4' || net.internal || net.address.startsWith('169.254.')) continue;
      const mask = (net.netmask || '255.255.255.0').split('.').map(Number);
      const local = net.address.split('.').map(Number);
      const remote = peer.split('.').map(Number);
      if (mask.every((m, i) => (local[i] & m) === (remote[i] & m))) return net.address;
    }
  }
  return lanAddresses()[0] || null;
}

/** UDP beacon so Unity clients on the same LAN can discover this server. */
function startDiscovery() {
  const socket = dgram.createSocket({ type: 'udp4', reuseAddr: true });
  socket.on('message', (msg, rinfo) => {
    if (msg.toString().trim() !== DISCOVERY_MAGIC) return;
    const ip = advertisedIp || lanAddressFor(rinfo.address);
    if (!ip) return;
    const payload = JSON.stringify({
      type: 'CTT_SERVER',
      url: `http://${ip}:${PORT}`,
      port: PORT,
    });
    socket.send(Buffer.from(payload), rinfo.port, rinfo.address, (err) => {
      if (err) console.error('[discovery] reply failed:', err.message);
    });
  });
  socket.on('error', (err) => console.error('[discovery]', err.message));
  socket.bind(DISCOVERY_PORT, () => {
    console.log(`LAN discovery listening on UDP ${DISCOVERY_PORT}`);
  });
  return socket;
}

function lanAddresses() {
  const addresses = [];
  for (const list of Object.values(os.networkInterfaces())) {
    for (const net of list || []) {
      // Skip loopback and link-local (APIPA) addresses: they cannot be shared.
      if (net.family === 'IPv4' && !net.internal && !net.address.startsWith('169.254.')) {
        addresses.push(net.address);
      }
    }
  }
  return [...new Set(addresses)];
}

/** The address the OS would use to reach the internet = the useful LAN address. */
function primaryLanAddress() {
  return new Promise((resolve) => {
    const socket = dgram.createSocket('udp4');
    socket.once('error', () => { socket.close(); resolve(null); });
    socket.connect(80, '8.8.8.8', () => {
      const address = socket.address().address;
      socket.close();
      resolve(address === '127.0.0.1' ? null : address);
    });
  });
}

server.listen(PORT, async () => {
  console.log(`Catch the Troublemaker -> http://localhost:${PORT}`);
  const primary = await primaryLanAddress();
  advertisedIp = primary;
  if (primary) console.log(`  SHARE THIS: http://${primary}:${PORT}   (same Wi-Fi / LAN)`);
  for (const ip of lanAddresses()) {
    if (ip !== primary) console.log(`  LAN: http://${ip}:${PORT}   (same Wi-Fi only)`);
  }
  console.log('Players on mobile data / another network need a public tunnel URL or a cloud host.');
  console.log(`REST + SSE ready.coinGoal=${CONFIG.coinGoal} tick=${CONFIG.tickMs}ms`);
  startDiscovery();
});
