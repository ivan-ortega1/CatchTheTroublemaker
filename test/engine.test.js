import test from 'node:test';
import assert from 'node:assert/strict';

import {
  Match, generateMap, firstStep, isWall, moveWithCollision,
} from '../server/engine.js';
import { CONFIG, ROLE_ORDER } from '../server/config.js';

const NOW = 1_000_000;

function human(id, role) {
  return { id, name: id, role };
}

test('map generation is deterministic per seed and has solid borders', () => {
  const a = generateMap(12345);
  const b = generateMap(12345);
  const c = generateMap(999);
  assert.deepEqual(a.tiles, b.tiles);
  assert.notDeepEqual(a.tiles, c.tiles);
  for (let x = 0; x < a.cols; x++) {
    assert.equal(isWall(a, x, 0), true);
    assert.equal(isWall(a, x, a.rows - 1), true);
  }
  for (let y = 0; y < a.rows; y++) {
    assert.equal(isWall(a, 0, y), true);
    assert.equal(isWall(a, a.cols - 1, y), true);
  }
  assert.ok(a.reachable.some((v) => v === 1), 'there must be reachable free tiles');
});

test('firstStep walks around walls instead of into them', () => {
  const map = generateMap(7);
  const from = { x: 2.5, y: 2.5 };
  const to = { x: 8.5, y: 2.5 };
  const step = firstStep(map, from, to);
  const next = { x: from.x + step.dx, y: from.y + step.dy };
  assert.ok(Math.abs(step.dx) + Math.abs(step.dy) === 1);
  assert.equal(isWall(map, Math.floor(next.x), Math.floor(next.y)), false);
});

test('movement collision never places a player inside a wall', () => {
  const map = generateMap(42);
  const entity = { x: 2.5, y: 2.5 };
  for (let i = 0; i < 200; i++) {
    moveWithCollision(entity, { dx: 1, dy: 0.4 }, 8, 0.05, map);
    assert.equal(isWall(map, Math.floor(entity.x), Math.floor(entity.y)), false);
  }
});

test('empty role slots are filled by bots so any room is playable', () => {
  const match = new Match({ id: 'T1', seed: 1, humans: [human('u1', 'fatherA')], now: NOW });
  assert.equal(match.players.length, 6);
  assert.deepEqual(
    ROLE_ORDER.every((role) => match.players.some((p) => p.role === role)),
    true,
  );
  assert.equal(match.players.filter((p) => p.isBot).length, 5);
});

test('a troublemaker collects a coin but a parent only confiscates it', () => {
  const match = new Match({ id: 'T2', seed: 2, humans: [], now: NOW });
  const child = match.getPlayer('bot-troublemakerA-T2');
  match.coins = [{ id: 1, x: child.x, y: child.y, activeAt: 0, spawnAt: 0 }];
  match.tick(NOW, 0.05);
  assert.equal(child.coins, 1);
  assert.equal(match.stats.coinsCollected, 1);

  const match2 = new Match({ id: 'T3', seed: 2, humans: [], now: NOW });
  match2.coins = [{ id: 1, x: 3.5, y: 3.5, activeAt: 0, spawnAt: 0 }];
  const dad2 = match2.getPlayer('bot-fatherA-T3');
  dad2.x = 3.5; dad2.y = 3.5;
  match2.tick(NOW, 0.05);
  assert.equal(match2.stats.confiscated, 1);
  assert.equal(match2.players.filter((p) => p.team === 'troublemakers').every((p) => p.coins === 0), true);
});

test('only the parents of the same family can catch their child', () => {
  const match = new Match({ id: 'T4', seed: 3, humans: [], now: NOW });
  match.coins = [];
  const dadA = match.getPlayer('bot-fatherA-T4');
  const childA = match.getPlayer('bot-troublemakerA-T4');
  const dadB = match.getPlayer('bot-fatherB-T4');
  childA.graceUntil = 0; // skip the deliberate 5s head start for this rule test

  // Wrong parent sits exactly on top of the child: no catch.
  dadB.x = childA.x; dadB.y = childA.y;
  match.tick(NOW, 0.05);
  assert.equal(childA.lives, CONFIG.livesPerTroublemaker);
  assert.equal(match.stats.catches, 0);

  // Right parent catches, child drops coins and respawns with grace.
  childA.coins = 7;
  dadA.x = childA.x; dadA.y = childA.y;
  match.tick(NOW + 50, 0.05);
  assert.equal(childA.lives, CONFIG.livesPerTroublemaker - 1);
  assert.equal(childA.coins, 0);
  assert.ok(childA.respawnAt > NOW);
  assert.ok(match.respawns.length >= 7, 'dropped coins are scheduled to respawn');
});

test('reaching the coin goal wins for the troublemakers', () => {
  const match = new Match({ id: 'T5', seed: 4, humans: [], now: NOW });
  const child = match.getPlayer('bot-troublemakerB-T5');
  child.coins = CONFIG.coinGoal - 1;
  match.coins = [{ id: 1, x: child.x, y: child.y, activeAt: 0, spawnAt: 0 }];
  match.tick(NOW, 0.05);
  assert.equal(match.status, 'finished');
  assert.equal(match.result.winnerTeam, 'troublemakers');
  assert.equal(match.result.winnerId, child.id);
  assert.equal(match.getResult().finished, true);
});

test('grounding both troublemakers wins for the parents', () => {
  const match = new Match({ id: 'T6', seed: 5, humans: [], now: NOW });
  match.coins = [];
  for (const role of ['troublemakerA', 'troublemakerB']) {
    const child = match.getPlayer(`bot-${role}-T6`);
    child.lives = 1;
    child.graceUntil = 0;
    const dad = match.getPlayer(`bot-father${role.at(-1)}-T6`);
    dad.x = child.x; dad.y = child.y;
  }
  match.tick(NOW, 0.05);
  assert.equal(match.status, 'finished');
  assert.equal(match.result.winnerTeam, 'parents');
  assert.equal(match.result.reason, 'both troublemakers were grounded');
});

test('time running out wins for the parents', () => {
  const match = new Match({ id: 'T7', seed: 6, humans: [], now: NOW });
  match.endsAt = NOW - 1;
  match.tick(NOW, 0.05);
  assert.equal(match.status, 'finished');
  assert.equal(match.result.winnerTeam, 'parents');
  assert.match(match.result.reason, /time ran out/);
});

test('snapshot exposes everything clients and the result endpoint need', () => {
  const match = new Match({ id: 'T8', seed: 8, humans: [human('u1', 'motherB')], now: NOW });
  const snap = match.getSnapshot(NOW, true);
  assert.equal(snap.players.length, 6);
  assert.equal(snap.goal, CONFIG.coinGoal);
  assert.equal(typeof snap.map.tiles, 'string');
  assert.equal(snap.map.tiles.length, CONFIG.map.cols * CONFIG.map.rows);
  assert.equal(snap.status, 'playing');
});
