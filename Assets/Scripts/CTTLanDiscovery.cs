using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace CatchTheTroublemaker
{
    [Serializable]
    public class CttDiscoveryReply
    {
        public string type;
        public string url;
        public int port;
    }

    /// <summary>
    /// Finds a Catch the Troublemaker server on the local network by UDP broadcast.
    /// The server answers with its HTTP URL, so players never have to type an IP.
    /// </summary>
    public class CttLanDiscovery : MonoBehaviour
    {
        public const int DiscoveryPort = 3001;
        public const string Magic = "CTT_DISCOVER";

        public void Find(Action<string> onFound, Action<string> onFail) =>
            StartCoroutine(FindRoutine(onFound, onFail));

        private IEnumerator FindRoutine(Action<string> onFound, Action<string> onFail)
        {
            UdpClient client;
            try
            {
                client = new UdpClient(0) { EnableBroadcast = true };
            }
            catch (Exception e)
            {
                onFail?.Invoke("Discovery error: " + e.Message);
                yield break;
            }

            byte[] request = Encoding.UTF8.GetBytes(Magic);
            var broadcast = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            string found = null;

            // Three attempts, half a second of listening each.
            for (int attempt = 0; attempt < 3 && found == null; attempt++)
            {
                try { client.Send(request, request.Length, broadcast); }
                catch (Exception e)
                {
                    client.Close();
                    onFail?.Invoke("Discovery error: " + e.Message);
                    yield break;
                }

                float until = Time.realtimeSinceStartup + 0.5f;
                while (Time.realtimeSinceStartup < until && found == null)
                {
                    while (client.Available > 0 && found == null)
                    {
                        try
                        {
                            var from = new IPEndPoint(IPAddress.Any, 0);
                            byte[] data = client.Receive(ref from);
                            var reply = JsonUtility.FromJson<CttDiscoveryReply>(
                                Encoding.UTF8.GetString(data));
                            if (reply != null && reply.type == "CTT_SERVER" &&
                                !string.IsNullOrEmpty(reply.url)) found = reply.url;
                        }
                        catch { /* ignore malformed or foreign UDP packets */ }
                    }
                    yield return null;
                }
            }

            client.Close();
            if (found != null) onFound?.Invoke(found);
            else onFail?.Invoke("No server found on this network");
        }
    }
}
