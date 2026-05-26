using System.Text.Json.Serialization;

namespace mindustry_launcher;

// ========== 配置相关 ==========
public class AppConfig
{
    public List<string> ManagedFolders { get; set; } = new();
    public string GlobalJavaPath { get; set; } = "";
    public string LastSelectedInstancePath { get; set; } = "";
    public int ProxyNodeIndex { get; set; } = 1;
    public int GlobalRamMB { get; set; } = 4096;
    public bool GlobalUseAutoRam { get; set; } = true;
    public string Language { get; set; } = "";
    public string PlayerNickname { get; set; } = "Mindustry玩家";
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
}

public class VersionConfig
{
    public bool UseIsolation { get; set; } = true;
    public string CustomJavaPath { get; set; } = "";
    public string CustomJvmArgs { get; set; } = "";
    public int CustomRamMB { get; set; } = 4096;
    public bool UseAutoRam { get; set; } = true;
}

// ========== 游戏实例 ==========
public class GameInstanceInfo
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    [JsonIgnore] public string UI_DeleteText => L.Get("version.delete");
}

// ========== Mod 相关 ==========
public class ModInfo
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string FileSize { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public System.Windows.Media.ImageSource? IconImage { get; set; }
    public string UI_Name => string.IsNullOrEmpty(DisplayName) ? FileName : DisplayName;
    public string UI_Author => string.IsNullOrEmpty(Author) ? "未知作者" : $"作者: {Author}";
    [JsonIgnore] public string UI_DeleteText => L.Get("mods.delete");
}

public class ModRegistryEntry
{
    [JsonPropertyName("repo")] public string Repo { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("author")] public string Author { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("stars")] public int Stars { get; set; }
    [JsonIgnore] public string AuthorFormatted => $"作者: {Author}";
    [JsonIgnore] public string StarsFormatted => $"★ {Stars}";
    [JsonIgnore] public string UI_InstallText => L.Get("mods.install");
    [JsonIgnore] public string UI_RightClickHint => L.Get("mods.right_click_hint");
    [JsonIgnore] public string IconUrl => UrlHelper.Format($"https://raw.githubusercontent.com/{Repo}/master/icon.png");
}

// ========== GitHub API ==========
public class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
    [JsonPropertyName("body")] public string? Body { get; set; }
    [JsonPropertyName("assets")] public List<GitHubAsset>? Assets { get; set; }
    [JsonIgnore] public string UI_DownloadText => L.Get("download.download");
    [JsonIgnore] public string UI_AvailableHint => L.Get("download.available_versions");
    [JsonIgnore] public string UI_RightClickHint => L.Get("mods.right_click_hint");
    public override string ToString() => TagName;
}

public class GitHubAsset
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
}

public class GitHubTreeResponse
{
    [JsonPropertyName("tree")] public List<GitHubTreeItem>? Tree { get; set; }
}

public class GitHubTreeItem
{
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
}

// ========== 蓝图 ==========
public record SchematicEntry(string RealName, string Description, string FileName, string ZipEntryFullName)
{
    public string UI_Name => string.IsNullOrEmpty(RealName) ? FileName : RealName;
    public string UI_Description => string.IsNullOrEmpty(Description) ? "暂无描述" : Description;
    [JsonIgnore] public string UI_DownloadText => L.Get("schematics.download");
}

// ========== Java ==========
public class JavaInfo
{
    public string Path { get; set; } = "";
    public string Version { get; set; } = "";
    public int VersionNumber { get; set; }
}

// ========== 存档 ==========
public class MindustrySaveMetadata
{
    public string MapName { get; set; } = "未知地图";
    public string Wave { get; set; } = "-";
    public string Version { get; set; } = "-";
    public string Author { get; set; } = "未知作者";
    public string Description { get; set; } = "无描述";
    public string PlayTime { get; set; } = "00:00";
}

public class SettingItem
{
    public string Key { get; set; } = "";
    public object OriginalValue { get; set; } = new();
    public string DisplayValue { get; set; } = "";
    public byte Type { get; set; }
    public bool IsBinary => Type == 5;
}

// ========== 联机 ==========
public class RoomPlayerInfo
{
    public string IP { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastSeen { get; set; }
}
