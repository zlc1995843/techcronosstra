using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace TechcronossTranslationLauncher;

internal static partial class ModInstaller
{
    private const string DllResource =
        "TechcronossTranslationLauncher.Payload.TechcronossTranslation.dll";
    private const string TranslationResource =
        "TechcronossTranslationLauncher.Payload.zh-Hans.json";
    private const string FontResource =
        "TechcronossTranslationLauncher.Payload.ttcuyuanj";
    private const string FontRegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    private const string ObsoleteCjkRegistryName = "Noto Sans CJK SC Medium (OpenType)";
    private const string ObsoleteCjkFileName = "NotoSansCJKsc-Medium.otf";
    private const string ObsoleteNotoRegistryName = "Noto Sans SC (TrueType)";
    private const string ObsoleteNotoFileName = "NotoSansSC-VF.ttf";
    private const string ObsoleteFontRegistryName = "Resource Han Rounded CN Medium (TrueType)";
    private const string ObsoleteFontFileName = "ResourceHanRoundedCN-Medium.ttf";
    private const int FontChangeMessage = 0x001D;
    private static readonly IntPtr BroadcastWindow = new(0xFFFF);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddFontResourceEx(string fileName, uint flags, IntPtr reserved);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool RemoveFontResourceEx(string fileName, uint flags, IntPtr reserved);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static string _gameRoot = AppContext.BaseDirectory.TrimEnd(
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar
    );

    internal static string GameRoot => _gameRoot;
    internal static bool HasValidGameRoot =>
        File.Exists(Path.Combine(GameRoot, "techcronoss.exe")) &&
        Directory.Exists(Path.Combine(GameRoot, "BepInEx"));

    internal static void ConfigureGameRoot(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            _gameRoot = Path.GetFullPath(path);
    }

    internal static void Apply(bool enabled)
    {
        var plugins = Path.Combine(GameRoot, "BepInEx", "plugins");
        if (!Directory.Exists(plugins))
            throw new DirectoryNotFoundException("未检测到 BepInEx，请把启动器放到游戏根目录。");

        var modDirectory = Path.Combine(plugins, "TechcronossTranslation");
        var translations = Path.Combine(modDirectory, "translations");
        var fonts = Path.Combine(modDirectory, "fonts");
        Directory.CreateDirectory(translations);
        Directory.CreateDirectory(fonts);

        var dllPath = Path.Combine(modDirectory, "TechcronossTranslation.dll");
        var disabledPath = dllPath + ".disabled";
        var translationPath = Path.Combine(translations, "zh-Hans.json");
        var fontPath = Path.Combine(fonts, "ttcuyuanj");
        var legacyDllPath = Path.Combine(plugins, "TechcronossTranslation.dll");
        var legacyDisabledPath = legacyDllPath + ".disabled";

        File.Delete(legacyDllPath);
        File.Delete(legacyDisabledPath);

        if (enabled)
        {
            Extract(DllResource, dllPath);
            Extract(TranslationResource, translationPath);
            Extract(FontResource, fontPath);
            UninstallUserFont(ObsoleteCjkRegistryName, ObsoleteCjkFileName);
            UninstallUserFont(ObsoleteNotoRegistryName, ObsoleteNotoFileName);
            UninstallObsoleteRoundedFont();
            DeleteObsoleteFonts(modDirectory);
            File.Delete(disabledPath);
            UpdateBepInExConfig(true);
        }
        else
        {
            if (File.Exists(dllPath))
                File.Move(dllPath, disabledPath, true);
            UninstallUserFont(ObsoleteCjkRegistryName, ObsoleteCjkFileName);
            UninstallUserFont(ObsoleteNotoRegistryName, ObsoleteNotoFileName);
            UninstallObsoleteRoundedFont();
            UpdateBepInExConfig(false);
        }
    }

    internal static void LaunchGame()
    {
        var candidates = new[]
        {
            Path.Combine(GameRoot, "TechcronossOffline.exe"),
            Path.Combine(GameRoot, "techcronoss.exe"),
        };
        var executable = candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("游戏根目录中未找到启动程序。");

        Process.Start(new ProcessStartInfo(executable)
        {
            WorkingDirectory = GameRoot,
            UseShellExecute = true,
        });
    }

    private static void Extract(string resourceName, string destination)
    {
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"启动器资源缺失：{resourceName}");
        var temporary = destination + ".new";
        using (var output = File.Create(temporary))
            source.CopyTo(output);
        File.Move(temporary, destination, true);
    }

    private static void UpdateBepInExConfig(bool enabled)
    {
        var path = Path.Combine(
            GameRoot,
            "BepInEx",
            "config",
            "zlc.techcronoss.translation.cfg"
        );
        if (!File.Exists(path))
            return;
        var content = File.ReadAllText(path);
        content = EnabledLine().Replace(content, $"Enabled = {enabled.ToString().ToLowerInvariant()}");
        content = CaptureMissingLine().Replace(content, "CaptureMissing = false");
        File.WriteAllText(path, content);
    }

    [GeneratedRegex("(?m)^Enabled\\s*=.*$")]
    private static partial Regex EnabledLine();

    [GeneratedRegex("(?m)^CaptureMissing\\s*=.*$")]
    private static partial Regex CaptureMissingLine();

    private static void DeleteObsoleteFonts(string modDirectory)
    {
        var fonts = Path.Combine(modDirectory, "fonts");
        File.Delete(Path.Combine(fonts, "TsukuARdGothic-Std-Bold"));
        File.Delete(Path.Combine(fonts, ObsoleteFontFileName));
    }

    private static string UserFontPath(string fileName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "Windows",
        "Fonts",
        fileName
    );

    private static void UninstallObsoleteRoundedFont()
    {
        UninstallUserFont(ObsoleteFontRegistryName, ObsoleteFontFileName);
    }

    private static void UninstallUserFont(string registryName, string fileName)
    {
        var path = UserFontPath(fileName);
        try
        {
            RemoveFontResourceEx(path, 0, IntPtr.Zero);
        }
        catch
        {
            // Cleanup must not prevent the translation plugin from being installed.
        }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(FontRegistryPath, true);
            key?.DeleteValue(registryName, false);
        }
        catch
        {
            // A stale registry entry is harmless and can be removed on the next launch.
        }
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Windows can retain a font file lock until every old game process exits.
        }
        PostMessage(BroadcastWindow, FontChangeMessage, IntPtr.Zero, IntPtr.Zero);
    }
}
