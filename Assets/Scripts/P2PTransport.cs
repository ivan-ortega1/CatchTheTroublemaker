using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CatchTheTroublemaker
{
    /// <summary>Wire message exchanged by peers. One JSON object per TCP line.</summary>
    [Serializable]
    public class P2PMessage
    {
        public string t;       // hello | roster | role | ready | input | bye
        public string id;      // sender peer id (set by Broadcast)
        public string name;
        public string room;
        public string role;
        public uint seed;
        public int tick;
        public int dx;
        public int dy;
        public bool sprint;
        public string claims;  // compact "id:role,id:role" roster, sent to late joiners
        public string host;    // id of the room creator (lobby master)
        public string people;  // compact "id|name|role;..." roster with usernames
        public string families; // compact "A~B" family selection from the master
    }

    /// <summary>
    /// Hostless transport: LAN broadcast discovery plus a direct TCP mesh between
    /// every pair of players. No central server and no relay are involved.
    /// Discovery uses UDP 3002; each peer's TCP port is chosen dynamically.
    /// Socket work runs on background threads; Unity callbacks run from Pump() on
    /// the main thread so no UnityEngine API is touched off-thread.
    /// </summary>
    public class P2PTransport : IDisposable
    {
        public const int DiscoveryPort = 3002;
        public const string Magic = "CTT_P2P";
        private const long HeartbeatMs = 2000;
        private const long PeerTimeoutMs = 6000;

        public string PeerId { get; private set; }
        public string Room { get; private set; }
        public int TcpPort { get; private set; }
        /// <summary>Creator of the room. Shared in every hello/roster/ping message.</summary>
        public string HostId { get; set; }

        public event Action<string, string> PeerJoined;   // id, name
        public event Action<string> PeerLeft;             // id
        public event Action<P2PMessage> MessageReceived;

        private sealed class Peer
        {
            public TcpClient client;
            public StreamWriter writer;
            public string id;
            public string name;
        }

        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<Peer> newConnections = new ConcurrentQueue<Peer>();
        private readonly Dictionary<string, Peer> peers = new Dictionary<string, Peer>();
        private readonly List<Peer> pending = new List<Peer>();
        private readonly HashSet<string> joined = new HashSet<string>();
        private readonly Dictionary<string, IPEndPoint> discovered = new Dictionary<string, IPEndPoint>();
        private readonly Dictionary<string, long> discoveredAt = new Dictionary<string, long>();
        private readonly HashSet<string> connecting = new HashSet<string>();
        private readonly Dictionary<string, long> lastSeen = new Dictionary<string, long>();
        private long nextHeartbeatAt;
        private readonly object sync = new object();

        private UdpClient udp;
        private TcpListener listener;
        private volatile bool running;
        private string localName;

        public void Start(string room, string name)
        {
            Room = room;
            localName = name;
            PeerId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

            listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            TcpPort = ((IPEndPoint)listener.LocalEndpoint).Port;

            running = true;
            RunThread(AcceptLoop, "CTT-P2P-Accept");
            RunThread(BeaconLoop, "CTT-P2P-Beacon");
            Debug.Log("[P2P] peer " + PeerId + " room " + room + " TCP " + TcpPort);
        }

        private static void RunThread(ThreadStart body, string name)
        {
            var thread = new Thread(body) { IsBackground = true, Name = name };
            thread.Start();
        }

        /* ----------------------------- discovery & sockets ----------------------------- */

        private void BeaconLoop()
        {
            UdpClient socket = null;
            try
            {
                socket = new UdpClient();
                socket.Client.ExclusiveAddressUse = false;
                socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                udp = socket;
                RunThread(() => ReceiveBeacons(socket), "CTT-P2P-UdpRead");

                var remote = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                while (running)
                {
                    string beacon = Magic + "|" + PeerId + "|" + Room + "|" + localName + "|" + TcpPort;
                    byte[] bytes = Encoding.UTF8.GetBytes(beacon);
                    try { socket.Send(bytes, bytes.Length, remote); }
                    catch (Exception e) { Debug.LogWarning("[P2P] beacon send: " + e.Message); }
                    Thread.Sleep(800);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[P2P] UDP discovery: " + e.Message);
            }
            finally
            {
                if (socket != null) socket.Close();
                udp = null;
            }
        }

        private void ReceiveBeacons(UdpClient socket)
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = socket.Receive(ref from);
                    string[] parts = Encoding.UTF8.GetString(data).Split('|');
                    if (parts.Length < 5 || parts[0] != Magic) continue;
                    // Only peers in our room matter.
                    if (parts[2] != Room) continue;
                    int port;
                    if (!int.TryParse(parts[4], out port)) continue;
                    NoteDiscovery(parts[1], parts[3], new IPEndPoint(from.Address, port));
                }
                catch (SocketException)
                {
                    if (running) break;
                }
                catch (Exception e)
                {
                    if (running) Debug.LogWarning("[P2P] beacon read: " + e.Message);
                }
            }
        }

        private void AcceptLoop()
        {
            while (running)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    StartReader(new Peer { client = client });
                }
                catch (Exception e)
                {
                    if (running) Debug.LogWarning("[P2P] accept: " + e.Message);
                }
            }
        }

        private void StartReader(Peer peer)
        {
            RunThread(() => ReadLoop(peer), "CTT-P2P-Read");
        }

        private void ReadLoop(Peer peer)
        {
            try
            {
                Stream stream = peer.client.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8);
                peer.writer = new StreamWriter(stream, new UTF8Encoding(false))
                {
                    AutoFlush = true,
                    NewLine = "\n",
                };
                newConnections.Enqueue(peer);
                // Without this first hello the TCP socket exists but no peer is
                // registered, so the master's player counter stays at 1 until
                // someone changes a role and sends an unrelated message.
                SendHello(peer);

                string line;
                while (running && (line = reader.ReadLine()) != null)
                    if (line.Length > 0) incoming.Enqueue(line);
            }
            catch (Exception e)
            {
                if (running) Debug.Log("[P2P] connection ended: " + e.Message);
            }
            finally
            {
                string id = peer.id;
                Close(peer);
                if (!string.IsNullOrEmpty(id) && running)
                    incoming.Enqueue("{\"t\":\"bye\",\"id\":\"" + id + "\"}");
            }
        }

        /// <summary>Send our identity as soon as a TCP socket is ready, on both the
        /// outbound and inbound side. One hello is enough: the receiver answers with
        /// its own hello/roster, and P2PGame de-duplicates with its joined set.</summary>
        private void SendHello(Peer peer)
        {
            try
            {
                var msg = new P2PMessage { t = "hello", name = localName };
                msg.id = PeerId;
                msg.room = Room;
                msg.host = HostId ?? "";
                peer.writer.Write(JsonUtility.ToJson(msg) + "\n");
                peer.writer.Flush();
            }
            catch (Exception e)
            {
                if (running) Debug.LogWarning("[P2P] hello send: " + e.Message);
            }
        }

        private void Close(Peer peer)
        {
            lock (sync)
            {
                if (!string.IsNullOrEmpty(peer.id) && peers.TryGetValue(peer.id, out Peer current)
                    && current == peer) peers.Remove(peer.id);
                pending.Remove(peer);
            }
            try { peer.client.Close(); } catch { /* already closed */ }
        }

        private void ConnectTo(string id, IPEndPoint endpoint)
        {
            lock (sync)
            {
                if (!connecting.Add(id) || peers.ContainsKey(id)) return;
            }
            RunThread(() =>
            {
                var peer = new Peer { id = id };
                try
                {
                    peer.client = new TcpClient();
                    peer.client.Connect(endpoint);
                    lock (sync) peers[id] = peer;
                    StartReader(peer);
                }
                catch (Exception e)
                {
                    Debug.Log("[P2P] connect to " + endpoint + ": " + e.Message);
                    lock (sync) connecting.Remove(id);
                }
            }, "CTT-P2P-Connect");
        }

        /// <summary>Remember a peer seen on UDP so the TCP mesh can be built.</summary>
        public void NoteDiscovery(string id, string name, IPEndPoint endpoint)
        {
            if (string.IsNullOrEmpty(id) || id == PeerId) return;
            lock (sync)
            {
                discovered[id] = endpoint;
                if (!discoveredAt.ContainsKey(id)) discoveredAt[id] = DateTime.UtcNow.Ticks / 10000;
            }
        }

        /* ----------------------------- main-thread API ----------------------------- */

        /// <summary>Call from Update(): parses queued messages and maintains the mesh.</summary>
        public void Pump()
        {
            if (!running) return;

            Peer fresh;
            while (newConnections.TryDequeue(out fresh))
                lock (sync) if (!pending.Contains(fresh)) pending.Add(fresh);

            string raw;
            int budget = 512;
            while (budget-- > 0 && incoming.TryDequeue(out raw))
            {
                P2PMessage msg;
                try { msg = JsonUtility.FromJson<P2PMessage>(raw); }
                catch { continue; }
                if (msg == null || string.IsNullOrEmpty(msg.id)) continue;
                lock (sync) lastSeen[msg.id] = DateTime.UtcNow.Ticks / 10000;

                if (msg.t == "bye")
                {
                    RemovePeer(msg.id);
                    continue;
                }

                if (msg.t == "hello")
                {
                    lock (sync)
                    {
                        connecting.Remove(msg.id);
                        Peer knownPending = null;
                        foreach (Peer candidate in pending)
                            if (candidate.id == msg.id) { knownPending = candidate; break; }
                        if (knownPending != null) pending.Remove(knownPending);
                        if (!peers.ContainsKey(msg.id))
                        {
                            // An inbound socket is identified by the hello it sends.
                            Peer match = null;
                            foreach (Peer candidate in pending)
                                if (candidate.writer != null && string.IsNullOrEmpty(candidate.id))
                                { match = candidate; break; }
                            if (match != null)
                            {
                                match.id = msg.id;
                                match.name = msg.name;
                                pending.Remove(match);
                                peers[msg.id] = match;
                            }
                        }
                    }
                    bool isNew;
                    lock (sync) isNew = joined.Add(msg.id);
                    if (isNew) PeerJoined?.Invoke(msg.id, msg.name);
                }

                MessageReceived?.Invoke(msg);
            }

            // Build the mesh. The lower peer id connects first; after 1.5 s either
            // side may initiate, so one-way broadcasts still resolve.
            long now = DateTime.UtcNow.Ticks / 10000;
            List<KeyValuePair<string, IPEndPoint>> snapshot;
            lock (sync) snapshot = new List<KeyValuePair<string, IPEndPoint>>(discovered);
            foreach (var entry in snapshot)
            {
                bool linked;
                long seenAt;
                lock (sync) linked = peers.ContainsKey(entry.Key) || connecting.Contains(entry.Key);
                if (linked || !discoveredAt.TryGetValue(entry.Key, out seenAt)) continue;
                if (string.CompareOrdinal(PeerId, entry.Key) < 0 || now - seenAt > 1500)
                    ConnectTo(entry.Key, entry.Value);
            }

            // Lobby peers are otherwise silent, so a closed/crashed app would never
            // be noticed. Heartbeat + timeout makes PeerLeft reliable without a bye.
            if (now >= nextHeartbeatAt)
            {
                nextHeartbeatAt = now + HeartbeatMs;
                Broadcast(new P2PMessage { t = "ping" });
            }
            List<string> live;
            lock (sync) live = new List<string>(peers.Keys);
            foreach (string id in live)
            {
                long seen;
                lock (sync) seen = lastSeen.ContainsKey(id) ? lastSeen[id] : 0;
                long connectedAt;
                lock (sync) connectedAt = discoveredAt.ContainsKey(id) ? discoveredAt[id] : now;
                if (now - (seen == 0 ? connectedAt : seen) > PeerTimeoutMs) RemovePeer(id);
            }
        }

        private void RemovePeer(string id)
        {
            Peer peer;
            lock (sync)
            {
                if (!peers.TryGetValue(id, out peer)) return;
                peers.Remove(id);
                joined.Remove(id);
                connecting.Remove(id);
                discovered.Remove(id);
                discoveredAt.Remove(id);
                lastSeen.Remove(id);
            }
            try { peer.client.Close(); } catch { /* ignore */ }
            PeerLeft?.Invoke(id);
        }

        public void Broadcast(P2PMessage msg)
        {
            if (msg == null) return;
            msg.id = PeerId;
            msg.room = Room;
            msg.host = HostId ?? "";
            string line = JsonUtility.ToJson(msg) + "\n";
            List<Peer> targets;
            lock (sync) targets = new List<Peer>(peers.Values);
            foreach (Peer peer in targets)
            {
                if (peer.writer == null) continue;
                try { peer.writer.Write(line); peer.writer.Flush(); }
                catch { Close(peer); }
            }
        }

        public int PeerCount
        {
            get { lock (sync) return peers.Count; }
        }

        public void Dispose()
        {
            running = false;
            try { if (udp != null) udp.Close(); } catch { /* ignore */ }
            try { if (listener != null) listener.Stop(); } catch { /* ignore */ }
            List<Peer> all;
            lock (sync)
            {
                all = new List<Peer>(peers.Values);
                all.AddRange(pending);
                peers.Clear();
                pending.Clear();
                discovered.Clear();
                discoveredAt.Clear();
                connecting.Clear();
                joined.Clear();
                lastSeen.Clear();
            }
            foreach (Peer peer in all) try { peer.client.Close(); } catch { /* ignore */ }
        }
    }
}
