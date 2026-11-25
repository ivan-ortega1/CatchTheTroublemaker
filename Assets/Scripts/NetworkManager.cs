using System;
using System.Collections;
using UnityEngine;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Owns all server communication: REST commands, the SSE event stream and the
    /// /state polling fallback. It raises strongly typed events for GameManager.
    /// The authoritative game logic remains in server/engine.js.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public event Action<CTTLobby> LobbyReceived;
        public event Action<CTTState> StateReceived;
        public event Action<CTTResult> ResultReceived;
        public event Action<string> ErrorReceived;

        [Header("Polling fallback")]
        [Tooltip("Seconds between /state requests when SSE is unavailable.")]
        public float pollInterval = 0.35f;

        private CTTApi api;
        private CttSseClient sse;
        private Coroutine pollRunner;
        private string roomCode;
        private string playerId;

        private void Awake()
        {
            api = GetComponent<CTTApi>();
            if (api == null) api = gameObject.AddComponent<CTTApi>();
            sse = GetComponent<CttSseClient>();
            if (sse == null) sse = gameObject.AddComponent<CttSseClient>();
        }

        // Defensive accessors: a manual scene may attach NetworkManager on its own,
        // so never assume Awake() already supplied these component references.
        private CTTApi Api => api != null ? api : (api = GetComponent<CTTApi>() ?? gameObject.AddComponent<CTTApi>());
        private CttSseClient Sse => sse != null ? sse : (sse = GetComponent<CttSseClient>() ?? gameObject.AddComponent<CttSseClient>());

        /// <summary>Start listening to a room. Idempotent for the same room.</summary>
        public void SetRoom(string code, string id)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(id)) return;
            if (roomCode == code && playerId == id && sse != null) return;

            roomCode = code;
            playerId = id;
            Sse.Connect(code, id, OnStreamEvent);
            if (pollRunner == null) pollRunner = StartCoroutine(Poll());
        }

        public void Stop()
        {
            if (sse != null) sse.Stop();  // no-op when the component was never created
            if (pollRunner != null)
            {
                StopCoroutine(pollRunner);
                pollRunner = null;
            }
            roomCode = null;
            playerId = null;
        }

        private void OnDestroy() => Stop();

        /* ----------------------------- commands ----------------------------- */

        public void CreateRoom(string name, Action<CTTRoomResponse> ok, Action<string> fail)
        {
            Api.Post("/api/rooms", new CTTNameRequest { name = name },
                json => Parse(json, ok, fail), fail);
        }

        public void Join(string code, string name, string returningId,
            Action<CTTRoomResponse> ok, Action<string> fail)
        {
            Api.Post("/api/rooms/" + code + "/join",
                new CTTJoinRequest { name = name, playerId = returningId },
                json => Parse(json, ok, fail), fail);
        }

        public void SetRole(string code, string id, string role,
            Action<CTTLobbyResponse> ok, Action<string> fail)
        {
            Api.Post("/api/rooms/" + code + "/role",
                new CTTRoleRequest { playerId = id, role = role },
                json =>
                {
                    var response = JsonUtility.FromJson<CTTLobbyResponse>(json);
                    if (response == null || !response.ok)
                    {
                        fail?.Invoke(response == null ? null : response.error);
                        return;
                    }
                    ok?.Invoke(response);
                }, fail);
        }

        /// <summary>Host-only: send the two chosen family folder ids to the server.</summary>
        public void SetFamilies(string code, string id, string[] families,
            Action<CTTLobbyResponse> ok, Action<string> fail)
        {
            Api.Post("/api/rooms/" + code + "/families",
                new CTTFamiliesRequest { playerId = id, families = families },
                json =>
                {
                    var response = JsonUtility.FromJson<CTTLobbyResponse>(json);
                    if (response == null || !response.ok)
                    {
                        fail?.Invoke(response == null ? null : response.error);
                        return;
                    }
                    ok?.Invoke(response);
                }, fail);
        }

        public void PostMatchAction(string code, string id, string action,
            Action<CTTOkResponse> ok, Action<string> fail)
        {
            Api.Post("/api/rooms/" + code + action, new CTTStartRequest { playerId = id },
                json =>
                {
                    var response = JsonUtility.FromJson<CTTOkResponse>(json);
                    if (response == null || !response.ok)
                    {
                        fail?.Invoke(response == null ? null : response.error);
                        return;
                    }
                    ok?.Invoke(response);
                }, fail);
        }

        public void SendInput(string code, string id, int dx, int dy, bool sprint)
        {
            Api.Post("/api/rooms/" + code + "/input",
                new CTTInputRequest { playerId = id, dx = dx, dy = dy, sprint = sprint },
                _ => { }, _ => { });
        }

        public void RefreshState()
        {
            if (string.IsNullOrEmpty(roomCode)) return;
            Api.Get("/api/rooms/" + roomCode + "/state", json =>
            {
                var snapshot = JsonUtility.FromJson<CTTState>(json);
                if (snapshot != null) StateReceived?.Invoke(snapshot);
            }, error => ErrorReceived?.Invoke(error));
        }

        /// <summary>Checks the configured URL without creating or joining a room.</summary>
        public void TestConnection(Action<string> ok, Action<string> fail) =>
            Api.Get("/api/health", json => ok?.Invoke(json), fail);

        private static void Parse<T>(string json, Action<T> ok, Action<string> fail) where T : class
        {
            var response = JsonUtility.FromJson<T>(json);
            if (response == null) fail?.Invoke(null);
            else ok?.Invoke(response);
        }

        /* ----------------------------- streaming ----------------------------- */

        private IEnumerator Poll()
        {
            while (!string.IsNullOrEmpty(roomCode))
            {
                RefreshState();
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, pollInterval));
            }
        }

        private void OnStreamEvent(string eventName, string data)
        {
            switch (eventName)
            {
                case "lobby":
                {
                    var lobby = JsonUtility.FromJson<CTTLobby>(data);
                    if (lobby != null) LobbyReceived?.Invoke(lobby);
                    break;
                }
                case "result":
                {
                    var response = JsonUtility.FromJson<CTTResultResponse>(data);
                    if (response != null && response.result != null)
                        ResultReceived?.Invoke(response.result);
                    break;
                }
                case "hello":
                case "start":
                case "state":
                {
                    var snapshot = JsonUtility.FromJson<CTTState>(data);
                    if (snapshot != null) StateReceived?.Invoke(snapshot);
                    break;
                }
            }
        }
    }
}
