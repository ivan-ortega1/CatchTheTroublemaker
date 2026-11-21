# Unity 2D client — Catch the Troublemaker

This folder is the **Unity client** for the game. It talks to the same authoritative
Node.js server as the browser client in `public/`.

## Run

1. Start the backend from the repository root: `node server/index.js` (or `npm start`).
2. Open `Assets/Scenes/SampleScene.unity`. The Hierarchy already contains:
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
| `CTTConfig.cs` | Role metadata, colours, shapes and CA/EN strings |
| `CTTDraw.cs` | Procedural textures: map, coins, square/triangle/star markers |
| `CTTUI.cs` | Small uGUI factory (panels, labels, buttons, fields) |
| `CTTHoldButton.cs` | Held-button behaviour for the touch D-pad and sprint |
| `Editor/CTTSceneSetup.cs` | Editor menu command that builds/wires the scene objects |

## Design notes

- **Server-authoritative:** the client only sends `{dx, dy, sprint}`. Coins, catches,
  lives, the timer and the winner are computed by `server/engine.js`.
- **Async transport:** SSE (`GET /api/rooms/:code/events?playerId=...`) plus a 350 ms
  `/state` poll as a fallback. Input intent is POSTed every 100 ms.
- **World-space 2D:** the map is one sprite generated with `Texture2D.SetPixels32`
  (16 px per tile); coins and players are `SpriteRenderer` objects, and the camera
  follows the local player with smoothing and map-bounds clamping.
- **Procedural art:** no external image assets. Shape + colour coding (■ parent,
  ▲ parent, ★ troublemaker), CA/EN toggle, high-contrast mode.
- **Reconnect:** room code and player ID are kept in `PlayerPrefs`; reloading the
  scene rejoins an in-progress match.

## Verification

The scripts are compiled against Unity 6000.6.9f1 assemblies (Input System 1.20.1,
uGUI 2.6.0). `npm test` in the repository root verifies the backend rules and API.
