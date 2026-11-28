using System.Collections.Generic;
using UnityEngine;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Result-screen voice lines. The engine marks the speaker in the result
    /// (the kid who reached the coin goal, or the last kid grounded on a parents
    /// win); this class turns that role into the matching family audio clip.
    /// </summary>
    public static class CTTDialogue
    {
        private const string ResourceRoot = "CTTAudio";
        private const string VictoryClip = "DIA_Troublemakervictory";
        private const string DefeatClip = "DIA_Troublemakerdefeat";

        private static readonly Dictionary<string, AudioClip> cache =
            new Dictionary<string, AudioClip>();

        /// <summary>Role used for the result caption and the audio lookup.</summary>
        public static string RoleForResult(CTTResult result)
        {
            if (result == null) return "troublemakerA";
            if (!string.IsNullOrEmpty(result.dialogueRole)) return result.dialogueRole;

            string id = !string.IsNullOrEmpty(result.dialogueId)
                ? result.dialogueId
                : result.winnerTeam == "troublemakers" ? result.winnerId : null;
            if (!string.IsNullOrEmpty(id) && result.standings != null)
                foreach (CTTStanding standing in result.standings)
                    if (standing != null && standing.id == id) return standing.role;

            if (result.standings != null)
                foreach (CTTStanding standing in result.standings)
                    if (standing != null && standing.team == "troublemakers" && standing.eliminated)
                        return standing.role;
            return "troublemakerA";
        }

        public static AudioClip Victory(string familyId) => Clip(familyId, true);

        public static AudioClip Defeat(string familyId) => Clip(familyId, false);

        /// <summary>Picks the family from the role slot, then the win/loss line.</summary>
        public static AudioClip ForResult(CTTResult result, string[] families)
        {
            if (result == null) return null;
            string role = RoleForResult(result);
            string familyId = CTTCharacterCatalog.FamilyIdForRole(families, role);
            return Clip(familyId, result.winnerTeam == "troublemakers");
        }

        private static AudioClip Clip(string familyId, bool victory)
        {
            if (string.IsNullOrEmpty(familyId)) return null;
            string key = familyId + "/" + (victory ? "victory" : "defeat");
            if (cache.TryGetValue(key, out AudioClip cached)) return cached;

            string name = victory ? VictoryClip : DefeatClip;
            AudioClip clip = Resources.Load<AudioClip>(
                ResourceRoot + "/" + familyId + "/Troublemaker/" + name);
            cache[key] = clip;
            return clip;
        }
    }
}
