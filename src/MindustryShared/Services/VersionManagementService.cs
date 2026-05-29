using System.Diagnostics;
using System.Text.Json;

namespace MindustryShared;

public class VersionManagementService
{
    private readonly ConfigService _config;

    public VersionManagementService(ConfigService config)
    {
        _config = config;
    }

    public GameInstanceInfo? CurrentInstance { get; set; }
    public HashSet<string> RunningInstancePaths { get; } = new(StringComparer.OrdinalIgnoreCase);
    public VersionConfig CurrentVersionConfig { get; set; } = new();

    public List<GameInstanceInfo> GetAllInstalledInstances()
    {
        var all = new List<GameInstanceInfo>();
        foreach (var root in _config.GetConfig().ManagedFolders)
        {
            all.AddRange(GetInstancesInFolder(root));
        }
        return all;
    }

    public static List<GameInstanceInfo> GetInstancesInFolder(string root)
    {
        var list = new List<GameInstanceInfo>();
        if (!Directory.Exists(root)) return list;

        string vDir = Path.Combine(root, "Versions");
        if (!Directory.Exists(vDir)) return list;

        foreach (var d in Directory.GetDirectories(vDir))
        {
            if (File.Exists(Path.Combine(d, "Mindustry.jar")))
                list.Add(new GameInstanceInfo { Name = Path.GetFileName(d), FullPath = d });
        }
        return list;
    }

    public void LoadVersionConfig(string instancePath)
    {
        string configPath = Path.Combine(instancePath, "mdl_instance_config.json");
        if (File.Exists(configPath))
        {
            try
            {
                CurrentVersionConfig = JsonSerializer.Deserialize<VersionConfig>(File.ReadAllText(configPath)) ?? new();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load version config: {ex.Message}");
                CurrentVersionConfig = new();
            }
        }
        else
        {
            CurrentVersionConfig = new();
            CurrentVersionConfig.CustomRamMB = _config.GetConfig().GlobalRamMB;
        }
    }

    public static void SaveVersionConfigToFile(string instancePath, VersionConfig config)
    {
        string configPath = Path.Combine(instancePath, "mdl_instance_config.json");
        try { File.WriteAllText(configPath, JsonSerializer.Serialize(config)); }
        catch (Exception ex) { Debug.WriteLine($"Failed to save version config: {ex.Message}"); }
    }

    public bool IsInstanceRunning(string instancePath)
    {
        return RunningInstancePaths.Contains(instancePath);
    }
}
