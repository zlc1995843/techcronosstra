using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace TechcronossTranslation;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "zlc.techcronoss.translation";
    public const string PluginName = "Techcronoss Chinese Translation";
    public const string PluginVersion = "0.1.4";

    internal static ManualLogSource Logger { get; private set; } = null!;
    internal static TranslationStore Translations { get; private set; } = null!;
    private TranslationBehaviour _behaviour;

    public override void Load()
    {
        Logger = Log;
        ModConfig.Initialize(Config);
        Translations = new TranslationStore(Logger);
        Translations.Load();

        _behaviour = AddComponent<TranslationBehaviour>();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Entries={Translations.Count}");
    }

    public override bool Unload()
    {
        if (_behaviour != null)
            UnityEngine.Object.Destroy(_behaviour);
        return true;
    }
}
