using HarmonyLib;
using TMPro;

namespace TechcronossTranslation;

// TMP is the final text-writing boundary used by the novel UI. Patching this
// setter keeps the replacement before mesh generation without touching the
// IL2CPP command object's unstable string setter.
[HarmonyPatch(typeof(TMP_Text), "set_text")]
internal static class NovelTextWritePatch
{
    private static bool _loggedFirstReplacement;

    private static void Prefix(TMP_Text __instance, ref string value)
    {
        if (!ModConfig.Enabled.Value
            || string.IsNullOrEmpty(value)
            || !Plugin.Translations.TryTranslateDisplay(value, out var translated))
            return;

        value = translated;
        if (_loggedFirstReplacement)
            return;

        _loggedFirstReplacement = true;
        Plugin.Logger.LogInfo("Novel text is translated before TMP mesh generation.");
    }
}
