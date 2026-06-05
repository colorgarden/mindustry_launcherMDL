using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace mindustry_launcher
{
    public class ModService
    {
        private readonly HttpClient _http;
        private readonly VersionManagementService _versionService;

        public ModService(HttpClient http, VersionManagementService versionService)
        {
            _http = http;
            _versionService = versionService;
        }

        public List<ModRegistryEntry> AllOnlineMods { get; set; } = new();
        public ModRegistryEntry? SelectedModToInstall { get; set; }

        public async Task<List<ModRegistryEntry>> FetchModRegistryAsync()
        {
            string url = UrlHelper.Format("https://raw.githubusercontent.com/Anuken/MindustryMods/master/mods.json", false);
            var list = await _http.GetFromJsonAsync<List<ModRegistryEntry>>(url);
            if (list != null)
                AllOnlineMods = list.OrderByDescending(m => m.Stars).ToList();
            return AllOnlineMods;
        }

        public async Task<List<GitHubRelease>?> FetchModReleasesAsync(string repo)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string apiUrl = UrlHelper.Format($"https://api.github.com/repos/{repo}/releases", true);
            return await _http.GetFromJsonAsync<List<GitHubRelease>>(apiUrl, cts.Token);
        }

        public static string GetModsDir(GameInstanceInfo? instance, VersionConfig? versionConfig)
        {
            if (instance == null || versionConfig == null)
                return "";
            string data = versionConfig.UseIsolation
                ? Path.Combine(instance.FullPath, "data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry");
            return Path.Combine(data, "mods");
        }

        public static List<ModInfo> ScanMods(string modsDir)
        {
            var list = new List<ModInfo>();
            if (!Directory.Exists(modsDir))
                return list;

            var files = new DirectoryInfo(modsDir).GetFiles()
                .Where(f => f.Extension.Equals(".jar", StringComparison.OrdinalIgnoreCase)
                         || f.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var f in files)
            {
                var info = new ModInfo { FileName = f.Name, FullPath = f.FullName, FileSize = $"{(f.Length / 1024.0):F2} KB" };
                ParseModArchive(info);
                list.Add(info);
            }
            return list;
        }

        public static void ParseModArchive(ModInfo info)
        {
            try
            {
                using var stream = File.OpenRead(info.FullPath);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

                var iconEntry = zip.Entries.FirstOrDefault(e => e.Name.Equals("icon.png", StringComparison.OrdinalIgnoreCase));
                if (iconEntry != null)
                {
                    using var iconStream = iconEntry.Open();
                    using var ms = new MemoryStream();
                    iconStream.CopyTo(ms);
                    ms.Position = 0;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    info.IconImage = bitmap;
                }

                var metaEntry = zip.Entries.FirstOrDefault(e =>
                    e.Name.Equals("mod.json", StringComparison.OrdinalIgnoreCase)
                    || e.Name.Equals("mod.hjson", StringComparison.OrdinalIgnoreCase));

                if (metaEntry != null)
                {
                    using var metaStream = metaEntry.Open();
                    using var reader = new StreamReader(metaStream);
                    string content = reader.ReadToEnd();
                    try
                    {
                        var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                        using var doc = JsonDocument.Parse(content, options);
                        var root = doc.RootElement;
                        string GetJsonString(string key) => root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? "" : "";
                        string name = GetJsonString("displayName");
                        if (string.IsNullOrEmpty(name)) name = GetJsonString("name");
                        info.DisplayName = StripColors(name);
                        info.Author = StripColors(GetJsonString("author"));
                        info.Description = StripColors(GetJsonString("description"));
                        info.Version = StripColors(GetJsonString("version"));
                    }
                    catch
                    {
                        info.DisplayName = StripColors(ExtractHjsonValue(content, "displayName") ?? ExtractHjsonValue(content, "name") ?? "");
                        info.Author = StripColors(ExtractHjsonValue(content, "author") ?? "");
                        string desc = ExtractHjsonValue(content, "description") ?? "";
                        info.Description = StripColors(desc).Replace("\\n", "\n");
                        info.Version = StripColors(ExtractHjsonValue(content, "version") ?? "");
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to parse mod hjson: {ex.Message}"); }
        }

        public static string? ExtractHjsonValue(string content, string key)
        {
            var match = Regex.Match(content, $@"""?{key}""?\s*:\s*([^""\r\n]+|""([^""]*)"")", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string val = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Value.Trim();
                return val.TrimEnd(',').Trim();
            }
            return null;
        }

        public static string StripColors(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return Regex.Replace(input, @"\[.*?\]", "");
        }
    }
}
