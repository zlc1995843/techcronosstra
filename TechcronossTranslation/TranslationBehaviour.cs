using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP.Utils.Collections;
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
    private readonly Dictionary<int, string> _lastValues = new();
    private readonly Dictionary<int, string> _pendingTranslations = new();

    private void Awake()
    {
        Instance = this;
        BeginLoadChineseFont();
    }

    private void OnDestroy()
    {
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
            foreach (var label in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
                Translate(label);
            foreach (var label in UnityEngine.Object.FindObjectsOfType<TextMeshPro>())
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
        if (Plugin.Translations.IsCharacterName(source)
            && !NovelTextPatch.CurrentLineHasTranslation)
        {
            _pendingTranslations.Remove(instanceId);
            _lastValues[instanceId] = source;
            return;
        }
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
