using System.Collections.Generic;
using UnityEngine;

namespace CatchTheTroublemaker
{
    public static class CTTConfig
    {
        public const string DefaultServer = "http://localhost:3000";
        public const string PlayerPrefKey = "ctt_unity_server";
        public const string NamePrefKey = "ctt_unity_name";

        /// <summary>Minimum number of real (non-bot) players to start a P2P multiplayer match.</summary>
        public const int P2pMinRealPlayers = 2;

        public static readonly string[] RoleOrder =
        {
            "fatherA", "motherA", "troublemakerA",
            "fatherB", "motherB", "troublemakerB",
        };

        private static readonly Dictionary<string, string> RoleColors = new Dictionary<string, string>
        {
            { "fatherA", "#ff8a5b" }, { "motherA", "#ffd166" }, { "troublemakerA", "#ef476f" },
            { "fatherB", "#4cc9f0" }, { "motherB", "#8ac4ff" }, { "troublemakerB", "#9b5de5" },
        };

        private static readonly Dictionary<string, string> RoleShort = new Dictionary<string, string>
        {
            { "fatherA", "FA" }, { "motherA", "MA" }, { "troublemakerA", "TA" },
            { "fatherB", "FB" }, { "motherB", "MB" }, { "troublemakerB", "TB" },
        };

        private static readonly Dictionary<string, string> RoleShapes = new Dictionary<string, string>
        {
            { "fatherA", "square" }, { "motherA", "triangle" }, { "troublemakerA", "star" },
            { "fatherB", "square" }, { "motherB", "triangle" }, { "troublemakerB", "star" },
        };

        public static Color ColorOf(string role, bool highContrast = false)
        {
            if (highContrast)
            {
                switch (role)
                {
                    case "fatherA": return new Color(1f, 0.45f, 0.1f);
                    case "motherA": return new Color(1f, 1f, 0f);
                    case "troublemakerA": return new Color(1f, 0.2f, 0.55f);
                    case "fatherB": return new Color(0f, 1f, 1f);
                    case "motherB": return new Color(0.6f, 0.85f, 1f);
                    case "troublemakerB": return new Color(1f, 0.3f, 1f);
                }
            }
            return Parse(RoleColors.ContainsKey(role) ? RoleColors[role] : "#ffffff");
        }

        public static string ShortOf(string role) =>
            RoleShort.ContainsKey(role) ? RoleShort[role] : "?";

        public static string ShapeOf(string role) =>
            RoleShapes.ContainsKey(role) ? RoleShapes[role] : "square";

        public static string RoleName(string role, bool catalan)
        {
            switch (role)
            {
                case "fatherA": return catalan ? "Pare A" : "Dad A";
                case "motherA": return catalan ? "Mare A" : "Mum A";
                case "troublemakerA": return catalan ? "Entremaliat A" : "Troublemaker A";
                case "fatherB": return catalan ? "Pare B" : "Dad B";
                case "motherB": return catalan ? "Mare B" : "Mum B";
                case "troublemakerB": return catalan ? "Entremaliat B" : "Troublemaker B";
                default: return catalan ? "Sense rol" : "No role";
            }
        }

        public static string TeamName(string team, bool catalan)
        {
            if (team == "parents") return catalan ? "Pares" : "Parents";
            if (team == "troublemakers") return catalan ? "Entremaliats" : "Troublemakers";
            return team;
        }

        public static Color Parse(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white;
        }

