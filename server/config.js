/**
 * Central configuration for Catch the Troublemaker.
 * All gameplay numbers live here so the balance can be tuned in one place
 * and documented easily in docs/AI_PROCESS.md.
 */

export const CONFIG = {
  map: {
    cols: 44,
    rows: 30,
    obstacles: 27,
  },

  coinGoal: 50,
  initialCoins: 132,
  maxCoins: 140,
  coinRespawnMs: 1400,

  // 6 minutes: long enough for a comeback, short enough for a class demo.
  gameDurationMs: 6 * 60 * 1000,

  tickMs: 50,          // server simulation: 20 Hz
  stateHz: 15,         // snapshots pushed to clients: 15 Hz

  playerRadius: 0.36,
  coinPickupRadius: 0.52,
  catchRadius: 0.95,

  livesPerTroublemaker: 3,
  respawnMs: 3000,
  graceMs: 1500,       // cannot be caught during this window after respawn
  startGraceMs: 5000,  // head start so troublemakers can build a lead

  // Troublemakers have the higher cruising speed; parents close the gap
  // with a shorter, less frequent sprint. See docs/AI_PROCESS.md (balance).
  speed: {
    parents: 5.0,
    troublemakers: 6.3,
  },
  sprint: {
    multiplierParents: 1.55,
    multiplierTroublemakers: 1.6,
    durationMs: 1400,
    cooldownMs: 7000,
  },
  confiscateCooldownMs: 900, // parents cannot vacuum the whole map instantly

  maxEvents: 24,
};

export const ROLE_ORDER = [
  'fatherA', 'motherA', 'troublemakerA',
  'fatherB', 'motherB', 'troublemakerB',
];

export const ROLES = {
  fatherA: {
    family: 'A', team: 'parents', short: 'FA', shape: 'square', color: '#ff8a5b',
    name: { en: 'Dad A', ca: 'Pare A' },
  },
  motherA: {
    family: 'A', team: 'parents', short: 'MA', shape: 'triangle', color: '#ffd166',
    name: { en: 'Mum A', ca: 'Mare A' },
  },
  troublemakerA: {
    family: 'A', team: 'troublemakers', short: 'TA', shape: 'star', color: '#ef476f',
    name: { en: 'Troublemaker A', ca: 'Entremaliat A' },
  },
  fatherB: {
    family: 'B', team: 'parents', short: 'FB', shape: 'square', color: '#4cc9f0',
    name: { en: 'Dad B', ca: 'Pare B' },
  },
  motherB: {
    family: 'B', team: 'parents', short: 'MB', shape: 'triangle', color: '#8ac4ff',
    name: { en: 'Mum B', ca: 'Mare B' },
  },
  troublemakerB: {
    family: 'B', team: 'troublemakers', short: 'TB', shape: 'star', color: '#9b5de5',
    name: { en: 'Troublemaker B', ca: 'Entremaliat B' },
  },
};

// Opposite corners: each troublemaker starts far from their OWN parents,
// but close to the other family (who cannot catch them).
export function spawnFor(role) {
  const { cols, rows } = CONFIG.map;
  const spots = {
    fatherA:        { x: 3.5,  y: 3.5 },
    motherA:        { x: 6.5,  y: 3.5 },
    troublemakerA:  { x: 3.5,  y: rows - 3.5 },
    fatherB:        { x: cols - 3.5, y: rows - 3.5 },
    motherB:        { x: cols - 6.5, y: rows - 3.5 },
    troublemakerB:  { x: cols - 3.5, y: 3.5 },
  };
  return spots[role];
}

export const BOT_NAMES = {
  fatherA: 'BOT Dad A',
  motherA: 'BOT Mum A',
  troublemakerA: 'BOT Trouble A',
  fatherB: 'BOT Dad B',
  motherB: 'BOT Mum B',
  troublemakerB: 'BOT Trouble B',
};
