# Exposició — esquema de presentació

Durada suggerida: 8–10 minuts + 3 minuts de demo i preguntes.
Guions detallats a `README.md` i `docs/AI_PROCESS.md`.

---

### 1. Portada
- **Catch the Troublemaker** / *Catch l'entremaliat*
- Joc multijugador de mapa compartit, fet amb agents d'IA.
- Autors · DAM2 · data.

### 2. La idea (30 s)
- Dues famílies: pares + entremaliat.
- Pares → capturar abans dels 50 coins. Nens → arribar a 50 i escapar.
- **Regla clau:** només els pares del mateix fill poden capturar-lo.
- Repte explícit de l'enunciat: equilibrar això.

### 3. Requisits → solució (1 min)

| Requisit | Solució |
| --- | --- |
| >1 jugador, màquines diferents | Clients Unity i navegadors connectats a un servidor Node |
| Sala / master | Lobby + amfitrió que inicia |
| Mapa aleatori | PRNG amb llavor per sala + BFS |
| Comunicació asíncrona | SSE + POST d'intenció |
| Endpoint del guanyador | `GET /api/rooms/:code/result` |
| Estètica / usabilitat / accessibilitat | uGUI + canvas, CA/EN, tàctil, ARIA (web), contrast, formes |
| Documentar el procés IA | `docs/AI_PROCESS.md` |
| Git | Repositori amb *commits* per fases |

### 4. Arquitectura (1 min)
- Dibuixar al quadre:
  `Unity / Navegador → POST /input → Servidor (20 Hz) → Motor → snapshots SSE → Unity / Navegador`
- **Servidor autoritatiu:** el client només envia intenció; no decideix res.
- Motor pur (`engine.js`) → testejable sense HTTP.
- **Client Unity:** `GameManager` a l'escena coordina `NetworkManager`, `UIManager`,
  `BoardRenderer` i `PlayerController`. El món és 2D de veritat: mapa i actors són
  `SpriteRenderer` i la càmera ortogràfica segueix el jugador. **Personatges = quadrats
  de color** al menú, al lobby i al HUD (marc blanc per al jugador local).
  `DownloadHandlerScript` llegeix el mateix flux SSE que `EventSource` al navegador.

### 5. El joc en acció (demo, 3 min)
1. `node server/index.js`.
2. A Unity: obrir `SampleScene` i prémer Play. Al navegador: `http://localhost:3000`.
3. Crear sala des d'Unity, copiar codi, unir-s'hi des del navegador (o una altra instància).
4. Triar rols: un pare A i un entremaliat A. La resta són bots.
5. Start → explicar HUD: monedes per família, temps, vides, esprint.
6. Moure's amb WASD; demonstrar captura *correcta* i captura d'**altra família** (no fa res).
7. Obrir `/api/rooms/<cod>/result` al final (o mostrar un resultat desat).
- **Punt fort per a la defensa:** són clients diferents (Unity + web) jugant a la mateixa partida.

### 6. Equilibri (1,5 min) — el punt important
- Problema: "només els teus pares poden caçar-te" afavoreix els nens.
- Taula ràpida de 7 contrapessos: velocitat, esprint, vides, pèrdua de monedes,
  confiscació, head start, bots i posicions inicials.
- **Dada observada:** abans l'esprint dels pares era ×1.9 (9.9 caselles/s) i les
  captures eren massa ràpides; després d'ajustar a ×1.55 i afegir 5 s d'immunitat,
  la partida dura més i hi ha marge de remuntada.

### 7. Accessibilitat i usabilitat (45 s)
- Color **+ forma** al navegador (■ pare, ▲ mare, ★ entremaliat); el client Unity fa
  servir **quadres de color** amb marc per al jugador local → no depèn només del color.
- Català/anglès, mida de text, alt contrast, `prefers-reduced-motion`.
- `aria-live` per a esdeveniments; teclat complet i control tàctil.

### 8. Procés amb IA (1,5 min)
- Eina: **OpenCode (Big Pickle)**.
- Prompt inicial = idea + enunciat complet + FAQ.
- **Pregunta prèvia:** quina tecnologia? → l'usuari va triar Web + Node.
- **Segon prompt:** "fes-ho amb el Unity Universal 2D Project" → es va afegir el client Unity
  conservant el servidor Node com a backend (l'enunciat demana endpoint i multi-màquina).
- **Tercer prompt:** "un joc 2D normal de Unity, amb un GameManager" → es va refactoritzar
  el client en managers especialitzats i es va muntar l'escena a la Hierarchy.
- **Quart prompt:** "full peer-to-peer, no host" → es va afegir un mode P2P amb motor
  determinista en C#, descobriment LAN i lockstep; l'endpoint del guanyador queda només
  al mode servidor opcional, tal com va decidir l'usuari.
- No es va copiar la primera resposta: es van investigar SSE, BFS, PRNG i `node --test`.

### 9. Iteracions (1 min) — "què va fallar"
- Rutes API desplaçades → 404 → corregit.
- Bots encallats a parets → BFS.
- Test amb la moneda al lloc equivocat → corregit.
- Esdeveniments duplicats → deduplicació.
- Desconnexions → bot que agafa el relleu.
- Equilibri ajustat després d'una partida real.
- Client Unity: `CttApi` vs `CTTApi`, paràmetre duplicat, enum `Screen` vs `UnityEngine.Screen`
  → 0 errors de compilació amb el Roslyn d'Unity.
- Presència del servidor: sense SSE un client Unity es convertia en bot → `room.seen`,
  recalculat al *tick loop* i a `/input`, amb finestra de gràcia a l'inici.
- Primer client Unity només codi (`CttBootstrap`) → l'usuari va demanar un joc 2D normal
  amb `GameManager` → es va dividir en managers i es va escriure l'escena amb un Editor script.

### 10. Verificació i lliçons (45 s)
- **15/15 tests** (`npm test`): regles + API + presència sense SSE.
- Prova al navegador: sala, rol, start, moneda, captura, sense errors de consola.
- Scripts Unity compilats amb 0 errors i importats per Unity 6; l'escena `SampleScene`
  conté el `GameManager` amb els seus quatre fills i referències serialitzades.
- Lliçó: preguntar abans, separar motor de transport, provar l'equilibri, documentar sobre la marxa, fer servir Git.

### Preguntes probables
- *Per què SSE i no WebSocket?* → només cal push unidireccional; HTTP pur i reconnexió automàtica.
- *I si un jugador no connecta?* → els rols lliures els agafen bots.
- *Es pot falsejar la victòria des del client?* → al mode servidor no (el motor del
  servidor decideix); al mode P2P tots els peers calculen el mateix resultat amb el
  mateix motor determinista, sense endpoint.
- *On es desa la partida?* → `data/matches.jsonl`; opcional, no obligatori a l'enunciat.
- *Per què Unity i Node alhora?* → Unity és el client que demanava l'usuari; Node conserva l'endpoint, l'asíncron i el jugable entre màquines.
- *Cal importar sprites?* → no: monedes, parets, mapa i formes es generen amb `Texture2D` en temps d'execució.
- *Com es reconnecta a Unity?* → codi i `playerId` es desen a `PlayerPrefs`; en rellegir l'escena es rejoin la partida.
