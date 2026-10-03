
using System.Text.RegularExpressions;
using UnityEngine;

namespace PracticeMode;

internal static class SettingManager
{
    private static MelonPreferences_Category _category;
    private static MelonPreferences_Entry<bool> _autoPlay;
    private static MelonPreferences_Entry<string> _toggleKey;
    internal static bool AutoPlay => _autoPlay.Value;
    internal static KeyCode ToggleKey { get; private set; } = KeyCode.F9;

    internal static void Register()
    {
        _category = MelonPreferences.CreateCategory(Name);
        string configPath = Path.Combine(MelonEnvironment.UserDataDirectory, Name + ".cfg");
        MigrateLegacyConfig(configPath);
        _category.SetFilePath(configPath);
        _autoPlay = _category.CreateEntry("AutoPlay", true, "Preview autoplay",
            "Only applies to Euterpe preview. False allows manual playing.");
        _toggleKey = _category.CreateEntry("ToggleKey", "F9", "Toggle key",
            "Unity KeyCode name. F9 avoids Euterpe preview shortcuts F1-F8.");
        if (Enum.TryParse<KeyCode>(_toggleKey.Value, true, out var key)) ToggleKey = key;
        else MelonLogger.Warning($"Unknown ToggleKey '{_toggleKey.Value}'; using F9.");
        _category.SaveToFile(false);
    }

    private static void MigrateLegacyConfig(string configPath)
    {
        string legacyPath = Path.Combine(MelonEnvironment.UserDataDirectory, "PreviewAutoPlayToggle.cfg");
        if (File.Exists(configPath) || !File.Exists(legacyPath)) return;
        try
        {
            string content = File.ReadAllText(legacyPath);
            content = Regex.Replace(content, @"(?m)^\[PreviewAutoPlayToggle\](?=\r?$)", "[PracticeMode]");
            content = Regex.Replace(content, @"(?m)^(?:Diagnostics\s*=.*|# Log a preview state snapshot.*)(?:\r?\n|$)", "");
            File.WriteAllText(configPath, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MelonLogger.Warning("Could not migrate the previous configuration: " + ex.Message);
        }
    }

    internal static void Toggle()
    {
        _autoPlay.Value = !_autoPlay.Value;
        _category.SaveToFile(false);
    }
}
