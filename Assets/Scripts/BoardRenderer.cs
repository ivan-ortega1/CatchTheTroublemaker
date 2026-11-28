using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Draws the authoritative snapshot as a normal world-space 2D scene: one
    /// SpriteRenderer for the map, one per coin and one per player. The orthographic
    /// camera follows the local player, unless that player is a grounded troublemaker:
    /// then it becomes a free pan/zoom camera. No rules are evaluated here.
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
        private string[] familyIds = System.Array.Empty<string>();
        private bool familiesDirty;

        // Spectator free camera. Started at the map centre and controlled by
        // WASD/arrows, mouse drag + wheel, and touch drag + pinch.
        private const float MinZoom = 1f;
        private const float MaxZoom = 3f;
        private bool spectator;
        private float freeZoom = 1f;
        private Vector3 freeCameraTarget;
        private bool mouseDragging;
        private Vector2 lastMouse;
        private bool touchDragging;
        private Vector2 lastTouch;
        private float lastPinchDistance;

        private sealed class PlayerView
        {
            public GameObject root;
            public SpriteRenderer body;
            public SpriteRenderer outline;
            public Sprite sprite;
        }

        /// <summary>Family ids picked in the lobby; A roles use slot 0, B roles slot 1.</summary>
        public void SetFamilies(string[] families)
        {
            familyIds = families != null ? (string[])families.Clone() : System.Array.Empty<string>();
            familiesDirty = true; // force the next snapshot to re-apply art
        }

        private void Awake()
        {
            mapObject = new GameObject("Map", typeof(SpriteRenderer));
            mapObject.transform.SetParent(transform, false);
            mapObject.transform.position = new Vector3(0f, 0f, 1f);
            mapRenderer = mapObject.GetComponent<SpriteRenderer>();
            mapRenderer.sharedMaterial = CTTDraw.SpriteMaterial;
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

        /// <summary>
        /// A grounded human troublemaker keeps their seat in the snapshot but stops
        /// controlling a character. Switch to a free camera; the caller restores the
        /// follow target when the spectator plays again.
        /// </summary>
        public void SetSpectator(bool enabled)
        {
            if (spectator == enabled) return;
            spectator = enabled;
            mouseDragging = touchDragging = false;
            lastPinchDistance = 0f;
            freeZoom = MinZoom;
            freeCameraTarget = TileToWorld(cols * 0.5f, rows * 0.5f);
            if (enabled) followId = null;
        }

        public void Clear()
        {
            mapKey = "";
            if (mapRenderer != null) mapRenderer.sprite = null;
            ReleaseMapAssets();
            foreach (var view in players.Values) if (view.root != null) Destroy(view.root);
            players.Clear();
            foreach (var coin in coinViews) if (coin != null) coin.gameObject.SetActive(false);
            followId = null;
            spectator = false;
            freeZoom = MinZoom;
            mouseDragging = touchDragging = false;
            lastPinchDistance = 0f;
            Camera main = Camera.main;
            if (main != null) main.transform.position = new Vector3(0f, 0f, -10f);
        }

        /* ----------------------------- map ----------------------------- */

        public void ApplyState(CTTState snapshot)
        {
            if (snapshot == null) return;

            // The map is only sent in a full snapshot (hello / start / /state).
            // Tick snapshots omit it, but they still carry live coin and player
            // positions, so always update actors once we know the map dimensions.
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
                    if (spectator) freeCameraTarget = TileToWorld(cols * 0.5f, rows * 0.5f);
                    RebuildMap();
                }
            }

            if (mapKey.Length == 0) return;
            UpdateCoins(snapshot.coins);
            UpdatePlayers(snapshot.players);
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
                sr.sharedMaterial = CTTDraw.SpriteMaterial;
                sr.sortingOrder = 1;
                go.transform.localScale = Vector3.one * 1.6f;
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

                // Repaint before the visibility check: a family changed while a
                // player was eliminated must still be applied on respawn.
                Sprite sprite = CTTCharacterCatalog.SpriteForRole(familyIds, p.role);
                if (familiesDirty || sprite != view.sprite)
                {
                    view.sprite = sprite;
                    ApplyActorSprite(view, sprite, CTTConfig.ColorOf(p.role, highContrast));
                }
                else if (sprite == null)
                {
                    view.body.color = CTTConfig.ColorOf(p.role, highContrast);
                }

                view.root.SetActive(!p.eliminated && p.respawnIn <= 0f);
                if (!view.root.activeSelf) continue;

                view.root.transform.position = TileToWorld(p.x, p.y);

                // A thin white silhouette frame is the only marker for the local player.
                view.outline.gameObject.SetActive(p.id == followId);
            }

            var stale = new List<string>();
            foreach (var pair in players) if (!seen.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (string id in stale)
            {
                if (players[id].root != null) Destroy(players[id].root);
                players.Remove(id);
            }
            familiesDirty = false;
        }

        private PlayerView CreatePlayerView(CTTPlayer p)
        {
            var root = new GameObject("Player " + p.name);
            root.transform.SetParent(transform, false);
            root.transform.localScale = Vector3.one * 1.5f;

            var outlineObject = new GameObject("Outline", typeof(SpriteRenderer));
            outlineObject.transform.SetParent(root.transform, false);
            var outline = outlineObject.GetComponent<SpriteRenderer>();
            outline.sharedMaterial = CTTDraw.SpriteMaterial;
            outline.sortingOrder = 2;
            outline.color = Color.white;
            outline.gameObject.SetActive(false);

            var bodyObject = new GameObject("Body", typeof(SpriteRenderer));
            bodyObject.transform.SetParent(root.transform, false);
            var body = bodyObject.GetComponent<SpriteRenderer>();
            body.sharedMaterial = CTTDraw.SpriteMaterial;
            body.sortingOrder = 3;

            var view = new PlayerView { root = root, body = body, outline = outline };
            ApplyActorSprite(view, CTTCharacterCatalog.SpriteForRole(familyIds, p.role),
                CTTConfig.ColorOf(p.role, highContrast));
            return view;
        }

        /// <summary>
        /// Uses the imported portrait sprites in the world. The PNGs are tall, so
        /// scale them to a roughly one-tile-high actor; the old square remains the
        /// fallback when a sprite is missing. The white silhouette is the local frame.
        /// </summary>
        private static void ApplyActorSprite(PlayerView view, Sprite sprite, Color fallback)
        {
            if (view == null || view.body == null || view.outline == null) return;

            Sprite drawn = sprite != null ? sprite : CTTDraw.Shape("square");
            view.sprite = sprite;
            view.body.sprite = drawn;
            view.outline.sprite = drawn;
            view.body.color = sprite != null ? Color.white : fallback;
            view.outline.color = Color.white;

            float height = sprite != null ? sprite.bounds.size.y : 0.48f;
            float scale = sprite != null
                ? Mathf.Clamp(1.3f / Mathf.Max(0.01f, height * 1.5f), 0.05f, 4f)
                : 1f;
            view.body.transform.localScale = Vector3.one * scale;
            view.outline.transform.localScale = Vector3.one * scale * 1.12f;
        }

        /* ----------------------------- camera ----------------------------- */

        private Vector3 TileToWorld(float x, float y) =>
            new Vector3(x - (cols - 1) * 0.5f, (rows - 1) * 0.5f - y, 0f);

        /// <summary>Free-look controls, only active while spectating.</summary>
        private void Update()
        {
            if (!spectator) return;
            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Vector2 pan = Vector2.zero;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.y -= 1f;
                if (pan.sqrMagnitude > 0f)
                    freeCameraTarget += new Vector3(pan.x, pan.y, 0f).normalized * (9f * dt / freeZoom);

                if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed) freeZoom += dt;
                if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed) freeZoom -= dt;
                if (keyboard.rKey.wasPressedThisFrame || keyboard.homeKey.wasPressedThisFrame)
                {
                    freeZoom = MinZoom;
                    freeCameraTarget = TileToWorld(cols * 0.5f, rows * 0.5f);
                }
                freeZoom = Mathf.Clamp(freeZoom, MinZoom, MaxZoom);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    freeZoom = Mathf.Clamp(freeZoom + scroll * 0.0015f, MinZoom, MaxZoom);

                Vector2 position = mouse.position.ReadValue();
                if (mouse.leftButton.isPressed)
                {
                    if (!mouseDragging) { mouseDragging = true; lastMouse = position; }
                    else { ApplyDrag(position - lastMouse); lastMouse = position; }
                }
                else mouseDragging = false;
            }

            Touchscreen touch = Touchscreen.current;
            if (touch != null)
            {
                Vector2 first = default;
                Vector2 second = default;
                int count = 0;
                foreach (var control in touch.touches)
                {
                    if (!control.press.isPressed) continue;
                    if (count == 0) first = control.position.ReadValue();
                    else if (count == 1) second = control.position.ReadValue();
                    count++;
                    if (count >= 2) break;
                }

                if (count >= 2)
                {
                    float distance = Vector2.Distance(first, second);
                    if (lastPinchDistance > 0f)
                        freeZoom = Mathf.Clamp(
                            freeZoom * (distance / Mathf.Max(1f, lastPinchDistance)), MinZoom, MaxZoom);
                    lastPinchDistance = distance;
                    touchDragging = false;
                }
                else
                {
                    lastPinchDistance = 0f;
                    if (count == 1)
                    {
                        if (!touchDragging) { touchDragging = true; lastTouch = first; }
                        else { ApplyDrag(first - lastTouch); lastTouch = first; }
                    }
                    else touchDragging = false;
                }
            }
        }

        private void ApplyDrag(Vector2 deltaPixels)
        {
            Camera main = Camera.main;
            if (main == null || deltaPixels.sqrMagnitude < 0.01f) return;
            float unitsPerPixel = (2f * main.orthographicSize) / Mathf.Max(1f, Screen.height);
            freeCameraTarget -= new Vector3(deltaPixels.x * unitsPerPixel, deltaPixels.y * unitsPerPixel, 0f);
        }

        private void LateUpdate()
        {
            Camera main = Camera.main;
            if (main == null) return;

            main.orthographic = true;
            main.orthographicSize = spectator ? cameraSize / freeZoom : cameraSize;

            Vector3 target;
            if (spectator)
            {
                target = freeCameraTarget;
            }
            else
            {
                target = Vector3.zero;
                if (!string.IsNullOrEmpty(followId) && players.TryGetValue(followId, out PlayerView view)
                    && view.root != null)
                {
                    target = view.root.transform.position;
                }
            }

            // Ghost/follow cameras share this outer-wall-only bound: inner wall
            // tiles never clip the view, the solid map border always does.
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
