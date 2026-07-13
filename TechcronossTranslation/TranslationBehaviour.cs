using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Garnet.Novel.View.UI;
using TMPro;
using UnityEngine;

namespace TechcronossTranslation;

internal sealed class TranslationBehaviour : MonoBehaviour
{
    internal static TranslationBehaviour Instance { get; private set; }

    private bool _loggedScanFailure;
    private bool _fontInitializationAttempted;
    private TMP_FontAsset _chineseFont;
    private AssetBundle _fontBundle;
    private string _privateFontPath;
    private readonly Dictionary<int, string> _lastValues = new();
    private readonly Dictionary<int, string> _pendingTranslations = new();

    private void Awake()
    {
        Instance = this;
        BeginLoadChineseFont();
    }

    private void OnDestroy()
    {
        UnloadPrivateFont();
        if (Instance == this)
            Instance = null;
    }

    internal static bool ApplyNovelFont(TMP_Text label)
    {
        var instance = Instance;
        if (instance == null || label == null || !instance.EnsureChineseFont())
            return false;
        instance.ApplyChineseFont(label);
        return true;
    }

    private void LateUpdate()
    {
        if (!_fontInitializationAttempted)
        {
            EnsureChineseFont();
        }

        if (!ModConfig.Enabled.Value)
            return;
        try
        {
            foreach (var label in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
                Translate(label);
            foreach (var label in UnityEngine.Object.FindObjectsOfType<RubyTextMeshProUGUI>())
                Translate(label);
        }
        catch (Exception exception)
        {
            if (_loggedScanFailure)
                return;

            _loggedScanFailure = true;
            Plugin.Logger.LogWarning($"Display text scan disabled after an error: {exception.Message}");
            enabled = false;
        }
    }

    private void Translate(TMP_Text label)
    {
        if (label == null)
            return;

        var source = label.text;
        var instanceId = label.GetInstanceID();
        if (_chineseFont == null)
        {
            if (!string.IsNullOrEmpty(source)
                && Plugin.Translations.TryTranslateDisplay(source, out var pending))
                _pendingTranslations[instanceId] = pending;

            if (_pendingTranslations.ContainsKey(instanceId))
            {
                label.text = string.Empty;
                _lastValues[instanceId] = string.Empty;
            }
            return;
        }

        if (_pendingTranslations.TryGetValue(instanceId, out var queuedTranslation))
        {
            if (!string.IsNullOrEmpty(source)
                && Plugin.Translations.TryTranslateDisplay(source, out var currentTranslation))
                queuedTranslation = currentTranslation;
            ApplyTranslation(label, queuedTranslation, instanceId);
            _pendingTranslations.Remove(instanceId);
            return;
        }

        if (_lastValues.TryGetValue(instanceId, out var previous)
            && string.Equals(previous, source, StringComparison.Ordinal))
            return;
        _lastValues[instanceId] = source;

        if (!Plugin.Translations.TryTranslateDisplay(source, out var translated))
        {
            if (Plugin.Translations.IsTranslatedValue(source)
                && label.font != _chineseFont)
            {
                ApplyChineseFont(label);
            }
            return;
        }

        ApplyTranslation(label, translated, instanceId);
    }

    private void ApplyTranslation(TMP_Text label, string translated, int instanceId)
    {
        ApplyChineseFont(label);
        label.text = translated;
        _lastValues[instanceId] = translated;
    }

    private void ApplyChineseFont(TMP_Text label)
    {
        var originalFont = label.font;
        if (originalFont != null
            && originalFont != _chineseFont
            && _chineseFont.fallbackFontAssetTable != null
            && !_chineseFont.fallbackFontAssetTable.Contains(originalFont))
            _chineseFont.fallbackFontAssetTable.Add(originalFont);
        label.font = _chineseFont;
        label.fontSharedMaterial = _chineseFont.material;
    }

    private bool EnsureChineseFont()
    {
        if (_chineseFont != null)
            return true;
        BeginLoadChineseFont();
        return false;
    }

    private void BeginLoadChineseFont()
    {
        if (_fontInitializationAttempted)
            return;
        _fontInitializationAttempted = true;
        try
        {
            var completeFont = TryCreateCompleteRoundedFont();
            if (completeFont != null)
            {
                _chineseFont = completeFont;
                Plugin.Logger.LogInfo(
                    $"Complete rounded Chinese story font loaded: {_chineseFont.name}"
                );
                return;
            }

            var path = Path.Combine(
                Paths.PluginPath,
                "TechcronossTranslation",
                "fonts",
                "ttcuyuanj"
            );
            if (!File.Exists(path))
            {
                Plugin.Logger.LogError($"Bundled rounded font was not found: {path}");
                return;
            }

            StartCoroutine(LoadRoundedFont(path).WrapToIl2Cpp());
        }
        catch (Exception exception)
        {
            Plugin.Logger.LogWarning($"Chinese font initialization failed: {exception.Message}");
        }
    }

    private TMP_FontAsset TryCreateCompleteRoundedFont()
    {
        try
        {
            const string fontName = "Resource Han Rounded CN Medium";
            LoadPrivateRoundedFont();
            var installedNames = Font.GetOSInstalledFontNames();
            var installed = installedNames != null
                && Array.IndexOf(installedNames, fontName) >= 0;
            Plugin.Logger.LogInfo(
                $"Complete rounded font discovery: installed={installed} "
                + $"candidates={installedNames?.Length ?? 0}"
            );
            var osFont = Font.CreateDynamicFontFromOSFont(new[] { fontName }, 64);
            if (osFont == null)
                return null;

            var asset = TMP_FontAsset.CreateFontAsset(osFont);
            if (asset == null)
                return null;

            asset.name = "Resource Han Rounded CN Medium Dynamic SDF";
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            return asset;
        }
        catch (Exception exception)
        {
            Plugin.Logger.LogWarning(
                $"Complete rounded Chinese font could not be created: {exception.Message}"
            );
            return null;
        }
    }

    private void LoadPrivateRoundedFont()
    {
        var path = Path.Combine(
            Paths.PluginPath,
            "TechcronossTranslation",
            "fonts",
            "ResourceHanRoundedCN-Medium.ttf"
        );
        if (!File.Exists(path))
            return;

        if (AddFontResourceEx(path, PrivateFont, IntPtr.Zero) > 0)
        {
            _privateFontPath = path;
            Plugin.Logger.LogInfo("Complete rounded font loaded into the game process.");
        }
    }

    private void UnloadPrivateFont()
    {
        if (string.IsNullOrEmpty(_privateFontPath))
            return;
        RemoveFontResourceEx(_privateFontPath, PrivateFont, IntPtr.Zero);
        _privateFontPath = null;
    }

    private const uint PrivateFont = 0x10;

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddFontResourceEx(string fileName, uint flags, IntPtr reserved);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool RemoveFontResourceEx(string fileName, uint flags, IntPtr reserved);

    private IEnumerator LoadRoundedFont(string path)
    {
        var bundleRequest = AssetBundle.LoadFromFileAsync(path);
        yield return bundleRequest;

        _fontBundle = bundleRequest.assetBundle;
        if (_fontBundle == null)
        {
            Plugin.Logger.LogError("Bundled rounded font AssetBundle could not be loaded.");
            yield break;
        }

        var assetRequest = _fontBundle.LoadAssetAsync("assets/ttcuyuanj sdf.asset");
        yield return assetRequest;

        var font = assetRequest.asset?.TryCast<TMP_FontAsset>();
        if (font == null)
        {
            Plugin.Logger.LogError("TTCuYuanJ SDF was not found in the rounded font bundle.");
            yield break;
        }
        if (!font.HasCharacters(Plugin.Translations.ChineseCharacterSet))
        {
            Plugin.Logger.LogWarning(
                $"Rounded font has incomplete glyph coverage and will use available glyphs: {font.name}"
            );
        }

        _chineseFont = font;
        Plugin.Logger.LogInfo(
            $"Rounded Chinese story font loaded: {font.name} "
            + $"chineseCharacters={Plugin.Translations.ChineseCharacterSet.Length}"
        );
    }

}
