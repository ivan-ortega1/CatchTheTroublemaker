using System;
using System.Collections.Generic;
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
        public CTTCharacterCatalog characterCatalog;

        private const string CodeKey = "ctt_code";
        private const string IdKey = "ctt_playerId";
        private const string LangKey = "ctt_catalan";
        private const string ContrastKey = "ctt_contrast";

        [Header("Music (wired in the Inspector)")]
        public AudioSource menuMusic;
        public AudioSource matchMusic;

        [Header("SFX clips (wired in the Inspector)")]
        public AudioClip hoverClip;
        public AudioClip pressClip;
        public AudioClip coinClip;

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
        private string lastDialogueKey;

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

            // Fallback for hand-built scenes: locate the two music sources by name.
            foreach (AudioSource source in FindObjectsByType<AudioSource>())
            {
                if (source.name == "MenuMusic") menuMusic = source;
                else if (source.name == "MatchMusic") matchMusic = source;
            }

            // SFX is created at runtime; GameManager owns the clip references.
            if (GetComponent<CttSfx>() == null)
            {
                CttSfx sfx = gameObject.AddComponent<CttSfx>();
                sfx.Configure(hoverClip, pressClip, coinClip);
            }

            if (network == null || ui == null || board == null || playerInput == null)
            {
                Debug.LogError("[CTT] GameManager is missing one or more manager references " +
                               "(network, ui, board, playerInput). Check the scene Inspector.");
                enabled = false;
                return;
            }

            Catalan = PlayerPrefs.GetInt(LangKey, 0) == 1;
            HighContrast = PlayerPrefs.GetInt(ContrastKey, 0) == 1;
            if (characterCatalog == null) characterCatalog = CTTCharacterCatalog.Current;

            network.LobbyReceived += OnLobbyReceived;
            network.StateReceived += OnStateReceived;
            network.ResultReceived += OnResultReceived;
            network.ErrorReceived += error => ui.Toast(error);

            ui.CreateRequested += CreateRoom;
            ui.JoinRequested += JoinRoom;
            ui.RoleRequested += SelectRole;
            ui.FamilyRequested += ToggleFamily;
            ui.StartRequested += StartMatch;
            ui.RematchRequested += Rematch;
            ui.LeaveRequested += LeaveRoom;
            ui.LanguageToggled += ToggleLanguage;
            ui.ContrastToggled += ToggleContrast;
            ui.ConnectionTestRequested += TestConnection;
            ui.DiscoveryRequested += () => FindLanServer(false);
            ui.P2pModeToggled += ToggleP2pMode;
            ui.P2pKindToggled += ToggleP2pKind;

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

            // A grounded troublemaker no longer drives a character. BoardRenderer
            // handles the free camera input instead. In P2P the local input is also
            // broadcast every tick, so explicitly neutralise it (otherwise the last
            // held direction would keep being sent after grounding).
            if (IsLocalSpectator(State))
            {
                if (ui.P2pMode && p2p != null && p2p.IsActive) p2p.SetLocalInput(0, 0, false);
                return;
            }

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
            board.SetSpectator(false);
            if (phase != GamePhase.Playing) board.SetFollowTarget(null);
            if (phase != GamePhase.Result)
            {
                CttSfx.Instance?.StopDialogue();
                lastDialogueKey = "";
            }
            UpdateMusic(phase);
        }

        /// <summary>Menu/lobby theme in Home+Lobby, match theme while playing,
        /// silence on the result screen.</summary>
        private void UpdateMusic(GamePhase phase)
        {
            if (menuMusic != null)
            {
                if (phase == GamePhase.Home || phase == GamePhase.Lobby)
                {
                    if (!menuMusic.isPlaying) menuMusic.Play();
                }
                else menuMusic.Stop();
            }
            if (matchMusic != null)
            {
                if (phase == GamePhase.Playing)
                {
                    if (!matchMusic.isPlaying) matchMusic.Play();
                }
                else matchMusic.Stop();
            }
        }

        private void EnterGame()
        {
            if (ui.P2pMode && p2p != null && p2p.IsActive)
            {
                RoomCode = p2p.RoomCode;
                PlayerId = p2p.PlayerId;
                SetPhase(GamePhase.Playing);
                board.SetSpectator(false);
                board.SetFollowTarget(PlayerId);
                nextInputAt = 0f;
                return;
            }

            network.SetRoom(RoomCode, PlayerId);
            SetPhase(GamePhase.Playing);
            board.SetSpectator(false);
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
                if (ui.P2pSolo) p2p.CreateSolo(name);
                else p2p.Create(name);
                RoomCode = p2p.RoomCode;
                PlayerId = p2p.PlayerId;
                return;
            }

            network.CreateRoom(name, response =>
            {
                RoomCode = response.code;
                PlayerId = response.playerId;
                ApplyLobby(response.lobby);
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
                ApplyLobby(response.lobby);
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

        /// <summary>Host/master toggles one family card. A selected card can always be
        /// removed; a third family cannot be added until one of the two is removed.</summary>
        private void ToggleFamily(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (ui.P2pMode && p2p != null && p2p.IsActive)
            {
                p2p.SetFamilies(NextFamilies(id, p2p.Families));
                return;
            }

            if (Lobby == null || Lobby.hostId != PlayerId)
            {
                ui.Toast(CTTConfig.Tr("onlyHost", Catalan));
                return;
            }
            network.SetFamilies(RoomCode, PlayerId, NextFamilies(id, Lobby.families),
                response =>
                {
                    ApplyLobby(response.lobby);
                    ui.RenderLobby(Lobby, PlayerId);
                }, Fail);
        }

        private static string[] NextFamilies(string id, string[] current)
        {
            var result = new List<string>();
            bool wasSelected = false;
            foreach (string family in current ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(family)) continue;
                if (family == id) { wasSelected = true; continue; }
                result.Add(family);
            }
            if (!wasSelected && result.Count < 2) result.Add(id);
            return result.ToArray();
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

        /// <summary>Keeps the world art in sync with the family pair picked in the lobby.</summary>
        private void ApplyLobby(CTTLobby lobby)
        {
            Lobby = lobby;
            if (board != null && lobby != null) board.SetFamilies(lobby.families);
        }

        private void OnLobbyReceived(CTTLobby lobby)
        {
            ApplyLobby(lobby);
            if (Phase == GamePhase.Lobby) ui.RenderLobby(lobby, PlayerId);
        }

        private void OnP2pLobby(CTTLobby lobby)
        {
            ApplyLobby(lobby);
            if (Phase == GamePhase.Lobby)
                ui.RenderLobby(lobby, p2p == null ? PlayerId : p2p.PlayerId);
        }

        private void OnStateReceived(CTTState snapshot)
        {
            if (snapshot == null) return;
            State = snapshot;
            if (snapshot.lobby != null) ApplyLobby(snapshot.lobby);

            if (snapshot.events != null)
                foreach (CTTEvent evt in snapshot.events)
                    if (IsMyCoinEvent(evt)) CttSfx.Instance?.PlayCoin();

            if (snapshot.status == "playing")
            {
                if (Phase != GamePhase.Playing) EnterGame();
                bool spectating = IsLocalSpectator(snapshot);
                board.SetSpectator(spectating);
                if (!spectating) board.SetFollowTarget(PlayerId);
                board.ApplyState(snapshot);
                ui.UpdateHud(snapshot, PlayerId, spectating);
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

        /// <summary>
        /// A troublemaker who used all three lives keeps their seat in every snapshot,
        /// but switches to a free spectator camera. Missing players get the same view.
        /// </summary>
        private bool IsLocalSpectator(CTTState snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(PlayerId)) return false;
            CTTPlayer me = null;
            foreach (CTTPlayer p in snapshot.players ?? System.Array.Empty<CTTPlayer>())
                if (p.id == PlayerId) { me = p; break; }
            return me == null || (me.team == "troublemakers" && me.eliminated);
        }

        /// <summary>
        /// Coin events are broadcast for every player; play the sound only for the
        /// local player. This covers server mode, P2P Solo ("local-solo") and P2P
        /// Multi (the transport peer id), because all three set PlayerId before play.
        /// </summary>
        private bool IsMyCoinEvent(CTTEvent evt)
        {
            return evt != null && evt.type == "coin"
                && !string.IsNullOrEmpty(PlayerId)
                && string.Equals(evt.id, PlayerId, StringComparison.Ordinal);
        }

        private void OnResultReceived(CTTResult result)
        {
            Result = result;
            SetPhase(GamePhase.Result);
            ui.ShowResult(result);
            PlayResultDialogue(result);
        }

        /// <summary>
        /// Plays the family voice line once per finished match. SSE, polling and
        /// the result endpoint can all deliver the same result, hence the key.
        /// </summary>
        private void PlayResultDialogue(CTTResult result)
        {
            if (result == null) return;
            string key = result.finishedAt + "|" + result.winnerTeam + "|" +
                         result.dialogueId + "|" + result.dialogueRole;
            if (key == lastDialogueKey) return;
            lastDialogueKey = key;
            CttSfx.Instance?.PlayDialogue(
                CTTDialogue.ForResult(result, Lobby == null ? null : Lobby.families));
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

        private void ToggleP2pKind()
        {
            if (Phase != GamePhase.Home)
            {
                ui.Toast(CTTConfig.Tr("backHome", Catalan));
                return;
            }
            if (p2p != null && p2p.IsActive) p2p.Leave();
            ui.SetP2pSolo(!ui.P2pSolo);
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
