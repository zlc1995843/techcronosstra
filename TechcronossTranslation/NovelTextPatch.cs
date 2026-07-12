using Garnet.Novel.Command;
using Garnet.Novel.Utility;
using Garnet.Novel.View.UI;
using HarmonyLib;
using TMPro;

namespace TechcronossTranslation;

[HarmonyPatch(typeof(AddTextMacroCommand), "set_Text")]
internal static class NovelCommandTextPatch
{
    private static bool _loggedFirstReplacement;

    private static void Prefix(ref string value)
    {
        if (!Plugin.Translations.TryTranslateExact(value, out var translated))
            return;

        value = translated;
        if (_loggedFirstReplacement)
            return;

        _loggedFirstReplacement = true;
        Plugin.Logger.LogInfo("Novel command text is translated during script deserialization.");
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
