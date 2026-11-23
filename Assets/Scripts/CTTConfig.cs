using System.Collections.Generic;
using UnityEngine;

namespace CatchTheTroublemaker
{
    public static class CTTConfig
    {
        public const string DefaultServer = "http://localhost:3000";
        public const string PlayerPrefKey = "ctt_unity_server";
        public const string NamePrefKey = "ctt_unity_name";

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
            { "playAgain", new[] { "PLAY AGAIN", "JUGAR DE NOU" } },
            { "backHome", new[] { "BACK HOME", "TORNAR A L'INICI" } },
            { "duration", new[] { "DURATION", "DURADA" } },
            { "catches", new[] { "CATCHES", "CAPTURES" } },
            { "collected", new[] { "COINS", "MONEDES" } },
            { "confiscated", new[] { "CONFISCATED", "CONFISCADES" } },
            { "spectator", new[] { "SPECTATOR", "ESPECTADOR" } },
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
