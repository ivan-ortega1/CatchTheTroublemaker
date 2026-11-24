using System;
using UnityEngine;

namespace CatchTheTroublemaker
{
    public enum GamePhase { Home, Lobby, Playing, Result }

    /// <summary>
    /// Central scene controller. It owns the match session and coordinates the
    /// network, UI, world and input managers. The Node server still decides every
    /// rule (coins, catches, timer, winner); this class only drives the client.
    /// Add it to a GameObject in the scene — no runtime bootstrap is involved.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene managers (wired in the Inspector)")]
        public NetworkManager network;
        public UIManager ui;
        public BoardRenderer board;
        public PlayerController playerInput;

        private const string CodeKey = "ctt_code";
        private const string IdKey = "ctt_playerId";
        private const string LangKey = "ctt_catalan";
        private const string ContrastKey = "ctt_contrast";

        public AudioSource matchMusic;

        public GamePhase Phase { get; private set; }
        public string RoomCode { get; private set; }
        public string PlayerId { get; private set; }
        public bool Catalan { get; private set; }
        public bool HighContrast { get; private set; }
        public CTTLobby Lobby { get; private set; }
        public CTTState State { get; private set; }
        public CTTResult Result { get; private set; }

        private CttLanDiscovery discovery;
        private P2PGame p2p;
        private float nextInputAt;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // The scene builder wires these. Manual setups may place managers as
            // siblings instead of children, so search the whole scene as a fallback.
            if (network == null) network = FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (ui == null) ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (board == null) board = FindFirstObjectByType<BoardRenderer>(FindObjectsInactive.Include);
            if (playerInput == null) playerInput = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

            if (network == null || ui == null || board == null || playerInput == null)
            {
                Debug.LogError("[CTT] GameManager is missing one or more manager references " +
                               "(network, ui, board, playerInput). Check the scene Inspector.");
                enabled = false;
                return;
            }

            Catalan = PlayerPrefs.GetInt(LangKey, 0) == 1;
            HighContrast = PlayerPrefs.GetInt(ContrastKey, 0) == 1;

            network.LobbyReceived += OnLobbyReceived;
            network.StateReceived += OnStateReceived;
            network.ResultReceived += OnResultReceived;
            network.ErrorReceived += error => ui.Toast(error);

            ui.CreateRequested += CreateRoom;
            ui.JoinRequested += JoinRoom;
            ui.RoleRequested += SelectRole;
            ui.StartRequested += StartMatch;
            ui.RematchRequested += Rematch;
            ui.LeaveRequested += LeaveRoom;
            ui.LanguageToggled += ToggleLanguage;
            ui.ContrastToggled += ToggleContrast;
            ui.ConnectionTestRequested += TestConnection;
            ui.DiscoveryRequested += () => FindLanServer(false);
            ui.P2pModeToggled += ToggleP2pMode;

            discovery = GetComponent<CttLanDiscovery>();
            if (discovery == null) discovery = gameObject.AddComponent<CttLanDiscovery>();

            // Hostless mode runs the exact same engine locally on every peer.
            p2p = GetComponent<P2PGame>();
            if (p2p == null) p2p = gameObject.AddComponent<P2PGame>();
            p2p.LobbyChanged += OnP2pLobby;
            p2p.StateChanged += OnStateReceived;
            p2p.ResultChanged += OnResultReceived;
            p2p.Error += error => ui.Toast(error);

