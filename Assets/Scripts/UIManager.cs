using System;
using System.Collections.Generic;
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
        public event Action<string> FamilyRequested;
        public event Action StartRequested;
        public event Action RematchRequested;
        public event Action LeaveRequested;
        public event Action LanguageToggled;
        public event Action ContrastToggled;
        public event Action ConnectionTestRequested;
        public event Action DiscoveryRequested;
        public event Action P2pModeToggled;
        public event Action P2pKindToggled;

        /// <summary>True when the home screen is configured for hostless LAN play.</summary>
        public bool P2pMode { get; private set; }

        /// <summary>In P2P mode: one real player plus bots instead of a code-based room.</summary>
        public bool P2pSolo { get; private set; }

        public int TouchX { get; private set; }
        public int TouchY { get; private set; }
        public bool TouchSprint { get; private set; }

        public string PlayerName => nameField != null ? nameField.text : "";

        public void SetServerUrl(string url)
        {
            CTTApi.ServerUrl = url;
            EnsureBuilt();
            if (serverField != null) serverField.text = url;
        }

        /// <summary>Switches the home screen between server and hostless P2P play.</summary>
        public void SetP2pMode(bool enabled)
        {
            P2pMode = enabled;
            EnsureBuilt();
            bool ca = game != null && game.Catalan;
            string captionKey = !enabled ? "server"
                : P2pSolo ? "p2pSoloCaption" : "p2pMultiCaption";
            string createKey = !enabled ? "create"
                : P2pSolo ? "playSolo" : "createP2p";
            if (serverCaptionLabel != null) serverCaptionLabel.text = CTTConfig.Tr(captionKey, ca);
            if (modeLabel != null)
                modeLabel.text = CTTConfig.Tr(enabled ? "modeP2p" : "modeServer", ca);
            if (createLabel != null) createLabel.text = CTTConfig.Tr(createKey, ca);
            if (p2pKindLabel != null)
                p2pKindLabel.text = CTTConfig.Tr(P2pSolo ? "kindSolo" : "kindMulti", ca);
            if (serverField != null) serverField.gameObject.SetActive(!enabled);
            if (findButton != null) findButton.gameObject.SetActive(!enabled);
            if (testButton != null) testButton.gameObject.SetActive(!enabled);
            if (p2pKindButton != null) p2pKindButton.gameObject.SetActive(enabled);
            if (joinField != null) joinField.gameObject.SetActive(enabled && !P2pSolo);
            if (joinButton != null) joinButton.gameObject.SetActive(enabled && !P2pSolo);
            Toast(enabled ? CTTConfig.Tr(P2pSolo ? "p2pSoloHint" : "p2pMultiHint", ca) : "");
        }

        /// <summary>Switches P2P between a solo match with bots and a code-based room.</summary>
        public void SetP2pSolo(bool enabled)
        {
            P2pSolo = enabled;
            SetP2pMode(P2pMode); // refresh captions, button labels and visibility
        }

        private GameManager game;
        private GamePhase phase;
        private Canvas canvas;
        private RectTransform homePanel, lobbyPanel, hudPanel, resultPanel;
        private Text toast, homeStatus, lobbyCodeCaption, lobbyCode, lobbyPlayers, lobbyNames, lobbyHint, familyCaption, familyHint, hudRole, hudLives;
        private Text hudTimer, hudScoreA, hudScoreB, hudSprint, resultTitle, resultReason, resultStats;
        private Text modeLabel, serverCaptionLabel, createLabel, p2pKindLabel;
        private Button modeButton, findButton, testButton, createButton, p2pKindButton, joinButton;
        private InputField nameField, serverField, joinField;
        private readonly Button[] roleButtons = new Button[6];
        private readonly List<Button> familyButtons = new List<Button>();
        private readonly List<string> familyIds = new List<string>();
        private readonly List<Text> familySlotLabels = new List<Text>();
        private readonly Image[] rosterIcons = new Image[6];
        private readonly Text[] rosterLabels = new Text[6];
        private Image hudRoleIcon;
        private Button startButton;

        private void Awake() => EnsureBuilt();

        public void Initialize(GameManager manager)
        {
            game = manager;

            // GameManager.Awake() may run before this component's Awake() when the
            // managers live on separate scene objects, so never assume Build() ran.
            EnsureBuilt();
            serverField.text = CTTApi.ServerUrl;
            if (game.Catalan || game.HighContrast)
                Refresh(game.Catalan, game.HighContrast, null, null, null, null, GamePhase.Home);
        }

        private void EnsureBuilt()
        {
            if (canvas != null && serverField != null && homePanel != null) return;
            Build();
        }

        private void ApplyServer()
        {
            if (serverField == null) return;
            string value = (serverField.text ?? "").Trim();
            if (value.Length == 0) return;
            if (!value.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
                value = "http://" + value;
            if (serverField.text != value) serverField.text = value;
            CTTApi.ServerUrl = value;
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

            // Do NOT add a permanent full-screen backdrop here: the overlay Canvas
            // draws above the world, so an opaque background would hide the whole
            // game. Each menu/lobby/result panel already has its own backdrop and
            // the in-game HUD is deliberately transparent.
            BuildHome();
            BuildLobby();
            BuildHud();
            BuildResult();

            // These overlay controls are added last so they stay above the full-screen
            // phase panels (a sibling added later is drawn on top in uGUI).
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

            if (P2pMode) SetP2pMode(true);
        }

        private static RectTransform At(RectTransform parent, string name, float width, float height,
            float top, float x = 0f)
        {
            RectTransform rt = CttUi.Rect(name, parent);
            CttUi.Place(rt, new Vector2(0.5f, 1f), new Vector2(width, height), new Vector2(x, -top));
            return rt;
        }

        // A uGUI graphic outside the Canvas hierarchy is never rendered, so every
        // full-screen phase panel must be a child of the canvas created in Build().
        private RectTransform Card(string name) => CttUi.PanelAt(name, canvas.transform, CttUi.Background);

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

            CttUi.Label(At(card, "Title", 1000, 84, 46), "Text", CTTConfig.Tr("title", ca), 58, CttUi.Accent);
            CttUi.Label(At(card, "Tagline", 1000, 54, 114), "Text", CTTConfig.Tr("tagline", ca), 28, CttUi.Muted);

            // The six characters are introduced as simple coloured squares.
            RectTransform legend = At(card, "CharacterLegend", 1000, 80, 182);
            for (int i = 0; i < CTTConfig.RoleOrder.Length; i++)
            {
                string role = CTTConfig.RoleOrder[i];
                float x = (i - 2.5f) * 156f;
                CttUi.Icon(legend, "Icon_" + role,
                    CTTConfig.ColorOf(role, game != null && game.HighContrast), new Vector2(52, 52),
                    new Vector2(0.5f, 0.5f), new Vector2(x, 10));
                CttUi.LabelAt(legend, "Code_" + role, CTTConfig.ShortOf(role), 21, CttUi.Muted,
                    new Vector2(0.5f, 0.5f), new Vector2(110, 26), new Vector2(x, -26));
            }

            CttUi.Label(At(card, "NameCaption", 900, 36, 250), "Text", CTTConfig.Tr("name", ca), 24,
                CttUi.Muted, TextAnchor.MiddleLeft);
            nameField = CttUi.Field(card, "Name", "Marta", PlayerPrefs.GetString(CTTConfig.NamePrefKey, ""),
                new Vector2(0.5f, 1f), new Vector2(900, 60), new Vector2(0, -306));

            serverCaptionLabel = CttUi.Label(At(card, "ServerCaption", 900, 32, 362), "Text",
                CTTConfig.Tr("server", ca), 22, CttUi.Muted, TextAnchor.MiddleLeft);
            serverField = CttUi.Field(card, "Server", CTTConfig.DefaultServer, CTTApi.ServerUrl,
                new Vector2(0.5f, 1f), new Vector2(470, 56), new Vector2(-265, -412));
            // Commit as soon as the field loses focus or Enter is pressed, so the URL
            // is stored even if the player never presses TEST/Create/Join.
            serverField.onEndEdit.AddListener(_ => ApplyServer());
            findButton = CttUi.Button(card, "FindLan", CTTConfig.Tr("find", ca), CttUi.Panel,
                () => DiscoveryRequested?.Invoke(),
                new Vector2(0.5f, 1f), new Vector2(110, 56), new Vector2(-5, -412), 15);
            testButton = CttUi.Button(card, "TestConnection", CTTConfig.Tr("test", ca), CttUi.Panel,
                () => { ApplyServer(); ConnectionTestRequested?.Invoke(); },
                new Vector2(0.5f, 1f), new Vector2(150, 56), new Vector2(135, -412), 20);
            modeButton = CttUi.Button(card, "Mode", "", CttUi.Accent2,
                () => P2pModeToggled?.Invoke(),
                new Vector2(0.5f, 1f), new Vector2(190, 56), new Vector2(315, -412), 16);
            modeLabel = modeButton.GetComponentInChildren<Text>();
            // Visible only in P2P mode; the server-only fields leave this row free.
            p2pKindButton = CttUi.Button(card, "P2pKind", "", CttUi.Accent2,
                () => P2pKindToggled?.Invoke(),
                new Vector2(0.5f, 1f), new Vector2(250, 56), new Vector2(0, -412), 16);
            p2pKindLabel = p2pKindButton.GetComponentInChildren<Text>();

            createButton = CttUi.Button(card, "Create", CTTConfig.Tr("create", ca), CttUi.Accent,
                () => { ApplyServer(); CreateRequested?.Invoke(nameField.text); },
                new Vector2(0.5f, 1f), new Vector2(900, 72), new Vector2(0, -500));
            createLabel = createButton.GetComponentInChildren<Text>();

            joinField = CttUi.Field(card, "JoinCode", "FQJFD", "",
                new Vector2(0.5f, 1f), new Vector2(560, 60), new Vector2(-170, -590));
            joinButton = CttUi.Button(card, "Join", CTTConfig.Tr("join", ca), CttUi.Accent2,
                () => { ApplyServer(); JoinRequested?.Invoke(joinField.text.Trim().ToUpperInvariant()); },
                new Vector2(0.5f, 1f), new Vector2(300, 60), new Vector2(320, -590), 26);

            CttUi.Label(At(card, "Rules", 960, 150, 700), "Text", CTTConfig.Tr("rules", ca), 22, CttUi.Muted);
            homeStatus = CttUi.Label(At(card, "Status", 960, 40, 830), "Text", "", 24, CttUi.Danger);
        }

        private void BuildLobby()
        {

            lobbyPanel = Card("LobbyPanel");
            RectTransform card = CardInner(lobbyPanel, 1120, 900);
            bool ca = game != null && game.Catalan;

            lobbyCodeCaption = CttUi.Label(At(card, "Title", 1000, 60, 40), "Text",
                CTTConfig.Tr("roomCode", ca), 36, CttUi.Accent);
            lobbyCode = CttUi.Label(At(card, "Code", 1000, 84, 108), "Text", "", 60, Color.white);
            lobbyPlayers = CttUi.Label(At(card, "Players", 1000, 40, 172), "Text", "", 26, CttUi.Muted);
            lobbyNames = CttUi.Label(At(card, "PlayerNames", 1000, 40, 208), "Text", "", 20, CttUi.Accent);
            lobbyHint = CttUi.Label(At(card, "LobbyHint", 1000, 28, 250), "Text", "", 20, CttUi.Danger);
            familyCaption = CttUi.Label(At(card, "FamiliesCaption", 1000, 40, 276), "Text",
                CTTConfig.Tr("chooseFamilies", ca), 24, CttUi.Muted);
            familyHint = CttUi.Label(At(card, "FamiliesHint", 1000, 26, 312), "Text",
                CTTConfig.Tr("familyHint", ca), 16, CttUi.Muted);
            BuildFamilyCards(card);

            CttUi.Label(At(card, "RolesCaption", 1000, 40, 470), "Text",
                CTTConfig.Tr("chooseRole", ca), 22, CttUi.Muted);

            // Each role is a card with one big coloured square as the character.
            for (int i = 0; i < CTTConfig.RoleOrder.Length; i++)
            {
                string role = CTTConfig.RoleOrder[i];
                float x = (i % 3 - 1) * 360f;
                float y = 520f + (i / 3) * 110f;
                Button button = CttUi.Button(card, "Role_" + role, "", CttUi.Panel,
                    () => RoleRequested?.Invoke(role),
                    new Vector2(0.5f, 1f), new Vector2(340, 100), new Vector2(x, -y), 20);
                roleButtons[i] = button;

                CttUi.Icon(button.transform, "Character",
                    CTTConfig.ColorOf(role, game != null && game.HighContrast), new Vector2(62, 62),
                    new Vector2(0f, 0.5f), new Vector2(52, 6));
                CttUi.InsetCaption(button, 92f);
            }

            startButton = CttUi.Button(card, "Start", CTTConfig.Tr("start", ca), CttUi.Accent,
                () => StartRequested?.Invoke(),
                new Vector2(0.5f, 1f), new Vector2(700, 66), new Vector2(0, -760), 28);
            CttUi.Button(card, "Leave", CTTConfig.Tr("leave", ca), CttUi.Panel,
                () => LeaveRequested?.Invoke(),
                new Vector2(0.5f, 1f), new Vector2(300, 44), new Vector2(0, -838), 20);
        }

        /// <summary>Four family cards, each showing the troublemaker + father + mother sprites.</summary>
        private void BuildFamilyCards(RectTransform card)
        {
            familyButtons.Clear();
            familyIds.Clear();
            familySlotLabels.Clear();

            CTTCharacterCatalog catalog = CTTCharacterCatalog.Current;
            CTTCharacterFamily[] families = catalog.families;
            if (families == null || families.Length == 0)
            {
                families = new CTTCharacterFamily[CTTCharacterCatalog.FallbackIds.Length];
                for (int i = 0; i < families.Length; i++)
                    families[i] = new CTTCharacterFamily
                    {
                        id = CTTCharacterCatalog.FallbackIds[i],
                        displayName = CTTCharacterCatalog.DisplayNameFor(CTTCharacterCatalog.FallbackIds[i]),
                    };
            }

            for (int i = 0; i < families.Length; i++)
            {
                CTTCharacterFamily family = families[i];
                if (family == null || string.IsNullOrEmpty(family.id)) continue;
                string id = family.id;
                float x = (i - (families.Length - 1) * 0.5f) * 270f;

                Button button = CttUi.Button(card, "Family_" + id, "", CttUi.Panel,
                    () => FamilyRequested?.Invoke(id),
                    new Vector2(0.5f, 1f), new Vector2(250, 110), new Vector2(x, -350), 18);
                familyButtons.Add(button);
                familyIds.Add(id);

                // Prefer the sprite loaded from the imported PNG in Resources (a copy
                // of the source folder); the catalog field is a fallback for assets
                // generated by the editor tool.
                CttUi.SpriteImage(button.transform, "Troublemaker",
                    CTTCharacterCatalog.SpriteFor(id, "Troublemaker") ?? family.troublemaker,
                    new Vector2(46, 46), new Vector2(0.5f, 0.5f), new Vector2(-62, 12));
                CttUi.SpriteImage(button.transform, "Father",
                    CTTCharacterCatalog.SpriteFor(id, "Father") ?? family.father,
                    new Vector2(46, 46), new Vector2(0.5f, 0.5f), new Vector2(0, 12));
                CttUi.SpriteImage(button.transform, "Mother",
                    CTTCharacterCatalog.SpriteFor(id, "Mother") ?? family.mother,
                    new Vector2(46, 46), new Vector2(0.5f, 0.5f), new Vector2(62, 12));

                CttUi.LabelAt(button.transform, "Name", family.displayName, 17, Color.white,
                    new Vector2(0.5f, 0.5f), new Vector2(230, 24), new Vector2(0, -34));
                familySlotLabels.Add(CttUi.LabelAt(button.transform, "Slot", "", 16, CttUi.Accent,
                    new Vector2(0.5f, 0.5f), new Vector2(40, 24), new Vector2(102, 38)));
            }
        }

        private void BuildHud()
        {

            hudPanel = CttUi.PanelAt("HudPanel", canvas.transform, new Color(0f, 0f, 0f, 0f));
            hudPanel.GetComponent<Image>().raycastTarget = false;

            RectTransform top = CttUi.Rect("TopBar", hudPanel);
            CttUi.Place(top, new Vector2(0.5f, 1f), new Vector2(1800, 120), new Vector2(0, -90));
            Image topBackground = top.gameObject.AddComponent<Image>();
            topBackground.color = new Color(0.04f, 0.06f, 0.16f, 0.82f);
            topBackground.raycastTarget = false;

            bool ca = game != null && game.Catalan;

            // Left: the local character is shown as its coloured square.
            hudRoleIcon = CttUi.Icon(top, "RoleIcon", Color.white, new Vector2(56, 56),
                new Vector2(0f, 0.5f), new Vector2(70, 0));
            hudRole = CttUi.LabelAt(top, "Role", "", 28, CttUi.Accent,
                new Vector2(0f, 0.5f), new Vector2(440, 90), new Vector2(350, 0), TextAnchor.MiddleLeft);
            hudLives = CttUi.LabelAt(top, "Lives", "", 26, Color.white,
                new Vector2(0f, 0.5f), new Vector2(220, 90), new Vector2(690, 0));

            // Centre: timer.
            hudTimer = CttUi.LabelAt(top, "Timer", "", 40, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(180, 90), new Vector2(-60, 0));

            // Right: family scores, each tagged with its troublemaker's colour square.
            CttUi.Icon(top, "ScoreAIcon", CTTConfig.ColorOf("fatherA"), new Vector2(36, 36),
                new Vector2(1f, 0.5f), new Vector2(-670, 0));
            hudScoreA = CttUi.LabelAt(top, "ScoreA", "", 25, CttUi.Accent2,
                new Vector2(1f, 0.5f), new Vector2(300, 90), new Vector2(-490, 0), TextAnchor.MiddleLeft);
            CttUi.Icon(top, "ScoreBIcon", CTTConfig.ColorOf("fatherB"), new Vector2(36, 36),
                new Vector2(1f, 0.5f), new Vector2(-310, 0));
            hudScoreB = CttUi.LabelAt(top, "ScoreB", "", 25, CttUi.Accent,
                new Vector2(1f, 0.5f), new Vector2(260, 90), new Vector2(-130, 0), TextAnchor.MiddleLeft);

            hudSprint = CttUi.LabelAt(hudPanel, "Sprint", "", 24, CttUi.Muted,
                new Vector2(1f, 1f), new Vector2(320, 50), new Vector2(-170, -190));

            CttUi.LabelAt(hudPanel, "Hint", CTTConfig.Tr("moveHint", ca), 21, CttUi.Muted,
                new Vector2(0.5f, 0f), new Vector2(1400, 40), new Vector2(0, 36));

            BuildCharacterStrip();
            BuildTouchPad();
        }

        /// <summary>A row of six coloured squares showing every character at a glance.</summary>
        private void BuildCharacterStrip()
        {
            RectTransform strip = CttUi.Rect("CharacterStrip", hudPanel);
            CttUi.Place(strip, new Vector2(0.5f, 0f), new Vector2(760, 100), new Vector2(0, 118));
            Image stripBackground = strip.gameObject.AddComponent<Image>();
            stripBackground.color = new Color(0.04f, 0.06f, 0.16f, 0.8f);
            stripBackground.raycastTarget = false;

            for (int i = 0; i < CTTConfig.RoleOrder.Length; i++)
            {
                string role = CTTConfig.RoleOrder[i];
                float x = (i - 2.5f) * 122f;
                rosterIcons[i] = CttUi.Icon(strip, "Icon_" + role,
                    CTTConfig.ColorOf(role, game != null && game.HighContrast),
                    new Vector2(48, 48), new Vector2(0.5f, 0.5f), new Vector2(x, 12));
                rosterLabels[i] = CttUi.LabelAt(strip, "Code_" + role, CTTConfig.ShortOf(role), 20,
                    CttUi.Muted, new Vector2(0.5f, 0.5f), new Vector2(100, 26), new Vector2(x, -28));
            }
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
            EnsureBuilt();
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
            if (toast == null) return;
            toast.text = message ?? "";
            if (homeStatus != null && phase == GamePhase.Home) homeStatus.text = message ?? "";
        }

        public void RenderLobby(CTTLobby lobby, string playerId)
        {
            if (lobby == null) return;
            EnsureBuilt();
            bool ca = game != null && game.Catalan;
            bool solo = P2pMode && P2pSolo;
            bool multiplayer = P2pMode && !P2pSolo;
            if (lobbyCodeCaption != null)
                lobbyCodeCaption.text = CTTConfig.Tr(solo ? "soloMode" : "roomCode", ca);
            lobbyCode.text = solo ? CTTConfig.Tr("youPlusBots", ca) : lobby.code;
            int humans = lobby.players != null ? lobby.players.Length : 0;
            lobbyPlayers.text = CTTConfig.Tr("players", ca) + ": " + humans + " / 6";

            // Live username list: grows/shrinks with the roster messages, so the
            // master sees a new name the moment the client finishes the handshake.
            var names = new System.Collections.Generic.List<string>();
            foreach (CTTLobbyPlayer p in lobby.players ?? System.Array.Empty<CTTLobbyPlayer>())
            {
                string label = string.IsNullOrEmpty(p.name) ? p.id : p.name;
                if (p.id == playerId) label += " (" + CTTConfig.Tr("you", ca) + ")";
                if (p.id == lobby.hostId) label += " [" + CTTConfig.Tr("host", ca) + "]";
                names.Add(label);
            }
            if (lobbyNames != null) lobbyNames.text = string.Join("   •   ", names);

            bool enough = !multiplayer || humans >= CTTConfig.P2pMinRealPlayers;
            bool rolesChosen = lobby.players != null && lobby.players.Length > 0
                && System.Array.TrueForAll(lobby.players, p => !string.IsNullOrEmpty(p.role));
            bool isHost = playerId == lobby.hostId;
            string[] chosenFamilies = lobby.families ?? Array.Empty<string>();
            int chosenCount = 0;
            foreach (string id in chosenFamilies) if (!string.IsNullOrEmpty(id)) chosenCount++;
            bool familiesChosen = chosenCount == 2;
            if (lobbyHint != null)
            {
                lobbyHint.text = multiplayer && !enough ? CTTConfig.Tr("needTwo", ca)
                    : !familiesChosen ? CTTConfig.Tr("needFamilies", ca)
                    : !rolesChosen ? CTTConfig.Tr("needRoles", ca)
                    : multiplayer && !isHost ? CTTConfig.Tr("waitHost", ca)
                    : "";
            }
            // Only the room creator may start, and only after every connected
            // player has explicitly picked a character and two families are set.
            if (startButton != null)
                startButton.interactable = (solo || isHost) && enough && rolesChosen && familiesChosen;
            RenderFamilyCards(chosenFamilies, solo || isHost);

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

        /// <summary>Marks the two chosen family cards and locks editing for guests.</summary>
        private void RenderFamilyCards(string[] chosen, bool canEdit)
        {
            int chosenCount = 0;
            foreach (string id in chosen) if (!string.IsNullOrEmpty(id)) chosenCount++;

            for (int i = 0; i < familyButtons.Count && i < familyIds.Count; i++)
            {
                Button button = familyButtons[i];
                if (button == null) continue;
                int slot = Array.IndexOf(chosen, familyIds[i]);
                if (familySlotLabels != null && i < familySlotLabels.Count && familySlotLabels[i] != null)
                    familySlotLabels[i].text = slot == 0 ? "A" : slot == 1 ? "B" : "";
                // A chosen card stays clickable so the host can remove it; once two
                // families are picked, the remaining cards are locked.
                button.interactable = canEdit && (slot >= 0 || chosenCount < 2);
                if (button.image != null)
                    button.image.color = slot >= 0
                        ? Color.Lerp(CttUi.Panel, CttUi.Accent, 0.35f)
                        : CttUi.Panel;
            }
        }

        public void UpdateHud(CTTState snapshot, string playerId)
        {

            if (snapshot == null) return;
            EnsureBuilt();
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
            UpdateRoster(snapshot, playerId);

            CTTPlayer me = null;
            foreach (CTTPlayer p in snapshot.players ?? Array.Empty<CTTPlayer>())
                if (p.id == playerId) { me = p; break; }

            if (me == null)
            {
                hudRole.text = CTTConfig.Tr("spectator", ca);
                hudRoleIcon.color = CttUi.Muted;
                hudLives.text = "";
                hudSprint.text = "";
                return;
            }

            hudRoleIcon.color = CTTConfig.ColorOf(me.role, game != null && game.HighContrast);
            hudRole.text = (ca ? "ETS  " : "YOU ARE  ") + CTTConfig.RoleName(me.role, ca);
            bool kids = me.team == "troublemakers";
            hudLives.text = kids
                ? (ca ? "VIDES  " : "LIVES  ") + me.lives
                : (ca ? "Pares" : "Parents");
            hudSprint.text = me.sprintIn > 0.01f
                ? (ca ? "Esprint " : "Sprint ") + me.sprintIn.ToString("0.0") + "s"
                : CTTConfig.Tr("ready", ca);
        }

        /// <summary>Keeps the six coloured character squares in sync with the snapshot.</summary>
        private void UpdateRoster(CTTState snapshot, string playerId)
        {
            bool contrast = game != null && game.HighContrast;
            foreach (CTTPlayer p in snapshot.players ?? Array.Empty<CTTPlayer>())
            {
                int index = Array.FindIndex(CTTConfig.RoleOrder, r => r == p.role);
                if (index < 0 || index >= rosterIcons.Length || rosterIcons[index] == null) continue;

                Color color = CTTConfig.ColorOf(p.role, contrast);
                if (p.eliminated) color = Color.Lerp(color, Color.black, 0.55f);
                rosterIcons[index].color = color;
                rosterIcons[index].transform.localScale = p.id == playerId
                    ? Vector3.one * 1.18f : Vector3.one;
                rosterLabels[index].color = p.id == playerId ? CttUi.Accent : CttUi.Muted;
                rosterLabels[index].text = CTTConfig.ShortOf(p.role) +
                    (p.isBot ? " ·" : "") + (p.id == playerId ? " ★" : "");
            }
        }

        public void ShowResult(CTTResult result)
        {
            
            EnsureBuilt();
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
            if (familyCaption != null) familyCaption.text = CTTConfig.Tr("chooseFamilies", catalan);
            if (familyHint != null) familyHint.text = CTTConfig.Tr("familyHint", catalan);
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
