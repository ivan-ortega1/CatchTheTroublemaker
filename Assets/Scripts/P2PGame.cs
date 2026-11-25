using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Hostless game session. Peers discover each other on the LAN, form a room by
    /// code, agree on a seed and player list without any central authority, and then
    /// run the same deterministic P2PMatch in lockstep. The winner is computed
    /// locally by every peer — there is no server and no winner endpoint.
    /// </summary>
    public class P2PGame : MonoBehaviour
    {
        public event Action<CTTLobby> LobbyChanged;
        public event Action<CTTState> StateChanged;
        public event Action<CTTResult> ResultChanged;
        public event Action<string> Error;

        private sealed class PeerInfo
        {
            public string id;
            public string name;
            public string role;   // claimed role, "" when undecided
            public uint seed;
            public bool ready;
        }

        private struct InputState
        {
            public int dx, dy;
            public bool sprint;
        }

        private P2PTransport transport;
        private P2PMatch match;
        private readonly Dictionary<string, PeerInfo> peers = new Dictionary<string, PeerInfo>();
        private readonly Dictionary<string, Dictionary<int, InputState>> inputs =
            new Dictionary<string, Dictionary<int, InputState>>();
        private readonly Dictionary<string, InputState> lastInput = new Dictionary<string, InputState>();
        private readonly List<string> participants = new List<string>();

        private const string SoloPlayerId = "local-solo";

        // Family folder ids chosen by the room creator. The simulation still uses
        // the internal A/B slots, so these only drive display and validation.
        private string familyA = "";
        private string familyB = "";

        public string[] Families => FamilyCount == 0
            ? System.Array.Empty<string>()
            : FamilyCount == 1 ? new[] { familyA } : new[] { familyA, familyB };

        private int FamilyCount => (string.IsNullOrEmpty(familyA) ? 0 : 1)
            + (string.IsNullOrEmpty(familyB) ? 0 : 1);

        private string localName = "Player";
        private string hostId;   // creator/master of a multiplayer room
        private bool soloSession;
        private InputState localInput;
        private bool allReadyDetected;
        private float startAt;
        private uint startSeed;
        private string startHumans;
        private string startFrom;
        private int pendingTick;
        private float tickOpenedAt;
        private float nextSendAt;
        private bool resultSent;
        private CTTResult cachedResult;

        /// <summary>A solo session is active without any transport.</summary>
        public bool IsActive => transport != null || soloSession;
        public string RoomCode => soloSession ? "SOLO" : (transport == null ? "" : transport.Room);
        public string PlayerId => transport == null ? (soloSession ? SoloPlayerId : "") : transport.PeerId;
        public bool IsHost => !string.IsNullOrEmpty(hostId) && hostId == PlayerId;

        /// <summary>True when every connected player has explicitly picked a character.</summary>
        public bool AllRolesChosen
        {
            get
            {
                if (!IsActive || peers.Count == 0) return false;
                foreach (PeerInfo p in peers.Values)
                    if (string.IsNullOrEmpty(p.role)) return false;
                return true;
            }
        }

        /* ----------------------------- session ----------------------------- */

        public void Create(string name)
        {
            StartSession(name, RandomCode(), true);
        }

        /// <summary>
        /// Local-only session: one human (the chosen role) plus five bots. No transport,
        /// no code, no messages — the same P2PMatch is ticked directly from Update().
        /// </summary>
        public void CreateSolo(string name)
        {
            Leave();
            localName = Sanitize(name);
            soloSession = true;
            hostId = SoloPlayerId;
            SetDefaultFamilies();
            peers[PlayerId] = new PeerInfo { id = PlayerId, name = localName, role = "" };
            LobbyChanged?.Invoke(BuildLobby());
        }

        public void Join(string code, string name)
        {
            StartSession(name, (code ?? "").Trim().ToUpperInvariant(), false);
        }

        private void StartSession(string name, string code, bool asHost)
        {
            Leave();
            localName = Sanitize(name);
            SetDefaultFamilies();
            if (code.Length != 5)
            {
                Error?.Invoke("Room code must be 5 letters");
                return;
            }

            transport = new P2PTransport();
            transport.PeerJoined += OnPeerJoined;
            transport.PeerLeft += OnPeerLeft;
            transport.MessageReceived += OnMessage;
            transport.Start(code, localName);
            hostId = asHost ? transport.PeerId : "";
            transport.HostId = hostId;

            peers[transport.PeerId] = new PeerInfo
            {
                id = transport.PeerId,
                name = localName,
                role = "",
            };
            SayHello();
            LobbyChanged?.Invoke(BuildLobby());
        }

        public void Leave()
        {
            if (transport != null)
            {
                transport.Broadcast(new P2PMessage { t = "bye" });
                transport.Dispose();
                transport = null;
            }
            soloSession = false;
            hostId = null;
            match = null;
            cachedResult = null;
            peers.Clear();
            inputs.Clear();
            lastInput.Clear();
            participants.Clear();
            allReadyDetected = false;
            startSeed = 0;
            startHumans = null;
            startFrom = null;
            pendingTick = 0;
            resultSent = false;
            localInput = default;
        }

        public void SetRole(string role)
        {
            if (!IsActive) return;
            PeerInfo me;
            if (!peers.TryGetValue(PlayerId, out me)) return;
            me.role = role;
            if (transport != null)
            {
                transport.Broadcast(new P2PMessage { t = "role", role = role });
                SayRoster();
            }
            LobbyChanged?.Invoke(BuildLobby());
        }

        /// <summary>Only the room creator/master may change the two families.</summary>
        public void SetFamilies(string[] families)
        {
            if (!IsActive || !IsHost) return;
            var selected = new List<string>();
            if (families != null)
                foreach (string id in families)
                    if (!string.IsNullOrEmpty(id) && !selected.Contains(id)) selected.Add(id);
            if (selected.Count > 2) return;

            familyA = selected.Count > 0 ? selected[0] : "";
            familyB = selected.Count > 1 ? selected[1] : "";
            if (transport != null) SayRoster();
            LobbyChanged?.Invoke(BuildLobby());
        }

        public void StartMatch()
        {
            if (!IsActive || match != null) return;
            PeerInfo me;
            if (!peers.TryGetValue(PlayerId, out me)) return;

            bool catalan = GameManager.Instance != null && GameManager.Instance.Catalan;
            if (!soloSession && peers.Count < CTTConfig.P2pMinRealPlayers)
            {
                Error?.Invoke(CTTConfig.Tr("needTwo", catalan));
                return;
            }
            if (!soloSession && !IsHost)
            {
                Error?.Invoke(CTTConfig.Tr("onlyHost", catalan));
                return;
            }
            if (!AllRolesChosen)
            {
                Error?.Invoke(CTTConfig.Tr("needRoles", catalan));
                return;
            }
            if (FamilyCount != 2)
            {
                Error?.Invoke(CTTConfig.Tr("needFamilies", catalan));
                return;
            }

            me.ready = true;
            me.seed = RandomSeed();
            if (soloSession)
            {
                // No handshake: the single human starts immediately, bots fill the rest.
                startSeed = me.seed;
                startHumans = EncodeHumans(AssignRoles());
                startFrom = PlayerId;
                allReadyDetected = true;
                startAt = Time.unscaledTime;
                return;
            }
            transport.Broadcast(new P2PMessage { t = "ready", seed = me.seed, name = me.name });
            TryAllReady();
        }

        public void Rematch()
        {
            if (!IsActive) return;
            match = null;
            cachedResult = null;
            resultSent = false;
            pendingTick = 0;
            allReadyDetected = false;
            startFrom = null;
            startHumans = null;
            inputs.Clear();
            lastInput.Clear();
            foreach (PeerInfo p in peers.Values) { p.ready = false; p.seed = 0; }
            StartMatch();
            LobbyChanged?.Invoke(BuildLobby());
        }

        public void SetLocalInput(int dx, int dy, bool sprint)
        {
            localInput = new InputState { dx = dx, dy = dy, sprint = sprint };
        }

        /* ----------------------------- peer messages ----------------------------- */

        private void OnPeerJoined(string id, string name)
        {
            if (!peers.ContainsKey(id))
                peers[id] = new PeerInfo { id = id, name = name, role = "" };
            SayHello();
            SayRoster();
            LobbyChanged?.Invoke(BuildLobby());
        }

        private void OnPeerLeft(string id)
        {
            peers.Remove(id);
            inputs.Remove(id);
            lastInput.Remove(id);
            if (!string.IsNullOrEmpty(hostId) && id == hostId)
            {
                // Promote the lowest remaining peer id so every client picks the
                // same new master without a central authority.
                hostId = "";
                foreach (PeerInfo p in peers.Values)
                    if (string.IsNullOrEmpty(hostId) || string.CompareOrdinal(p.id, hostId) < 0)
                        hostId = p.id;
                if (transport != null) transport.HostId = hostId;
            }
            if (match != null && participants.Contains(id))
                Error?.Invoke("A player left the match — restart from the result screen.");
            LobbyChanged?.Invoke(BuildLobby());
        }

        private void OnMessage(P2PMessage msg)
        {
            if (transport == null || msg.id == transport.PeerId) return;
            if (!string.IsNullOrEmpty(msg.host) && string.IsNullOrEmpty(hostId))
            {
                hostId = msg.host;
                transport.HostId = hostId;
            }
            PeerInfo peer;
            bool known = peers.TryGetValue(msg.id, out peer);
            if (!known)
            {
                peer = new PeerInfo { id = msg.id, name = msg.name };
                peers[msg.id] = peer;
            }

            switch (msg.t)
            {
                case "hello":
                    peer.name = msg.name;
                    if (msg.id == hostId) ApplyFamilies(msg.families);
                    SayRoster();
                    break;

                case "roster":
                    ApplyRoster(msg.claims);
                    ApplyPeople(msg.people);
                    if (msg.id == hostId) ApplyFamilies(msg.families);
                    break;

                case "role":
                    peer.role = msg.role;
                    SayRoster();
                    break;

                case "ready":
                    peer.name = msg.name;
                    peer.seed = msg.seed;
                    peer.ready = true;
                    // One player pressing START is enough: the other peers answer with
                    // their own ready seed, so everybody reaches the same agreement.
                    PeerInfo mine;
                    if (peers.TryGetValue(transport.PeerId, out mine) && !mine.ready &&
                        peers.Count >= CTTConfig.P2pMinRealPlayers && AllRolesChosen &&
                        FamilyCount == 2)
                    {
                        mine.ready = true;
                        mine.seed = RandomSeed();
                        transport.Broadcast(new P2PMessage
                        {
                            t = "ready", seed = mine.seed, name = mine.name,
                        });
                    }
                    TryAllReady();
                    break;

                case "start":
                    AcceptStart(msg);
                    break;

                case "input":
                    RecordInput(msg.id, msg);
                    break;
            }
            if (match == null) LobbyChanged?.Invoke(BuildLobby());
        }

        private void SayHello()
        {
            transport?.Broadcast(new P2PMessage
            {
                t = "hello", name = localName, families = EncodeFamilies(),
            });
        }

        private void SayRoster()
        {
            if (transport == null) return;
            transport.Broadcast(new P2PMessage
            {
                t = "roster",
                claims = EncodingClaims(),
                people = EncodingPeople(),
                families = EncodeFamilies(),
            });
        }

        private string EncodingClaims()
        {
            var sb = new StringBuilder();
            foreach (PeerInfo p in peers.Values)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(p.id).Append(':').Append(string.IsNullOrEmpty(p.role) ? "-" : p.role);
            }
            return sb.ToString();
        }

        private void ApplyRoster(string claims)
        {
            if (string.IsNullOrEmpty(claims)) return;
            foreach (string pair in claims.Split(','))
            {
                string[] parts = pair.Split(':');
                if (parts.Length != 2) continue;
                PeerInfo p;
                if (!peers.TryGetValue(parts[0], out p)) continue;
                p.role = parts[1] == "-" ? "" : parts[1];
            }
        }

        /// <summary>Roster with usernames, so the lobby list is complete even if a
        /// hello arrives out of order or a client joins an existing room.</summary>
        private string EncodingPeople()
        {
            var sb = new StringBuilder();
            foreach (PeerInfo p in peers.Values)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(p.id).Append('|').Append(SafeField(p.name)).Append('|')
                  .Append(string.IsNullOrEmpty(p.role) ? "-" : SafeField(p.role));
            }
            return sb.ToString();
        }

        private void ApplyPeople(string people)
        {
            if (string.IsNullOrEmpty(people)) return;
            foreach (string entry in people.Split(';'))
            {
                string[] parts = entry.Split('|');
                if (parts.Length != 3) continue;
                PeerInfo p;
                if (!peers.TryGetValue(parts[0], out p))
                {
                    p = new PeerInfo { id = parts[0] };
                    peers[parts[0]] = p;
                }
                p.name = parts[1];
                p.role = parts[2] == "-" ? "" : parts[2];
            }
        }

        private static string SafeField(string value)
        {
            return (value ?? "").Replace('|', ' ').Replace(';', ' ').Replace(':', ' ');
        }

        private string EncodeFamilies()
        {
            return (string.IsNullOrEmpty(familyA) ? "-" : familyA) + "~"
                + (string.IsNullOrEmpty(familyB) ? "-" : familyB);
        }

        private void ApplyFamilies(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return;
            string[] parts = encoded.Split('~');
            if (parts.Length != 2) return;
            familyA = parts[0] == "-" ? "" : parts[0];
            familyB = parts[1] == "-" ? "" : parts[1];
        }

        private void SetDefaultFamilies()
        {
            string[] defaults = CTTCharacterCatalog.Current.DefaultIds(2);
            familyA = defaults.Length > 0 ? defaults[0] : "";
            familyB = defaults.Length > 1 ? defaults[1] : "";
        }

        /* ----------------------------- hostless agreement ----------------------------- */

        private Dictionary<string, string> AssignRoles()
        {
            var ordered = new List<PeerInfo>(peers.Values);
            ordered.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            var result = new Dictionary<string, string>();
            var used = new HashSet<string>();

            // A claim is valid only when no lower-id peer claims the same role.
            foreach (PeerInfo p in ordered)
            {
                if (string.IsNullOrEmpty(p.role)) continue;
                bool contested = false;
                foreach (PeerInfo other in ordered)
                {
                    if (other.id == p.id) continue;
                    if (other.role == p.role && string.CompareOrdinal(other.id, p.id) < 0)
                    { contested = true; break; }
                }
                if (!contested && !used.Contains(p.role))
                {
                    result[p.id] = p.role;
                    used.Add(p.role);
                }
            }

            // Everyone else gets the first free role, in peer-id order.
            foreach (PeerInfo p in ordered)
            {
                if (result.ContainsKey(p.id)) continue;
                foreach (string role in P2PConfig.RoleOrder)
                {
                    if (used.Contains(role)) continue;
                    result[p.id] = role;
                    used.Add(role);
                    break;
                }
            }
            return result;
        }

        private string EncodeHumans(Dictionary<string, string> roles)
        {
            var ordered = new List<string>(roles.Keys);
            ordered.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (string id in ordered)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(id).Append(':').Append(roles[id]);
            }
            return sb.ToString();
        }

        private void TryAllReady()
        {
            if (transport == null || match != null || allReadyDetected) return;
            if (peers.Count < CTTConfig.P2pMinRealPlayers || !AllRolesChosen) return;
            foreach (PeerInfo p in peers.Values)
                if (!p.ready) return;

            allReadyDetected = true;
            startAt = Time.unscaledTime + 2f;

            // XOR is commutative, so the seed does not depend on message order.
            uint seed = 0;
            foreach (PeerInfo p in peers.Values) seed ^= p.seed;
            startSeed = seed;
            startHumans = EncodeHumans(AssignRoles());
            startFrom = transport.PeerId;
            transport.Broadcast(new P2PMessage { t = "start", seed = seed, claims = startHumans });
        }

        private void AcceptStart(P2PMessage msg)
        {
            if (match != null || string.IsNullOrEmpty(msg.claims)) return;
            if (!allReadyDetected)
            {
                allReadyDetected = true;
                startAt = Time.unscaledTime + 2f;
            }
            // No fixed host: if several peers announce a start, everyone picks the
            // same one by taking the lowest sender id.
            if (string.IsNullOrEmpty(startFrom)
                || string.CompareOrdinal(msg.id, startFrom) < 0)
            {
                startFrom = msg.id;
                startSeed = msg.seed;
                startHumans = msg.claims;
            }
        }

        private void BeginMatch()
        {
            if (string.IsNullOrEmpty(startHumans))
                startHumans = EncodeHumans(AssignRoles());

            var humans = new List<P2PHuman>();
            participants.Clear();
            foreach (string pair in startHumans.Split(','))
            {
                string[] parts = pair.Split(':');
                if (parts.Length != 2) continue;
                PeerInfo p;
                string name = peers.TryGetValue(parts[0], out p) ? p.name : parts[0];
                humans.Add(new P2PHuman { id = parts[0], name = name, role = parts[1] });
                participants.Add(parts[0]);
            }
            if (humans.Count == 0) { allReadyDetected = false; return; }

            match = new P2PMatch(startSeed, humans, 0L);
            pendingTick = 0;
            tickOpenedAt = Time.unscaledTime;
            nextSendAt = Time.unscaledTime;
            resultSent = false;
            transport?.Broadcast(new P2PMessage { t = "roster", claims = startHumans });
            StateChanged?.Invoke(match.GetSnapshot(0L, true));
        }

        /* ----------------------------- lockstep ----------------------------- */

        private void RecordInput(string id, P2PMessage msg)
        {
            Dictionary<int, InputState> byTick;
            if (!inputs.TryGetValue(id, out byTick))
            {
                byTick = new Dictionary<int, InputState>();
                inputs[id] = byTick;
            }
            if (msg.tick < pendingTick - 4) return;
            byTick[msg.tick] = new InputState { dx = msg.dx, dy = msg.dy, sprint = msg.sprint };
            lastInput[id] = byTick[msg.tick];
        }

        private void Update()
        {
            if (!IsActive) return;
            transport?.Pump();

            if (match == null)
            {
                if (allReadyDetected && Time.unscaledTime >= startAt) BeginMatch();
                return;
            }
            if (resultSent) return;

            if (Time.unscaledTime >= nextSendAt)
            {
                nextSendAt = Time.unscaledTime + P2PConfig.TickMs / 1000f;
                var msg = new P2PMessage
                {
                    t = "input",
                    tick = pendingTick,
                    dx = localInput.dx,
                    dy = localInput.dy,
                    sprint = localInput.sprint,
                };
                if (transport != null) transport.Broadcast(msg);
                RecordInput(PlayerId, msg);
            }

            bool everyoneReady = true;
            foreach (string id in participants)
            {
                Dictionary<int, InputState> byTick;
                if (!inputs.TryGetValue(id, out byTick) || !byTick.ContainsKey(pendingTick))
                { everyoneReady = false; break; }
            }
            // Never stall forever on a lost packet: neutral input is deterministic.
            if (!everyoneReady && Time.unscaledTime - tickOpenedAt < 0.25f) return;

            long simNow = (long)pendingTick * P2PConfig.TickMs;
            foreach (string id in participants)
            {
                InputState state;
                Dictionary<int, InputState> byTick;
                if (inputs.TryGetValue(id, out byTick) && byTick.TryGetValue(pendingTick, out state))
                    lastInput[id] = state;
                else if (!lastInput.TryGetValue(id, out state))
                    state = default;
                match.SetInput(id, state.dx, state.dy, state.sprint, simNow);
            }

            match.Tick(simNow, P2PConfig.TickMs / 1000f);
            CTTState snapshot = match.GetSnapshot(simNow, pendingTick == 0);
            snapshot.events = ParseEvents(match.DrainEvents());
            StateChanged?.Invoke(snapshot);
            pendingTick++;
            tickOpenedAt = Time.unscaledTime;

            foreach (Dictionary<int, InputState> byTick in inputs.Values)
            {
                var stale = new List<int>();
                foreach (int tick in byTick.Keys) if (tick < pendingTick - 4) stale.Add(tick);
                foreach (int tick in stale) byTick.Remove(tick);
            }

            if (match.Finished && !resultSent)
            {
                resultSent = true;
                cachedResult = match.CttResult;
                ResultChanged?.Invoke(cachedResult);
            }
        }

        /* ----------------------------- lobby DTO ----------------------------- */

        private CTTLobby BuildLobby()
        {
            var playerList = new List<CTTLobbyPlayer>();
            var ordered = new List<PeerInfo>(peers.Values);
            ordered.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            foreach (PeerInfo p in ordered)
                playerList.Add(new CTTLobbyPlayer
                {
                    id = p.id, name = p.name, role = p.role, isHost = false,
                });

            var roleList = new List<CTTRoleSlot>();
            foreach (string role in P2PConfig.RoleOrder)
            {
                // Lobby shows only explicit choices. Bot fallback happens at kickoff,
                // not while players are still deciding their characters.
                string takenBy = "";
                foreach (PeerInfo p in peers.Values)
                    if (p.role == role) { takenBy = p.id; break; }
                roleList.Add(new CTTRoleSlot
                {
                    role = role,
                    team = P2PConfig.TeamOf(role),
                    family = P2PConfig.FamilyOf(role),
                    color = CTTConfig.ColorOf(role, false).ToString(),
                    name = new CTTLocalizedName
                    {
                        en = CTTConfig.RoleName(role, false),
                        ca = CTTConfig.RoleName(role, true),
                    },
                    takenBy = takenBy,
                });
            }
            return new CTTLobby
            {
                code = RoomCode,
                status = match == null ? "waiting" : "playing",
                // The creator is the lobby master; the simulation itself stays hostless.
                hostId = hostId,
                players = playerList.ToArray(),
                roles = roleList.ToArray(),
                families = Families,
            };
        }

        /* ----------------------------- helpers ----------------------------- */

        private static string Sanitize(string value)
        {
            string result = SafeField((value ?? "").Trim());
            if (result.Length == 0) result = "Player";
            return result.Substring(0, Mathf.Min(24, result.Length));
        }

        private static string RandomCode()
        {
            var random = new System.Random((int)DateTime.UtcNow.Ticks);
            var chars = new char[5];
            for (int i = 0; i < chars.Length; i++) chars[i] = (char)('A' + random.Next(0, 26));
            return new string(chars);
        }

        /// <summary>Convert the engine's pipe-separated events to the same DTO the
        /// server sends, so both modes drive identical client effects.</summary>
        private static CTTEvent[] ParseEvents(string[] raw)
        {
            if (raw == null || raw.Length == 0) return System.Array.Empty<CTTEvent>();
            var result = new List<CTTEvent>();
            foreach (string line in raw)
            {
                string[] p = (line ?? "").Split('|');
                if (p.Length < 2) continue;
                var evt = new CTTEvent { type = p[0], id = p[1] };
                if (p.Length > 2) float.TryParse(p[2], out evt.x);
                if (p.Length > 3) float.TryParse(p[3], out evt.y);
                if (p.Length > 4) long.TryParse(p[4], out evt.now);
                if (evt.type == "finish")
                {
                    evt.winnerTeam = p[1];
                    evt.winnerId = p.Length > 2 ? p[2] : "";
                    evt.id = "";
                    if (p.Length > 3) long.TryParse(p[3], out evt.now);
                }
                result.Add(evt);
            }
            return result.ToArray();
        }

        private uint RandomSeed()
        {
            unchecked
            {
                uint t = (uint)DateTime.UtcNow.Ticks;
                t ^= (uint)(transport == null ? 0 : PlayerId.GetHashCode());
                t ^= (uint)GetHashCode();
                return t == 0 ? 1u : t;
            }
        }

        private void OnDestroy() => Leave();
        private void OnApplicationQuit() => Leave();
    }
}
