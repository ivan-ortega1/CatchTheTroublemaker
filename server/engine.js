/**
 * Authoritative game engine for Catch the Troublemaker.
 *
 * Deliberately free of HTTP / socket code so it can be unit tested with
 * `node --test`. The server owns all state; clients only send intent.
 */

import { CONFIG, ROLES, ROLE_ORDER, BOT_NAMES, spawnFor } from './config.js';

const DIRS = [[1, 0], [-1, 0], [0, 1], [0, -1]];

/** Small deterministic PRNG so a room seed always rebuilds the same map. */
export function mulberry32(seed) {
  let a = seed >>> 0;
  return function rng() {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export const indexOfTile = (map, c, r) => r * map.cols + c;
export const isWall = (map, c, r) =>
  c < 0 || r < 0 || c >= map.cols || r >= map.rows || map.tiles[indexOfTile(map, c, r)] === 1;

/** All non-wall tiles reachable from a starting tile (4-connected). */
export function floodFill(map, startC, startR) {
  const { cols, rows } = map;
  const seen = new Uint8Array(cols * rows);
  if (isWall(map, startC, startR)) return seen;
  const start = indexOfTile(map, startC, startR);
  const queue = [start];
  seen[start] = 1;
  for (let head = 0; head < queue.length; head++) {
    const cur = queue[head];
    const c = cur % cols;
    const r = (cur / cols) | 0;
    for (const [dc, dr] of DIRS) {
      const nc = c + dc;
      const nr = r + dr;
      if (isWall(map, nc, nr)) continue;
      const ni = indexOfTile(map, nc, nr);
      if (seen[ni]) continue;
      seen[ni] = 1;
      queue.push(ni);
    }
  }
  return seen;
}

export function generateMap(seed) {
  const { cols, rows, obstacles } = CONFIG.map;
  const rand = mulberry32(seed);
  const tiles = new Uint8Array(cols * rows);

  // Solid border.
  for (let c = 0; c < cols; c++) {
    tiles[indexOfTile({ cols }, c, 0)] = 1;
    tiles[indexOfTile({ cols }, c, rows - 1)] = 1;
  }
  for (let r = 0; r < rows; r++) {
    tiles[indexOfTile({ cols }, 0, r)] = 1;
    tiles[indexOfTile({ cols }, cols - 1, r)] = 1;
  }

  // Keep a 4x4 bubble free around every spawn so nobody starts stuck.
  const protectedZones = ROLE_ORDER.map(spawnFor).map((p) => ({
    c: Math.floor(p.x), r: Math.floor(p.y),
  }));

  let placed = 0;
  let attempts = 0;
  while (placed < obstacles && attempts < obstacles * 20) {
    attempts++;
    const c = 2 + Math.floor(rand() * (cols - 5));
    const r = 2 + Math.floor(rand() * (rows - 5));
    const w = 2 + Math.floor(rand() * 3); // 2..4
    const h = 1 + Math.floor(rand() * 3); // 1..3
    let blocked = false;
    for (const zone of protectedZones) {
      if (c <= zone.c + 4 && c + w >= zone.c - 1 && r <= zone.r + 4 && r + h >= zone.r - 1) {
        blocked = true;
        break;
      }
    }
    if (blocked) continue;
    for (let rr = r; rr < r + h && rr < rows - 1; rr++) {
      for (let cc = c; cc < c + w && cc < cols - 1; cc++) {
        tiles[indexOfTile({ cols }, cc, rr)] = 1;
      }
    }
    placed++;
  }

  const map = { cols, rows, tiles: Array.from(tiles) };
  const start = spawnFor('fatherA');
  map.reachable = floodFill(map, Math.floor(start.x), Math.floor(start.y));
  return map;
}

/** First step (unit direction) of a shortest 4-connected path to a target. */
export function firstStep(map, from, to) {
  const { cols, rows } = map;
  const sc = Math.max(0, Math.min(cols - 1, Math.floor(from.x)));
  const sr = Math.max(0, Math.min(rows - 1, Math.floor(from.y)));
  const tc = Math.max(0, Math.min(cols - 1, Math.floor(to.x)));
  const tr = Math.max(0, Math.min(rows - 1, Math.floor(to.y)));
  const startIdx = indexOfTile(map, sc, sr);
  const targetIdx = indexOfTile(map, tc, tr);
  if (startIdx === targetIdx || map.tiles[targetIdx] === 1) return { dx: 0, dy: 0 };

  const prev = new Int32Array(cols * rows).fill(-1);
  const seen = new Uint8Array(cols * rows);
  const queue = [startIdx];
  seen[startIdx] = 1;
  let found = -1;
  for (let head = 0; head < queue.length && found === -1; head++) {
    const cur = queue[head];
    const c = cur % cols;
    const r = (cur / cols) | 0;
    for (const [dc, dr] of DIRS) {
      const nc = c + dc;
      const nr = r + dr;
      if (isWall(map, nc, nr)) continue;
      const ni = indexOfTile(map, nc, nr);
      if (seen[ni]) continue;
      seen[ni] = 1;
      prev[ni] = cur;
      if (ni === targetIdx) { found = ni; break; }
      queue.push(ni);
    }
  }
  if (found === -1) return { dx: 0, dy: 0 };

  let step = found;
  while (prev[step] !== -1 && prev[step] !== startIdx) step = prev[step];
  return { dx: (step % cols) - sc, dy: ((step / cols) | 0) - sr };
}

/** Choose a reachable tile that is far from a threat but still nearby. */
export function fleeTarget(map, from, threat, rand) {
  const { cols, rows } = map;
  let best = null;
  let bestScore = -Infinity;
  for (let i = 0; i < 48; i++) {
    const idx = Math.floor(rand() * map.tiles.length);
    if (map.tiles[idx] === 1 || map.reachable[idx] !== 1) continue;
    const x = (idx % cols) + 0.5;
    const y = ((idx / cols) | 0) + 0.5;
    const fromDist = Math.hypot(x - from.x, y - from.y);
    if (fromDist > 13) continue;
    const threatDist = Math.hypot(x - threat.x, y - threat.y);
    const score = threatDist - fromDist * 0.12;
    if (score > bestScore) { bestScore = score; best = { x, y }; }
  }
  return best || { x: from.x, y: from.y };
}

function canStand(map, x, y, radius) {
  const r = radius;
  return !isWall(map, Math.floor(x - r), Math.floor(y - r))
    && !isWall(map, Math.floor(x + r), Math.floor(y - r))
    && !isWall(map, Math.floor(x - r), Math.floor(y + r))
    && !isWall(map, Math.floor(x + r), Math.floor(y + r));
}

export function moveWithCollision(entity, dir, speed, dt, map) {
  let { dx, dy } = dir;
  const length = Math.hypot(dx, dy);
  if (length < 0.001) return;
  dx /= length;
  dy /= length;
  const nx = entity.x + dx * speed * dt;
  const ny = entity.y + dy * speed * dt;
  const radius = CONFIG.playerRadius;
  if (canStand(map, nx, entity.y, radius)) entity.x = nx;
  if (canStand(map, entity.x, ny, radius)) entity.y = ny;
}

const round2 = (n) => Math.round(n * 100) / 100;

export class Match {
  constructor({ id, seed, humans = [], now = Date.now() }) {
    this.id = id;
    this.seed = seed;
    this.map = generateMap(seed);
    this.rand = mulberry32(seed ^ 0x9e3779b9);

    this.players = humans.map((h) => this.makePlayer(h));
    for (const role of ROLE_ORDER) {
      if (!this.players.some((p) => p.role === role)) {
        this.players.push(this.makePlayer({
          id: `bot-${role}-${id}`, name: BOT_NAMES[role], role, isBot: true,
        }));
      }
    }
    // Head start: nobody can be caught in the first few seconds.
    for (const p of this.players) {
      if (p.team === 'troublemakers') p.graceUntil = now + CONFIG.startGraceMs;
    }

    this.coins = [];
    this.respawns = [];
    this.nextCoinId = 1;
    this.spawnable = [];
    for (let i = 0; i < this.map.tiles.length; i++) {
      if (this.map.tiles[i] === 1 || this.map.reachable[i] !== 1) continue;
      const c = i % this.map.cols;
      const r = (i / this.map.cols) | 0;
      if (ROLE_ORDER.some((role) => {
        const s = spawnFor(role);
        return Math.abs(s.x - (c + 0.5)) < 1.5 && Math.abs(s.y - (r + 0.5)) < 1.5;
      })) continue;
      this.spawnable.push(i);
    }
    for (let i = 0; i < CONFIG.initialCoins; i++) this.spawnCoin();

    this.status = 'playing';
    this.startedAt = now;
    this.endsAt = now + CONFIG.gameDurationMs;
    this.tickCount = 0;
    this.events = [];
    this.result = null;
    this.lastCatcherId = null;
    this.stats = { catches: 0, coinsCollected: 0, confiscated: 0 };
  }

  makePlayer(human) {
    const meta = ROLES[human.role];
    const spawn = spawnFor(human.role);
    return {
      id: human.id,
      name: String(human.name || 'Player').slice(0, 24),
      role: human.role,
      family: meta.family,
      team: meta.team,
      color: meta.color,
      shape: meta.shape,
      isBot: !!human.isBot,
      x: spawn.x,
      y: spawn.y,
      intent: { dx: 0, dy: 0 },
      sprintHeld: false,
      sprintUntil: 0,
      sprintReadyAt: 0,
      coins: 0,
      lives: meta.team === 'troublemakers' ? CONFIG.livesPerTroublemaker : 0,
      eliminated: false,
      respawnAt: 0,
      graceUntil: 0,
      nextConfiscateAt: 0,
      connected: !human.isBot,
      nextThink: 0,
    };
  }

  spawnCoin(at = 0) {
    if (this.coins.length >= CONFIG.maxCoins) return;
    if (this.spawnable.length === 0) return;
    const idx = this.spawnable[Math.floor(this.rand() * this.spawnable.length)];
    this.coins.push({
      id: this.nextCoinId++,
      x: (idx % this.map.cols) + 0.5,
      y: ((idx / this.map.cols) | 0) + 0.5,
      activeAt: at,
      spawnAt: at,
    });
  }

  getPlayer(playerId) {
    return this.players.find((p) => p.id === playerId);
  }

  setDisconnected(playerId, disconnected) {
    const p = this.getPlayer(playerId);
    if (!p || p.id.startsWith('bot-')) return;
    p.connected = !disconnected;
    // A disconnected human is taken over by a bot so the match can continue.
    p.isBot = disconnected;
    if (disconnected) p.intent = { dx: 0, dy: 0 };
  }

  setInput(playerId, input, now = Date.now()) {
    const p = this.getPlayer(playerId);
    if (!p || p.eliminated || now < p.respawnAt) return;
    const dx = Math.max(-1, Math.min(1, Number(input?.dx) || 0));
    const dy = Math.max(-1, Math.min(1, Number(input?.dy) || 0));
    p.intent = { dx, dy };

    const sprint = !!input?.sprint;
    if (sprint && !p.sprintHeld && now >= p.sprintReadyAt) this.triggerSprint(p, now);
    p.sprintHeld = sprint;
  }

  triggerSprint(player, now) {
    player.sprintUntil = now + CONFIG.sprint.durationMs;
    player.sprintReadyAt = now + CONFIG.sprint.cooldownMs;
    this.events.push({ type: 'sprint', id: player.id, x: player.x, y: player.y, now });
  }

  thinkBot(p, now) {
    if (now < p.nextThink) return;
    p.nextThink = now + 300 + Math.floor(this.rand() * 180);

    if (p.team === 'parents') {
      const child = this.players.find((q) =>
        q.team === 'troublemakers' && q.family === p.family && !q.eliminated);
      if (child) {
        const dist = Math.hypot(child.x - p.x, child.y - p.y);
        p.intent = firstStep(this.map, p, child);
        if (dist > 1.3 && dist < 5 && now >= p.sprintReadyAt) this.triggerSprint(p, now);
      } else {
        const coin = this.nearestCoin(p);
        p.intent = coin ? firstStep(this.map, p, coin) : { dx: 0, dy: 0 };
      }
      return;
    }

    // Troublemaker bot: run away from own parents, otherwise collect coins.
    const threats = this.players.filter((q) => q.team === 'parents' && q.family === p.family);
    let nearest = null;
    let nearestDist = Infinity;
    for (const t of threats) {
      const d = Math.hypot(t.x - p.x, t.y - p.y);
      if (d < nearestDist) { nearestDist = d; nearest = t; }
    }
    if (nearest && nearestDist < 6) {
      const target = fleeTarget(this.map, p, nearest, this.rand);
      p.intent = firstStep(this.map, p, target);
      if (nearestDist < 4.5 && now >= p.sprintReadyAt) this.triggerSprint(p, now);
    } else {
      const coin = this.nearestCoin(p, threats);
      p.intent = coin ? firstStep(this.map, p, coin) : { dx: 0, dy: 0 };
    }
  }

  nearestCoin(from, avoid = []) {
    let best = null;
    let bestScore = Infinity;
    for (const coin of this.coins) {
      const d = Math.hypot(coin.x - from.x, coin.y - from.y);
      let score = d;
      for (const a of avoid) {
        score += Math.max(0, 6 - Math.hypot(coin.x - a.x, coin.y - a.y)) * 0.8;
      }
      if (score < bestScore) { bestScore = score; best = coin; }
    }
    return best;
  }

  tick(now = Date.now(), dt = CONFIG.tickMs / 1000) {
    if (this.status !== 'playing') return;
    this.tickCount++;

    // Scheduled coin respawns (including coins dropped by a caught child).
    for (let i = this.respawns.length - 1; i >= 0; i--) {
      if (now >= this.respawns[i]) {
        this.respawns.splice(i, 1);
        this.spawnCoin(now);
      }
    }

    for (const p of this.players) {
      if (p.isBot) this.thinkBot(p, now);
      if (p.eliminated || now < p.respawnAt) continue;
      const mult = now < p.sprintUntil
        ? (p.team === 'parents' ? CONFIG.sprint.multiplierParents : CONFIG.sprint.multiplierTroublemakers)
        : 1;
      moveWithCollision(p, p.intent, CONFIG.speed[p.team] * mult, dt, this.map);
    }

    // Coin interactions. Parents confiscate, troublemakers collect.
    for (const p of this.players) {
      if (p.eliminated || now < p.respawnAt) continue;
      for (let i = this.coins.length - 1; i >= 0; i--) {
        const coin = this.coins[i];
        if (now < coin.activeAt) continue;
        if (p.team === 'parents' && now < p.nextConfiscateAt) continue;
        if (Math.hypot(coin.x - p.x, coin.y - p.y) > CONFIG.coinPickupRadius) continue;
        this.coins.splice(i, 1);
        if (p.team === 'troublemakers') {
          p.coins += 1;
          this.stats.coinsCollected++;
          this.events.push({ type: 'coin', id: p.id, x: coin.x, y: coin.y, now });
        } else {
          p.nextConfiscateAt = now + CONFIG.confiscateCooldownMs;
          this.stats.confiscated++;
          this.events.push({ type: 'confiscate', id: p.id, x: coin.x, y: coin.y, now });
        }
        this.respawns.push(now + CONFIG.coinRespawnMs);
        break; // one coin per tick per player keeps movement readable
      }
    }

    // THE core rule: only the parents of the SAME family can catch their child.
    for (const parent of this.players) {
      if (parent.team !== 'parents') continue;
      for (const child of this.players) {
        if (child.team !== 'troublemakers' || child.family !== parent.family) continue;
        if (child.eliminated || now < child.respawnAt || now < child.graceUntil) continue;
        if (Math.hypot(child.x - parent.x, child.y - parent.y) > CONFIG.catchRadius) continue;
        this.catchChild(parent, child, now);
      }
    }

    this.checkWin(now);
    if (this.events.length > CONFIG.maxEvents) {
      this.events.splice(0, this.events.length - CONFIG.maxEvents);
    }
  }

  catchChild(parent, child, now) {
    child.lives -= 1;
    this.stats.catches++;
    this.lastCatcherId = parent.id;
    const dropped = child.coins;
    child.coins = 0;
    for (let i = 0; i < dropped; i++) this.respawns.push(now + 200 + i * 45);

    this.events.push({
      type: 'catch', by: parent.id, id: child.id, family: child.family,
      dropped, livesLeft: child.lives, x: child.x, y: child.y, now,
    });

    if (child.lives <= 0) {
      child.eliminated = true;
    } else {
      const spawn = spawnFor(child.role);
      child.x = spawn.x;
      child.y = spawn.y;
      child.respawnAt = now + CONFIG.respawnMs;
      child.graceUntil = now + CONFIG.respawnMs + CONFIG.graceMs;
      child.intent = { dx: 0, dy: 0 };
    }
  }

  checkWin(now) {
    const troublemakers = this.players.filter((p) => p.team === 'troublemakers');
    const winner = troublemakers.find((p) => p.coins >= CONFIG.coinGoal);
    if (winner) {
      this.finish('troublemakers', winner, 'reached the coin goal', now);
      return;
    }
    if (troublemakers.every((p) => p.eliminated)) {
      const catcher = this.getPlayer(this.lastCatcherId) || null;
      this.finish('parents', catcher, 'both troublemakers were grounded', now);
      return;
    }
    if (now >= this.endsAt) {
      this.finish('parents', null, 'time ran out before the coin goal', now);
    }
  }

  finish(winnerTeam, winnerPlayer, reason, now = Date.now()) {
    if (this.result) return;
    this.status = 'finished';
    this.result = {
      winnerTeam,
      winnerId: winnerPlayer ? winnerPlayer.id : null,
      winnerName: winnerPlayer ? winnerPlayer.name : 'The parents',
      reason,
      durationMs: Math.min(now - this.startedAt, CONFIG.gameDurationMs),
      goal: CONFIG.coinGoal,
      stats: { ...this.stats },
      standings: this.players.map((p) => ({
        id: p.id, name: p.name, role: p.role, team: p.team,
        coins: p.coins, lives: p.lives, eliminated: p.eliminated,
      })),
      finishedAt: now,
    };
    this.events.push({ type: 'finish', winnerTeam, winnerId: this.result.winnerId, now });
  }

  drainEvents() {
    const out = this.events;
    this.events = [];
    return out;
  }

  getSnapshot(now = Date.now(), full = false) {
    const snap = {
      status: this.status,
      now,
      tick: this.tickCount,
      remainingMs: Math.max(0, this.endsAt - now),
      goal: CONFIG.coinGoal,
      coins: this.coins
        .filter((c) => now >= c.activeAt)
        .map((c) => ({ id: c.id, x: round2(c.x), y: round2(c.y) })),
      players: this.players.map((p) => ({
        id: p.id,
        name: p.name,
        role: p.role,
        team: p.team,
        family: p.family,
        color: p.color,
        shape: p.shape,
        x: round2(p.x),
        y: round2(p.y),
        coins: p.coins,
        lives: p.lives,
        eliminated: p.eliminated,
        respawnIn: Math.max(0, p.respawnAt - now),
        grace: now < p.graceUntil,
        sprintIn: Math.max(0, p.sprintReadyAt - now),
        sprinting: now < p.sprintUntil,
        isBot: p.isBot,
        connected: p.connected,
      })),
      events: this.events.slice(-8),
      result: this.result,
    };
    if (full) {
      snap.map = {
        cols: this.map.cols,
        rows: this.map.rows,
        tiles: this.map.tiles.join(''),
      };
    }
    return snap;
  }

  getResult() {
    return {
      status: this.status,
      finished: !!this.result,
      result: this.result,
    };
  }
}
