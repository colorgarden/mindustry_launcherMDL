using System.Diagnostics;
using System.Text.Json;

namespace MindustryShared;

public class ConfigService
{
    private AppConfig _config = new();
    private string _configFilePath = "launcher_config.json";

    /// <summary>Set the config file path before calling LoadConfig().</summary>
    public void SetConfigFilePath(string path) => _configFilePath = path;

    public AppConfig GetConfig() => _config;

    public void LoadConfig()
    {
        if (File.Exists(_configFilePath))
        {
            try
            {
                _config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_configFilePath)) ?? new AppConfig();
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to deserialize config: {ex.Message}"); }
        }
    }

    public void SaveConfig()
    {
        try
        {
            string dir = Path.GetDirectoryName(_configFilePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(_config));
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to save config: {ex.Message}"); }
    }

    public string InitializeLanguage()
    {
        if (string.IsNullOrEmpty(_config.Language))
        {
            _config.Language = L.AutoDetect();
            SaveConfig();
        }
        return _config.Language;
    }

    public void SetLanguage(string langCode)
    {
        _config.Language = langCode;
        SaveConfig();
    }

    public string GetEffectiveLanguage(string selectedTag)
    {
        return selectedTag == "auto" ? L.AutoDetect() : selectedTag;
    }
}
