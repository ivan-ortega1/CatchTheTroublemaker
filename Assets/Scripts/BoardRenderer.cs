using System.Collections.Generic;
using UnityEngine;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Draws the authoritative snapshot as a normal world-space 2D scene: one
    /// SpriteRenderer for the map, one per coin and one per player. The orthographic
    /// camera follows the local player. No rules are evaluated here.
    /// </summary>
    public class BoardRenderer : MonoBehaviour
    {
        [Header("World look")]
        [Tooltip("Pixels per map tile used when creating the generated sprite.")]
        public int pixelsPerTile = 16;
        [Tooltip("Orthographic camera height in world units.")]
        public float cameraSize = 7.5f;
        [Tooltip("Camera smoothing while following the local player.")]
        public float cameraSmoothing = 8f;

        private GameObject mapObject;
        private SpriteRenderer mapRenderer;
        private Texture2D mapTexture;
        private Sprite mapSprite;

        private readonly List<SpriteRenderer> coinViews = new List<SpriteRenderer>();
        private readonly Dictionary<string, PlayerView> players = new Dictionary<string, PlayerView>();

        private int cols = 1;
        private int rows = 1;
        private string mapKey = "";
        private bool highContrast;
        private string followId;

        private sealed class PlayerView
        {
            public GameObject root;
            public SpriteRenderer body;
            public SpriteRenderer halo;
            public TextMesh label;
        }

        private void Awake()
        {
            mapObject = new GameObject("Map", typeof(SpriteRenderer));
            mapObject.transform.SetParent(transform, false);
            mapObject.transform.position = new Vector3(0f, 0f, 1f);
            mapRenderer = mapObject.GetComponent<SpriteRenderer>();
            mapRenderer.sortingOrder = -10;
        }

        public void Initialize(bool contrast)
        {
            highContrast = contrast;
            if (mapKey.Length > 0) RebuildMap();
        }

        public void SetHighContrast(bool contrast)
        {
            if (highContrast == contrast) return;
            highContrast = contrast;
            if (mapKey.Length > 0) RebuildMap();
        }

        public void SetFollowTarget(string playerId) => followId = playerId;

        public void Clear()
        {
            mapKey = "";
            if (mapRenderer != null) mapRenderer.sprite = null;
            ReleaseMapAssets();
            foreach (var view in players.Values) if (view.root != null) Destroy(view.root);
            players.Clear();
            foreach (var coin in coinViews) if (coin != null) coin.gameObject.SetActive(false);
            followId = null;
            Camera main = Camera.main;
            if (main != null) main.transform.position = new Vector3(0f, 0f, -10f);
        }

        /* ----------------------------- map ----------------------------- */

        public void ApplyState(CTTState snapshot)
        {
            if (snapshot == null) return;

            CTTMap map = snapshot.map;
            if (map != null && map.cols > 0 && map.rows > 0 && !string.IsNullOrEmpty(map.tiles))
            {
                string key = map.cols + "x" + map.rows + ":" + map.tiles;
                if (key != mapKey)
                {
                    mapKey = key;
                    cols = map.cols;
                    rows = map.rows;
                    currentTiles = map.tiles;
                    RebuildMap();
                }
                UpdateCoins(snapshot.coins);
                UpdatePlayers(snapshot.players);
            }
        }

        private void RebuildMap()
        {
            ReleaseMapAssets();
            if (mapRenderer == null || cols <= 0 || rows <= 0) return;

            mapTexture = CTTDraw.BuildMap(new CTTMap { cols = cols, rows = rows, tiles = CurrentTiles() }, highContrast);
            mapSprite = Sprite.Create(mapTexture,
                new Rect(0, 0, mapTexture.width, mapTexture.height),
                new Vector2(0.5f, 0.5f), pixelsPerTile);
            mapRenderer.sprite = mapSprite;
        }

        // CTTDraw.BuildMap only reads cols/rows/tiles, so store the raw tiles separately.
        private string currentTiles = "";
        private string CurrentTiles() => currentTiles;

        private void ReleaseMapAssets()
        {
            if (mapSprite != null) { Destroy(mapSprite); mapSprite = null; }
            if (mapTexture != null) { Destroy(mapTexture); mapTexture = null; }
        }

        /* ----------------------------- actors ----------------------------- */

        private void UpdateCoins(CTTCoin[] coins)
        {
            int needed = coins != null ? coins.Length : 0;
            while (coinViews.Count < needed)
            {
                var go = new GameObject("Coin", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = CTTDraw.Coin();
                sr.sortingOrder = 1;
                go.transform.localScale = Vector3.one * 1.2f;
                coinViews.Add(sr);
            }

            for (int i = 0; i < coinViews.Count; i++)
            {
                bool visible = i < needed;
                coinViews[i].gameObject.SetActive(visible);
                if (visible) coinViews[i].transform.position = TileToWorld(coins[i].x, coins[i].y);
            }
        }

        private void UpdatePlayers(CTTPlayer[] snapshotPlayers)
        {
            var seen = new HashSet<string>();
            foreach (CTTPlayer p in snapshotPlayers ?? System.Array.Empty<CTTPlayer>())
            {
                seen.Add(p.id);
                if (!players.TryGetValue(p.id, out PlayerView view))
                {
                    view = CreatePlayerView(p);
                    players[p.id] = view;
                }

                view.root.SetActive(!p.eliminated && p.respawnIn <= 0f);
                if (!view.root.activeSelf) continue;

                view.root.transform.position = TileToWorld(p.x, p.y);
                Color color = CTTConfig.ColorOf(p.role, highContrast);
                if (p.grace) color = Color.Lerp(color, Color.white, 0.35f);
                if (p.sprinting) color = Color.Lerp(color, Color.yellow, 0.25f);
                view.body.color = color;

                bool local = p.id == followId;
                view.halo.gameObject.SetActive(local);
                view.halo.color = new Color(1f, 1f, 1f, 0.45f);

                Vector3 markerPos = new Vector3(0f, 0.78f, 0f);
                view.label.transform.localPosition = markerPos;
                view.label.text = CTTConfig.ShortOf(p.role) + (p.isBot ? " ·" : "");
            }

            var stale = new List<string>();
            foreach (var pair in players) if (!seen.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (string id in stale)
            {
                if (players[id].root != null) Destroy(players[id].root);
                players.Remove(id);
            }
        }

        private PlayerView CreatePlayerView(CTTPlayer p)
        {
            var root = new GameObject("Player " + p.name);
            root.transform.SetParent(transform, false);
            root.transform.localScale = Vector3.one * 1.6f;

            var haloObject = new GameObject("Halo", typeof(SpriteRenderer));
            haloObject.transform.SetParent(root.transform, false);
            haloObject.transform.localScale = Vector3.one * 1.25f;
            var halo = haloObject.GetComponent<SpriteRenderer>();
            halo.sprite = CTTDraw.Shape("square");
            halo.sortingOrder = 2;
            halo.color = new Color(1f, 1f, 1f, 0.45f);

            var bodyObject = new GameObject("Body", typeof(SpriteRenderer));
            bodyObject.transform.SetParent(root.transform, false);
            var body = bodyObject.GetComponent<SpriteRenderer>();
            body.sprite = CTTDraw.Shape(CTTConfig.ShapeOf(p.role));
            body.sortingOrder = 3;
            body.color = CTTConfig.ColorOf(p.role, highContrast);

            var labelObject = new GameObject("Label", typeof(TextMesh));
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            var label = labelObject.GetComponent<TextMesh>();
            label.font = CttUi.Font;
            label.fontSize = 42;
            label.characterSize = 0.2f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            label.text = CTTConfig.ShortOf(p.role);

            return new PlayerView { root = root, body = body, halo = halo, label = label };
        }

        /* ----------------------------- camera ----------------------------- */

        private Vector3 TileToWorld(float x, float y) =>
            new Vector3(x - (cols - 1) * 0.5f, (rows - 1) * 0.5f - y, 0f);

        private void LateUpdate()
        {
            Camera main = Camera.main;
            if (main == null) return;

            main.orthographic = true;
            main.orthographicSize = cameraSize;

            Vector3 target = Vector3.zero;
            if (!string.IsNullOrEmpty(followId) && players.TryGetValue(followId, out PlayerView view)
                && view.root != null)
            {
                target = view.root.transform.position;
            }

            float halfH = main.orthographicSize;
            float halfW = halfH * main.aspect;
            float limitX = Mathf.Max(0f, cols * 0.5f - halfW);
            float limitY = Mathf.Max(0f, rows * 0.5f - halfH);
            target.x = Mathf.Clamp(target.x, -limitX, limitX);
            target.y = Mathf.Clamp(target.y, -limitY, limitY);

            Vector3 current = main.transform.position;
            Vector3 next = Vector3.Lerp(current, new Vector3(target.x, target.y, current.z),
                1f - Mathf.Exp(-cameraSmoothing * Time.unscaledDeltaTime));
            main.transform.position = next;
        }
    }
}
