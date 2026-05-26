using System.Diagnostics;
using System.IO;

namespace mindustry_launcher
{
    public class GameLaunchService
    {
        private readonly VersionManagementService _versionService;
        private readonly ConfigService _configService;

        public GameLaunchService(VersionManagementService versionService, ConfigService configService)
        {
            _versionService = versionService;
            _configService = configService;
        }

        public static int CalculateSmartRam()
        {
            int raw = (HardwareInfo.GetTotalPhysicalMemoryMB() - 2048) / 2;
            int clamped = Math.Clamp(raw, 1024, 8192);
            return (clamped / 512) * 512;
        }

        public string GetEffectiveJavaPath()
        {
            string exe = string.IsNullOrWhiteSpace(_versionService.CurrentVersionConfig.CustomJavaPath)
                ? _configService.GetConfig().GlobalJavaPath
                : _versionService.CurrentVersionConfig.CustomJavaPath;
            return string.IsNullOrWhiteSpace(exe) ? "java" : exe;
        }

        public int GetEffectiveRamMb()
        {
            if (_versionService.CurrentVersionConfig.UseAutoRam)
                return CalculateSmartRam();
            return _versionService.CurrentVersionConfig.CustomRamMB;
        }

        public string GetDataDir(string instancePath)
        {
            return _versionService.CurrentVersionConfig.UseIsolation
                ? Path.Combine(instancePath, "data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry");
        }

        public static (string title, string advice) AnalyzeCrashLog(string log)
        {
            string advice = L.Get("crash.unknown");
            if (string.IsNullOrWhiteSpace(log))
            {
                log = L.Get("crash.no_log");
            }
            else if (log.Contains("OutOfMemoryError"))
                advice = L.Get("crash.oom");
            else if (log.Contains("UnsupportedClassVersionError"))
                advice = L.Get("crash.java_old");
            else if (log.Contains("MixinTransformationException") || log.Contains("MixinApplyError"))
                advice = L.Get("crash.mod_conflict");
            else if (log.Contains("NoSuchMethodError") || log.Contains("ClassNotFoundException"))
                advice = L.Get("crash.version_mismatch");

            string report = $"{advice}\n\n--- {L.Get("crash.log_header")} ---\n{(log.Length > 800 ? log.Substring(log.Length - 800) : log)}";
            return (L.Get("crash.title"), report);
        }

        public ProcessStartInfo BuildLaunchProcessInfo(string instancePath, string jarPath)
        {
            string exe = GetEffectiveJavaPath();
            int finalRam = GetEffectiveRamMb();
            string memArg = $"-Xmx{finalRam}m ";
            string jvmArgs = string.IsNullOrWhiteSpace(_versionService.CurrentVersionConfig.CustomJvmArgs)
                ? ""
                : _versionService.CurrentVersionConfig.CustomJvmArgs + " ";

            var pInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"{memArg}{jvmArgs}-jar \"{jarPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = instancePath
            };

            if (_versionService.CurrentVersionConfig.UseIsolation)
            {
                pInfo.EnvironmentVariables["MINDUSTRY_DATA_DIR"] = GetDataDir(instancePath);
            }

            return pInfo;
        }

        public void MarkInstanceRunning(string instancePath)
        {
            _versionService.RunningInstancePaths.Add(instancePath);
        }

        public void MarkInstanceStopped(string instancePath)
        {
            _versionService.RunningInstancePaths.Remove(instancePath);
        }

        public bool IsInstanceRunning(string instancePath)
        {
            return _versionService.RunningInstancePaths.Contains(instancePath);
        }
    }
}
