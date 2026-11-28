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

        /// <summary>Sprite filename (without extension) used by a role id.</summary>
        public static string SpriteNameForRole(string role)
        {
            switch (role)
            {
                case "fatherA": case "fatherB": return "Father";
                case "motherA": case "motherB": return "Mother";
                case "troublemakerA": case "troublemakerB": return "Troublemaker";
                default: return null;
            }
        }

        /// <summary>0 for the A slot, 1 for the B slot. Unknown roles use A.</summary>
        public static int SlotOfRole(string role) =>
            role != null && role.EndsWith("B", System.StringComparison.Ordinal) ? 1 : 0;

        /// <summary>
        /// The two selected family ids, padded with the catalog defaults. This lets
        /// the home screen and a fresh lobby show real art before a host picks a pair.
        /// </summary>
        public static string[] EffectiveFamilies(string[] selected)
        {
            string[] defaults = Current.DefaultIds(2);
            var result = new string[2];
            for (int slot = 0; slot < 2; slot++)
            {
                string id = selected != null && selected.Length > slot ? selected[slot] : null;
                result[slot] = string.IsNullOrEmpty(id)
                    ? (slot < defaults.Length ? defaults[slot] : FallbackIds[slot])
                    : id;
            }
            return result;
        }

        public static string FamilyIdForRole(string[] selected, string role) =>
            EffectiveFamilies(selected)[SlotOfRole(role)];

        /// <summary>
        /// Character sprite for a role id: father/mother/troublemaker from family A
        /// for the A roles, and from family B for the B roles. Falls back to the
        /// serialized catalog entry if a Resources copy is missing.
        /// </summary>
        public static Sprite SpriteForRole(string[] selected, string role)
        {
            string spriteName = SpriteNameForRole(role);
            if (string.IsNullOrEmpty(spriteName)) return null;

            string familyId = FamilyIdForRole(selected, role);
            Sprite sprite = SpriteFor(familyId, spriteName);
            if (sprite != null) return sprite;

            CTTCharacterFamily family = Current.Find(familyId);
            if (family == null) return null;
            switch (spriteName)
            {
                case "Father": return family.father;
                case "Mother": return family.mother;
                default: return family.troublemaker;
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
