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
