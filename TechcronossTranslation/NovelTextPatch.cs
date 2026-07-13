using Garnet.Novel.Utility;
using Garnet.Novel.View.UI;
using HarmonyLib;
using TMPro;

namespace TechcronossTranslation;

[HarmonyPatch(typeof(NovelText), nameof(NovelText.CreateInfo))]
internal static class NovelTextPatch
{
    private static bool _loggedFirstReplacement;

    private static void Prefix(ref string __0)
    {
        if (Plugin.Translations.IsCharacterName(__0)
            || !Plugin.Translations.TryTranslateExact(__0, out var translated))
            return;

        __0 = translated;
        if (_loggedFirstReplacement)
            return;

        _loggedFirstReplacement = true;
        Plugin.Logger.LogInfo("Novel source text is translated before typewriter parsing.");
    }
}

[HarmonyPatch(typeof(NovelTextView), nameof(NovelTextView.Execute))]
internal static class NovelNameDisplayPatch
{
    private static void Postfix(NovelTextView __instance)
    {
        var label = __instance?.nameText;
        if (label == null || !Plugin.Translations.IsCharacterName(label.text))
            return;
        if (!Plugin.Translations.TryTranslateExact(label.text, out var translated))
            return;

        TranslationBehaviour.ApplyNovelFont(label);
        label.text = translated;
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
