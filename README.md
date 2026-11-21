# Catch the Troublemaker / Catch l'entremaliat

A hyper-casual **multiplayer** game for 2–6 players on different machines. Two families share one randomly generated map: parents chase their own child, troublemakers collect coins and try to escape.

Two clients share the same authoritative Node.js backend:

- **Unity Universal 2D client** — the project in this folder (`Assets/CatchTheTroublemaker`).
- **Browser client** — a dependency-free HTML5 fallback in `public/`.

The server has **no npm dependencies** and no build step.

---

## Quick start

```bash
node server/index.js
```

Requires Node.js ≥ 18 (tested on Node 24). Optional: `PORT=4000 node server/index.js`. For a quick demo match, `GAME_DURATION_MS=15000 node server/index.js`.

### Play in Unity

1. Open this folder in Unity 6000.6.9f1 (the Universal 2D template).
2. Open `Assets/Scenes/SampleScene.unity` and press **Play**. No scene wiring is needed: `CttBootstrap` creates the camera, EventSystem and UI at runtime.
3. The home screen has a **Server URL** field (default `http://localhost:3000`). Change it to your machine's LAN IP (e.g. `http://192.168.1.20:3000`) when playing from another computer.
4. Create a room or join with the 5-letter code, pick a role, and let the host start.

All artwork (coins, walls, ■ ▲ ★ player shapes and the map texture) is generated procedurally at runtime — no image assets to import.

### Play in a browser

Open `http://localhost:3000` (or `http://<server-ip>:3000`).

