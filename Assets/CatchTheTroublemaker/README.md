# Unity 2D client — Catch the Troublemaker

This folder is the **Unity client** for the game. It talks to the same authoritative
Node.js server as the browser client in `public/`.

## Run

1. Start the backend from the repository root: `node server/index.js` (or `npm start`).
2. Open a scene with the wired objects (`Assets/Scenes/SampleScene.unity`, or your own
   scene after running the setup command). The Hierarchy contains:
   - `Main Camera` (orthographic, follows the local player)
   - `Global Light 2D`
   - `EventSystem`
   - **`GameManager`** with children `Network`, `UI`, `World` and `Input`
3. Press **Play**. No runtime bootstrap is used — everything is a normal scene object.
4. In the home screen set **Server URL** (`http://localhost:3000` by default).
   For a different machine use the host's LAN IP, e.g. `http://192.168.1.20:3000`.
5. Create a room, share the 5-letter code, pick a role, and let the host press **Start**.

If you ever need to recreate the scene structure, use
**Tools > Catch the Troublemaker > Setup Scene** in the editor.

## Peer-to-peer (no server)

Press **MODE: ONLINE** until it reads **MODE: P2P** on the Home screen, then toggle
**P2P: SOLO / P2P: MULTI**:

- **Solo** — press **PLAY SOLO**, pick your character in the lobby and press **START**.
  Five bots fill the remaining seats; no code, server or LAN connection is needed.
- **Multi** — one player creates a 5-letter room, the others join with that code.
  Peers on the same LAN find each other via UDP 3002 and connect directly over TCP.
  **START is disabled until at least 2 real players are connected**; empty roles are
  bots. There is no host and no HTTP winner endpoint — every peer computes the winner
  locally. See [docs/P2P.md](../../docs/P2P.md).

## Connecting from another network

See [docs/NETWORKING.md](../../docs/NETWORKING.md) for the full step-by-step guide
(LAN, mobile data, Cloudflare Tunnel / ngrok / cloud hosting).

`localhost` always means **the device running the Unity client**. It only works when
the Node server runs on that same machine. The **TEST** button next to Server URL
checks `GET /api/health` without creating a room; the server prints its LAN addresses
on startup:

```
Catch the Troublemaker -> http://localhost:3000
  LAN: http://192.168.1.20:3000   (same Wi-Fi only)
```

- **Same Wi-Fi / LAN:** use one of those LAN URLs on every player's device and allow
  port 3000 through the host's firewall. Mobile data counts as a *different* network,
  so a LAN IP will not work there.
- **Mobile data / different place:** the server must be reachable publicly — publish
  port 3000 with a tunnel (Cloudflare Tunnel, ngrok, localtunnel, …) or deploy the
  Node server to a cloud host, then paste that HTTPS/HTTP URL into **Server URL**.
- The lobby keeps polling every 350 ms, so it recovers automatically when connectivity
  returns. If you changed networks while a room was open, go **Leave → Home**, set the
  new Server URL, press **TEST**, then create or join again.

## Scripts

| File | Responsibility |
| --- | --- |
| `GameManager.cs` | **Central scene controller**: session state machine (Home → Lobby → Playing → Result), commands, preferences |
| `NetworkManager.cs` | REST commands, SSE stream, `/state` polling fallback; raises typed events |
| `UIManager.cs` | uGUI overlay: home, waiting room, HUD, touch pad, result screen |
| `BoardRenderer.cs` | World-space 2D map, coins and player sprites; orthographic camera follow |
| `PlayerController.cs` | Input System keyboard and touch intent (never moves the player locally) |
| `CTTApi.cs` | Async `UnityWebRequest` GET/POST wrapper |
| `CTTSseClient.cs` | Server-Sent Events reader with a `DownloadHandlerScript` parser and main-thread event queue |
| `CTTData.cs` | Serializable DTOs matching the server JSON |
| `CTTConfig.cs` | Role metadata, colours and CA/EN strings |
| `CTTDraw.cs` | Procedural textures: map, coins and the square character fallback marker |
| `CTTUI.cs` | uGUI factory: panels, labels, buttons, fields and sprite/colour fallback icons |
| `CTTHoldButton.cs` | Held-button behaviour for the touch D-pad and sprint |
| `P2PConfig.cs` / `P2PEngine.cs` | Deterministic C# port of the rules engine (solo bots and multiplayer lockstep) |
| `P2PTransport.cs` | UDP LAN discovery + direct TCP mesh for multiplayer rooms |
| `P2PGame.cs` | Solo/multi sessions, role agreement, 2-player minimum, 20 Hz lockstep, local result |
| `Assets/Editor/CTTSceneSetup.cs` | Editor menu command that builds/wires the scene objects |

The runtime scripts now live in `Assets/Scripts/`; this folder keeps the client notes.

## Design notes

- **Server-authoritative:** the client only sends `{dx, dy, sprint}`. Coins, catches,
  lives, the timer and the winner are computed by `server/engine.js`.
- **Async transport:** SSE (`GET /api/rooms/:code/events?playerId=...`) plus a 350 ms
  `/state` poll as a fallback. Input intent is POSTed every 100 ms.
- **World-space 2D:** the map is one sprite generated with `Texture2D.SetPixels32`
  (16 px per tile); coins and players are `SpriteRenderer` objects, and the camera
  follows the local player with smoothing and map-bounds clamping.
- **Characters are selected-family sprites:** `fatherA`/`motherA`/`troublemakerA` come
  from family A and the B roles from family B. The menu legend, lobby cards, HUD strip,
  score tags and world `SpriteRenderer` objects all use the same art. The local player
  gets a thin white frame; the flat role-coloured square remains the fallback.
- **Procedural art:** the map and coins are generated with `Texture2D.SetPixels32`;
  character PNGs are copied from `Assets/Sprites/Characters/Troublemakers/` into
  `Assets/Resources/CTTCharacters/` so runtime builds can load them. CA/EN toggle included.
- **Reconnect:** room code and player ID are kept in `PlayerPrefs`; reloading the
  scene rejoins an in-progress match.

## Verification

The scripts are compiled against Unity 6000.6.9f1 assemblies (Input System 1.20.1,
uGUI 2.6.0). `npm test` in the repository root verifies the backend rules and API.