        private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            { "title", new[] { "CATCH THE TROUBLEMAKER", "CATCH THE TROUBLEMAKER" } },
            { "tagline", new[] { "One kid. Fifty coins. Two very tired parents.", "Un nen. Cinquanta monedes. Dos pares molt cansats." } },
            { "name", new[] { "Your name", "El teu nom" } },
            { "server", new[] { "Server URL  •  other PCs: use the host's LAN IP", "URL del servidor  •  altres PCs: IP LAN de l'amfitrió" } },
            { "test", new[] { "TEST", "PROVA" } },
            { "find", new[] { "FIND LAN", "CERCA LAN" } },
            { "modeServer", new[] { "MODE: ONLINE", "MODE: EN LÍNIA" } },
            { "modeP2p", new[] { "MODE: P2P", "MODE: P2P" } },
            { "p2pServer", new[] { "P2P  •  same network, no server needed", "P2P  •  mateixa xarxa, sense servidor" } },
            { "createP2p", new[] { "CREATE P2P ROOM", "CREA SALA P2P" } },
            { "p2pHint", new[] { "P2P: everyone joins with the same 5-letter code. No server, no endpoint.", "P2P: tots s'uneixen amb el mateix codi de 5 lletres. Sense servidor ni endpoint." } },
            { "kindSolo", new[] { "P2P: SOLO", "P2P: SOL" } },
            { "kindMulti", new[] { "P2P: MULTI", "P2P: MULTI" } },
            { "playSolo", new[] { "PLAY SOLO", "JUGAR SOL" } },
            { "p2pSoloCaption", new[] { "P2P SOLO  •  pick a character; the other seats are bots", "P2P SOL  •  tria un personatge; els altres llocs són bots" } },
            { "p2pMultiCaption", new[] { "P2P MULTI  •  share the 5-letter code (2 real players minimum)", "P2P MULTI  •  comparteix el codi de 5 lletres (mínim 2 jugadors reals)" } },
            { "p2pSoloHint", new[] { "Solo: choose a role in the lobby, then START. Empty seats are bots.", "Sol: tria un rol a la sala i prem COMENÇAR. Els llocs lliures són bots." } },
            { "p2pMultiHint", new[] { "Multiplayer: everyone joins with the same code; START needs 2 real players.", "Multijugador: tots s'uneixen amb el mateix codi; calen 2 jugadors reals per COMENÇAR." } },
            { "soloMode", new[] { "SOLO MODE", "MODE SOL" } },
            { "youPlusBots", new[] { "YOU + 5 BOTS", "TU + 5 BOTS" } },
            { "needTwo", new[] { "At least 2 real players are needed to start (empty seats become bots).", "Calen almenys 2 jugadors reals per començar (els llocs lliures són bots)." } },
            { "needRoles", new[] { "Everyone must choose a role before starting.", "Tothom ha de triar un personatge abans de començar." } },
            { "chooseFamilies", new[] { "CHOOSE THE TWO FAMILIES", "TRIA LES DUES FAMÍLIES" } },
            { "familyHint", new[] { "Host picks two different families. Click a chosen one to remove it.", "L'amfitrió tria dues famílies diferents. Prem una de triada per treure-la." } },
            { "needFamilies", new[] { "Choose two different families before starting.", "Tria dues famílies diferents abans de començar." } },
            { "familyA", new[] { "FAMILY A", "FAMÍLIA A" } },
            { "familyB", new[] { "FAMILY B", "FAMÍLIA B" } },
            { "onlyHost", new[] { "Only the room creator can start the match.", "Només qui crea la sala pot començar la partida." } },
            { "waitHost", new[] { "Waiting for the room creator to start…", "Esperant que qui crea la sala comenci…" } },
            { "searching", new[] { "Searching LAN…", "Cercant a la LAN…" } },
            { "found", new[] { "Found:", "Trobat:" } },
            { "autoFound", new[] { "LAN server:", "Servidor LAN:" } },
            { "testing", new[] { "Testing server…", "Provant el servidor…" } },
            { "connectionOk", new[] { "Server OK", "Servidor OK" } },
            { "create", new[] { "CREATE ROOM", "CREAR SALA" } },
            { "join", new[] { "JOIN ROOM", "UNIR-SE" } },
            { "code", new[] { "Room code (5 letters)", "Codi de sala (5 lletres)" } },
            { "rules", new[] {
                "RULES  •  A troublemaker wins at 50 coins. Parents win by grounding both kids (3 catches each) or on time. Only a child's OWN parents can catch them. Caught kids drop every coin.",
                "REGLES  •  Un entremaliat guanya en arribar a 50 monedes. Els pares guanyen si posen a terra els dos nens (3 captures cadascun) o per temps. Només els pares propis poden agafar el seu fill." } },
            { "roomCode", new[] { "ROOM CODE", "CODI DE SALA" } },
            { "players", new[] { "PLAYERS", "JUGADORS" } },
            { "chooseRole", new[] { "CHOOSE YOUR ROLE  (free seats are bots)", "TRIA EL TEU ROL  (els llocs lliures són bots)" } },
            { "start", new[] { "START GAME", "COMENÇAR" } },
            { "leave", new[] { "LEAVE", "SORTIR" } },
            { "free", new[] { "free · bot", "lliure · bot" } },
            { "host", new[] { "HOST", "AMFITRIÓ" } },
            { "you", new[] { "YOU", "TU" } },
            { "youAre", new[] { "YOU ARE", "ETS" } },
            { "timeLeft", new[] { "TIME LEFT", "TEMPS RESTANT" } },
            { "sprint", new[] { "SPRINT", "ESPRINT" } },
            { "ready", new[] { "READY", "A PUNT" } },
            { "moveHint", new[] { "WASD / Arrows: move    Space: sprint    (or use the pad below)", "WASD / Fletxes: moure't    Espai: esprint    (o el pad de sota)" } },
            { "gameOver", new[] { "GAME OVER", "FI DE LA PARTIDA" } },
            { "parentsWin", new[] { "THE PARENTS WIN!", "ELS PARES GUANYEN!" } },
            { "kidsWin", new[] { "ESCAPES WITH THE COINS!", "SE'N SURT AMB LES MONEDES!" } },
            { "reasonGoal", new[] { "Fifty coins, straight into the pockets.", "Cinquanta monedes directes a les butxaques." } },
            { "reasonCaught", new[] { "Both troublemakers were grounded.", "Els dos entremaliats han estat posats a terra." } },
            { "reasonTimeout", new[] { "Time ran out before the coin goal.", "El temps s'ha acabat abans de la meta." } },
            { "resultVoice", new[] { "CHARACTER LINE", "MISSATGE DEL PERSONATGE" } },
            { "voiceVictory", new[] { "victory", "victòria" } },
            { "voiceDefeat", new[] { "defeat", "derrota" } },
            { "playAgain", new[] { "PLAY AGAIN", "JUGAR DE NOU" } },
            { "backHome", new[] { "BACK HOME", "TORNAR A L'INICI" } },
            { "duration", new[] { "DURATION", "DURADA" } },
            { "catches", new[] { "CATCHES", "CAPTURES" } },
            { "collected", new[] { "COINS", "MONEDES" } },
            { "confiscated", new[] { "CONFISCATED", "CONFISCADES" } },
            { "spectator", new[] { "SPECTATOR", "ESPECTADOR" } },
            { "spectatorOut", new[] { "YOU'RE OUT — SPECTATING", "ETS FORA — ESPECTADOR" } },
            { "spectatorHint", new[] {
                "Free camera: WASD / arrows or drag to pan · scroll or +/− to zoom · R / Home to reset",
                "Càmera lliure: WASD / fletxes o arrossega per moure · roda o +/− per ampliar · R / Home per reset" } },
            { "spectatorHintShort", new[] { "GHOST CAMERA", "CÀMERA ESPECTRE" } },
            { "ghostIn", new[] { "Ghost camera in {0}s", "Càmera espectre en {0}s" } },
            { "ghostSoon", new[] { "GHOST CAMERA…", "CÀMERA ESPECTRE…" } },
            { "paused", new[] { "PAUSED", "PAUSAT" } },
            { "pauseHint", new[] { "Esc: resume  •  the match keeps running", "Esc: reprendre  •  la partida continua" } },
            { "restartMatch", new[] { "RESTART MATCH", "RECOMENÇAR LA PARTIDA" } },
            { "exitMenu", new[] { "EXIT TO MENU", "SORTIR AL MENÚ" } },
            { "exitDesktop", new[] { "EXIT TO DESKTOP", "SORTIR A L'ESCRIPTORI" } },
            { "lang", new[] { "CA / EN", "EN / CA" } },
            { "contrast", new[] { "HIGH CONTRAST", "ALT CONTRAST" } },
            { "invalidCode", new[] { "A room code has 5 characters.", "Un codi de sala té 5 caràcters." } },
            { "reconnecting", new[] { "Reconnecting…", "Reconnectant…" } },
            { "connectionError", new[] { "Connection error", "Error de connexió" } },
        };

        public static string Tr(string key, bool catalan)
        {
            if (!Texts.ContainsKey(key)) return key;
            return Texts[key][catalan ? 1 : 0];
        }
    }
}