1. **Create a room.** The creator is the *host* (game master).
2. **Share the 5-letter code.** Each player opens the URL and joins with that code.
3. **Pick a role** in the waiting room (if you don't, one is assigned at kickoff). Free roles are played by **bots**, so 2 humans are enough.
4. **Host presses Start.** The server generates a new random map and begins the match.

### Controls

| Action | Keyboard | Touch |
| --- | --- | --- |
| Move | `WASD` or arrow keys | D-pad |
| Sprint | `Space` or `Shift` | SPRINT button |
| Change language | top-right `CA / EN` | same |
| High contrast | top-right `High contrast` | same |
| Text size (browser only) | `A−` / `A+` | same |

Both clients enforce the same rule: only your own parents can catch you. The Unity client has a LAN server field so it can play against browser players on other machines.

---

## Rules

| Team | Win condition |
| --- | --- |
| **Troublemakers** | Any troublemaker reaches **50 coins**. |
| **Parents** | Both troublemakers are grounded (3 catches each) **or** the 6-minute timer expires. |

**The key asymmetry:** father A and mother A can catch **only** troublemaker A. Father B and mother B can catch only troublemaker B. A wrong-family tag does nothing — this is enforced on the server, not in the client.

When a child is caught they lose **all** their coins, are sent home for 3 seconds and cannot be caught again for 1.5 seconds. Parents may also **confiscate** coins by walking over them (900 ms cooldown per parent).

### How the difficulty is balanced

The "only your own parents can catch you" rule heavily favours the kids, so the following counters were added:

| Lever | Troublemakers | Parents | Why |
| --- | --- | --- | --- |
| Cruising speed | 6.3 tiles/s | 5.0 tiles/s | Kids can outrun a parent in a straight line. |
| Sprint | ×1.6, 1.4 s, 7 s cooldown | ×1.55, 1.4 s, 7 s cooldown | Parents get a *comparable* burst to close a gap, not an instant catch. |
| Lives | 3 each | — | One mistake is not the end of the match. |
| Coin penalty | Drop every coin when caught | Confiscate with cooldown | Parents can slow a kid without deleting the whole map. |
| Start | 5 s grace | — | Kids get a head start to build a lead. |
| Information | Everyone sees everyone | Everyone sees everyone | Keeps the rules legible; asymmetry comes from speed/roles instead of fog. |
| Bots | Fill empty seats | Fill empty seats | A match is never stuck waiting for a sixth player. |
| Opponent corner | Start far from own parents | Start far from own child | No instant spawn-camping. |

All values live in [`server/config.js`](server/config.js) so they can be tuned in one place. The reasoning and the iterations are documented in [`docs/AI_PROCESS.md`](docs/AI_PROCESS.md).

---

## Architecture

```
 Unity player ─┐                ┌── HTTP REST ── create / join / role / start / input / result
 Browser player┼── async ──────▶│  Node.js server (server/index.js)
 Unity player ─┘                │   • rooms & host control
                               │   • authoritative 20 Hz simulation loop
                               │   • SSE broadcaster (15 Hz snapshots)
                               └──▶ Match engine (server/engine.js)
                                      • seeded random map + BFS pathfinding (bots)
                                      • collision, coins, catch rule, win detection
                                      • pure module → unit-testable
```

- **Client → server:** small `POST /api/rooms/:code/input` messages carrying only `{dx, dy, sprint}`.
- **Server → client:** Server-Sent Events stream `hello`, `lobby`, `start`, `state` and `result`. Browsers use `EventSource`; Unity parses the same stream with a custom `DownloadHandlerScript`. Both also poll `/state` as a fallback.
- **Authority:** neither client decides coins, catches or the winner. Both only draw snapshots.
- **Mixed matches:** a Unity player can play against a browser player — they are just HTTP clients of the same room.

### Files

| Path | Purpose |
| --- | --- |
| `server/index.js` | HTTP routes, SSE, room lifecycle, match persistence |
| `server/engine.js` | Deterministic map generation, movement, rules, bots, snapshots |
| `server/config.js` | All balance constants and role metadata |
| `Assets/CatchTheTroublemaker/Scripts/*.cs` | Unity 2D client: bootstrap, HTTP/SSE transport, JSON DTOs, procedural art, uGUI screens and input |
| `public/index.html` | Four screens: home, lobby, game, result |
| `public/styles.css` | Responsive dark UI, high-contrast and reduced-motion support |
| `public/app.js` | i18n (CA/EN), canvas renderer, input, SSE client |
| `test/engine.test.js` | Rule tests (map, catch ownership, coins, wins) |
| `test/api.test.js` | End-to-end HTTP tests |
| `docs/AI_PROCESS.md` | AI-assisted development log (Catalan) |
| `docs/PRESENTATION.md` | Schematic exposition outline |
| `data/matches.jsonl` | Append-only record of finished matches (created at runtime) |

### Endpoints

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/health` | Server status |
| `GET` | `/api/config` | Balance constants and role metadata |
| `POST` | `/api/rooms` | Create room → `{ code, playerId }` |
| `POST` | `/api/rooms/:code/join` | Join (reuses `playerId` on reconnect) |
| `GET` | `/api/rooms/:code` | Lobby state |
| `POST` | `/api/rooms/:code/role` | Choose/swap role |
| `POST` | `/api/rooms/:code/start` | Host starts the match |
| `POST` | `/api/rooms/:code/input` | Player intent |
| `GET` | `/api/rooms/:code/events` | **SSE** stream of async updates |
| `GET` | `/api/rooms/:code/state` | Full snapshot (polling fallback) |
| `GET` | `/api/rooms/:code/result` | **Required endpoint: who won** |
| `POST` | `/api/rooms/:code/restart` | Host starts a rematch with a new seed |

Example result:

```json
{
  "status": "finished",
  "finished": true,
  "result": {
    "winnerTeam": "parents",
    "winnerId": "p-4f2a...",
    "winnerName": "The parents",
    "reason": "both troublemakers were grounded",
    "durationMs": 143220,
    "goal": 50,
    "stats": { "catches": 6, "coinsCollected": 71, "confiscated": 12 }
  }
}
```

---

## Verification

```bash
npm test          # 15 tests: engine rules + HTTP API + no-SSE presence
```

A browser smoke test was also performed: a room was created, a role chosen, the match started, the SSE stream delivered state, a coin was collected and a valid catch fired (see `docs/AI_PROCESS.md`).

## Requirements checklist

| Requirement | Implementation |
| --- | --- |
| More than one player, each on their own machine | Unity and browser clients around one Node server |
| Lobby / master | Waiting room + host-only start/restart |
| Random board with spawn points | Seeded map generation, four corner spawns, BFS connectivity |
| Shared goal | 50 coins, 132 on the map with respawn |
| Asynchronous communication | Server-Sent Events + input POSTs, polling fallback |
| Endpoint returning the winner | `GET /api/rooms/:code/result` |
| Aesthetics / usability / accessibility | Procedural uGUI + canvas UI, CA/EN, keyboard + touch, ARIA live toasts (browser), high contrast, text sizing (browser), shape+colour coding, reduced motion (browser) |
| Developed with AI agents and documented | This repository + `docs/AI_PROCESS.md` + `docs/PRESENTATION.md` |
| Git | Repository initialised; commits at each milestone |

The original Universal 2D template structure (`Assets/Scenes`, `Settings`, `Packages`) is preserved; the game is added under `Assets/CatchTheTroublemaker/` alongside `public/` and `server/`.
