using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace MindustryShared;

public class SchematicService
{
    private readonly HttpClient _http;

    public SchematicService(HttpClient http)
    {
        _http = http;
    }

    public string CurrentRepo { get; set; } = "MinRi2/schematics-archives";
    public string CurrentBranch { get; set; } = "master";
    public List<SchematicEntry> AllOnlineSchematics { get; set; } = new();
    public SchematicEntry? SelectedSchematicToInstall { get; set; }
    public CancellationTokenSource? FetchCts { get; set; }
    public SemaphoreSlim FetchLock { get; } = new(1, 1);

    public string GetCacheZipPath(string cacheDir)
    {
        Directory.CreateDirectory(cacheDir);
        return Path.Combine(cacheDir, $"{CurrentRepo.Replace("/", "_")}.zip");
    }

    public async Task DownloadRepoZipAsync(string zipPath, CancellationToken token)
    {
        string zipUrl = UrlHelper.Format($"https://github.com/{CurrentRepo}/archive/refs/heads/{CurrentBranch}.zip");
        using var resp = await _http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, token);
        resp.EnsureSuccessStatusCode();
        using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await resp.Content.CopyToAsync(fs, token);
    }

    public static List<SchematicEntry> ParseSchematicsFromZip(string zipPath, CancellationToken token)
    {
        var list = new List<SchematicEntry>();
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (token.IsCancellationRequested) break;
            if (entry.Name.EndsWith(".msch", StringComparison.OrdinalIgnoreCase))
            {
                using var es = entry.Open();
                using var ms = new MemoryStream();
                es.CopyTo(ms);
                string desc = "";
                string? realName = ParseMschName(ms.ToArray(), out desc);
                list.Add(new SchematicEntry(realName ?? "", desc, entry.Name, entry.FullName));
            }
        }
        return list;
    }

    public static string? ParseMschName(byte[] mschBytes, out string description)
    {
        description = "";
        try
        {
            using var ms = new MemoryStream(mschBytes);
            using var reader = new BinaryReader(ms);

            if (reader.ReadByte() != 'm' || reader.ReadByte() != 's'
                || reader.ReadByte() != 'c' || reader.ReadByte() != 'h')
                return null;

            reader.ReadByte();
            ms.Seek(2, SeekOrigin.Current);

            using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
            using var deflatedMs = new MemoryStream();
            deflate.CopyTo(deflatedMs);
            deflatedMs.Position = 0;

            using var dataReader = new BinaryReader(deflatedMs);

            short ReadShort() => (short)((dataReader.ReadByte() << 8) | dataReader.ReadByte());
            string ReadString() => Encoding.UTF8.GetString(dataReader.ReadBytes(ReadShort()));

            ReadShort();
            ReadShort();
            byte tagsCount = dataReader.ReadByte();
            string? foundName = null;

            for (int i = 0; i < tagsCount; i++)
            {
                string key = ReadString();
                string val = ReadString();
                if (key == "name") foundName = StripColors(val);
                if (key == "description") description = StripColors(val);
            }

            return foundName;
        }
        catch
        {
            return null;
        }
    }

    private static string StripColors(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return Regex.Replace(input, @"\[.*?\]", "");
    }
}
