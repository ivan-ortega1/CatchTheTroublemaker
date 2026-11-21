using System;
using UnityEngine;
using UnityEngine.UI;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Builds and owns the uGUI overlay: home, waiting room, in-game HUD, touch pad
    /// and result screen. It only raises commands; GameManager owns the session.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public event Action<string> CreateRequested;
        public event Action<string> JoinRequested;
        public event Action<string> RoleRequested;
        public event Action StartRequested;
        public event Action RematchRequested;
        public event Action LeaveRequested;
        public event Action LanguageToggled;
        public event Action ContrastToggled;

        public int TouchX { get; private set; }
        public int TouchY { get; private set; }
        public bool TouchSprint { get; private set; }

        public string PlayerName => nameField != null ? nameField.text : "";

        private GameManager game;
        private GamePhase phase;
        private Canvas canvas;
        private RectTransform homePanel, lobbyPanel, hudPanel, resultPanel;
        private Text toast, homeStatus, lobbyCode, lobbyPlayers, hudRole, hudLives;
        private Text hudTimer, hudScoreA, hudScoreB, hudSprint, resultTitle, resultReason, resultStats;
        private InputField nameField, serverField, joinField;
        private readonly Button[] roleButtons = new Button[6];
        private Button startButton;

        private void Awake() => Build();

        public void Initialize(GameManager manager)
        {
            game = manager;
            serverField.text = CTTApi.ServerUrl;
            if (game.Catalan || game.HighContrast)
                Refresh(game.Catalan, game.HighContrast, null, null, null, null, GamePhase.Home);
        }

        private void ApplyServer()
        {
            if (serverField != null && !string.IsNullOrWhiteSpace(serverField.text))
                CTTApi.ServerUrl = serverField.text;
        }

        /* ----------------------------- construction ----------------------------- */

        private void Build()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            TouchX = TouchY = 0;
            TouchSprint = false;

            var canvasObject = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CttUi.PanelAt("Background", canvas.transform, CttUi.Background);
            background.SetAsFirstSibling();

            bool ca = game != null && game.Catalan;
            CttUi.Button(canvas.transform, "Language", CTTConfig.Tr("lang", ca), CttUi.Panel,
                () => LanguageToggled?.Invoke(),
                new Vector2(1f, 1f), new Vector2(170, 54), new Vector2(-210, -44), 24);
            CttUi.Button(canvas.transform, "Contrast", CTTConfig.Tr("contrast", ca), CttUi.Panel,
                () => ContrastToggled?.Invoke(),
                new Vector2(1f, 1f), new Vector2(230, 54), new Vector2(-430, -44), 22);

            var toastHolder = CttUi.Rect("ToastHolder", canvas.transform);
            CttUi.Place(toastHolder, new Vector2(0.5f, 1f), new Vector2(1100, 54), new Vector2(0, -128));
            toast = CttUi.Label(toastHolder, "Toast", "", 26, CttUi.Accent);

            BuildHome();
            BuildLobby();
            BuildHud();
            BuildResult();
        }

        private static RectTransform At(RectTransform parent, string name, float width, float height,
            float top, float x = 0f)
        {
            RectTransform rt = CttUi.Rect(name, parent);
            CttUi.Place(rt, new Vector2(0.5f, 1f), new Vector2(width, height), new Vector2(x, -top));
            return rt;
        }

        private static RectTransform Card(string name) => CttUi.PanelAt(name, null, CttUi.Background);

        private static RectTransform CardInner(RectTransform parent, float width, float height)
        {
            RectTransform rt = CttUi.Rect("Card", parent);
            CttUi.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(width, height), Vector2.zero);
            rt.gameObject.AddComponent<Image>().color = CttUi.Panel;
            return rt;
        }

        private void BuildHome()
        {
            homePanel = Card("HomePanel");
            RectTransform card = CardInner(homePanel, 1040, 900);
            bool ca = game != null && game.Catalan;

            CttUi.Label(At(card, "Title", 1000, 90, 50), "Text", CTTConfig.Tr("title", ca), 58, CttUi.Accent);
            CttUi.Label(At(card, "Tagline", 1000, 60, 130), "Text", CTTConfig.Tr("tagline", ca), 30, CttUi.Muted);

            CttUi.Label(At(card, "NameCaption", 900, 40, 214), "Text", CTTConfig.Tr("name", ca), 26,
                CttUi.Muted, TextAnchor.MiddleLeft);
            nameField = CttUi.Field(card, "Name", "Marta", PlayerPrefs.GetString(CTTConfig.NamePrefKey, ""),
                new Vector2(0.5f, 1f), new Vector2(900, 64), new Vector2(0, -282));

            CttUi.Label(At(card, "ServerCaption", 900, 36, 362), "Text", CTTConfig.Tr("server", ca), 24,
                CttUi.Muted, TextAnchor.MiddleLeft);
            serverField = CttUi.Field(card, "Server", CTTConfig.DefaultServer, CTTApi.ServerUrl,
                new Vector2(0.5f, 1f), new Vector2(900, 58), new Vector2(0, -416));

            CttUi.Button(card, "Create", CTTConfig.Tr("create", ca), CttUi.Accent,
                () => { ApplyServer(); CreateRequested?.Invoke(nameField.text); },
                new Vector2(0.5f, 1f), new Vector2(900, 76), new Vector2(0, -510));

            joinField = CttUi.Field(card, "JoinCode", "FQJFD", "",
                new Vector2(0.5f, 1f), new Vector2(560, 64), new Vector2(-170, -610));
            CttUi.Button(card, "Join", CTTConfig.Tr("join", ca), CttUi.Accent2,
                () => { ApplyServer(); JoinRequested?.Invoke(joinField.text.Trim().ToUpperInvariant()); },
                new Vector2(0.5f, 1f), new Vector2(300, 64), new Vector2(320, -610), 26);

            CttUi.Label(At(card, "Rules", 960, 160, 712), "Text", CTTConfig.Tr("rules", ca), 24, CttUi.Muted);
            homeStatus = CttUi.Label(At(card, "Status", 960, 40, 838), "Text", "", 24, CttUi.Danger);
        }

        private void BuildLobby()
        {
            lobbyPanel = Card("LobbyPanel");
            RectTransform card = CardInner(lobbyPanel, 1120, 900);
            bool ca = game != null && game.Catalan;

            CttUi.Label(At(card, "Title", 1000, 70, 40), "Text", CTTConfig.Tr("roomCode", ca), 40, CttUi.Accent);
            lobbyCode = CttUi.Label(At(card, "Code", 1000, 90, 120), "Text", "", 64, Color.white);
            lobbyPlayers = CttUi.Label(At(card, "Players", 1000, 50, 210), "Text", "", 28, CttUi.Muted);
            CttUi.Label(At(card, "RolesCaption", 1000, 50, 290), "Text", CTTConfig.Tr("chooseRole", ca), 26, CttUi.Muted);

            for (int i = 0; i < CTTConfig.RoleOrder.Length; i++)
            {
                string role = CTTConfig.RoleOrder[i];
                float x = (i % 3 - 1) * 340f;
                float y = 370f + (i / 3) * 150f;
                roleButtons[i] = CttUi.Button(card, "Role_" + role, CTTConfig.RoleName(role, ca),
                    CTTConfig.ColorOf(role), () => RoleRequested?.Invoke(role),
                    new Vector2(0.5f, 1f), new Vector2(320, 130), new Vector2(x, -y), 22);
            }

            startButton = CttUi.Button(card, "Start", CTTConfig.Tr("start", ca), CttUi.Accent,
                () => StartRequested?.Invoke(),
                new Vector2(0.5f, 1f), new Vector2(700, 76), new Vector2(0, -700), 32);
            CttUi.Button(card, "Leave", CTTConfig.Tr("leave", ca), CttUi.Panel,
                () => LeaveRequested?.Invoke(),
                new Vector2(0.5f, 1f), new Vector2(300, 58), new Vector2(0, -790), 24);
        }

        private void BuildHud()
        {
            hudPanel = CttUi.PanelAt("HudPanel", null, new Color(0f, 0f, 0f, 0f));
            hudPanel.GetComponent<Image>().raycastTarget = false;
            RectTransform top = CttUi.Rect("TopBar", hudPanel);
            CttUi.Place(top, new Vector2(0.5f, 1f), new Vector2(1800, 120), new Vector2(0, -90));
            top.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.16f, 0.82f);

            bool ca = game != null && game.Catalan;
            hudRole = CttUi.LabelAt(top, "Role", "", 30, CttUi.Accent,
                new Vector2(0f, 0.5f), new Vector2(520, 90), new Vector2(-620, 0), TextAnchor.MiddleLeft);
            hudLives = CttUi.LabelAt(top, "Lives", "", 28, Color.white,
                new Vector2(0f, 0.5f), new Vector2(340, 90), new Vector2(-180, 0));
            hudTimer = CttUi.LabelAt(top, "Timer", "", 40, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(220, 90), Vector2.zero);
            hudScoreA = CttUi.LabelAt(top, "ScoreA", "", 26, CttUi.Accent2,
                new Vector2(1f, 0.5f), new Vector2(330, 90), new Vector2(-480, 0));
            hudScoreB = CttUi.LabelAt(top, "ScoreB", "", 26, CttUi.Accent,
                new Vector2(1f, 0.5f), new Vector2(330, 90), new Vector2(-160, 0));
            hudSprint = CttUi.LabelAt(top, "Sprint", "", 24, CttUi.Muted,
                new Vector2(1f, 0.5f), new Vector2(240, 90), new Vector2(-50, 0));

            CttUi.LabelAt(hudPanel, "Hint", CTTConfig.Tr("moveHint", ca), 22, CttUi.Muted,
                new Vector2(0.5f, 0f), new Vector2(1400, 50), new Vector2(0, 60));

            BuildTouchPad();
        }

        private void BuildTouchPad()
        {
            var pad = CttUi.Rect("TouchPad", hudPanel);
            CttUi.Place(pad, new Vector2(0f, 0f), new Vector2(420, 420), new Vector2(210, 210));

            HoldAt(pad, "Up", new Vector2(0, 1), new Vector2(-140, 0), -1, 0);
            HoldAt(pad, "Down", new Vector2(0, -1), new Vector2(-140, 0), 1, 0);
            HoldAt(pad, "Left", new Vector2(-1, 0), new Vector2(-280, -140), 0, -1);
            HoldAt(pad, "Right", new Vector2(1, 0), new Vector2(0, -140), 0, 1);

            RectTransform sprint = CttUi.Rect("SprintButton", hudPanel);
            CttUi.Place(sprint, new Vector2(1f, 0f), new Vector2(260, 120), new Vector2(-180, 150));
            sprint.gameObject.AddComponent<Image>().color = CttUi.Accent2;
            var hold = sprint.gameObject.AddComponent<CttHoldButton>();
            hold.Setup(pressed => TouchSprint = pressed);
            CttUi.Label(sprint, "Caption", CTTConfig.Tr("sprint", game != null && game.Catalan),
                28, CttUi.Background);
        }

        private void HoldAt(RectTransform parent, string name, Vector2 direction,
            Vector2 position, int dx, int dy)
        {
            RectTransform rt = CttUi.Rect(name, parent);
            CttUi.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(130, 130), position);
            rt.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.22f, 0.44f, 0.9f);
            rt.gameObject.AddComponent<CttHoldButton>().Setup(pressed =>
            {
                if (dx < 0) TouchX = pressed ? -1 : 0;
                if (dx > 0) TouchX = pressed ? 1 : 0;
                if (dy < 0) TouchY = pressed ? -1 : 0;
                if (dy > 0) TouchY = pressed ? 1 : 0;
            });
            CttUi.Label(rt, "Arrow", new string(new[]
            {
                direction == Vector2.up ? '▲' : direction == Vector2.down ? '▼' :
                direction == Vector2.left ? '◀' : '▶'
            }, 0, 1), 44, Color.white);
        }

        private void BuildResult()
        {
            resultPanel = Card("ResultPanel");
            RectTransform card = CardInner(resultPanel, 1080, 780);
            bool ca = game != null && game.Catalan;

            CttUi.Label(At(card, "Over", 1000, 60, 50), "Text", CTTConfig.Tr("gameOver", ca), 34, CttUi.Muted);
            resultTitle = CttUi.Label(At(card, "Title", 1000, 90, 130), "Text", "", 56, CttUi.Accent);
            resultReason = CttUi.Label(At(card, "Reason", 1000, 70, 224), "Text", "", 28, CttUi.Muted);
            resultStats = CttUi.Label(At(card, "Stats", 980, 270, 336), "Text", "", 25, Color.white, TextAnchor.UpperCenter);
            CttUi.Button(card, "Again", CTTConfig.Tr("playAgain", ca), CttUi.Accent,
                () => RematchRequested?.Invoke(),
                new Vector2(0.5f, 1f), new Vector2(700, 74), new Vector2(0, -636), 32);
            CttUi.Button(card, "Home", CTTConfig.Tr("backHome", ca), CttUi.Panel,
                () => LeaveRequested?.Invoke(),
                new Vector2(0.5f, 1f), new Vector2(400, 60), new Vector2(0, -726), 24);
        }

        /* ----------------------------- presentation ----------------------------- */

        public void ShowPhase(GamePhase target)
        {
            phase = target;
            homePanel.gameObject.SetActive(target == GamePhase.Home);
            lobbyPanel.gameObject.SetActive(target == GamePhase.Lobby);
            hudPanel.gameObject.SetActive(target == GamePhase.Playing);
            resultPanel.gameObject.SetActive(target == GamePhase.Result);
            if (target != GamePhase.Playing)
            {
                TouchX = TouchY = 0;
                TouchSprint = false;
            }
            Toast("");
        }

        public void Toast(string message)
        {
            if (toast != null) toast.text = message ?? "";
            if (homeStatus != null && phase == GamePhase.Home) homeStatus.text = message ?? "";
        }

        public void RenderLobby(CTTLobby lobby, string playerId)
        {
            if (lobby == null) return;
            bool ca = game != null && game.Catalan;
            lobbyCode.text = CTTConfig.Tr("roomCode", ca) + ": " + lobby.code;
            int humans = lobby.players != null ? lobby.players.Length : 0;
            lobbyPlayers.text = CTTConfig.Tr("players", ca) + ": " + humans + " / 6";

            if (startButton != null) startButton.interactable = playerId == lobby.hostId;

            var taken = new System.Collections.Generic.Dictionary<string, string>();
            if (lobby.players != null)
            {
                foreach (CTTLobbyPlayer p in lobby.players)
                    if (!string.IsNullOrEmpty(p.role) && !taken.ContainsKey(p.role)) taken[p.role] = p.name;
            }
            foreach (CTTRoleSlot slot in lobby.roles ?? Array.Empty<CTTRoleSlot>())
            {
                int index = Array.FindIndex(CTTConfig.RoleOrder, r => r == slot.role);
                if (index < 0 || index >= roleButtons.Length || roleButtons[index] == null) continue;
                string shortName = CTTConfig.RoleName(slot.role, ca);
                bool mine = slot.takenBy == playerId;
                roleButtons[index].GetComponentInChildren<Text>().text =
                    shortName + "\n" + (taken.ContainsKey(slot.role) ? taken[slot.role] : CTTConfig.Tr("free", ca))
                    + (mine ? "  ✓" : "");
            }
        }

        public void UpdateHud(CTTState snapshot, string playerId)
        {
            if (snapshot == null) return;
            bool ca = game != null && game.Catalan;

            int seconds = Mathf.Max(0, Mathf.CeilToInt(snapshot.remainingMs / 1000f));
            hudTimer.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");

            int a = 0, b = 0;
            foreach (CTTPlayer p in snapshot.players ?? Array.Empty<CTTPlayer>())
            {
                if (p.role == "troublemakerA") a = p.coins;
                if (p.role == "troublemakerB") b = p.coins;
            }
            hudScoreA.text = (ca ? "Família A  " : "Family A  ") + a + "/" + snapshot.goal;
            hudScoreB.text = (ca ? "Família B  " : "Family B  ") + b + "/" + snapshot.goal;

            CTTPlayer me = null;
            foreach (CTTPlayer p in snapshot.players ?? Array.Empty<CTTPlayer>())
                if (p.id == playerId) { me = p; break; }

            if (me == null)
            {
                hudRole.text = CTTConfig.Tr("spectator", ca);
                hudLives.text = "";
                hudSprint.text = "";
                return;
            }

            hudRole.text = (ca ? "ETS  " : "YOU ARE  ") + CTTConfig.RoleName(me.role, ca);
            bool kids = me.team == "troublemakers";
            hudLives.text = kids
                ? (ca ? "VIDES  " : "LIVES  ") + me.lives
                : (ca ? "Pares" : "Parents");
            hudSprint.text = me.sprintIn > 0.01f
                ? (ca ? "Esprint " : "Sprint ") + me.sprintIn.ToString("0.0") + "s"
                : CTTConfig.Tr("ready", ca);
        }

        public void ShowResult(CTTResult result)
        {
            bool ca = game != null && game.Catalan;
            resultTitle.text = result != null && result.winnerTeam == "troublemakers"
                ? CTTConfig.Tr("kidsWin", ca) : CTTConfig.Tr("parentsWin", ca);
            resultReason.text = result == null ? "" : ReasonText(result.reason, ca);

            var sb = new System.Text.StringBuilder();
            if (result == null) { resultStats.text = ""; return; }
            sb.Append(CTTConfig.Tr("duration", ca)).Append(": ").Append(result.durationMs / 1000).Append("s\n");
            if (result.stats != null)
            {
                sb.Append(CTTConfig.Tr("catches", ca)).Append(": ").Append(result.stats.catches).Append("    ")
                  .Append(CTTConfig.Tr("collected", ca)).Append(": ").Append(result.stats.coinsCollected).Append("    ")
                  .Append(CTTConfig.Tr("confiscated", ca)).Append(": ").Append(result.stats.confiscated).Append("\n\n");
            }
            foreach (CTTStanding standing in result.standings ?? Array.Empty<CTTStanding>())
            {
                sb.Append(CTTConfig.ShortOf(standing.role)).Append("  ").Append(standing.name)
                  .Append("  —  ").Append(standing.coins).Append(' ').Append(ca ? "monedes" : "coins").Append('\n');
            }
            resultStats.text = sb.ToString();
        }

        private static string ReasonText(string reason, bool ca)
        {
            if (string.IsNullOrEmpty(reason)) return "";
            if (reason.Contains("time")) return CTTConfig.Tr("reasonTimeout", ca);
            if (reason.Contains("grounded")) return CTTConfig.Tr("reasonCaught", ca);
            return CTTConfig.Tr("reasonGoal", ca);
        }

        /// <summary>Rebuild after a language or contrast change, then redraw current data.</summary>
        public void Refresh(bool catalan, bool contrast, CTTState state, CTTLobby lobby,
            CTTResult result, string playerId, GamePhase current)
        {
            string serverUrl = CTTApi.ServerUrl;
            string playerName = PlayerName;
            Build();
            serverField.text = serverUrl;
            nameField.text = playerName;
            phase = current;
            ShowPhase(current);
            if (lobby != null) RenderLobby(lobby, playerId);
            if (state != null && current == GamePhase.Playing) UpdateHud(state, playerId);
            if (result != null && current == GamePhase.Result) ShowResult(result);
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
