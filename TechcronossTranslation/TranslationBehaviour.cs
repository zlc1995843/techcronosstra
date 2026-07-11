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
    private bool _loggedScanFailure;
    private bool _fontInitializationAttempted;
    private TMP_FontAsset _chineseFont;
    private AssetBundle _fontBundle;
    private readonly Dictionary<int, string> _lastValues = new();

    private void LateUpdate()
    {
        if (!_fontInitializationAttempted)
        {
            _fontInitializationAttempted = true;
            TryCreateChineseFont();
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
        if (label == null || _chineseFont == null)
            return;

        var source = label.text;
        var instanceId = label.GetInstanceID();
        if (_lastValues.TryGetValue(instanceId, out var previous)
            && string.Equals(previous, source, StringComparison.Ordinal))
            return;
        _lastValues[instanceId] = source;

        if (!Plugin.Translations.TryTranslateDisplay(source, out var translated))
            return;

        if (!_fontInitializationAttempted)
        {
            _fontInitializationAttempted = true;
            TryCreateChineseFont();
        }
        var originalFont = label.font;
        if (originalFont != null
            && originalFont != _chineseFont
            && _chineseFont.fallbackFontAssetTable != null
            && !_chineseFont.fallbackFontAssetTable.Contains(originalFont))
            _chineseFont.fallbackFontAssetTable.Add(originalFont);
        label.font = _chineseFont;
        label.text = translated;
        _lastValues[instanceId] = translated;
    }

    private void TryCreateChineseFont()
    {
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
            _chineseFont = null;
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
            Plugin.Logger.LogError($"Rounded font failed glyph validation: {font.name}");
            yield break;
        }

        _chineseFont = font;
        Plugin.Logger.LogInfo(
            $"Rounded Chinese story font loaded and validated: {font.name} "
            + $"chineseCharacters={Plugin.Translations.ChineseCharacterSet.Length}"
        );
    }

}
