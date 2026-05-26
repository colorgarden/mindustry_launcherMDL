using System.Net.Http;
using System.Net.Http.Json;
using System.IO;

namespace mindustry_launcher
{
    public class RemoteDownloadService
    {
        private readonly HttpClient _http;
        private readonly ConfigService _configService;

        public RemoteDownloadService(HttpClient http, ConfigService configService)
        {
            _http = http;
            _configService = configService;
        }

        public string CurrentDownloadRepo { get; set; } = "Anuken/Mindustry";

        public async Task<List<GitHubRelease>> FetchFilteredReleasesAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string apiUrl = UrlHelper.Format($"https://api.github.com/repos/{CurrentDownloadRepo}/releases", true);
            var rels = await _http.GetFromJsonAsync<List<GitHubRelease>>(apiUrl, cts.Token);

            if (rels == null) return new List<GitHubRelease>();

            return rels.Where(r =>
                r.Assets != null && r.Assets.Any(a =>
                    a.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                    && !a.Name.Contains("server", StringComparison.OrdinalIgnoreCase)
                    && !a.Name.Contains("android", StringComparison.OrdinalIgnoreCase)
                    && !a.Name.Contains("dependencies", StringComparison.OrdinalIgnoreCase)
                    && !a.Name.Contains("javadoc", StringComparison.OrdinalIgnoreCase)
                    && !a.Name.Contains("sources", StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        public static GitHubAsset? SelectBestAsset(List<GitHubAsset>? candidates, string repo)
        {
            if (candidates == null || candidates.Count == 0) return null;

            if (repo.Contains("antigrief", StringComparison.OrdinalIgnoreCase))
                return SelectFooAsset(candidates);

            var asset = candidates.FirstOrDefault(a => a.Name.Equals("Mindustry.jar", StringComparison.OrdinalIgnoreCase));
            if (asset != null) return asset;

            asset = candidates.FirstOrDefault(a =>
                a.Name.Contains("desktop", StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains("Desktop")
                || a.Name.Contains("client", StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains("windows", StringComparison.OrdinalIgnoreCase));

            if (asset != null) return asset;

            var nonModAssets = candidates.Where(a =>
                !a.Name.Contains("mod", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("addon", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("plugin", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            return nonModAssets.Count > 0 ? nonModAssets[0] : candidates[0];
        }

        private static GitHubAsset? SelectFooAsset(List<GitHubAsset> candidates)
        {
            var audio = candidates.FirstOrDefault(a =>
                a.Name.Contains("audio", StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains("voice", StringComparison.OrdinalIgnoreCase));

            var standard = candidates.FirstOrDefault(a =>
                (a.Name.Contains("desktop", StringComparison.OrdinalIgnoreCase)
                 || a.Name.Contains("client", StringComparison.OrdinalIgnoreCase))
                && !a.Name.Contains("audio", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("voice", StringComparison.OrdinalIgnoreCase));

            if (standard == null)
                standard = candidates.FirstOrDefault(a =>
                    !a.Name.Contains("audio", StringComparison.OrdinalIgnoreCase)
                    && !a.Name.Contains("voice", StringComparison.OrdinalIgnoreCase));

            // Returns both; caller decides
            return standard ?? audio;
        }

        public static GitHubAsset? SelectFooAudioAsset(List<GitHubAsset> candidates)
        {
            return candidates.FirstOrDefault(a =>
                a.Name.Contains("audio", StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains("voice", StringComparison.OrdinalIgnoreCase));
        }

        public string GetDownloadFolderName(string tagName)
        {
            string suffix = "";
            if (CurrentDownloadRepo.Contains("TinyLake", StringComparison.OrdinalIgnoreCase))
                suffix = L.Get("download.suffix_x");
            else if (CurrentDownloadRepo.Contains("antigrief", StringComparison.OrdinalIgnoreCase))
                suffix = L.Get("download.suffix_foo");

            string folder = Path.Combine(_configService.GetConfig().ManagedFolders[0], "Versions", tagName + suffix);

            int c = 1;
            string baseF = folder;
            while (Directory.Exists(folder))
                folder = $"{baseF}-{c++}";

            return folder;
        }

        public async Task DownloadFileAsync(string url, string path, IProgress<double> prog)
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var total = resp.Content.Headers.ContentLength ?? -1L;
            using var rs = await resp.Content.ReadAsStreamAsync();
            using var ws = File.Open(path, FileMode.Create);
            var buf = new byte[8192];
            long read = 0;
            int r;
            while ((r = await rs.ReadAsync(buf, 0, buf.Length)) != 0)
            {
                await ws.WriteAsync(buf, 0, r);
                read += r;
                if (total != -1)
                    prog.Report((double)read / total * 100);
            }
        }

        public static List<GitHubAsset> FilterClientAssets(GitHubRelease rel)
        {
            return rel.Assets?.Where(a =>
                a.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("server", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("android", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("dependencies", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("javadoc", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("sources", StringComparison.OrdinalIgnoreCase)
            ).ToList() ?? new List<GitHubAsset>();
        }
    }
}
