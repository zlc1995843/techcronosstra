using Garnet.Novel.Utility;
using HarmonyLib;
using TMPro;

namespace TechcronossTranslation;

// Translate the complete novel command before the typewriter parser sees it.
// Non-novel TMP labels are limited to exact character-name/title matches. This
// covers the history panel without letting dialogue fragments corrupt normal UI.
[HarmonyPatch(typeof(NovelText), nameof(NovelText.CreateInfo))]
internal static class NovelSourceTextPatch
{
    private static bool _loggedFirstReplacement;

    private static void Prefix(ref string __0)
    {
        if (!ModConfig.Enabled.Value
            || !Plugin.Translations.TryTranslateDisplay(__0, out var translated))
            return;

        __0 = translated;
        if (_loggedFirstReplacement)
            return;

        _loggedFirstReplacement = true;
        Plugin.Logger.LogInfo("Novel source text is translated before typewriter parsing.");
    }
}

[HarmonyPatch(typeof(TMP_Text), "set_text")]
internal static class NovelTextWritePatch
{
    private static bool _loggedFirstReplacement;

    private static void Prefix(TMP_Text __instance, ref string value)
    {
        if (!ModConfig.Enabled.Value || string.IsNullOrEmpty(value))
            return;

        string translated;
        var changed = __instance is RubyTextMeshProUGUI
            ? Plugin.Translations.TryTranslateDisplay(value, out translated)
            : Plugin.Translations.TryTranslateStoryLabel(value, out translated);
        if (!changed)
            return;

        value = translated;
        TranslationBehaviour.ApplyNovelFont(__instance);
        if (_loggedFirstReplacement)
            return;

        _loggedFirstReplacement = true;
        Plugin.Logger.LogInfo("Novel text is translated before TMP mesh generation.");
    }
}

// RubyTextMeshProUGUI owns the story atlas and can rebuild its mesh after the
// normal TMP setter. Bind the complete font before either operation.
[HarmonyPatch]
internal static class RubyTextFontPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RubyTextMeshProUGUI), "SetTextCustom")]
    private static void AfterSetTextCustom(RubyTextMeshProUGUI __instance)
    {
        BindFont(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(RubyTextMeshProUGUI),
        nameof(RubyTextMeshProUGUI.ForceMeshUpdate),
        typeof(bool),
        typeof(bool)
    )]
    private static void BeforeForceMeshUpdate(RubyTextMeshProUGUI __instance)
    {
        BindFont(__instance);
    }

    private static void BindFont(RubyTextMeshProUGUI text)
    {
        TranslationBehaviour.ApplyNovelFont(text);
    }
}
