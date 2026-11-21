using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Parses the Server-Sent Events stream from the Node server. Events are
    /// queued and dispatched on the main thread so UI code stays thread-safe.
    /// </summary>
    public class CttSseHandler : DownloadHandlerScript
    {
        private readonly Action<string, string> dispatch;
        private readonly StringBuilder buffer = new StringBuilder();
        private readonly Decoder decoder = Encoding.UTF8.GetDecoder();

        public CttSseHandler(Action<string, string> dispatch) : base(new byte[16384])
        {
            this.dispatch = dispatch;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0) return true;

            var chars = new char[Encoding.UTF8.GetMaxCharCount(dataLength)];
            int count = decoder.GetChars(data, 0, dataLength, chars, 0, false);
            buffer.Append(chars, 0, count);

            int end;
            while ((end = FindFrameEnd()) >= 0)
            {
                string frame = buffer.ToString(0, end);
                buffer.Remove(0, end + 2);
                ParseFrame(frame);
            }
            if (buffer.Length > 1 << 20) buffer.Length = 0; // corrupt stream guard
            return true;
        }

        private int FindFrameEnd()
        {
            for (int i = 0; i < buffer.Length - 1; i++)
            {
                if (buffer[i] == '\n' && buffer[i + 1] == '\n') return i;
            }
            return -1;
        }

        private void ParseFrame(string frame)
        {
            string eventName = "message";
            var data = new StringBuilder();
            foreach (string raw in frame.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith("event:", StringComparison.Ordinal))
                    eventName = line.Substring(6).Trim();
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    if (data.Length > 0) data.Append('\n');
                    data.Append(line.Substring(5).TrimStart());
                }
            }
            if (data.Length > 0) dispatch?.Invoke(eventName, data.ToString());
        }
    }

    public class CttSseClient : MonoBehaviour
    {
        private readonly Queue<KeyValuePair<string, string>> pending =
            new Queue<KeyValuePair<string, string>>();
        private readonly object gate = new object();

        private Action<string, string> onEvent;
        private Coroutine runner;
        private string roomCode;
        private string playerId;
        private bool active;

        public bool Connected { get; private set; }

        public void Connect(string code, string id, Action<string, string> handler)
        {
            Stop();
            roomCode = code;
            playerId = id;
            onEvent = handler;
            active = true;
            runner = StartCoroutine(Stream());
        }

        public void Stop()
        {
            active = false;
            Connected = false;
            if (runner != null) { StopCoroutine(runner); runner = null; }
            lock (gate) pending.Clear();
        }

        private void Enqueue(string eventName, string data)
        {
            lock (gate) pending.Enqueue(new KeyValuePair<string, string>(eventName, data));
        }

        private void Update()
        {
            lock (gate)
            {
                while (pending.Count > 0)
                {
                    var item = pending.Dequeue();
                    onEvent?.Invoke(item.Key, item.Value);
                }
            }
        }

        private IEnumerator Stream()
        {
            while (active)
            {
                Connected = false;
                string url = string.Format("{0}/api/rooms/{1}/events?playerId={2}",
                    CTTApi.ServerUrl, roomCode, UnityWebRequest.EscapeURL(playerId));
                using (var request = new UnityWebRequest(url, "GET"))
                {
                    request.downloadHandler = new CttSseHandler(Enqueue);
                    request.SetRequestHeader("Accept", "text/event-stream");
                    request.timeout = 0;
                    yield return request.SendWebRequest();
                    Connected = false;
                }
                if (active) yield return new WaitForSecondsRealtime(2f);
            }
        }

        private void OnDestroy() => Stop();
    }
}
