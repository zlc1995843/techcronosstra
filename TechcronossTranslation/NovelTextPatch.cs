using Garnet.Novel.Utility;
using HarmonyLib;
using TMPro;

namespace TechcronossTranslation;

[HarmonyPatch(typeof(NovelText), nameof(NovelText.CreateInfo))]
internal static class NovelTextPatch
{
    private static bool _loggedFirstReplacement;
    internal static bool CurrentLineHasTranslation { get; private set; }

    private static void Prefix(ref string __0)
    {
        if (Plugin.Translations.IsCharacterName(__0))
        {
            CurrentLineHasTranslation = false;
            return;
        }
        if (!Plugin.Translations.TryTranslateDisplay(__0, out var translated))
        {
            CurrentLineHasTranslation = false;
            return;
        }

        CurrentLineHasTranslation = true;
        __0 = translated;
        if (_loggedFirstReplacement)
            return;

        _loggedFirstReplacement = true;
        Plugin.Logger.LogInfo("Novel source text is translated before typewriter parsing.");
    }
}

[HarmonyPatch]
internal static class RubyTextFontPatch
{
    private static bool _loggedFirstBinding;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RubyTextMeshProUGUI), "SetTextCustom")]
    private static void Postfix(RubyTextMeshProUGUI __instance)
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
        if (!ModConfig.Enabled.Value
            || !TranslationBehaviour.ApplyNovelFont(text)
            || _loggedFirstBinding)
            return;

        _loggedFirstBinding = true;
        Plugin.Logger.LogInfo("Ruby story font and atlas are bound before mesh generation.");
    }
}
