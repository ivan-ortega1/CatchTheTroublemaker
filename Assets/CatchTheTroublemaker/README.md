# Unity 2D client — Catch the Troublemaker

This folder is the **Unity client** for the game. It talks to the same authoritative
Node.js server as the browser client in `public/`.

## Run

1. Start the backend from the repository root: `node server/index.js` (or `npm start`).
2. In Unity, open `Assets/Scenes/SampleScene.unity` and press **Play**.
3. In the game's home screen set **Server URL** (`http://localhost:3000` by default).
   For a different machine use the host's LAN IP, e.g. `http://192.168.1.20:3000`.
4. Create a room, share the 5-letter code, pick a role, and let the host press **Start**.

No scene setup is required. `CttBootstrap` runs on scene load and creates the camera,
the EventSystem (with the new Input System UI module), and all uGUI screens.

## Scripts

| File | Responsibility |
| --- | --- |
| `CTTBootstrap.cs` | Runtime setup; no manual scene wiring |
| `CTTApi.cs` | Async `UnityWebRequest` GET/POST wrapper |
| `CTTSseClient.cs` | Server-Sent Events reader with a `DownloadHandlerScript` parser and main-thread event queue |
| `CTTData.cs` | Serializable DTOs matching the server JSON |
| `CTTConfig.cs` | Role metadata, colours, shapes and CA/EN strings |
| `CTTDraw.cs` | Procedural textures: map, walls, coins, square/triangle/star markers |
| `CTTUI.cs` | Small uGUI factory (panels, labels, buttons, fields) |
| `CTTHoldButton.cs` | Held-button behaviour for the touch D-pad and sprint |
| `CTTGame.cs` | Screens, lobby, polling, input, rendering, result |

## Design notes

- **Server-authoritative:** the client only sends `{dx, dy, sprint}`. Coins, catches,
  lives, the timer and the winner are computed by `server/engine.js`.
- **Async transport:** SSE (`GET /api/rooms/:code/events?playerId=...`) plus a 350 ms
  `/state` poll as a fallback. Input intent is POSTed every 100 ms.
- **Procedural art:** every sprite and the whole map texture are generated at runtime
  with `Texture2D.SetPixels32`; there are no external image dependencies.
- **Accessibility:** shape + colour coding (■ parent, ▲ parent, ★ troublemaker),
  CA/EN toggle, high-contrast mode, keyboard and touch controls.
- **Reconnect:** the room code and player ID are kept in `PlayerPrefs`; reloading the
  scene rejoins an in-progress match.

## Verification

The scripts are compiled against Unity 6000.6.9f1 assemblies (Input System 1.20.1,
uGUI 2.6.0). `npm test` in the repository root verifies the backend rules and API.
