using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Unity 2D client. It owns the screens, input and rendering; the Node server
    /// remains authoritative for the map, coins, catches and the final result.
    /// </summary>
    public class CttGame : MonoBehaviour
    {
        private enum Screen { Home, Lobby, Game, Result }

        private const string CodeKey = "ctt_code";
        private const string IdKey = "ctt_playerId";
        private const string LangKey = "ctt_catalan";
        private const string ContrastKey = "ctt_contrast";

        private Screen screen;
        private string code;
        private string playerId;
        private bool catalan;
        private bool highContrast;

        private CTTState state;
        private CTTLobby lobby;
        private CTTResult finalResult;

        private CTTApi api;
        private CttSseClient sse;
        private Coroutine pollRunner;

        private float nextInputAt;
        private float nextPollAt;
        private int lastScreenW, lastScreenH;

        // UI references
        private Canvas canvas;
        private RectTransform homePanel, lobbyPanel, gamePanel, resultPanel, gameArea;
        private Text toast, homeStatus, lobbyStatus, lobbyCodeText, lobbyPlayersText;
        private Text hudRole, hudLives, hudTimer, hudScoreA, hudScoreB, hudSprint, resultTitle, resultReason, resultStats;
        private InputField nameField, serverField, joinField;
        private readonly Button[] roleButtons = new Button[7];
        private Button startButton;
        private RawImage boardImage;
        private RectTransform boardFrame, boardContent;
        private Texture2D boardTexture;
        private string boardKey = "";
        private readonly List<RectTransform> coinViews = new List<RectTransform>();
        private readonly Dictionary<string, Marker> markers = new Dictionary<string, Marker>();
        private int touchDx, touchDy;
        private bool touchSprint;

        private sealed class Marker
        {
            public RectTransform root;
            public Image icon;
            public Text label;
        }

        private void Awake()
        {
            api = GetComponent<CTTApi>();
            sse = GetComponent<CttSseClient>();
            catalan = PlayerPrefs.GetInt(LangKey, 0) == 1;
            highContrast = PlayerPrefs.GetInt(ContrastKey, 0) == 1;
            BuildUi();
        }

        private void Start()
        {
            pollRunner = StartCoroutine(Poll());

            string savedCode = PlayerPrefs.GetString(CodeKey, "");
            string savedId = PlayerPrefs.GetString(IdKey, "");
            if (savedCode.Length == 5 && savedId.Length > 0)
            {
                code = savedCode;
                playerId = savedId;
                Say("Reconnecting…");
                Join(savedId, true);
            }
            else
            {
                ShowScreen(Screen.Home);
            }
        }

        private void OnDestroy()
        {
            if (pollRunner != null) StopCoroutine(pollRunner);
            if (sse != null) sse.Stop();
            if (boardTexture != null) Destroy(boardTexture);
        }

        /* ----------------------------- UI construction ----------------------------- */

        private void BuildUi()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            coinViews.Clear();
            markers.Clear();
            boardTexture = null;
            boardKey = "";

            var canvasObject = new GameObject("CTT Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = CttUi.PanelAt("Background", canvas.transform, CttUi.Background);
            bg.SetAsFirstSibling();

            // Accessibility controls, always visible.
            CttUi.Button(canvas.transform, "Lang", T("lang"), CttUi.Panel, ToggleLanguage,
                new Vector2(1f, 1f), new Vector2(170, 54), new Vector2(-210, -44), 24);
            CttUi.Button(canvas.transform, "Contrast", T("contrast"), CttUi.Panel, ToggleContrast,
                new Vector2(1f, 1f), new Vector2(230, 54), new Vector2(-430, -44), 22);

            var toastHolder = CttUi.Rect("ToastHolder", canvas.transform);
            CttUi.Place(toastHolder, new Vector2(0.5f, 1f), new Vector2(1100, 54), new Vector2(0, -128));
            toast = CttUi.Label(toastHolder, "Toast", "", 26, CttUi.Accent);

            BuildHome();
            BuildLobby();
            BuildGame();
            BuildResult();
        }

        private static RectTransform At(RectTransform parent, string name, float width, float height, float top, float x = 0)
        {
            var rt = CttUi.Rect(name, parent);
            CttUi.Place(rt, new Vector2(0.5f, 1f), new Vector2(width, height), new Vector2(x, -top));
            return rt;
        }

        private void BuildHome()
        {
            homePanel = Card("HomePanel");
            var card = CardInner(homePanel, 1040, 900);

            CttUi.Label(At(card, "Title", 1000, 90, 50), "Text", CTTConfig.Tr("title", catalan), 58, CttUi.Accent);
            CttUi.Label(At(card, "Tagline", 1000, 60, 130), "Text", CTTConfig.Tr("tagline", catalan), 30, CttUi.Muted);

            CttUi.Label(At(card, "NameCaption", 900, 40, 214), "Text", T("name"), 26, CttUi.Muted, TextAnchor.MiddleLeft);
            nameField = CttUi.Field(card, "Name", "Marta", PlayerPrefs.GetString(CTTConfig.NamePrefKey, ""),
                new Vector2(0.5f, 1f), new Vector2(900, 64), new Vector2(0, -282));

            CttUi.Label(At(card, "ServerCaption", 900, 36, 362), "Text", T("server"), 24, CttUi.Muted, TextAnchor.MiddleLeft);
            serverField = CttUi.Field(card, "Server", "http://localhost:3000", CTTApi.ServerUrl,
                new Vector2(0.5f, 1f), new Vector2(900, 58), new Vector2(0, -416));

            CttUi.Button(card, "Create", T("create"), CttUi.Accent, CreateRoom,
                new Vector2(0.5f, 1f), new Vector2(900, 76), new Vector2(0, -510));

            joinField = CttUi.Field(card, "JoinCode", "FQJFD", "",
                new Vector2(0.5f, 1f), new Vector2(560, 64), new Vector2(-170, -610));
            CttUi.Button(card, "Join", T("join"), CttUi.Accent2, () => Join(joinField.text.Trim().ToUpperInvariant(), false),
                new Vector2(0.5f, 1f), new Vector2(300, 64), new Vector2(320, -610), 26);

            CttUi.Label(At(card, "Rules", 960, 160, 712), "Text", CTTConfig.Tr("rules", catalan), 24, CttUi.Muted);
            var statusHolder = At(card, "Status", 960, 40, 838);
            homeStatus = CttUi.Label(statusHolder, "Text", "", 24, CttUi.Danger);
        }

        private void BuildLobby()
        {
            lobbyPanel = Card("LobbyPanel");
            var card = CardInner(lobbyPanel, 1120, 900);

            CttUi.Label(At(card, "Title", 1000, 70, 40), "Text",
                catalan ? "SALA D'ESPERA" : "WAITING ROOM", 48, CttUi.Accent);
            var codeHolder = At(card, "Code", 1000, 70, 110);
            lobbyCodeText = CttUi.Label(codeHolder, "Text", "", 40, Color.white);

            var playersHolder = At(card, "Players", 1000, 84, 194);
            lobbyPlayersText = CttUi.Label(playersHolder, "Text", "", 24, CttUi.Muted);

            CttUi.Label(At(card, "RolesCaption", 1000, 40, 282), "Text", CTTConfig.Tr("chooseRole", catalan),
                26, CttUi.Muted);

            for (int i = 0; i < CTTConfig.RoleOrder.Length; i++)
            {
                string role = CTTConfig.RoleOrder[i];
                int col = i % 3;
                int row = i / 3;
                float x = (col - 1) * 340f;
                float y = 342f + row * 94f;
                roleButtons[i] = CttUi.Button(card, "Role_" + role,
                    CTTConfig.RoleName(role, catalan), CTTConfig.ColorOf(role, highContrast),
                    () => ChooseRole(role), new Vector2(0.5f, 1f), new Vector2(320, 78),
                    new Vector2(x, -y), 24);
            }

            startButton = CttUi.Button(card, "Start", T("start"), CttUi.Accent, StartMatch,
                new Vector2(0.5f, 1f), new Vector2(700, 78), new Vector2(0, -576), 34);
            CttUi.Button(card, "Leave", T("leave"), CttUi.Panel, LeaveRoom,
                new Vector2(0.5f, 1f), new Vector2(300, 60), new Vector2(0, -676), 24);

            var statusHolder = At(card, "Status", 1000, 50, 758);
            lobbyStatus = CttUi.Label(statusHolder, "Text", "", 24, CttUi.Danger);
        }

        private void BuildGame()
        {
            gamePanel = CttUi.PanelAt("GamePanel", canvas.transform, CttUi.Background);

            var topBar = CttUi.Rect("TopBar", gamePanel);
            topBar.anchorMin = new Vector2(0, 1);
            topBar.anchorMax = new Vector2(1, 1);
            topBar.pivot = new Vector2(0.5f, 1);
            topBar.offsetMin = new Vector2(0, -118);
            topBar.offsetMax = new Vector2(0, 0);
            var barImage = topBar.gameObject.AddComponent<Image>();
            barImage.color = CttUi.Panel;

            hudRole = CttUi.LabelAt(topBar, "Role", "—", 30, CttUi.Accent,
                new Vector2(0, 0.5f), new Vector2(600, 100), new Vector2(320, 0), TextAnchor.MiddleLeft);
            hudTimer = CttUi.LabelAt(topBar, "Timer", "6:00", 38, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(280, 100), Vector2.zero);
            hudScoreA = CttUi.LabelAt(topBar, "ScoreA", "", 26, CTTConfig.ColorOf("troublemakerA", highContrast),
                new Vector2(1, 0.5f), new Vector2(260, 100), new Vector2(-490, 0));
            hudScoreB = CttUi.LabelAt(topBar, "ScoreB", "", 26, CTTConfig.ColorOf("troublemakerB", highContrast),
                new Vector2(1, 0.5f), new Vector2(260, 100), new Vector2(-210, 0));

            gameArea = CttUi.Rect("GameArea", gamePanel);
            CttUi.Stretch(gameArea, 40, 40, 132, 196);

            boardFrame = CttUi.Rect("BoardFrame", gameArea);
            CttUi.Place(boardFrame, new Vector2(0.5f, 0.5f), new Vector2(800, 500), Vector2.zero);
            boardImage = CttUi.BoardImage(boardFrame);
            boardContent = CttUi.Rect("BoardContent", boardFrame);
            CttUi.Stretch(boardContent);

            var bottom = CttUi.Rect("BottomBar", gamePanel);
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = new Vector2(1, 0);
            bottom.pivot = new Vector2(0.5f, 0);
            bottom.offsetMin = new Vector2(0, 0);
            bottom.offsetMax = new Vector2(0, 186);
            var bottomImage = bottom.gameObject.AddComponent<Image>();
            bottomImage.color = CttUi.Panel;

            hudLives = CttUi.LabelAt(bottom, "Lives", "", 26, Color.white,
                new Vector2(0, 0.5f), new Vector2(600, 140), new Vector2(330, 0), TextAnchor.MiddleLeft);
            hudSprint = CttUi.LabelAt(bottom, "SprintInfo", "", 22, CttUi.Muted,
                new Vector2(1, 0.5f), new Vector2(360, 60), new Vector2(-250, 55));

            var sprintButton = CttUi.Button(bottom, "Sprint", T("sprint"), CttUi.Danger, null,
                new Vector2(1, 0.5f), new Vector2(300, 78), new Vector2(-190, -24), 28);
            var sprintHold = sprintButton.gameObject.AddComponent<CttHoldButton>();
            sprintHold.Setup(value => touchSprint = value);

            MakePadButton(bottom, "Up", "▲", new Vector2(0, 1), new Vector2(78, 0), value => touchDy = value ? -1 : 0);
            MakePadButton(bottom, "Down", "▼", new Vector2(0, -1), new Vector2(78, -80), value => touchDy = value ? 1 : 0);
            MakePadButton(bottom, "Left", "◀", new Vector2(-1, 0), new Vector2(-10, -40), value => touchDx = value ? -1 : 0);
            MakePadButton(bottom, "Right", "▶", new Vector2(1, 0), new Vector2(166, -40), value => touchDx = value ? 1 : 0);

            CttUi.LabelAt(bottom, "Hint", CTTConfig.Tr("moveHint", catalan), 20, CttUi.Muted,
                new Vector2(0.5f, 0), new Vector2(1000, 44), new Vector2(0, 16));
        }

        private void MakePadButton(Transform parent, string name, string glyph, Vector2 direction,
            Vector2 position, Action<bool> callback)
        {
            var button = CttUi.Button(parent, "Pad" + name, glyph, CttUi.Panel, null,
                new Vector2(0.5f, 0.5f), new Vector2(76, 68), position, 30);
            var hold = button.gameObject.AddComponent<CttHoldButton>();
            hold.Setup(callback);
        }

        private void BuildResult()
        {
            resultPanel = Card("ResultPanel");
            var card = CardInner(resultPanel, 1080, 780);
            CttUi.Label(At(card, "Over", 1000, 60, 50), "Text", CTTConfig.Tr("gameOver", catalan), 34, CttUi.Muted);
            var titleHolder = At(card, "Title", 1000, 90, 130);
            resultTitle = CttUi.Label(titleHolder, "Text", "", 56, CttUi.Accent);
            var reasonHolder = At(card, "Reason", 1000, 70, 224);
            resultReason = CttUi.Label(reasonHolder, "Text", "", 28, CttUi.Muted);
            var statsHolder = At(card, "Stats", 980, 270, 336);
            resultStats = CttUi.Label(statsHolder, "Text", "", 25, Color.white, TextAnchor.UpperCenter);
            CttUi.Button(card, "Again", T("playAgain"), CttUi.Accent, Rematch,
                new Vector2(0.5f, 1f), new Vector2(700, 74), new Vector2(0, -636), 32);
            CttUi.Button(card, "Home", T("backHome"), CttUi.Panel, LeaveRoom,
                new Vector2(0.5f, 1f), new Vector2(400, 60), new Vector2(0, -726), 24);
        }

        private static RectTransform Card(string name) =>
            CttUi.PanelAt(name, null, CttUi.Background);

        private RectTransform CardInner(RectTransform parent, float width, float height)
        {
            var rt = CttUi.Rect("Card", parent);
            CttUi.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(width, height), Vector2.zero);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = CttUi.Panel;
            return rt;
        }

        /* ----------------------------- screens / i18n ----------------------------- */

        private string T(string key) => CTTConfig.Tr(key, catalan);

        private void ShowScreen(Screen target)
        {
            screen = target;
            homePanel.gameObject.SetActive(target == Screen.Home);
            lobbyPanel.gameObject.SetActive(target == Screen.Lobby);
            gamePanel.gameObject.SetActive(target == Screen.Game);
            resultPanel.gameObject.SetActive(target == Screen.Result);
            toast.text = "";
        }

        private void Say(string message)
        {
            if (toast != null) toast.text = message;
        }

        private void ToggleLanguage()
        {
            catalan = !catalan;
            PlayerPrefs.SetInt(LangKey, catalan ? 1 : 0);
            PlayerPrefs.Save();
            Rebuild();
        }

        private void ToggleContrast()
        {
            highContrast = !highContrast;
            PlayerPrefs.SetInt(ContrastKey, highContrast ? 1 : 0);
            PlayerPrefs.Save();
            Rebuild();
        }

        private void Rebuild()
        {
            BuildUi();
            ShowScreen(screen);
            if (screen == Screen.Lobby) RenderLobby();
            if (screen == Screen.Game && state != null) ApplyState(state);
            if (screen == Screen.Result && finalResult != null) ShowResult(finalResult, false);
        }

        /* ----------------------------- networking ----------------------------- */

        private void CreateRoom()
        {
            string name = Sanitize(nameField.text);
            PlayerPrefs.SetString(CTTConfig.NamePrefKey, name);
            PlayerPrefs.Save();
            api.Post("/api/rooms", new CTTNameRequest { name = name },
                json =>
                {
                    var response = JsonUtility.FromJson<CTTRoomResponse>(json);
                    if (response == null || string.IsNullOrEmpty(response.code))
                    {
                        Fail(response == null ? null : response.error);
                        return;
                    }
                    code = response.code;
                    playerId = response.playerId;
                    lobby = response.lobby;
                    SaveSession();
                    EnterLobby();
                },
                Fail);
        }

        private void Join(string roomCode, bool reconnect)
        {
            roomCode = (roomCode ?? "").Trim().ToUpperInvariant();
            if (roomCode.Length != 5)
            {
                if (!reconnect) Say(CTTConfig.Tr("invalidCode", catalan));
                return;
            }
            api.Post("/api/rooms/" + roomCode + "/join",
                new CTTJoinRequest { name = Sanitize(nameField != null ? nameField.text : PlayerPrefs.GetString(CTTConfig.NamePrefKey, "Player")), playerId = reconnect ? playerId : null },
                json =>
                {
                    var response = JsonUtility.FromJson<CTTRoomResponse>(json);
                    if (response == null || string.IsNullOrEmpty(response.code))
                    {
                        Fail(response == null ? null : response.error);
                        return;
                    }
                    code = response.code;
                    playerId = response.playerId;
                    lobby = response.lobby;
                    SaveSession();
                    if (lobby != null && lobby.status == "playing") EnterGame();
                    else EnterLobby();
                },
                err =>
                {
                    if (reconnect)
                    {
                        ClearSession();
                        ShowScreen(Screen.Home);
                        Say("");
                    }
                    else Fail(err);
                });
        }

        private void ChooseRole(string role)
        {
            api.Post("/api/rooms/" + code + "/role", new CTTRoleRequest { playerId = playerId, role = role },
                json =>
                {
                    var response = JsonUtility.FromJson<CTTLobbyResponse>(json);
                    if (response == null || !response.ok) { Fail(response == null ? null : response.error); return; }
                    lobby = response.lobby;
                    RenderLobby();
                }, Fail);
        }

        private void StartMatch()
        {
            api.Post("/api/rooms/" + code + "/start", new CTTStartRequest { playerId = playerId },
                json =>
                {
                    var response = JsonUtility.FromJson<CTTOkResponse>(json);
                    if (response == null || !response.ok) { Fail(response == null ? null : response.error); return; }
                    EnterGame();
                }, Fail);
        }

        private void Rematch()
        {
            api.Post("/api/rooms/" + code + "/restart", new CTTStartRequest { playerId = playerId },
                json =>
                {
                    var response = JsonUtility.FromJson<CTTOkResponse>(json);
                    if (response == null || !response.ok) { Fail(response == null ? null : response.error); return; }
                    EnterGame();
                }, Fail);
        }

        private void LeaveRoom()
        {
            if (sse != null) sse.Stop();
            ClearSession();
            code = null;
            playerId = null;
            state = null;
            finalResult = null;
            boardKey = "";
            ShowScreen(Screen.Home);
        }

        private void EnterLobby()
        {
            ConnectSse();
            ShowScreen(Screen.Lobby);
            RenderLobby();
            nextPollAt = 0;
        }

        private void EnterGame()
        {
            ConnectSse();
            boardKey = "";
            nextPollAt = 0;
            nextInputAt = 0;
            ShowScreen(Screen.Game);
            RefreshState();
        }

        private void ConnectSse()
        {
            if (sse == null || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(playerId)) return;
            sse.Connect(code, playerId, OnSseEvent);
        }

        private void RefreshState()
        {
            if (string.IsNullOrEmpty(code)) return;
            api.Get("/api/rooms/" + code + "/state",
                json =>
                {
                    var snapshot = JsonUtility.FromJson<CTTState>(json);
                    if (snapshot == null) return;
                    if (snapshot.lobby != null) lobby = snapshot.lobby;
                    if (screen == Screen.Lobby) RenderLobby();
                    if (screen == Screen.Game && snapshot.status == "playing") ApplyState(snapshot);
                    if (snapshot.status == "finished" && snapshot.result != null) ShowResult(snapshot.result);
                }, err => { });
        }

        private IEnumerator Poll()
        {
            while (true)
            {
                if ((screen == Screen.Lobby || screen == Screen.Game) && !string.IsNullOrEmpty(code)
                    && Time.unscaledTime >= nextPollAt)
                {
                    nextPollAt = Time.unscaledTime + 0.35f;
                    RefreshState();
                }
                yield return null;
            }
        }

        private void OnSseEvent(string eventName, string data)
        {
            if (eventName == "lobby")
            {
                var updated = JsonUtility.FromJson<CTTLobby>(data);
                if (updated != null) { lobby = updated; if (screen == Screen.Lobby) RenderLobby(); }
                return;
            }
            if (eventName == "result")
            {
                var response = JsonUtility.FromJson<CTTResultResponse>(data);
                if (response != null && response.result != null) ShowResult(response.result);
                return;
            }
            if (eventName == "hello" || eventName == "start" || eventName == "state")
            {
                var snapshot = JsonUtility.FromJson<CTTState>(data);
                if (snapshot == null) return;
                if (snapshot.lobby != null) lobby = snapshot.lobby;
                if (screen == Screen.Lobby) RenderLobby();
                if (snapshot.status == "playing")
                {
                    if (screen != Screen.Game) EnterGame();
                    ApplyState(snapshot);
                }
                else if (snapshot.status == "finished" && snapshot.result != null)
                {
                    ShowResult(snapshot.result);
                }
            }
        }

        /* ----------------------------- rendering ----------------------------- */

        private void RenderLobby()
        {
            if (lobby == null) return;
            lobbyCodeText.text = CTTConfig.Tr("roomCode", catalan) + ": " + lobby.code;
            int humans = lobby.players != null ? lobby.players.Length : 0;
            lobbyPlayersText.text = CTTConfig.Tr("players", catalan) + ": " + humans + " / 6   •   "
                + (catalan ? "Comparteix el codi amb els teus companys" : "Share the code with your friends");

            bool host = playerId == lobby.hostId;
            if (startButton != null) startButton.interactable = host;

            var taken = new Dictionary<string, string>();
            if (lobby.players != null)
            {
                foreach (var p in lobby.players)
                {
                    if (!string.IsNullOrEmpty(p.role) && !taken.ContainsKey(p.role))
                        taken[p.role] = p.name;
                }
            }
            foreach (var slot in lobby.roles ?? Array.Empty<CTTRoleSlot>())
            {
                int index = Array.FindIndex(CTTConfig.RoleOrder, r => r == slot.role);
                if (index < 0 || index >= roleButtons.Length || roleButtons[index] == null) continue;
                string shortName = CTTConfig.RoleName(slot.role, catalan);
                roleButtons[index].GetComponentInChildren<Text>().text =
                    shortName + "\n" + (taken.ContainsKey(slot.role) ? taken[slot.role] : CTTConfig.Tr("free", catalan));
            }
        }

        private void ApplyState(CTTState snapshot)
        {
            state = snapshot;
            if (snapshot.map != null && snapshot.map.cols > 0 && snapshot.map.rows > 0
                && !string.IsNullOrEmpty(snapshot.map.tiles))
            {
                string key = snapshot.map.cols + "x" + snapshot.map.rows + ":" + snapshot.map.tiles;
                if (key != boardKey)
                {
                    boardKey = key;
                    if (boardTexture != null) Destroy(boardTexture);
                    boardTexture = CTTDraw.BuildMap(snapshot.map, highContrast);
                    if (boardImage != null) boardImage.texture = boardTexture;
                }
                LayoutBoard(snapshot.map.cols, snapshot.map.rows);
                UpdateActors(snapshot);
            }
            UpdateHud(snapshot);
        }

        private void LayoutBoard(int cols, int rows)
        {
            if (boardFrame == null || gameArea == null) return;
            float aspect = (float)cols / rows;
            float w = Mathf.Max(100, gameArea.rect.width - 32f);
            float h = Mathf.Max(100, gameArea.rect.height - 32f);
            if (w / h > aspect) w = h * aspect;
            else h = w / aspect;
            CttUi.Place(boardFrame, new Vector2(0.5f, 0.5f), new Vector2(w, h), Vector2.zero);
        }

        private void UpdateActors(CTTState snapshot)
        {
            if (boardFrame == null || snapshot.map == null) return;
            float cols = snapshot.map.cols;
            float rows = snapshot.map.rows;
            float cellW = boardFrame.rect.width / cols;
            float cellH = boardFrame.rect.height / rows;
            float size = Mathf.Max(7f, Mathf.Min(cellW, cellH) * 0.78f);

            var coins = snapshot.coins ?? Array.Empty<CTTCoin>();
            for (int i = 0; i < coins.Length; i++)
            {
                while (coinViews.Count <= i)
                {
                    var view = CttUi.Rect("Coin", boardContent);
                    view.anchorMin = view.anchorMax = Vector2.zero;
                    view.pivot = new Vector2(0.5f, 0.5f);
                    var image = view.gameObject.AddComponent<Image>();
                    image.sprite = CTTDraw.Coin();
                    image.raycastTarget = false;
                    coinViews.Add(view);
                }
                var rt = coinViews[i];
                rt.gameObject.SetActive(true);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2((coins[i].x + 0.5f) * cellW,
                    boardFrame.rect.height - (coins[i].y + 0.5f) * cellH);
            }
            for (int i = coins.Length; i < coinViews.Count; i++) coinViews[i].gameObject.SetActive(false);

            var seen = new HashSet<string>();
            foreach (var p in snapshot.players ?? Array.Empty<CTTPlayer>())
            {
                seen.Add(p.id);
                if (!markers.TryGetValue(p.id, out var marker))
                {
                    var root = CttUi.Rect("Player_" + p.id, boardContent);
                    root.anchorMin = root.anchorMax = Vector2.zero;
                    root.pivot = new Vector2(0.5f, 0.5f);
                    var icon = root.gameObject.AddComponent<Image>();
                    icon.raycastTarget = false;
                    var labelRt = CttUi.Rect("Label", root);
                    CttUi.Stretch(labelRt, -30, -30, 0, -28);
                    var label = labelRt.gameObject.AddComponent<Text>();
                    if (CttUi.Font != null) label.font = CttUi.Font;
                    label.fontSize = Mathf.Max(13, Mathf.RoundToInt(size * 0.62f));
                    label.alignment = TextAnchor.UpperCenter;
                    label.color = Color.white;
                    label.raycastTarget = false;
                    label.supportRichText = false;
                    marker = new Marker { root = root, icon = icon, label = label };
                    markers[p.id] = marker;
                }
                marker.root.gameObject.SetActive(!p.eliminated && p.respawnIn <= 0f);
                marker.root.sizeDelta = new Vector2(size * 1.15f, size * 1.15f);
                marker.root.anchoredPosition = new Vector2((p.x + 0.5f) * cellW,
                    boardFrame.rect.height - (p.y + 0.5f) * cellH);
                marker.icon.sprite = CTTDraw.Shape(p.shape);
                marker.icon.color = CTTConfig.ColorOf(p.role, highContrast);
                marker.label.text = CTTConfig.ShortOf(p.role)
                    + (p.team == "troublemakers" ? "\n" + p.coins : "");
                marker.root.SetAsLastSibling();
            }
            var dead = new List<string>();
            foreach (var pair in markers) if (!seen.Contains(pair.Key)) dead.Add(pair.Key);
            foreach (var id in dead)
            {
                Destroy(markers[id].root.gameObject);
                markers.Remove(id);
            }
        }

        private void UpdateHud(CTTState snapshot)
        {
            if (hudRole == null) return;
            CTTPlayer me = null;
            foreach (var p in snapshot.players ?? Array.Empty<CTTPlayer>())
                if (p.id == playerId) { me = p; break; }

            hudRole.text = CTTConfig.Tr("youAre", catalan) + ": "
                + (me == null ? CTTConfig.Tr("spectator", catalan) : CTTConfig.RoleName(me.role, catalan));
            hudLives.text = me == null ? "" :
                (me.team == "troublemakers"
                    ? me.coins + "/" + snapshot.goal + "  •  " + me.lives + " ♥"
                    : CTTConfig.Tr("catches", catalan) + ": " + me.coins);
            hudSprint.text = me == null ? "" :
                (me.sprinting ? (catalan ? "ESPRINTANT!" : "SPRINTING!")
                    : me.sprintIn > 0 ? CTTConfig.Tr("sprint", catalan) + " " + me.sprintIn.ToString("0.0") + "s"
                    : CTTConfig.Tr("ready", catalan));

            int seconds = Mathf.Max(0, Mathf.CeilToInt(snapshot.remainingMs / 1000f));
            hudTimer.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");

            int a = 0, b = 0;
            foreach (var p in snapshot.players ?? Array.Empty<CTTPlayer>())
            {
                if (p.role == "troublemakerA") a = p.coins;
                if (p.role == "troublemakerB") b = p.coins;
            }
            hudScoreA.text = (catalan ? "Família A  " : "Family A  ") + a + "/" + snapshot.goal;
            hudScoreB.text = (catalan ? "Família B  " : "Family B  ") + b + "/" + snapshot.goal;
        }

        private void ShowResult(CTTResult result, bool refreshUi = true)
        {
            if (refreshUi && screen != Screen.Result) Rebuild();
            finalResult = result;
            ShowScreen(Screen.Result);
            if (result == null) return;

            bool kids = result.winnerTeam == "troublemakers";
            resultTitle.text = kids ? CTTConfig.Tr("kidsWin", catalan) : CTTConfig.Tr("parentsWin", catalan);
            resultReason.text = ReasonText(result.reason);

            var sb = new System.Text.StringBuilder();
            sb.Append(CTTConfig.Tr("duration", catalan)).Append(": ")
                .Append(result.durationMs / 1000).Append("s\n");
            if (result.stats != null)
            {
                sb.Append(CTTConfig.Tr("catches", catalan)).Append(": ").Append(result.stats.catches).Append("    ")
                  .Append(CTTConfig.Tr("collected", catalan)).Append(": ").Append(result.stats.coinsCollected).Append("    ")
                  .Append(CTTConfig.Tr("confiscated", catalan)).Append(": ").Append(result.stats.confiscated).Append("\n\n");
            }
            foreach (var standing in result.standings ?? Array.Empty<CTTStanding>())
            {
                sb.Append(CTTConfig.ShortOf(standing.role)).Append("  ").Append(standing.name)
                  .Append("  —  ").Append(standing.coins).Append(" coins\n");
            }
            resultStats.text = sb.ToString();
        }

        private string ReasonText(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "";
            if (reason.Contains("time")) return CTTConfig.Tr("reasonTimeout", catalan);
            if (reason.Contains("grounded")) return CTTConfig.Tr("reasonCaught", catalan);
            return CTTConfig.Tr("reasonGoal", catalan);
        }

        /* ----------------------------- input ----------------------------- */

        private void Update()
        {
            if (screen != Screen.Game) return;

            if (lastScreenW != UnityEngine.Screen.width || lastScreenH != UnityEngine.Screen.height)
            {
                lastScreenW = UnityEngine.Screen.width;
                lastScreenH = UnityEngine.Screen.height;
                if (state != null && state.map != null) LayoutBoard(state.map.cols, state.map.rows);
            }

            int dx = touchDx;
            int dy = touchDy;
            bool sprint = touchSprint;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) dx--;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) dx++;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) dy--;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) dy++;
                sprint |= keyboard.spaceKey.isPressed || keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }
            dx = Mathf.Clamp(dx, -1, 1);
            dy = Mathf.Clamp(dy, -1, 1);

            if (Time.unscaledTime >= nextInputAt && !string.IsNullOrEmpty(code))
            {
                nextInputAt = Time.unscaledTime + 0.1f;
                api.Post("/api/rooms/" + code + "/input",
                    new CTTInputRequest { playerId = playerId, dx = dx, dy = dy, sprint = sprint },
                    ok => { }, err => { });
            }
        }

        /* ----------------------------- helpers ----------------------------- */

        private static string Sanitize(string value)
        {
            string result = (value ?? "").Trim();
            if (result.Length == 0) result = "Player";
            return result.Substring(0, Mathf.Min(24, result.Length));
        }

        private void SaveSession()
        {
            PlayerPrefs.SetString(CodeKey, code);
            PlayerPrefs.SetString(IdKey, playerId);
            PlayerPrefs.Save();
        }

        private void ClearSession()
        {
            PlayerPrefs.DeleteKey(CodeKey);
            PlayerPrefs.DeleteKey(IdKey);
            PlayerPrefs.Save();
        }

        private void Fail(string error)
        {
            Say(string.IsNullOrEmpty(error) ? (catalan ? "Error de connexió" : "Connection error") : error);
        }
    }
}
