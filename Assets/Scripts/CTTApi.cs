using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Thin async HTTP layer. The Unity client sends intent and reads snapshots;
    /// the Node server remains authoritative for coins, catches and the winner.
    /// </summary>
    public class CTTApi : MonoBehaviour
    {
        public static CTTApi Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public static string ServerUrl
        {
            get
            {
                string url = PlayerPrefs.GetString(CTTConfig.PlayerPrefKey, CTTConfig.DefaultServer);
                if (string.IsNullOrWhiteSpace(url)) url = CTTConfig.DefaultServer;
                return url.TrimEnd('/');
            }
            set
            {
                PlayerPrefs.SetString(CTTConfig.PlayerPrefKey, value.TrimEnd('/'));
                PlayerPrefs.Save();
            }
        }

        public void Get(string path, Action<string> ok, Action<string> fail) =>
            StartCoroutine(Request("GET", path, null, ok, fail));

        public void Post(string path, object payload, Action<string> ok, Action<string> fail) =>
            StartCoroutine(Request("POST", path, JsonUtility.ToJson(payload), ok, fail));

        private IEnumerator Request(string method, string path, string body, Action<string> ok, Action<string> fail)
        {
            string url = ServerUrl + path;
            var request = new UnityWebRequest(url, method);
            if (body != null)
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ok?.Invoke(request.downloadHandler.text);
            }
            else
            {
                string message = string.IsNullOrEmpty(request.downloadHandler.text)
                    ? request.error
                    : request.downloadHandler.text;
                fail?.Invoke(Explain(message, request.result));
            }
            request.Dispose();
        }

        /// <summary>
        /// Unity's raw "Cannot connect to destination host" is technically correct but
        /// useless here: localhost means the device running the client, not the host
        /// PC. Give the player an actionable hint instead.
        /// </summary>
        private static string Explain(string raw, UnityWebRequest.Result result)
        {
            string message = raw ?? "";
            bool unreachable = result == UnityWebRequest.Result.ConnectionError
                || message.IndexOf("connect", System.StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("resolve", System.StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("timeout", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (unreachable)
                return "Cannot reach server. Check Server URL: localhost = this device only.";
            return message;
        }
    }
}
