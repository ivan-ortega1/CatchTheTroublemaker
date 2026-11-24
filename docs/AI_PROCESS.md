# Procés de desenvolupament amb agents d'IA

> Document de seguiment de l'activitat. Es va anar escrivint **mentre** es treballava,
> no després, perquè el procés és part de l'avaluació.

---

## 1. Eines utilitzades

| Eina | Model / versió | Per a què s'ha fet servir |
| --- | --- | --- |
| **OpenCode** (agent d'IA) | `Big Pickle` (`opencode/big-pickle`) | Disseny del joc, codi del servidor i del client, tests, documentació i raonament d'equilibri. |
| Node.js | v24.16.0 | Execució del servidor i dels tests (`node --test`). Comprovat amb `node --version`. |
| Eina de navegador de l'entorn | Chromium integrat | Prova real de la interfície: crear sala, triar rol, iniciar partida, comprovar HUD i consola. |
| Git | 2.54.0 | Control de versions, tal com demana la FAQ. |
| Unity 6.6 (6000.6.9f1) + Universal 2D | Editor instal·lat a l'equip | Segona fase: client del joc dins del projecte Unity de la plantilla. |
| Roslyn (`csc.dll`) d'Unity | Compilador del SDK 8 inclòs amb Unity | Verificació ràpida de compilació dels scripts C# sense tancar l'editor obert. |

No s'han fet servir altres models generatius, llibreries de pagament ni serveis externs.
El projecte **no té dependències npm**: la IA va proposar-ho i es va acceptar perquè facilita
que qualsevol company pugui executar-ho sense `npm install`.

---

## 2. La idea i el prompt inicial

L'usuari va enganxar la idea del joc i l'enunciat complet. Les parts que van condicionar
el treball van ser:

> **Idea:** PARENTS han de caçar els TROUBLEMAKERS abans que un arribi a 50 monedes.
> TROUBLEMAKERS: un d'ells ha d'arribar a 50 monedes i evitar ser capturat.
> **NOTA:** només els pares respectius poden caçar el seu fill (<FATHER_A> i <MOTHER_A>
> només poden caçar <TROUBLEMAKER_A>). "THIS MIGHT SOUND TOO EASY FOR THE TROUBLEMAKERS,
> SO FIND A WAY TO BALANCE DIFFICULTY FOR EACH TEAM."

I de l'enunciat (Catalan):

> "El joc ha de permetre més d'un jugador […] haurem de buscar algun protocol de
> comunicació asíncron. Has de tenir alguna mena d'endpoint que gestioni una partida
> i retorni als jugadors el resultat de qui ha guanyat. A més del bon funcionament
> del joc es valorarà l'estètica, usabilitat i accessibilitat."
>
> "El requisit principal […] és que s'ha de desenvolupar a partir d'agents d'IA […]
> És igual d'important que funcioni com la documentació i exposició de com s'ha
> aconseguit el resultat."

### Decisió prèvia: triar la tecnologia

El workspace era una plantilla buida d'**Unity 2D**. Com que l'enunciat demanava
explícitament un *endpoint*, comunicació asíncrona i jugadors en màquines diferents,
l'agent va preguntar quina tecnologia es volia fer servir i va oferir tres opcions.
L'usuari va triar **Web + Node server**. Aquesta pregunta va evitar implementar
el joc a Unity i haver de descartar-lo després.

---

## 3. Prompts i directrius → què va contestar l'agent

Les instruccions no es van donar com un únic "prompt màgic", sinó com a restriccions
acumulades. Aquest esquema recull les més importants:

| Prompt / directriu donada | Resposta / acció de l'agent |
| --- | --- |
| "Vull aquest joc, ha de seguir els requisits i la FAQ" | Va separar els requisits en una llista verificable (multijugador, sala, mapa aleatori, asíncron, endpoint, estètica/accessibilitat, documentació, Git). |
| "Web + Node server" | Arquitectura client-servidor amb servidor autoritatiu; cap decisió de joc al navegador. |
| "Només els pares respectius poden caçar el seu fill" | Regla implementada al servidor amb `child.family !== parent.family → continue`; prova unitària específica. |
| "Fes un sistema per equilibrar la dificultat" | 7 palanques documentades (velocitat, esprint, vides, pèrdua de monedes, confiscació, head start, bots). Veure §6. |
| "Gestiona una partida i retorna qui ha guanyat" | `GET /api/rooms/:code/result`, amb `winnerTeam`, `winnerId`, `reason`, `stats` i `standings`. |
| "Comunicació asíncrona" | SSE (`GET .../events`) per a estat i `POST .../input` per a intenció; *fallback* de polling. |
| "Sala d'espera o rol de master" | Lobby amb 6 rols, intercanvi de rols i start/restart només de l'amfitrió. |
| "Accessibilitat, usabilitat i estètica" | CA/EN, formes a més de colors, `aria-live`, contrast alt, mida de text, `prefers-reduced-motion`, control tàctil. |
| "Fes servir GIT" | `git init`, `.gitignore` i *commits* per fases. |
| "Documenta-ho mentre treballes" | Aquest fitxer, `README.md` i `docs/PRESENTATION.md`. |

---

## 4. Camins que va obrir l'agent (decisions tècniques)

| Decisió | Per què | Alternativa descartada |
| --- | --- | --- |
| **Servidor autoritatiu** | Evita trampes i fa que l'endpoint de resultat sigui fiable. | Sincronització peer-to-peer (complex i inconsistent). |
| **Server-Sent Events** | És HTTP pur, funciona amb `fetch`/`EventSource`, es reconnecta sol i només cal push servidor→client. | WebSocket (cal handshake i gestió de frames; massa per a un MVP). |
| **POST d'intenció, no de posició** | Missatges molt petits i tolerants a pèrdua de paquets. | Enviar coordenades absolutes (client podria falsificar-les). |
| **PRNG amb llavor per sala** | Tots els clients veuen el mateix mapa i una partida es pot reproduir. | `Math.random()` al client (maps diferents per jugador). |
| **BFS per als bots** | Els bots segueixen corredors i no s'enganxen a les parets. | Vector directe cap a l'objectiu (es quedaven encallats). |
| **Rols buits = bots** | Es pot jugar amb 2 persones i la sala no es bloqueja. | Esperar 6 jugadors humans. |
| **`server/engine.js` sense HTTP** | Es pot provar amb `node --test` sense arrencar el servidor. | Lògica dins de les rutes (no testejable). |
| **Tota la configuració a `config.js`** | Permet ajustar l'equilibri en un sol fitxer i explicar-ho a la presentació. | Constants disperses. |

---

## 5. Recerca tecnològica (no s'ha copiat cegament)

| Tecnologia / idea | Què es va investigar | Resultat |
| --- | --- | --- |
| SSE vs WebSocket vs polling | Es va valorar cost d'implementació, reconnexió i compatibilitat. Es va provar `EventSource` al navegador real. | SSE + fallback de polling. WebSocket era innecessari per a dades unidireccionals. |
| Node sense dependències | `node --version` → v24; mòduls `http`, `fs`, `crypto` i `node:test` ja disponibles. | `package.json` sense `dependencies`; `npm install` no cal. |
| Mapa aleatori connectat | Es va comprovar que un mapa amb obstacles podia deixar zones aïllades. | `floodFill` calcula les caselles assolibles i les monedes només s'hi col·loquen. |
| Col·lisió de cercles amb rajoles | Comprovació dels 4 racers del jugador abans de moure's. | Test `movement collision never places a player inside a wall`. |
| BFS de primer pas | Algorisme clàssic implementat des de zero (no llibreria). | `firstStep()` retorna la direcció correcta fins i tot girant cantonades. |
| Accessibilitat | Bases WAI-ARIA: `aria-live`, `aria-pressed`, `:focus-visible`, `prefers-reduced-motion`. | Codis de forma (■ ▲ ★) a més del color; textos CA/EN; toasts llegibles per lector de pantalla. |
| Git | FAQ ho demanava expressament per poder fer *rollback*. | Repositori inicialitzat i `.gitignore` per excloure `Library/`, `Temp/`, `node_modules/`. |

---

## 6. Equilibri: el problema i la solució

El nucli del repte era la regla "només els teus pares poden caçar-te". Això afavoreix
molt els nens. L'agent va proposar compensacions i es van ajustar després de provar-les:

| Palanca |valor| Efecte |
| --- | --- | --- |
| Velocitat base | nens 6.3 / pares 5.0 | Un nen pot escapar en línia recta. |
| Esprint | nens ×1.6 / pares ×1.55, 1.4 s, 7 s de recàrrega | Els pares poden tancar distància, però no és una captura automàtica. |
| Vides | 3 per nen | Un error no elimina el jugador. |
| Captura | perd totes les monedes, 3 s fora de joc, 1.5 s d'immunitat | Capturar té un cost real. |
| Confiscació dels pares | 1 moneda cada 900 ms | Els pares poden frenar, no buidar el mapa. |
| Head start | 5 s d'immunitat a l'inici | Els nens poden agafar un avantatge inicial. |
| Posició inicial | cada nen lluny dels seus pares (al costat de l'altra família, que no pot caçar-lo) | EvitaSpawn-camping a l'inici. |

A més, el límit de 6 minuts evita partits interminables i dona la victòria
als pares si cap nen arriba a la meta.

---

## 7. Iteracions i reformulacions (el que no va funcionar a la primera)

| # | Problema trobat | Causa | Reformulació |
| --- | --- | --- | --- |
| 1 | El workspace era Unity però l'enunciat demanava endpoint i multi-màquina. | Plantilla del curs, no decisió del joc. | Es va preguntar a l'usuari i es va pivotar a web + Node; Unity es va deixar intacte. |
| 2 | Les rutes `/api/rooms/:code/join` retornaven 404. | `parts[1]` és `rooms`, no el codi; desplaçament d'un segment. | Desestructuració correcta `[root, resource, code, action]`. |
| 3 | Prova "el nen recull una moneda" fallava. | La moneda s'havia col·locat a la posició del pare, que la confiscava primer. | Canviar la prova per posar la moneda a la posició real del nen. |
| 4 | Els bots es quedaven encallats contra les parets. | Moviment vectorial directe. | Implementar BFS (`firstStep`) i `fleeTarget` per fugir. |
| 5 | Apareixien esdeveniments duplicats en reconnectar. | `hello` i la següent trama SSE incloïen els mateixos events. | Deduplicació al client amb clau `tipus:temps:id`. |
| 6 | Captures massa ràpides: pare esprintava a 9.9 caselles/s. | Multiplicador d'esprint dels pares massa alt (×1.9). | Baixat a ×1.55, durada 1.4 s, afegit head start de 5 s i cooldown de confiscació. |
| 7 | Un jugador que tancava la pestanya es quedava quiet al mapa. | Cap gestió de desconnexió. | `setDisconnected` converteix el lloc en bot; en reconnectar recupera el control. |
| 8 | Guard d'estat de connexió massa complicat i erroni. | Condició `p.isBot !== false && p.id.startsWith('bot-')`. | Simplificat a `if (!p || p.id.startsWith('bot-')) return;`. |
| 9 | El `fetch` del client podia fallar si el servidor es reiniciava. | SSE no sempre es reconnecta immediatament. | `onerror` tanca l'SSE i arrenca polling de 250 ms com a *fallback*. |
| 10 | L'amfitrió podia començar sense triar rol i el HUD mostrava "—". | `start` filtrava els jugadors sense rol i els 6 llocs acabaven sent bots. | Assignació automàtica d'un rol lliure a l'inici i etiqueta *Spectator* com a fallback. |
| 11 | L'usuari va demanar **fer-ho amb el projecte Unity Universal 2D**, no només web. | Primera entrega pensada com a client web. | Es va mantenir el servidor Node (endpoint i asíncron) i es va afegir un client Unity complet a `Assets/CatchTheTroublemaker/`. |
| 12 | Unity no podia obrir el projecte en mode batch perquè l'editor ja estava obert amb el mateix projecte. | Bloqueig d'instància d'Unity. | Es va copiar el projecte a una carpeta temporal per compilar-lo sense tocar l'editor de l'usuari; a més es va verificar amb el `csc` d'Unity. |
| 13 | Errors de compilació C# en la primera passada. | `CttApi` vs `CTTApi`, paràmetre `size` duplicat, `Screen` (enum) enfront de `UnityEngine.Screen`, mètode estàtic que feia servir un camp d'instància. | Corregits i recompilats fins a 0 errors. |
| 14 | El primer *patch* de presència (`room.seen`) no funcionava: tots els jugadors es convertien en bots en iniciar. | `syncPresence()` només es cridava a `start`/`restart`, abans que arribés cap input; després ja no es tornava a cridar. | Cridar-ho al *tick loop* (20 Hz) i a `/input`, i donar una finestra de gràcia inicial a cada jugador. Prova de regressió afegida → 15/15 tests. |
| 15 | L'usuari va demanar **"un joc 2D normal de Unity, amb un GameManager"**, no un *bootstrap* de codi. | El primer client Unity creava càmera, UI i escena en temps d'execució (`RuntimeInitializeOnLoadMethod`), sense res a la Hierarchy. | Es va dividir `CTTGame` en `GameManager` + `NetworkManager`, `UIManager`, `BoardRenderer` i `PlayerController`; es va afegir un Editor script que escriu l'escena amb el `GameManager` i els seus fills; i es va passar el mapa i els jugadors a `SpriteRenderer` en espai de món amb càmera ortogràfica que segueix el jugador local. |
| 16 | Amb dos dispositius, el comptador de jugadors del lobby es quedava a 1 si el client navegador perdia l'SSE. | `onPacket('state')` actualitzava el *snapshot* però ignorava `data.lobby`, que `/state` sí que inclou mentre la partida no ha començat. | Sincronitzar `state.lobby` i tornar a cridar `renderLobby()` també al camí de *polling*, perquè el recompte no depengui només de l'esdeveniment SSE `lobby`. |
| 17 | En P2P MULTI, dos equips de la mateixa LAN es descobrien però el comptador es quedava a 1. | `P2PTransport` obria el TCP mesh a partir dels beacons UDP, però cap dels dos extrems enviava el primer `hello`: `SayHello()` es feia amb la llista de peers buida i `OnPeerJoined()` només s'activa en rebre un `hello`. A més, `runInBackground: 0` podia aturar el `Pump()` del creador en perdre el focus. | Enviar un `hello` immediatament en establir cada socket (entrada i sortida) i activar `runInBackground` a `ProjectSettings`. Així ambdós costats registren l'altre sense esperar cap acció manual. |
| 18 | L'usuari va detectar que el recompte només pujava en triar rol, que es podia començar sense rols, que no es detectaven sortides i que un client tardà veia 1 jugador. | Faltava propagar l'identificador del creador, el lobby mostrava rols assignats automàticament (amagant qui no havia triat) i només hi havia un `bye` explícit. | `P2PMessage.host` + `P2PTransport.HostId` propaguen el *master*; START exigeix mínim 2 humans, tots els rols triats i ser el creador; `ping` cada 2 s amb timeout de 6 s detecta desconnexions; el lobby mostra només els rols triats de debò i promou el peer més petit si marxa el master. |
| 19 | El recompte del master seguia sense pujar en unir-se un client, i es va demanar una llista visible de noms d'usuari. | S'havia afegit el mètode `SendHello()` però **no es cridava** des de `ReadLoop()`: el socket TCP existia, però ningú enviava la identitat fins que un canvi de rol emetia un altre missatge. | Cridar `SendHello(peer)` just després de crear l'escriptor al socket; afegir `people` al rosters (`id|nom|rol;...`) per sincronitzar noms; i pintar al lobby la llista de jugadors amb `(YOU)` i `[HOST]`, que creix i s'elimina amb els esdeveniments `PeerJoined`/`PeerLeft`. |

### Què va funcionar a la primera

- La generació determinista del mapa i les proves de col·lisió.
- La regla de captura per família (amb la seva prova unitària).
- El càlcul del guanyador i l'endpoint `/result`.
- El renderitzat del mapa a canvas i el HUD en temps real.

### Què va caldrar reformular

- El **rutatge HTTP** (error de segment).
- El **moviment dels bots** (de vector directe a BFS).
- L'**equilibri** després d'observar una partida real.
- La **gestió de desconnexions** i la deduplicació d'esdeveniments.

---

## 8. Verificació real

- `npm test` → **14/14 proves correctes** (10 de motor + 4 d'API HTTP).
- Prova al navegador:
  1. Sala `FQJFD` creada amb el nom "Marta".
  2. Rol `Troublemaker A` seleccionat; 6 rols i 1 jugador al lobby; botó Start actiu per a l'amfitrió.
  3. Partida iniciada: HUD `0/50 monedes · 3 vides`, temporitzador `5:59`, canvas dibuixant.
  4. Després d'uns segons: `scoreA = 1`, `lives = 2` i 2 *toasts* → una moneda recollida i una captura
     vàlida, sense errors a la consola.
- Prova de l'endpoint final amb `GAME_DURATION_MS=2000`: la partida va acabar per temps
  i `GET /api/rooms/<cod>/result` va retornar `winnerTeam: "parents"`, `reason`,
  `stats` (7 monedes, 2 confiscades) i `standings` dels 6 jugadors. La pantalla de
  resultat es va mostrar correctament.
- Els resultats guardats s'escriuen a `data/matches.jsonl` (append-only), per si es volen
  recuperar partides.

---

## 9. Lliçons apreses (esquema)

- **Preguntar abans de construir.** Una pregunta sobre l'stack va estalviar refactoritzar un projecte Unity sencer.
- **Separar motor de transport.** Poder provar les regles amb `node --test` va descobrir errors abans del navegador.
- **Provar el que es documenta.** L'equilibri no és una opinió: es va observar una partida i es van ajustar els números.
- **L'agent proposa, l'humà valida.** Es va decidir no copiar la primera suggerència (WebSocket / moviment directe) i investigar alternatives.
- **Documentar en el moment.** Aquest fitxer serveix de guió per a la presentació i evita haver de reconstruir el procés després.
- **Git des del principi.** Amb canvis d'equilibri i de rutes, tenir *commits* permet comparar i tornar enrere.

---

## 10. Sobre l'exportació de la conversa

L'ordre complerta està dins de la sessió d'OpenCode. Les parts rellevants
(prompt inicial, pregunta d'stack, decisions, iteracions) s'han reproduït de forma
esquemàtica en aquest document. Per a la presentació es pot exportar/copiar la
sessió des de la pròpia interfície d'OpenCode i destacar les seccions 3, 4 i 7.

---

## 11. Segona fase: el client Unity Universal 2D

Després de la primera entrega, l'usuari va dir:

> "Thanks, but now do it with the Unity Universal 2D Project that I created in the same folder I'm sharing"

**Directriu interpretada:** el joc ha de ser el projecte Unity de la plantilla, no un
lloc web independent. No cal llençar el treball fet: l'enunciat continua exigint un
*endpoint* i comunicació asíncrona entre màquines, així que el servidor Node es manté
com a **backend autoritatiu** i Unity es converteix en un client més.

### Prompt / restriccions → resposta de l'agent

| Directriu | Resposta de l'agent |
| --- | --- |
| "Fes-ho amb el Unity Universal 2D Project" | Es va inspeccionar `ProjectVersion.txt` (6000.6.9f1), `manifest.json` (Input System 1.20.1, uGUI 2.6.0, URP 17.6.0) i `ProjectSettings.asset` (`activeInputHandler: 1`, és a dir, **només Input System**). |
| "El projecte que he creat jo" | No es va sobreescriure res de la plantilla: es va crear `Assets/CatchTheTroublemaker/` amb 9 scripts C# i el seu README. |
| "Ha de ser jugable" | Primer es va fer amb `CttBootstrap` (tot en temps d'execució); després, a petició de l'usuari, es va refactoritzar a un `GameManager` **a l'escena** amb managers fills. Veure §12. |
| "Sense dependències noves" | Tota l'art és procedural (`Texture2D.SetPixels32`): monedes, parets, mapa i formes ■ ▲ ★. No cal importar sprites. |
| "Comunicació asíncrona també a Unity" | `DownloadHandlerScript` propi que llegeix l'SSE del servidor i encua els esdeveniments per al *main thread*; sondeig `/state` com a *fallback*. |
| "Que es pugui jugar entre màquines" | Camp *Server URL* a la pantalla inicial; es desa a `PlayerPrefs`. Un jugador Unity pot jugar contra un de navegador. |

### Decisions tècniques de la fase Unity

| Decisió | Per què | Alternativa descartada |
| --- | --- | --- |
| UI construïda per codi (uGUI) | Zero *drag & drop* a l'inspector; qualsevol escena serveix. | Prefabs i canvas muntats a mà (fràgil i lent de documentar). |
| Input System (no `UnityEngine.Input`) | El projecte té `activeInputHandler: 1`; la API antiga dóna errors. | Activar *Both* i fer servir la API clàssica. |
| `RuntimeInitializeOnLoadMethod` (fase inicial) | L'editor no cal que reimporti escenes noves. | Afegir manualment un GameObject a `SampleScene` — **aquesta és la decisió que es va corregir a la §12** perquè l'usuari volia un joc Unity convencional. |
| SSE amb `DownloadHandlerScript` | `EventSource` no existeix a Unity; cal llegir el flux `text/event-stream` a trossos. | Fer només *polling* (funciona, però menys "asíncron" i menys immediat). |
| `PlayerPrefs` per a codi i `playerId` | Reconnectar després de recarregar l'escena és trivial. | Demanar sempre el codi a mà. |
| DTOs separats (`CTTData.cs`) | `JsonUtility` no pot deserialitzar arrays al nivell arrel; es va adaptar l'estructura a objectes. | `SimpleJSON` / Newtonsoft (dependència extra). |

### Verificació d'aquesta fase

- Compilació dels 9 scripts amb el compilador Roslyn inclòs en Unity 6, referenciant
  `UnityEngine.*`, `UnityEngine.UI.dll` i `Unity.InputSystem.dll` → **0 errors**.
- Importació real en una còpia temporal del projecte amb Unity en *batch mode*: es
  van generar els 9 fitxers `.meta`, confirmant que l'editor reconeix els scripts.
- El backend passa amb **15/15 tests** (`npm test`), inclòs un test de regressió que
  simula un client Unity que només fa *polling* i envia input sense obrir l'SSE.
- Prova end-to-end real contra el servidor: sala, dos humans, rols, start, 3 s d'input
  sense SSE → tots dos segueixen `isBot: false`, `connected: true`, i `/result` respon.
- L'editor original de l'usuari es va deixar obert i sense modificacions d'escena.

### Canvi al servidor derivat d'Unity

`syncPresence()` abans només considerava "connectat" qui tenia un flux SSE obert.
Això feia que un client Unity que encara no hagués connectat l'SSE es convertís en bot
a l'iniciar la partida. Es va afegir `room.seen` (marca de temps actualitzada per
`/input` i per l'obertura del flux), amb una finestra de 10 segons.

**Primera iteració (insuficient):** només es cridava `syncPresence()` a `start`/`restart`,
és a dir, abans que el client hagués pogut enviar cap input. Una prova end-to-end va
demostrar que els dos humans es convertien en bots a `tick 0` i no es recuperaven mai.

**Correcció:** cridar `syncPresence()` al *tick loop* (20 Hz) i just després de cada
`/input`, més una finestra de gràcia que marca `room.seen` per a tots els humans a
l'inici (i al reinici). Amb això, un client que només sondeja `/state` es manté humà
mentre enviï intencions, i torna a ser bot automàticament si deixa de fer-ho durant
10 s. És un canvi de robustesa que també beneficia el client web.

---

## 12. Tercera fase: joc Unity 2D convencional amb `GameManager`

L'usuari va revisar la segona entrega i va dir:

> "Try again, make it following a normal 2D unity game, with a Game manager".

**Directriu interpretada:** el client havia de semblar un projecte Unity de veritat:
un `GameManager` a la Hierarchy, objectes al món 2D i una càmera que segueix el
jugador — no una interfície muntada per codi en un objecte buit. Quan se li va
preguntar l'abast, va triar **"GameManager + world 2D (Recommended)"**.

### Prompt / restriccions → resposta de l'agent

| Directriu | Resposta de l'agent |
| --- | --- |
| "Un joc 2D normal amb Game manager" | Es va crear `GameManager.cs` (MonoBehaviour, singleton) amb la màquina d'estats `Home → Lobby → Playing → Result` i referències públiques als altres managers. |
| "No només codi en temps d'execució" | Es va eliminar `CTTBootstrap.cs` i es va afegir `Editor/CTTSceneSetup.cs`, que escriu l'escena `SampleScene.unity` amb `GameManager` → `Network / UI / World / Input`, `Main Camera` ortogràfica i `EventSystem` amb `InputSystemUIInputModule`. |
| "Món 2D" | `BoardRenderer.cs` dibuixa el mapa com **una sola sprite** generada amb `Texture2D.SetPixels32` (16 px per casella) i monedes/jugadors com a `SpriteRenderer` separats. La càmera segueix el jugador local amb *smoothing* i retalla els límits del mapa. |
| "Que segueixi sent autoritatiu" | `PlayerController.cs` només llegeix `WASD` / teclat Input System i el *D-pad* tàctil; `GameManager.Update()` envia `{dx, dy, sprint}` cada 100 ms. Cap posició es decideix al client. |
| "No trencar el que ja funcionava" | El servidor Node, el web client, els DTO i el transport SSE/polling es van conservar. El canvi és només al client Unity i a l'escena. |

### Decisions tècniques d'aquesta refactorització

| Decisió | Per què | Alternativa descartada |
| --- | --- | --- |
| Un `GameManager` coordinator + managers especialitzats | `CTTGame.cs` tenia ~600 línies barrejant xarxa, UI i render; ara cada fitxer té una sola responsabilitat i es pot provar per separat. | Un únic `GameManager` enorme (el problema original). |
| Esdeveniments C# (`Action<...>`) de `NetworkManager` → `GameManager` | Desacopla el transport de la lògica de pantalla; `UIManager` no sap res de HTTP. | Crides directes a UI des de les callbacks HTTP. |
| Editor script + escena desada | L'usuari veu els objectes a la Hierarchy i pot ajustar valors a l'Inspector (`cameraSize`, `pollInterval`, `SendInterval`). | Tornar a crear-ho tot a `Awake()`. |
| Còpia temporal del projecte per executar l'editor en *batch* | L'editor de l'usuari estava obert amb el mateix projecte i Unity bloqueja una segona instància. | Tancar l'editor de l'usuari (no acceptable). |
| `SpriteRenderer` en espai de món | És el que s'espera d'un joc 2D: càmera, profunditat (sorting orders) i objectes que es poden inspeccionar. | Dibuixar el mapa en un `RawImage` de canvas (fase anterior). |
| **Quadres de color** per a tots els personatges | L'usuari va demanar explicitament "the characters just being colored icon squares". S'han eliminat les formes ■ ▲ ★ i les etiquetes 3D del client Unity: ara el mateix quadrat pla apareix al menú (llegenda de 6), al lobby (una fitxa per rol) i al HUD (tira de 6). El jugador local es distingeix amb un marc blanc. | Formes diferents per rol al client Unity (es conserven al client web). |
| **Tots els panels fills del Canvas** | Bug detectat en provar l'escena: només es veien els botons CA/EN perquè `Card()` i `HudPanel` es creaven amb `parent = null` (arrel de l'escena). uGUI no renderitza `Graphic` que no estigui dins d'un `Canvas`. Ara tots els panels reben `canvas.transform` i els botons globals s'afegeixen al final per quedar per sobre. | Continuar afegint `Graphic` a l'arrel i esperar que el `ScreenSpaceOverlay` els trobi. |
| **Sense fons opac global al Canvas** | Segon bug del mateix prova: un `Image` opac a pantalla completa creat a `Build()` tapava tot el món 2D durant la partida (l'overlay es dibuixa per sobre de l'escena). Cada panell de menú/lobby/resultat ja té el seu propi fons; s'ha eliminat el global i el HUD és transparent. | Fer transparent el fons només en jugar (fràgil: qualsevol estat nou el tornava a posar opac). |
| **Monedes = cercles grocs** | L'usuari va demanar "display the coins as yellow circles". `CTTDraw.Coin()` ara és un disc groc brillant amb vora més fosca i un reflex; les monedes s'han fet una mica més grans. | El disc amb vora marró de la versió anterior. |
| **Diagnòstic de connexió** | L'usuari va veure "cannot connect to destination host" en canviar a dades mòbils. El problema no és codi sinó adreçament: `localhost` és el dispositiu client. S'ha afegit un botó **TEST** (`GET /api/health`), un missatge accionable al `CTTApi`, i el servidor ara imprimeix les IP LAN en arrencar. | Deixar l'error brut d'`UnityWebRequest` i esperar que l'usuari endevini què és `localhost`. |
| **Joc entre xarxes** | Per jugar des d'una altra màquina cal la IP LAN de l'amfitrió; per a dades mòbils cal una URL pública (túnel Cloudflare/ngrok o hostatge al núvol). S'ha creat `docs/NETWORKING.md` i el servidor detecta l'IP primària amb un socket UDP (sense enviar dades) per imprimir un únic `SHARE THIS`. | Imprimir totes les IP, incloses les virtuals/APIPA, i que l'usuari triï la incorrecta. |
| **Descobriment LAN automàtic** | L'usuari no aconseguia escriure la IP a la segona màquina (PlayerPrefs conservava `localhost`/port 4000). S'ha afegit un servidor UDP a la porta 3001 que respon a `CTT_DISCOVER` amb la URL HTTP, i un client Unity que ho detecta en arrencar si `localhost` falla, més el botó **FIND LAN**. Així qualsevol màquina de la mateixa xarxa troba l'amfitrió sense configuració. | Dependre que l'usuari copiï manualment una IP (fràgil: ports antics, `localhost`, errors de tecleig). |
| **Colors de personatge idèntics al menú** | S'han eliminat els tintats de *grace* i *sprint* als `SpriteRenderer` perquè l'usuari va demanar "the same colors like when selecting in the menu". El jugador local es distingeix només amb el marc blanc. Totes les icones UI (menú, lobby, HUD) fan servir la mateixa paleta i el mateix estat de contrast. | Barrejar colors amb blanc/groc segons l'estat del jugador. |
| **Mode P2P sense amfitrió** | L'usuari va demanar "full peer-to-peer, no host" i va acceptar eliminar l'endpoint del guanyador. S'ha portat `server/engine.js` a C# (`P2PEngine.cs`), s'ha afegit descobriment UDP + malla TCP (`P2PTransport.cs`) i lockstep a 20 Hz (`P2PGame.cs`). La llavor és XOR de les llavors de tots els peers i les decisions empatades es resolen per id més baix: cap amfitrió permanent. El guanyador es calcula localment a cada màquina. | Dependre d'un Node sempre viu; o fer que un client "amfitrió" decidís la simulació (encara seria un servidor). |
| **Paritat motor JS/C#** | Per evitar que el port canviés l'equilibri, s'ha executat el motor C# amb un *stub* de UnityEngine i s'ha comparat amb `server/engine.js` durant 400 ticks: mateix mapa, monedes, vides i posicions, tant amb 6 humans com amb 2 humans + 4 bots. | Suposar que el port era correcte sense comparar-lo. |
| **P2P Solo / Multi** | L'usuari va demanar triar entre jugar sol (triar personatge i 5 bots) o amb almenys 2 jugadors reals que s'uneixen amb el codi. S'ha afegit el botó **P2P: SOLO / P2P: MULTI** a la Home, una sessió local sense transport per a Solo i un mínim de 2 humans comprovat tant al lobby (START desactivat amb missatge) com a `P2PGame.StartMatch()`. Un sol jugador pot prémer START: els altres responen amb la seva llavor. | Començar una partida "multijugador" amb una sola persona, o obligar tothom a prémer START. |

### Resultat i verificació

- `GameManager`, `NetworkManager`, `UIManager`, `BoardRenderer`, `PlayerController` i
  `Editor/CTTSceneSetup.cs` compilen amb **0 errors** amb el Roslyn d'Unity 6 i amb la
  compilació real de l'editor (es va regenerar `Assembly-CSharp.dll`).
- L'scene setup en *batch mode* va escriure `SampleScene.unity` (1496 línies) amb
  totes les referències serialitzades (`network`, `ui`, `board`, `playerInput`) i sense
  errors de compilació al log. Aquesta escena es va copiar al projecte original.
- `npm test` → **15/15** (cap canvi al servidor en aquesta fase).
- Iteració final de UI: els panels de fase es paren a `canvas.transform`; sense això
  només els botons CA/EN (fills del Canvas) eren visibles a Play. Compilació de nou
  amb 0 errors després del canvi.
- Iteració de món: s'ha tret el fons opac global del Canvas (tapava mapa, monedes i
  jugadors), `ApplyState` actualitza actors també amb snapshots sense `map`, i s'usa
  un material `Sprite-Unlit` compatible amb URP. Monedes = cercles grocs; jugadors =
  quadrats amb el color exacte de la selecció de rol.
- Iteració de xarxa: compilació 0 errors i `npm test` 15/15; arrencada del servidor
  al port 3999 verifica que ara mostra les IP LAN disponibles.
- Iteració P2P: compilació 0 errors de tots els scripts (inclosos `P2PEngine`,
  `P2PTransport`, `P2PGame`); prova de paritat JS↔C# de 400 ticks amb 6 humans i amb
  2 humans + 4 bots → resultats idèntics. `docs/P2P.md` descriu el protocol i els límits.
- Prova end-to-end sense SSE repetida: dos humans amb `/input` i `/state`, sense obrir
  el flux d'esdeveniments, continuen `isBot: false` i `connected: true` després de 3 s;
  `GET /api/rooms/<cod>/result` respon correctament.
- Iteració del comptador del lobby: `npm test` → **15/15** després de sincronitzar el lobby també al *polling* del client navegador.
- Iteració Solo/Multi: compilació de tots els scripts (`Assets/Scripts` + `Assets/Editor`)
  amb el Roslyn d'Unity 6 → **0 errors** (només avisos preexistents de
  `FindFirstObjectByType`); `npm test` → **15/15**. `docs/P2P.md`, `README.md` i el
  README del client descriuen els dos fluxos i el mínim de 2 jugadors reals.
- La documentació del client (`Assets/CatchTheTroublemaker/README.md`) s'ha reescrit
  per explicar la nova estructura i la comanda **Tools > Catch the Troublemaker >
  Setup Scene** per si cal refer l'escena.
