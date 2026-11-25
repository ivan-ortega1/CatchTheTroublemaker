using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CatchTheTroublemaker
{
    /// <summary>One playable family: the folder name plus its three character sprites.</summary>
    [Serializable]
    public class CTTCharacterFamily
    {
        public string id;
        public string displayName;
        public Sprite troublemaker;
        public Sprite father;
        public Sprite mother;
    }

    /// <summary>
    /// Runtime-safe list of families. The editor tool scans
    /// Assets/Sprites/Characters/Troublemakers and writes this asset into Resources,
    /// so builds never need AssetDatabase access.
    /// </summary>
    [CreateAssetMenu(menuName = "Catch the Troublemaker/Character Catalog")]
    public class CTTCharacterCatalog : ScriptableObject
    {
        public CTTCharacterFamily[] families = Array.Empty<CTTCharacterFamily>();

        public static readonly string[] FallbackIds =
        {
            "ClassicCaillou", "EmilyAnderson", "JonathanJohnson", "LittleBill",
        };

        private static CTTCharacterCatalog current;
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Loads one of the real PNGs from Assets/Sprites/Characters/Troublemakers/&lt;family&gt;.
        /// The editor tool copies those files under Resources so runtime code can reach
        /// them without AssetDatabase. Sliced textures keep their Sprite as a sub-asset,
        /// so LoadAll is used instead of Load.
        /// </summary>
        public static Sprite SpriteFor(string familyId, string role)
        {
            if (string.IsNullOrEmpty(familyId) || string.IsNullOrEmpty(role)) return null;
            string key = familyId + "/" + role;
            if (spriteCache.TryGetValue(key, out Sprite cached)) return cached;
            Sprite[] all = Resources.LoadAll<Sprite>("CTTCharacters/" + key);
            Sprite sprite = all != null && all.Length > 0 ? all[0] : null;
            spriteCache[key] = sprite;
            return sprite;
        }

        public static CTTCharacterCatalog Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<CTTCharacterCatalog>("CTTCharacterCatalog");
                if (current == null) current = CreateFallback();
                return current;
            }
        }

        /// <summary>Player-facing family names. Folder ids stay stable for the
        /// network protocol; only these labels are renamed.</summary>
        public static string DisplayNameFor(string id)
        {
            switch (id)
            {
                case "ClassicCaillou": return "The Andersons";
                case "EmilyAnderson": return "The Other Andersons";
                case "JonathanJohnson": return "The Johnsons";
                case "LittleBill": return "The Bills";
                default: return PrettyName(id);
            }
        }

        public static string PrettyName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (i > 0 && char.IsUpper(c) &&
                    (char.IsLower(id[i - 1]) || char.IsDigit(id[i - 1])))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        public CTTCharacterFamily Find(string id)
        {
            if (string.IsNullOrEmpty(id) || families == null) return null;
            foreach (CTTCharacterFamily family in families)
                if (family != null && family.id == id) return family;
            return null;
        }

        public string DisplayName(string id)
        {
            CTTCharacterFamily family = Find(id);
            if (family != null && !string.IsNullOrEmpty(family.displayName))
                return family.displayName;
            return DisplayNameFor(id);
        }

        /// <summary>First N folder ids, used as the default selection in new rooms.</summary>
        public string[] DefaultIds(int count)
        {
            var result = new System.Collections.Generic.List<string>();
            if (families != null)
                foreach (CTTCharacterFamily family in families)
                {
                    if (family == null || string.IsNullOrEmpty(family.id)) continue;
                    result.Add(family.id);
                    if (result.Count >= count) break;
                }
            if (result.Count == 0)
                foreach (string id in FallbackIds)
                {
                    result.Add(id);
                    if (result.Count >= count) break;
                }
            return result.ToArray();
        }

        private static CTTCharacterCatalog CreateFallback()
        {
            var catalog = CreateInstance<CTTCharacterCatalog>();
            catalog.families = new CTTCharacterFamily[FallbackIds.Length];
            for (int i = 0; i < FallbackIds.Length; i++)
                catalog.families[i] = new CTTCharacterFamily
                {
                    id = FallbackIds[i],
                    displayName = DisplayNameFor(FallbackIds[i]),
                };
            return catalog;
        }
    }
}
