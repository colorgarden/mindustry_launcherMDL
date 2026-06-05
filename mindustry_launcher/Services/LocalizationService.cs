using System.Globalization;
using System.IO;
using System.Text.Json;

namespace mindustry_launcher;

public static class L
{
    private static Dictionary<string, string> _strings = new();
    private static string _currentLang = "zh-CN";

    public static string CurrentLang => _currentLang;

    public static event Action? LanguageChanged;

    public static string Get(string key)
    {
        if (_strings.TryGetValue(key, out var val)) return val;
        return $"[[{key}]]";
    }

    public static string T(string key, params object[] args)
    {
        var template = Get(key);
        try { return string.Format(template, args); }
        catch { return template; }
    }

    public static void LoadLanguage(string langCode)
    {
        string path = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Lang", $"{langCode}.json");

        if (!File.Exists(path))
        {
            langCode = "zh-CN";
            path = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Lang", "zh-CN.json");
        }

        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
        }

        _currentLang = langCode;
        LanguageChanged?.Invoke();
    }

    public static string AutoDetect()
    {
        var culture = CultureInfo.CurrentUICulture;
        return culture.Name switch
        {
            string s when s.StartsWith("zh") => "zh-CN",
            _ => "en-US"
        };
    }
}
