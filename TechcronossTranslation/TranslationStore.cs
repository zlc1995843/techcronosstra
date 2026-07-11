using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;

namespace TechcronossTranslation;

internal sealed class TranslationStore
{
    private static readonly Regex Japanese = new("[\\u3040-\\u30ff\\u3400-\\u9fff]", RegexOptions.Compiled);
    private readonly ManualLogSource _logger;
    private readonly ConcurrentDictionary<string, byte> _captured = new(StringComparer.Ordinal);
    private readonly object _captureLock = new();
    private Dictionary<string, string> _translations = new(StringComparer.Ordinal);
    private KeyValuePair<string, string>[] _orderedTranslations = [];
    private Dictionary<string, string> _prefixTranslations = new(StringComparer.Ordinal);
    private string _capturePath = string.Empty;

    internal int Count => _translations.Count;
    internal string CharacterSet { get; private set; } = string.Empty;
    internal string ChineseCharacterSet { get; private set; } = string.Empty;

    internal TranslationStore(ManualLogSource logger)
    {
        _logger = logger;
    }

    internal void Load()
    {
        var relative = ModConfig.TranslationFile.Value.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.IsPathRooted(relative) ? relative : Path.Combine(Paths.PluginPath, relative);
        _capturePath = Path.Combine(Path.GetDirectoryName(path)!, "untranslated.jsonl");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"Translation file not found: {path}");
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var source = root.TryGetProperty("translations", out var translations) ? translations : root;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in source.EnumerateObject())
        {
            var translated = item.Value.GetString();
            if (!string.IsNullOrWhiteSpace(translated))
                result[item.Name] = translated;
        }
        _translations = result;
        _orderedTranslations = result
            .OrderByDescending(item => item.Key.Length)
            .ToArray();
        _prefixTranslations = BuildPrefixTranslations(result);
        var characters = new HashSet<char>();
        var chineseCharacters = new HashSet<char>();
        foreach (var value in result.Values)
            foreach (var character in value)
                if (!char.IsControl(character))
                {
                    characters.Add(character);
                    if (character >= '\u3400' && character <= '\u9fff')
                        chineseCharacters.Add(character);
                }
        var characterArray = new char[characters.Count];
        characters.CopyTo(characterArray);
        CharacterSet = new string(characterArray);
        var chineseCharacterArray = new char[chineseCharacters.Count];
        chineseCharacters.CopyTo(chineseCharacterArray);
        ChineseCharacterSet = new string(chineseCharacterArray);
    }

    internal string Translate(string value)
    {
        if (!ModConfig.Enabled.Value || string.IsNullOrEmpty(value))
            return value ?? string.Empty;
        if (_translations.TryGetValue(value, out var translated))
            return translated;
        var normalized = value.Replace("\r\n", "\n").Trim();
        if (_translations.TryGetValue(normalized, out translated))
            return translated;
        Capture(value);
        return value;
    }

    internal bool TryTranslateDisplay(string value, out string translated)
    {
        translated = value ?? string.Empty;
        if (!ModConfig.Enabled.Value || string.IsNullOrEmpty(value))
            return false;

        var exact = Translate(value);
        if (!string.Equals(value, exact, StringComparison.Ordinal))
        {
            translated = exact;
            return true;
        }

        var normalizedValue = value.Replace("\r\n", "\n");
        if (_prefixTranslations.TryGetValue(normalizedValue, out var prefixTranslation))
        {
            translated = prefixTranslation ?? string.Empty;
            return true;
        }

        foreach (var item in _orderedTranslations)
        {
            var source = item.Key;
            if (source.Length < 2)
                continue;

            var index = value.IndexOf(source, StringComparison.Ordinal);
            if (index < 0 && source.Contains('\n'))
            {
                source = source.Replace("\n", "\r\n");
                index = value.IndexOf(source, StringComparison.Ordinal);
            }
            if (index < 0)
                continue;

            translated = value.Replace(source, item.Value, StringComparison.Ordinal);
            return true;
        }

        return false;
    }

    private static Dictionary<string, string> BuildPrefixTranslations(
        Dictionary<string, string> translations
    )
    {
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in translations)
        {
            for (var length = 1; length < item.Key.Length; length++)
            {
                var prefix = item.Key.Substring(0, length);
                if (!prefixes.TryGetValue(prefix, out var existing))
                {
                    prefixes[prefix] = item.Value;
                }
                else if (!string.Equals(existing, item.Value, StringComparison.Ordinal))
                {
                    prefixes[prefix] = null;
                }
            }
        }
        return prefixes;
    }

    private void Capture(string value)
    {
        if (!ModConfig.CaptureMissing.Value || !Japanese.IsMatch(value) || !_captured.TryAdd(value, 0))
            return;
        try
        {
            lock (_captureLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_capturePath)!);
                var line = JsonSerializer.Serialize(new { text = value });
                File.AppendAllText(_capturePath, line + Environment.NewLine);
            }
        }
        catch (System.Exception exception)
        {
            _logger.LogDebug($"Could not capture untranslated text: {exception.Message}");
        }
    }
}
