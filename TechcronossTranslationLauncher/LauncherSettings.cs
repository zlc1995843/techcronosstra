using System.Text.Json;

namespace TechcronossTranslationLauncher;

internal sealed class LauncherSettings
{
    public bool ChineseEnabled { get; set; } = true;
    public string? GameRoot { get; set; }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TechcronossTranslationLauncher",
        "settings.json"
    );

    internal static LauncherSettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsPath)) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }

    internal void Save()
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })
        );
    }
}
