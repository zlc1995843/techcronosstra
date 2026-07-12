using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace TechcronossTranslation;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "zlc.techcronoss.translation";
    public const string PluginName = "Techcronoss Chinese Translation";
    public const string PluginVersion = "0.1.19";

    internal static ManualLogSource Logger { get; private set; } = null!;
    internal static TranslationStore Translations { get; private set; } = null!;
    private TranslationBehaviour _behaviour;
    private Harmony _harmony;

    public override void Load()
    {
        Logger = Log;
        ModConfig.Initialize(Config);
        Translations = new TranslationStore(Logger);
        Translations.Load();

        _behaviour = AddComponent<TranslationBehaviour>();
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Entries={Translations.Count}");
    }

    public override bool Unload()
    {
        if (_behaviour != null)
            UnityEngine.Object.Destroy(_behaviour);
        _harmony?.UnpatchSelf();
        return true;
    }
}
