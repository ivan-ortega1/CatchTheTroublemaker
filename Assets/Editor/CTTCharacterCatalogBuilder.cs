using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatchTheTroublemaker.Editor
{
    /// <summary>
    /// Turns the folders in Assets/Sprites/Characters/Troublemakers into a runtime
    /// catalog asset. PNG files may be sliced (spriteMode 2), so the first Sprite
    /// sub-asset is used rather than AssetDatabase.LoadAssetAtPath&lt;Sprite&gt;.
    /// Run from Tools > Catch the Troublemaker > Rebuild Character Catalog.
    /// </summary>
    public static class CTTCharacterCatalogBuilder
    {
        private const string SourceRoot = "Assets/Sprites/Characters/Troublemakers";
        private const string AssetPath = "Assets/Resources/CTTCharacterCatalog.asset";
        private const string ResourceRoot = "Assets/Resources/CTTCharacters";

        [MenuItem("Tools/Catch the Troublemaker/Rebuild Character Catalog")]
        public static void Rebuild()
        {
            var families = new List<CTTCharacterFamily>();
            if (Directory.Exists(SourceRoot))
            {
                string[] folders = Directory.GetDirectories(SourceRoot);
                System.Array.Sort(folders, System.StringComparer.Ordinal);
                foreach (string folder in folders)
                {
                    string id = Path.GetFileName(folder);
                    if (id.EndsWith(".meta")) continue;
                    string path = folder.Replace('\\', '/');
                    families.Add(new CTTCharacterFamily
                    {
                        id = id,
                        displayName = CTTCharacterCatalog.PrettyName(id),
                        troublemaker = FirstSprite(path + "/Troublemaker.png"),
                        father = FirstSprite(path + "/Father.png"),
                        mother = FirstSprite(path + "/Mother.png"),
                    });
                }
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            SyncResourceCopies(families);

            CTTCharacterCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CTTCharacterCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CTTCharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }
            catalog.families = families.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CTT] Character catalog rebuilt with " + families.Count + " families.");
        }

        /// <summary>
        /// Runtime code cannot read Assets/Sprites directly, so keep byte-for-byte
        /// copies of the source PNGs under Resources. The family cards load these by
        /// path; existing copies are left untouched so user edits are not overwritten.
        /// </summary>
        private static void SyncResourceCopies(List<CTTCharacterFamily> families)
        {
            if (!AssetDatabase.IsValidFolder(ResourceRoot))
                AssetDatabase.CreateFolder("Assets/Resources", "CTTCharacters");
            foreach (CTTCharacterFamily family in families)
            {
                if (family == null || string.IsNullOrEmpty(family.id)) continue;
                string folder = ResourceRoot + "/" + family.id;
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(ResourceRoot, family.id);
                string sourceFolder = SourceRoot + "/" + family.id;
                foreach (string role in new[] { "Troublemaker", "Father", "Mother" })
                {
                    string source = sourceFolder + "/" + role + ".png";
                    string target = folder + "/" + role + ".png";
                    if (AssetDatabase.LoadMainAssetAtPath(target) == null &&
                        AssetDatabase.LoadMainAssetAtPath(source) != null)
                        AssetDatabase.CopyAsset(source, target);
                }
            }
        }

        private static Sprite FirstSprite(string assetPath)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (asset is Sprite sprite) return sprite;
            if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null)
                Debug.LogWarning("[CTT] No sprite found in " + assetPath);
            return null;
        }
    }
}
