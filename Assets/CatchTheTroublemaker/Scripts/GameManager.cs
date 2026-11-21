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

        public GamePhase Phase { get; private set; }
        public string RoomCode { get; private set; }
        public string PlayerId { get; private set; }
        public bool Catalan { get; private set; }
        public bool HighContrast { get; private set; }
        public CTTLobby Lobby { get; private set; }
        public CTTState State { get; private set; }
        public CTTResult Result { get; private set; }

        private float nextInputAt;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // The scene builder wires these; the fallbacks keep manual scene setup friendly.
            if (network == null) network = GetComponentInChildren<NetworkManager>(true);
            if (ui == null) ui = GetComponentInChildren<UIManager>(true);
            if (board == null) board = GetComponentInChildren<BoardRenderer>(true);
            if (playerInput == null) playerInput = GetComponentInChildren<PlayerController>(true);

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

            ui.Initialize(this);
            board.Initialize(HighContrast);
            playerInput.Initialize(ui);
        }

        private void Start()
        {
            SetPhase(GamePhase.Home);
            board.Clear();

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
            if (Phase != GamePhase.Playing || network == null || playerInput == null) return;
            if (Time.unscaledTime < nextInputAt) return;

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
            network.SetRole(RoomCode, PlayerId, role, response =>
            {
                Lobby = response.lobby;
                ui.RenderLobby(Lobby, PlayerId);
            }, Fail);
        }

        private void StartMatch() => RequestMatch("/start");

        private void Rematch() => RequestMatch("/restart");

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
        }

        private void Fail(string error)
        {
            ui.Toast(string.IsNullOrEmpty(error)
                ? CTTConfig.Tr("connectionError", Catalan)
                : error);
        }

        /* ----------------------------- preferences ----------------------------- */

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
