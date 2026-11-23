using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Gameplay constants mirrored from server/config.js. Keeping them here means the
    /// peer-to-peer mode plays exactly like the server mode without needing a server.
    /// </summary>
    public static class P2PConfig
    {
        public const int Cols = 44;
        public const int Rows = 30;
        public const int Obstacles = 27;

        public const int CoinGoal = 50;
        public const int InitialCoins = 132;
        public const int MaxCoins = 140;
        public const int CoinRespawnMs = 1400;
        public const int GameDurationMs = 6 * 60 * 1000;
        public const int TickMs = 50;

        public const float PlayerRadius = 0.36f;
        public const float CoinPickupRadius = 0.52f;
        public const float CatchRadius = 0.95f;

        public const int LivesPerTroublemaker = 3;
        public const int RespawnMs = 3000;
        public const int GraceMs = 1500;
        public const int StartGraceMs = 5000;

        public const float SpeedParents = 5f;
        public const float SpeedTroublemakers = 6.3f;
        public const float SprintMultiplierParents = 1.55f;
        public const float SprintMultiplierTroublemakers = 1.6f;
        public const int SprintDurationMs = 1400;
        public const int SprintCooldownMs = 7000;
        public const int ConfiscateCooldownMs = 900;
        public const int MaxEvents = 24;

        public static string[] RoleOrder => CTTConfig.RoleOrder;

        public static string FamilyOf(string role) =>
            role != null && role.EndsWith("A", StringComparison.Ordinal) ? "A" : "B";

        public static string TeamOf(string role) =>
            role != null && role.StartsWith("troublemaker", StringComparison.Ordinal)
                ? "troublemakers" : "parents";

        public static string BotName(string role) => "BOT " + CTTConfig.RoleName(role, false);

        public static Vector2 SpawnFor(string role)
        {
            switch (role)
            {
                case "fatherA": return new Vector2(3.5f, 3.5f);
                case "motherA": return new Vector2(6.5f, 3.5f);
                case "troublemakerA": return new Vector2(3.5f, Rows - 3.5f);
                case "fatherB": return new Vector2(Cols - 3.5f, Rows - 3.5f);
                case "motherB": return new Vector2(Cols - 6.5f, Rows - 3.5f);
                default: return new Vector2(Cols - 3.5f, 3.5f); // troublemakerB
            }
        }
    }

    /// <summary>One human participant in a peer-to-peer match.</summary>
    [Serializable]
    public class P2PHuman
    {
        public string id;
        public string name;
        public string role;
    }

    /// <summary>
    /// Deterministic lockstep match. Every peer constructs this with the same seed and
    /// the same ordered player list, feeds it the same per-tick inputs, and therefore
    /// reaches the same positions, the same winner and the same result — with no server.
    /// This is a direct C# port of server/engine.js.
    /// </summary>
    public class P2PMatch
    {
        private sealed class Map
        {
            public int cols, rows;
            public int[] tiles;
            public int[] reachable;
        }

        private sealed class Player
        {
            public string id, name, role, family, team, color, shape;
            public bool isBot;
            public float x, y;
            public float intentX, intentY;
            public bool sprintHeld;
            public long sprintUntil, sprintReadyAt;
            public int coins, lives;
            public bool eliminated;
            public long respawnAt, graceUntil, nextConfiscateAt, nextThink;
            public bool connected = true;
        }

        private sealed class Coin
        {
            public int id;
            public float x, y;
            public long activeAt, spawnAt;
        }

        private sealed class MatchResult
        {
            public string winnerTeam, winnerId, winnerName, reason;
            public long durationMs, finishedAt;
            public int goal;
            public CTTStats stats;
            public CTTStanding[] standings;
        }

        private static readonly int[,] Directions = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        private readonly Map map;
        private readonly List<Player> players = new List<Player>();
        private readonly List<Coin> coins = new List<Coin>();
        private readonly List<long> respawns = new List<long>();
        private readonly List<int> spawnable = new List<int>();
        private readonly List<string> events = new List<string>();
        private uint rng;
        private int nextCoinId = 1;
        private string status = "playing";
        private long startedAt, endsAt;
        private long tickCount;
        private string lastCatcherId;
        private CTTStats stats = new CTTStats();
        private MatchResult result;

        public string Status => status;
        public bool Finished => result != null;
        public long TickCount => tickCount;
        public CTTResult CttResult => result == null ? null : new CTTResult
        {
            winnerTeam = result.winnerTeam,
            winnerId = result.winnerId,
            winnerName = result.winnerName,
            reason = result.reason,
            durationMs = result.durationMs,
            goal = result.goal,
            stats = result.stats,
            standings = result.standings,
            finishedAt = result.finishedAt,
        };

        public P2PMatch(uint seed, IList<P2PHuman> humans, long now = 0)
        {
            map = GenerateMap(seed);
            rng = seed ^ 0x9e3779b9u;

            foreach (P2PHuman human in humans) players.Add(MakePlayer(human));
            foreach (string role in P2PConfig.RoleOrder)
            {
                bool taken = false;
                foreach (Player p in players) if (p.role == role) { taken = true; break; }
                if (!taken) players.Add(MakePlayer(new P2PHuman
                {
                    id = "bot-" + role, name = P2PConfig.BotName(role), role = role,
                }));
            }

            // Head start: troublemakers cannot be caught for the first few seconds.
            foreach (Player p in players)
                if (p.team == "troublemakers") p.graceUntil = now + P2PConfig.StartGraceMs;

            for (int i = 0; i < map.tiles.Length; i++)
            {
                if (map.tiles[i] == 1 || map.reachable[i] != 1) continue;
                int c = i % map.cols;
                int r = i / map.cols;
                bool nearSpawn = false;
                foreach (string role in P2PConfig.RoleOrder)
                {
                    Vector2 s = P2PConfig.SpawnFor(role);
                    if (Mathf.Abs(s.x - (c + 0.5f)) < 1.5f && Mathf.Abs(s.y - (r + 0.5f)) < 1.5f)
                    { nearSpawn = true; break; }
                }
                if (!nearSpawn) spawnable.Add(i);
            }
            for (int i = 0; i < P2PConfig.InitialCoins; i++) SpawnCoin(now);

            status = "playing";
            startedAt = now;
            endsAt = now + P2PConfig.GameDurationMs;
        }

        private static Player MakePlayer(P2PHuman human)
        {
            Vector2 spawn = P2PConfig.SpawnFor(human.role);
            string team = P2PConfig.TeamOf(human.role);
            return new Player
            {
                id = human.id,
                name = string.IsNullOrEmpty(human.name) ? "Player" : human.name.Substring(0, Mathf.Min(24, human.name.Length)),
                role = human.role,
                family = P2PConfig.FamilyOf(human.role),
                team = team,
                color = CTTConfig.ColorOf(human.role, false).ToString(),
                shape = "square",
                isBot = human.id != null && human.id.StartsWith("bot-", StringComparison.Ordinal),
                x = spawn.x, y = spawn.y,
                lives = team == "troublemakers" ? P2PConfig.LivesPerTroublemaker : 0,
                connected = !(human.id != null && human.id.StartsWith("bot-", StringComparison.Ordinal)),
            };
        }

        /* ----------------------------- deterministic primitives ----------------------------- */

        // mulberry32 returns a double in [0,1). Implemented explicitly to avoid
        // float rounding differences between clients.
        private double Rand()
        {
            unchecked
            {
                rng += 0x6d2b79f5u;
                uint t = rng;
                t = (t ^ (t >> 15)) * (1u | t);
                t ^= t + ((t ^ (t >> 7)) * (61u | t));
                return ((t ^ (t >> 14)) & 0xffffffffu) / 4294967296.0;
            }
        }

        private static int Index(int c, int r, int cols) => r * cols + c;

        private static bool IsWall(Map m, int c, int r)
        {
            if (c < 0 || r < 0 || c >= m.cols || r >= m.rows) return true;
            return m.tiles[Index(c, r, m.cols)] == 1;
        }

        private static int[] FloodFill(Map map, int startC, int startR)
        {
            var seen = new int[map.cols * map.rows];
            if (IsWall(map, startC, startR)) return seen;
            int start = Index(startC, startR, map.cols);
            var queue = new List<int> { start };
            seen[start] = 1;
            for (int head = 0; head < queue.Count; head++)
            {
                int cur = queue[head];
                int c = cur % map.cols;
                int r = cur / map.cols;
                for (int d = 0; d < 4; d++)
                {
                    int nc = c + Directions[d, 0];
                    int nr = r + Directions[d, 1];
                    if (IsWall(map, nc, nr)) continue;
                    int ni = Index(nc, nr, map.cols);
                    if (seen[ni] == 1) continue;
                    seen[ni] = 1;
                    queue.Add(ni);
                }
            }
            return seen;
        }

        private static Map GenerateMap(uint seed)
        {
            var randSeed = seed;
            Func<double> rand = () => RandStatic(ref randSeed);
            var tiles = new int[P2PConfig.Cols * P2PConfig.Rows];

            for (int c = 0; c < P2PConfig.Cols; c++)
            {
                tiles[Index(c, 0, P2PConfig.Cols)] = 1;
                tiles[Index(c, P2PConfig.Rows - 1, P2PConfig.Cols)] = 1;
            }
            for (int r = 0; r < P2PConfig.Rows; r++)
            {
                tiles[Index(0, r, P2PConfig.Cols)] = 1;
                tiles[Index(P2PConfig.Cols - 1, r, P2PConfig.Cols)] = 1;
            }

            var zones = new List<Vector2Int>();
            foreach (string role in P2PConfig.RoleOrder)
            {
                Vector2 s = P2PConfig.SpawnFor(role);
                zones.Add(new Vector2Int(Mathf.FloorToInt(s.x), Mathf.FloorToInt(s.y)));
            }

            int placed = 0;
            int attempts = 0;
            while (placed < P2PConfig.Obstacles && attempts < P2PConfig.Obstacles * 20)
            {
                attempts++;
                int c = 2 + Mathf.FloorToInt((float)(rand() * (P2PConfig.Cols - 5)));
                int r = 2 + Mathf.FloorToInt((float)(rand() * (P2PConfig.Rows - 5)));
                int w = 2 + Mathf.FloorToInt((float)(rand() * 3));
                int h = 1 + Mathf.FloorToInt((float)(rand() * 3));
                bool blocked = false;
                foreach (Vector2Int zone in zones)
                {
                    if (c <= zone.x + 4 && c + w >= zone.x - 1 && r <= zone.y + 4 && r + h >= zone.y - 1)
                    { blocked = true; break; }
                }
                if (blocked) continue;
                for (int rr = r; rr < r + h && rr < P2PConfig.Rows - 1; rr++)
                    for (int cc = c; cc < c + w && cc < P2PConfig.Cols - 1; cc++)
                        tiles[Index(cc, rr, P2PConfig.Cols)] = 1;
                placed++;
            }

            var map = new Map { cols = P2PConfig.Cols, rows = P2PConfig.Rows, tiles = tiles };
            Vector2 start = P2PConfig.SpawnFor("fatherA");
            map.reachable = FloodFill(map, Mathf.FloorToInt(start.x), Mathf.FloorToInt(start.y));
            return map;
        }

        private static double RandStatic(ref uint state)
        {
            unchecked
            {
                state += 0x6d2b79f5u;
                uint t = state;
                t = (t ^ (t >> 15)) * (1u | t);
                t ^= t + ((t ^ (t >> 7)) * (61u | t));
                return ((t ^ (t >> 14)) & 0xffffffffu) / 4294967296.0;
            }
        }

        private Vector2 FirstStep(Player from, Player to)
        {
            int sc = Mathf.Clamp(Mathf.FloorToInt(from.x), 0, map.cols - 1);
            int sr = Mathf.Clamp(Mathf.FloorToInt(from.y), 0, map.rows - 1);
            int tc = Mathf.Clamp(Mathf.FloorToInt(to.x), 0, map.cols - 1);
            int tr = Mathf.Clamp(Mathf.FloorToInt(to.y), 0, map.rows - 1);
            int startIdx = Index(sc, sr, map.cols);
            int targetIdx = Index(tc, tr, map.cols);
            if (startIdx == targetIdx || map.tiles[targetIdx] == 1) return Vector2.zero;

            var prev = new int[map.cols * map.rows];
            for (int i = 0; i < prev.Length; i++) prev[i] = -1;
            var seen = new int[map.cols * map.rows];
            var queue = new List<int> { startIdx };
            seen[startIdx] = 1;
            int found = -1;
            for (int head = 0; head < queue.Count && found == -1; head++)
            {
                int cur = queue[head];
                int c = cur % map.cols;
                int r = cur / map.cols;
                for (int d = 0; d < 4; d++)
                {
                    int nc = c + Directions[d, 0];
                    int nr = r + Directions[d, 1];
                    if (IsWall(map, nc, nr)) continue;
                    int ni = Index(nc, nr, map.cols);
                    if (seen[ni] == 1) continue;
                    seen[ni] = 1;
                    prev[ni] = cur;
                    if (ni == targetIdx) { found = ni; break; }
                    queue.Add(ni);
                }
            }
            if (found == -1) return Vector2.zero;
            int step = found;
            while (prev[step] != -1 && prev[step] != startIdx) step = prev[step];
            return new Vector2((step % map.cols) - sc, (step / map.cols) - sr);
        }

        private Vector2 FirstStepTo(Player from, Vector2 target)
        {
            var proxy = new Player { x = target.x, y = target.y };
            return FirstStep(from, proxy);
        }

        private Vector2 FleeTarget(Player from, Player threat)
        {
            Vector2 best = new Vector2(from.x, from.y);
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < 48; i++)
            {
                int idx = (int)(Rand() * map.tiles.Length);
                if (idx < 0 || idx >= map.tiles.Length) continue;
                if (map.tiles[idx] == 1 || map.reachable[idx] != 1) continue;
                float x = (idx % map.cols) + 0.5f;
                float y = (idx / map.cols) + 0.5f;
                float fromDist = Mathf.Sqrt((x - from.x) * (x - from.x) + (y - from.y) * (y - from.y));
                if (fromDist > 13f) continue;
                float threatDist = Mathf.Sqrt((x - threat.x) * (x - threat.x) + (y - threat.y) * (y - threat.y));
                float score = threatDist - fromDist * 0.12f;
                if (score > bestScore) { bestScore = score; best = new Vector2(x, y); }
            }
            return best;
        }

        private static bool CanStand(Map map, float x, float y, float radius)
        {
            return !IsWall(map, Mathf.FloorToInt(x - radius), Mathf.FloorToInt(y - radius))
                && !IsWall(map, Mathf.FloorToInt(x + radius), Mathf.FloorToInt(y - radius))
                && !IsWall(map, Mathf.FloorToInt(x - radius), Mathf.FloorToInt(y + radius))
                && !IsWall(map, Mathf.FloorToInt(x + radius), Mathf.FloorToInt(y + radius));
        }

        private void Move(Player p, float dx, float dy, float speed, float dt)
        {
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f) return;
            dx /= length;
            dy /= length;
            float nx = p.x + dx * speed * dt;
            float ny = p.y + dy * speed * dt;
            if (CanStand(map, nx, p.y, P2PConfig.PlayerRadius)) p.x = nx;
            if (CanStand(map, p.x, ny, P2PConfig.PlayerRadius)) p.y = ny;
        }

        private static float Round2(float n) => Mathf.Round(n * 100f) / 100f;

        private static float Dist(float ax, float ay, float bx, float by) =>
            Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        /* ----------------------------- commands ----------------------------- */

        public void SetInput(string id, float dx, float dy, bool sprint, long now)
        {
            Player p = Find(id);
            if (p == null || p.eliminated || now < p.respawnAt) return;
            p.intentX = Mathf.Clamp(dx, -1f, 1f);
            p.intentY = Mathf.Clamp(dy, -1f, 1f);
            if (sprint && !p.sprintHeld && now >= p.sprintReadyAt) TriggerSprint(p, now);
            p.sprintHeld = sprint;
        }

        private void TriggerSprint(Player p, long now)
        {
            p.sprintUntil = now + P2PConfig.SprintDurationMs;
            p.sprintReadyAt = now + P2PConfig.SprintCooldownMs;
            events.Add("sprint|" + p.id + "|" + p.x + "|" + p.y + "|" + now);
        }

        private Player Find(string id)
        {
            foreach (Player p in players) if (p.id == id) return p;
            return null;
        }

        private Coin NearestCoin(Player from, IList<Player> avoid = null)
        {
            Coin best = null;
            double bestScore = double.PositiveInfinity;
            foreach (Coin coin in coins)
            {
                double score = Dist(coin.x, coin.y, from.x, from.y);
                if (avoid != null)
                    foreach (Player a in avoid)
                        score += Mathf.Max(0f, 6f - Dist(coin.x, coin.y, a.x, a.y)) * 0.8f;
                if (score < bestScore) { bestScore = score; best = coin; }
            }
            return best;
        }

        private void Think(Player p, long now)
        {
            if (now < p.nextThink) return;
            p.nextThink = now + 300 + (int)(Rand() * 180);

            if (p.team == "parents")
            {
                Player child = null;
                foreach (Player q in players)
                    if (q.team == "troublemakers" && q.family == p.family && !q.eliminated) { child = q; break; }
                if (child != null)
                {
                    float dist = Dist(child.x, child.y, p.x, p.y);
                    Vector2 step = FirstStep(p, child);
                    p.intentX = step.x;
                    p.intentY = step.y;
                    if (dist > 1.3f && dist < 5f && now >= p.sprintReadyAt) TriggerSprint(p, now);
                }
                else
                {
                    Coin coin = NearestCoin(p);
                    Vector2 step = coin == null ? Vector2.zero : FirstStepTo(p, new Vector2(coin.x, coin.y));
                    p.intentX = step.x;
                    p.intentY = step.y;
                }
                return;
            }

            Player nearest = null;
            float nearestDist = float.PositiveInfinity;
            var threats = new List<Player>();
            foreach (Player q in players)
                if (q.team == "parents" && q.family == p.family)
                {
                    threats.Add(q);
                    float d = Dist(q.x, q.y, p.x, p.y);
                    if (d < nearestDist) { nearestDist = d; nearest = q; }
                }
            if (nearest != null && nearestDist < 6f)
            {
                Vector2 target = FleeTarget(p, nearest);
                Vector2 step = FirstStepTo(p, target);
                p.intentX = step.x;
                p.intentY = step.y;
                if (nearestDist < 4.5f && now >= p.sprintReadyAt) TriggerSprint(p, now);
            }
            else
            {
                Coin coin = NearestCoin(p, threats);
                Vector2 step = coin == null ? Vector2.zero : FirstStepTo(p, new Vector2(coin.x, coin.y));
                p.intentX = step.x;
                p.intentY = step.y;
            }
        }

        private void SpawnCoin(long at)
        {
            if (coins.Count >= P2PConfig.MaxCoins || spawnable.Count == 0) return;
            int idx = spawnable[(int)(Rand() * spawnable.Count)];
            coins.Add(new Coin
            {
                id = nextCoinId++,
                x = (idx % map.cols) + 0.5f,
                y = (idx / map.cols) + 0.5f,
                activeAt = at,
                spawnAt = at,
            });
        }

        public void Tick(long now, float dt)
        {
            if (status != "playing") return;
            tickCount++;

            for (int i = respawns.Count - 1; i >= 0; i--)
            {
                if (now >= respawns[i])
                {
                    respawns.RemoveAt(i);
                    SpawnCoin(now);
                }
            }

            foreach (Player p in players)
            {
                if (p.isBot) Think(p, now);
                if (p.eliminated || now < p.respawnAt) continue;
                float mult = now < p.sprintUntil
                    ? (p.team == "parents" ? P2PConfig.SprintMultiplierParents : P2PConfig.SprintMultiplierTroublemakers)
                    : 1f;
                float speed = (p.team == "parents" ? P2PConfig.SpeedParents : P2PConfig.SpeedTroublemakers) * mult;
                Move(p, p.intentX, p.intentY, speed, dt);
            }

            foreach (Player p in players)
            {
                if (p.eliminated || now < p.respawnAt) continue;
                for (int i = coins.Count - 1; i >= 0; i--)
                {
                    Coin coin = coins[i];
                    if (now < coin.activeAt) continue;
                    if (p.team == "parents" && now < p.nextConfiscateAt) continue;
                    if (Dist(coin.x, coin.y, p.x, p.y) > P2PConfig.CoinPickupRadius) continue;
                    coins.RemoveAt(i);
                    if (p.team == "troublemakers")
                    {
                        p.coins++;
                        stats.coinsCollected++;
                        events.Add("coin|" + p.id + "|" + coin.x + "|" + coin.y + "|" + now);
                    }
                    else
                    {
                        p.nextConfiscateAt = now + P2PConfig.ConfiscateCooldownMs;
                        stats.confiscated++;
                        events.Add("confiscate|" + p.id + "|" + coin.x + "|" + coin.y + "|" + now);
                    }
                    respawns.Add(now + P2PConfig.CoinRespawnMs);
                    break;
                }
            }

            // THE core rule: only the parents of the SAME family can catch their child.
            foreach (Player parent in players)
            {
                if (parent.team != "parents") continue;
                foreach (Player child in players)
                {
                    if (child.team != "troublemakers" || child.family != parent.family) continue;
                    if (child.eliminated || now < child.respawnAt || now < child.graceUntil) continue;
                    if (Dist(child.x, child.y, parent.x, parent.y) > P2PConfig.CatchRadius) continue;
                    CatchChild(parent, child, now);
                }
            }

            CheckWin(now);
            if (events.Count > P2PConfig.MaxEvents)
                events.RemoveRange(0, events.Count - P2PConfig.MaxEvents);
        }

        private void CatchChild(Player parent, Player child, long now)
        {
            child.lives--;
            stats.catches++;
            lastCatcherId = parent.id;
            int dropped = child.coins;
            child.coins = 0;
            for (int i = 0; i < dropped; i++) respawns.Add(now + 200 + i * 45);
            events.Add("catch|" + parent.id + "|" + child.id + "|" + child.family + "|" + dropped + "|"
                       + child.lives + "|" + child.x + "|" + child.y + "|" + now);

            if (child.lives <= 0) child.eliminated = true;
            else
            {
                Vector2 spawn = P2PConfig.SpawnFor(child.role);
                child.x = spawn.x;
                child.y = spawn.y;
                child.respawnAt = now + P2PConfig.RespawnMs;
                child.graceUntil = now + P2PConfig.RespawnMs + P2PConfig.GraceMs;
                child.intentX = child.intentY = 0f;
            }
        }

        private void CheckWin(long now)
        {
            Player winner = null;
            foreach (Player p in players)
                if (p.team == "troublemakers" && p.coins >= P2PConfig.CoinGoal) { winner = p; break; }
            if (winner != null) { Finish("troublemakers", winner, "reached the coin goal", now); return; }

            bool allOut = true;
            foreach (Player p in players)
                if (p.team == "troublemakers" && !p.eliminated) { allOut = false; break; }
            if (allOut)
            {
                Finish("parents", Find(lastCatcherId), "both troublemakers were grounded", now);
                return;
            }
            if (now >= endsAt) Finish("parents", null, "time ran out before the coin goal", now);
        }

        private void Finish(string winnerTeam, Player winnerPlayer, string reason, long now)
        {
            if (result != null) return;
            status = "finished";
            var standings = new CTTStanding[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                standings[i] = new CTTStanding
                {
                    id = p.id, name = p.name, role = p.role, team = p.team,
                    coins = p.coins, lives = p.lives, eliminated = p.eliminated,
                };
            }
            result = new MatchResult
            {
                winnerTeam = winnerTeam,
                winnerId = winnerPlayer == null ? null : winnerPlayer.id,
                winnerName = winnerPlayer == null ? "The parents" : winnerPlayer.name,
                reason = reason,
                durationMs = Math.Min(now - startedAt, P2PConfig.GameDurationMs),
                goal = P2PConfig.CoinGoal,
                stats = new CTTStats
                {
                    catches = stats.catches,
                    coinsCollected = stats.coinsCollected,
                    confiscated = stats.confiscated,
                },
                standings = standings,
                finishedAt = now,
            };
            events.Add("finish|" + winnerTeam + "|" + result.winnerId + "|" + now);
        }

        /* ----------------------------- presentation ----------------------------- */

        public CTTState GetSnapshot(long now, bool full)
        {
            var coinList = new List<CTTCoin>();
            foreach (Coin c in coins)
                if (now >= c.activeAt)
                    coinList.Add(new CTTCoin { id = c.id, x = Round2(c.x), y = Round2(c.y) });

            var playerList = new List<CTTPlayer>();
            foreach (Player p in players)
                playerList.Add(new CTTPlayer
                {
                    id = p.id,
                    name = p.name,
                    role = p.role,
                    team = p.team,
                    family = p.family,
                    color = p.color,
                    shape = p.shape,
                    x = Round2(p.x),
                    y = Round2(p.y),
                    coins = p.coins,
                    lives = p.lives,
                    eliminated = p.eliminated,
                    respawnIn = Math.Max(0L, p.respawnAt - now),
                    grace = now < p.graceUntil,
                    sprintIn = Math.Max(0L, p.sprintReadyAt - now),
                    sprinting = now < p.sprintUntil,
                    isBot = p.isBot,
                    connected = p.connected,
                });

            var snap = new CTTState
            {
                status = status,
                now = now,
                tick = tickCount,
                remainingMs = Math.Max(0L, endsAt - now),
                goal = P2PConfig.CoinGoal,
                coins = coinList.ToArray(),
                players = playerList.ToArray(),
                result = CttResult,
            };
            if (full)
            {
                var tiles = new char[map.tiles.Length];
                for (int i = 0; i < map.tiles.Length; i++) tiles[i] = map.tiles[i] == 1 ? '1' : '0';
                snap.map = new CTTMap { cols = map.cols, rows = map.rows, tiles = new string(tiles) };
            }
            return snap;
        }

        public string[] DrainEvents()
        {
            string[] copy = events.ToArray();
            events.Clear();
            return copy;
        }
    }
}
