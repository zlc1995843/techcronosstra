using BepInEx.Configuration;

namespace TechcronossTranslation;

internal static class ModConfig
{
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> CaptureMissing { get; private set; } = null!;
    internal static ConfigEntry<string> TranslationFile { get; private set; } = null!;

    internal static void Initialize(ConfigFile config)
    {
        Enabled = config.Bind("Translation", "Enabled", true, "Enable Chinese translation.");
        CaptureMissing = config.Bind(
            "Translation",
            "CaptureMissing",
            false,
            "Record untranslated Japanese strings for future updates."
        );
        TranslationFile = config.Bind(
            "Translation",
            "File",
            "TechcronossTranslation/translations/zh-Hans.json",
            "Path relative to BepInEx/plugins."
        );
    }
}