            ui.Initialize(this);
            board.Initialize(HighContrast);
            playerInput.Initialize(ui);
        }

        private void Start()
        {
            SetPhase(GamePhase.Home);
            board.Clear();

            // A second machine usually still has the default localhost URL, which
            // points at itself. If that URL is not reachable, find the host on the LAN.
            string savedUrl = CTTApi.ServerUrl;
            if (savedUrl.Contains("localhost") || savedUrl.Contains("127.0.0.1"))
                network.TestConnection(_ => { }, _ => FindLanServer(true));

            string savedCode = PlayerPrefs.GetString(CodeKey, "");
            string savedId = PlayerPrefs.GetString(IdKey, "");
            if (savedCode.Length == 5 && !string.IsNullOrEmpty(savedId))
            {
                RoomCode = savedCode;
                PlayerId = savedId;
                ui.Toast(CTTConfig.Tr("reconnecting", Catalan));
                JoinRoomInternal(savedId, true);
            }
        }

        private void OnDestroy()
        {
            if (network != null) network.Stop();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Phase != GamePhase.Playing || playerInput == null) return;

            // In P2P mode every peer feeds its own input straight into the local
            // lockstep simulation; no HTTP request is involved.
            if (ui.P2pMode)
            {
                if (p2p != null && p2p.IsActive)
                {
                    Vector3 localIntent = playerInput.ReadIntent();
                    p2p.SetLocalInput((int)localIntent.x, (int)localIntent.y, localIntent.z > 0.5f);
                }
                return;
            }

            if (network == null || Time.unscaledTime < nextInputAt) return;

            nextInputAt = Time.unscaledTime + playerInput.SendInterval;
            Vector3 intent = playerInput.ReadIntent();
            network.SendInput(RoomCode, PlayerId, (int)intent.x, (int)intent.y, intent.z > 0.5f);
        }

        /* ----------------------------- phase ----------------------------- */

        private void SetPhase(GamePhase phase)
        {
            Phase = phase;
            ui.ShowPhase(phase);
            if (phase != GamePhase.Playing) board.SetFollowTarget(null);
        }

        private void EnterGame()
        {
            if (ui.P2pMode && p2p != null && p2p.IsActive)
            {
                RoomCode = p2p.RoomCode;
                PlayerId = p2p.PlayerId;
                SetPhase(GamePhase.Playing);
                board.SetFollowTarget(PlayerId);
                nextInputAt = 0f;
                return;
            }

            network.SetRoom(RoomCode, PlayerId);
            SetPhase(GamePhase.Playing);
            board.SetFollowTarget(PlayerId);
            nextInputAt = 0f;
            network.RefreshState();
        }

        /* ----------------------------- commands ----------------------------- */

        private void CreateRoom(string rawName)
        {
            string name = Sanitize(rawName);
            PlayerPrefs.SetString(CTTConfig.NamePrefKey, name);
            PlayerPrefs.Save();

            if (ui.P2pMode)
            {
                SetPhase(GamePhase.Lobby);
                p2p.Create(name);
                RoomCode = p2p.RoomCode;
                PlayerId = p2p.PlayerId;
                return;
            }

            network.CreateRoom(name, response =>
            {
                RoomCode = response.code;
                PlayerId = response.playerId;
                Lobby = response.lobby;
                SaveSession();
                network.SetRoom(RoomCode, PlayerId);
                SetPhase(GamePhase.Lobby);
                ui.RenderLobby(Lobby, PlayerId);
            }, Fail);
        }

        private void JoinRoom(string roomCode) => JoinRoomInternal(roomCode, false);

        private void JoinRoomInternal(string roomCode, bool reconnect)
        {
            string code = (roomCode ?? "").Trim().ToUpperInvariant();
            if (code.Length != 5)
            {
                if (!reconnect) ui.Toast(CTTConfig.Tr("invalidCode", Catalan));
                return;
            }

            string name = Sanitize(ui.PlayerName);

            if (ui.P2pMode)
            {
                SetPhase(GamePhase.Lobby);
                p2p.Join(code, name);
                if (!p2p.IsActive)
                {
                    SetPhase(GamePhase.Home);
                    return;
                }
                RoomCode = p2p.RoomCode;
                PlayerId = p2p.PlayerId;
                return;
            }

            network.Join(code, name, reconnect ? PlayerId : null, response =>
            {
                RoomCode = response.code;
                PlayerId = response.playerId;
                Lobby = response.lobby;
                SaveSession();
                network.SetRoom(RoomCode, PlayerId);
                if (Lobby != null && Lobby.status == "playing") EnterGame();
                else
                {
                    SetPhase(GamePhase.Lobby);
                    ui.RenderLobby(Lobby, PlayerId);
                }
            }, error =>
            {
                if (reconnect)
                {
                    ClearSession();
                    RoomCode = null;
                    PlayerId = null;
                    ui.Toast("");
                    SetPhase(GamePhase.Home);
                }
                else Fail(error);
            });
        }

        private void SelectRole(string role)
        {
            if (ui.P2pMode && p2p != null && p2p.IsActive)
            {
                p2p.SetRole(role);
                return;
            }
            network.SetRole(RoomCode, PlayerId, role, response =>
            {
                Lobby = response.lobby;
                ui.RenderLobby(Lobby, PlayerId);
            }, Fail);
        }

        private void StartMatch()
        {
            matchMusic.Play();
            if (ui.P2pMode && p2p != null && p2p.IsActive) { p2p.StartMatch(); return; }
            RequestMatch("/start");
        }

        private void Rematch()
        {
            if (ui.P2pMode && p2p != null && p2p.IsActive) { p2p.Rematch(); return; }
            RequestMatch("/restart");
        }

        private void RequestMatch(string action)
        {
            network.PostMatchAction(RoomCode, PlayerId, action, response =>
            {
                if (response != null && response.ok) EnterGame();
                else Fail(response == null ? null : response.error);
            }, Fail);
        }

        private void LeaveRoom()
        {
            if (p2p != null && p2p.IsActive) p2p.Leave();
            network.Stop();
            ClearSession();
            RoomCode = null;
            PlayerId = null;
            Lobby = null;
            State = null;
            Result = null;
            board.Clear();
            SetPhase(GamePhase.Home);
        }

        /* ----------------------------- network callbacks ----------------------------- */

        private void OnLobbyReceived(CTTLobby lobby)
        {
            Lobby = lobby;
            if (Phase == GamePhase.Lobby) ui.RenderLobby(lobby, PlayerId);
        }

        private void OnP2pLobby(CTTLobby lobby)
        {
            Lobby = lobby;
            if (Phase == GamePhase.Lobby)
                ui.RenderLobby(lobby, p2p == null ? PlayerId : p2p.PlayerId);
        }

        private void OnStateReceived(CTTState snapshot)
        {
            if (snapshot == null) return;
            State = snapshot;
            if (snapshot.lobby != null) Lobby = snapshot.lobby;

            if (snapshot.status == "playing")
            {
                if (Phase != GamePhase.Playing) EnterGame();
                board.ApplyState(snapshot);
                ui.UpdateHud(snapshot, PlayerId);
            }
            else if (snapshot.status == "finished" && snapshot.result != null)
            {
                OnResultReceived(snapshot.result);
            }
            else if (Phase == GamePhase.Lobby)
            {
                ui.RenderLobby(Lobby, PlayerId);
            }
        }

        private void OnResultReceived(CTTResult result)
        {
            Result = result;
            SetPhase(GamePhase.Result);
            ui.ShowResult(result);
            matchMusic.Stop();
        }

        private void Fail(string error)
        {
            ui.Toast(string.IsNullOrEmpty(error)
                ? CTTConfig.Tr("connectionError", Catalan)
                : error);
        }

        /* ----------------------------- preferences ----------------------------- */

        private void ToggleP2pMode()
        {
            if (Phase != GamePhase.Home)
            {
                ui.Toast(CTTConfig.Tr("backHome", Catalan));
                return;
            }
            if (p2p != null && p2p.IsActive) p2p.Leave();
            ui.SetP2pMode(!ui.P2pMode);
        }

        private void TestConnection()
        {
            ui.Toast(CTTConfig.Tr("testing", Catalan));
            network.TestConnection(
                _ => ui.Toast(CTTConfig.Tr("connectionOk", Catalan)),
                Fail);
        }

        private void FindLanServer(bool automatic)
        {
            if (discovery == null) return;
            if (!automatic) ui.Toast(CTTConfig.Tr("searching", Catalan));
            discovery.Find(url =>
            {
                ui.SetServerUrl(url);
                string label = automatic ? CTTConfig.Tr("autoFound", Catalan) : CTTConfig.Tr("found", Catalan);
                ui.Toast(label + " " + url);
            }, error =>
            {
                if (!automatic) ui.Toast(error);
            });
        }

        private void ToggleLanguage()
        {
            Catalan = !Catalan;
            PlayerPrefs.SetInt(LangKey, Catalan ? 1 : 0);
            PlayerPrefs.Save();
            ui.Refresh(Catalan, HighContrast, State, Lobby, Result, PlayerId, Phase);
        }

        private void ToggleContrast()
        {
            HighContrast = !HighContrast;
            PlayerPrefs.SetInt(ContrastKey, HighContrast ? 1 : 0);
            PlayerPrefs.Save();
            board.SetHighContrast(HighContrast);
            ui.Refresh(Catalan, HighContrast, State, Lobby, Result, PlayerId, Phase);
        }

        private static string Sanitize(string value)
        {
            string result = (value ?? "").Trim();
            if (result.Length == 0) result = "Player";
            return result.Substring(0, Mathf.Min(24, result.Length));
        }

        private void SaveSession()
        {
            PlayerPrefs.SetString(CodeKey, RoomCode);
            PlayerPrefs.SetString(IdKey, PlayerId);
            PlayerPrefs.Save();
        }

        private static void ClearSession()
        {
            PlayerPrefs.DeleteKey(CodeKey);
            PlayerPrefs.DeleteKey(IdKey);
            PlayerPrefs.Save();
        }
    }
}
