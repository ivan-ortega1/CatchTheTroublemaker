# Peer-to-peer mode (no server, no host)

The Unity client has a second networking mode: **P2P**. In this mode there is no
Node server, no host/master player and no HTTP winner endpoint. Every machine runs
the same game simulation locally and computes the same winner.

## How to play

1. On the Home screen press **MODE: ONLINE** until it reads **MODE: P2P**. The
   Server URL field, FIND LAN and TEST buttons disappear — they are not needed.
2. Press the new **P2P: MULTI / P2P: SOLO** button to choose the kind of session.

### Solo (1 real player + 5 bots)

1. Leave the toggle on **P2P: SOLO**. The join-code field is hidden and the main
   button reads **PLAY SOLO**.
2. Press **PLAY SOLO**. You go straight to the lobby; no room code or network
   connection is needed.
3. Choose the two families on the family cards (two are preselected, click a selected
   card to remove it) and the character you want to be (one of the six coloured squares).
   Then press **START**. The other five seats are bots controlled by the same local engine.
4. The Result screen shows the winner computed locally, exactly as in a network match.

### Multiplayer (2–6 real players, empty seats are bots)

1. Toggle to **P2P: MULTI**. The button reads **CREATE P2P ROOM** and the join-code
   field is visible again.
2. One player presses **CREATE P2P ROOM** and shares the generated 5-letter code.
3. Everyone else types that code in **Join code** and presses **JOIN**.
   Peers on the same LAN find each other automatically (UDP broadcast on port 3002).
4. The player counter and the **username list** increase as soon as the TCP
   handshake completes; you do not have to pick a role first. The room creator is
   the master shown as `[HOST]` in that list. Names disappear again when a player
   leaves (explicit Leave/Quit or a 6 s heartbeat timeout).
5. The master picks two different families; cards show all three character sprites
   and get an **A**/**B** badge. Click a selected card to remove it — a third card is
   locked until one of the two is removed. Guests see the cards read-only, and the
   choice is rebroadcast in the roster message (also to late joiners).
6. Everyone picks a character. **START stays disabled until (a) at least 2 real
   players are connected, (b) every connected player has chosen a role,
   (c) two different families are chosen, and (d) the pressing player is the room
   creator.** The lobby hint explains which condition is still missing. One press by
   the master is enough: the other peers answer with their own ready seed.
7. Any empty role is played by a bot, so 2 humans can fill the six seats.
8. When the match ends, every peer shows the same winner on its own Result screen.

The Node process does **not** need to be running in either mode. Keep every Unity
window/player open; if a peer disconnects mid-match the others are notified.

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
| Session / lockstep | `Assets/Scripts/P2PGame.cs` | Solo session (one human + five bots, no transport), room code, role claims, master-only family selection broadcast in the roster, hostless seed and player-list agreement, 20 Hz input exchange, 2-human minimum for multiplayer, local result. |
| UI / integration | `UIManager.cs`, `GameManager.cs` | Mode switch and routing; the existing BoardRenderer/HUD draw P2P snapshots exactly like server snapshots. |

### No central server, no endpoint

- In **Solo** there is no transport at all: `P2PGame` ticks `P2PMatch` locally with
  one human and five bots. The UI never shows a code and never requires the LAN.
- In **Multiplayer** the room creator is the **lobby master**: only that client's
  START and family cards are enabled, and only after all connected players have
  chosen a role and two different families are set. This is lobby control only;
  there is no authoritative simulator. If the master leaves, the lowest remaining
  peer id is promoted on every client and keeps the already-chosen families.
- Family ids are **display metadata**: they only name the two cards in the lobby.
  The engine keeps its internal `A`/`B` slots, role colours and family-only catch
  rule, so no simulation parity is affected.
- START is gated in two places: the lobby disables it with an explanatory hint and
  `P2PGame.StartMatch()` rejects an early/non-master start.
- **Leaving is detected immediately** on an explicit `bye` (Leave/Quit) and within
  6 s otherwise: every peer sends a `ping` every 2 s and a silent peer is removed.
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
Pressing START once makes the other peers answer with their own ready seed, so the
shared kickoff does not require everybody to click. In Solo the same loop runs with
a single participant and no network messages.

The TCP mesh alone is not enough to populate the lobby: as soon as a socket is
ready, `P2PTransport` now sends a `hello` with the peer id/name on **both** ends.
That is what makes `P2PGame.OnPeerJoined()` fire and the player counter increase.
`runInBackground` is enabled in `ProjectSettings` so the peer that created the room
keeps pumping messages even while its Unity window is not focused.

## Verification

The C# engine was checked against the authoritative JS engine for the same seed,
the same six players and 400 ticks, and for a 2-human + 4-bot match. Positions,
coin counts, lives and map layout matched exactly. After the Solo/Multiplayer
change, all `Assets/Scripts` and `Assets/Editor` C# files compile with Unity's
Roslyn with 0 errors and `npm test` still passes 15/15.

## Limitations

- **Same LAN only for now.** UDP broadcast and direct TCP do not cross NAT. For
  players on different networks, either keep using the server mode with a public
  tunnel (see [NETWORKING.md](NETWORKING.md)) or add a relay/tunnel to the P2P
  transport later. P2P does not remove the addressing problem across the internet.
- Guest Wi-Fi / client isolation blocks peer discovery.
- A peer joining after the match has started waits for the next match.
- All peers must use the same build (same gameplay constants).
