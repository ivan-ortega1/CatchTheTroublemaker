using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatchTheTroublemaker.Editor
{
    /// <summary>
    /// Copies the imported dialogue clips under Resources so runtime builds can
    /// load them by family. Existing copies are left alone; run again after adding
    /// or replacing a source clip under Assets/Audio/Dialogue/Characters/Troublemakers.
    /// </summary>
    public static class CTTDialogueCatalogBuilder
    {
        private const string SourceRoot = "Assets/Audio/Dialogue/Characters/Troublemakers";
        private const string ResourceRoot = "Assets/Resources/CTTAudio";
        private static readonly string[] Outcomes =
        {
            "DIA_Troublemakervictory", "DIA_Troublemakerdefeat",
        };

        [MenuItem("Tools/Catch the Troublemaker/Rebuild Dialogue Catalog")]
        public static void Rebuild()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourceRoot))
                AssetDatabase.CreateFolder("Assets/Resources", "CTTAudio");

            int copied = 0;
            if (Directory.Exists(SourceRoot))
            {
                string[] folders = Directory.GetDirectories(SourceRoot);
                System.Array.Sort(folders, System.StringComparer.Ordinal);
                foreach (string folder in folders)
                {
                    string id = Path.GetFileName(folder);
                    if (id.EndsWith(".meta")) continue;
                    EnsureFolder(ResourceRoot, id);
                    string targetFolder = ResourceRoot + "/" + id;
                    EnsureFolder(targetFolder, "Troublemaker");

                    foreach (string outcome in Outcomes)
                    {
                        string source = FindSource(folder.Replace('\\', '/'), outcome);
                        if (source == null)
                        {
                            Debug.LogWarning("[CTT] Missing dialogue clip " + outcome + " for " + id);
                            continue;
                        }
                        string target = targetFolder + "/Troublemaker/" + Path.GetFileName(source);
                        if (AssetDatabase.LoadMainAssetAtPath(target) != null) continue;
                        AssetDatabase.CopyAsset(source, target);
                        copied++;
                    }
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[CTT] Dialogue catalog ready; copied " + copied + " new clip(s).");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static string FindSource(string familyFolder, string outcome)
        {
            foreach (string extension in new[] { ".mp3", ".wav" })
            {
                string path = familyFolder + "/Troublemaker/" + outcome + extension;
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) return path;
            }
            return null;
        }
    }
}
