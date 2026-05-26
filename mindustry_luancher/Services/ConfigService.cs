using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace mindustry_launcher
{
    public class ConfigService
    {
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");
        private AppConfig _config = new();

        public AppConfig GetConfig() => _config;

        public void LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    _config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFilePath)) ?? new AppConfig();
                }
                catch (Exception ex) { Debug.WriteLine($"Failed to deserialize config: {ex.Message}"); }
            }
        }

        public void SaveConfig()
        {
            try { File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(_config)); }
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

        public void SaveWindowState(double width, double height, double left, double top, double javaRam, string javaPath, string nickname)
        {
            _config.WindowWidth = width;
            _config.WindowHeight = height;
            _config.WindowLeft = left;
            _config.WindowTop = top;
            _config.GlobalRamMB = (int)javaRam;
            _config.GlobalJavaPath = javaPath;
            _config.PlayerNickname = nickname;
            SaveConfig();
        }
    }
}
