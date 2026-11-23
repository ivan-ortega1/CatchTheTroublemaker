# Peer-to-peer mode (no server, no host)

The Unity client has a second networking mode: **P2P**. In this mode there is no
Node server, no host/master player and no HTTP winner endpoint. Every machine runs
the same game simulation locally and computes the same winner.

## How to play

1. On **every** machine, open the project and press Play.
2. On the Home screen press **MODE: ONLINE** until it reads **MODE: P2P**. The
   Server URL field, FIND LAN and TEST buttons disappear — they are not needed.
3. One player presses **CREATE P2P ROOM** and shares the generated 5-letter code.
4. Everyone else types that code in **Join code** and presses **JOIN**.
   Peers on the same LAN find each other automatically (UDP broadcast on port 3002).
5. Pick a role and press **START** on any machine. Empty roles become bots.
6. When the match ends, every peer shows the same winner on its own Result screen.

The Node process does **not** need to be running. Keep every Unity window/player
open; if a peer disconnects mid-match the others are notified.

## How it works

```
peer A  ─┐
peer B  ─┼── LAN broadcast discovery (UDP 3002) ──> direct TCP mesh
peer C  ─┘
```

| Layer | File | Responsibility |
| --- | --- | --- |
| Discovery + mesh | `Assets/Scripts/P2PTransport.cs` | Finds peers on the LAN, opens one TCP connection per pair, delivers newline-delimited JSON messages. |
| Deterministic rules | `Assets/Scripts/P2PEngine.cs` | C# port of `server/engine.js`: seeded map, BFS bots, collision, coins, family-only catching, respawn, win detection. |
| Session / lockstep | `Assets/Scripts/P2PGame.cs` | Room code, role claims, hostless seed and player-list agreement, 20 Hz input exchange, local result. |
| UI / integration | `UIManager.cs`, `GameManager.cs` | Mode switch and routing; the existing BoardRenderer/HUD draw P2P snapshots exactly like server snapshots. |

### No host, no endpoint

- The **seed** is the XOR of every peer's random seed, so no peer controls it.
- If several peers announce a start, everyone picks the message from the
  **lowest peer id** — a deterministic tie-break, not a permanent host.
- Role conflicts use the same "lowest peer id wins" rule; remaining free roles are
  assigned in peer-id order.
- The winner is computed by `P2PMatch` on each machine. The assignment's HTTP
  `GET /api/rooms/:code/result` endpoint exists only in the optional server mode
  and is deliberately unused here.

### Lockstep

Every 50 ms each peer broadcasts its movement intent for the next tick. A tick is
simulated when all participants' inputs have arrived; after 250 ms a missing input
is treated as "no movement" so a lost packet cannot freeze the game. Because all
peers run the identical `P2PMatch`, identical inputs produce identical state.

## Verification

The C# engine was checked against the authoritative JS engine for the same seed,
the same six players and 400 ticks, and for a 2-human + 4-bot match. Positions,
coin counts, lives and map layout matched exactly.

## Limitations

- **Same LAN only for now.** UDP broadcast and direct TCP do not cross NAT. For
  players on different networks, either keep using the server mode with a public
  tunnel (see [NETWORKING.md](NETWORKING.md)) or add a relay/tunnel to the P2P
  transport later. P2P does not remove the addressing problem across the internet.
- Guest Wi-Fi / client isolation blocks peer discovery.
- A peer joining after the match has started waits for the next match.
- All peers must use the same build (same gameplay constants).
