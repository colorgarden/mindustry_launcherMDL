using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace mindustry_launcher
{
    public class GameLaunchService
    {
        // 注册 Javaw.exe 到 Windows GPU 偏好，让 Mindustry 进程使用独显
        public static void RegisterGpuPreference(string exePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exePath) || exePath == "java" || !File.Exists(exePath)) return;
                exePath = Path.GetFullPath(exePath);
                const string key = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences";
                var v = Registry.GetValue(key, exePath, null) as string;
                if (!string.Equals(v, "GpuPreference=2;", StringComparison.Ordinal))
                    Registry.SetValue(key, exePath, "GpuPreference=2;", RegistryValueKind.String);
            }
            catch { /* best-effort */ }
        }
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
            RegisterGpuPreference(exe);  // Windows GPU 偏好注册表
            NvidiaProfile.CreateProfile(exe);  // NVIDIA Control Panel Profile（驱动级强制）
            int finalRam = GetEffectiveRamMb();
            string memArg = $"-Xmx{finalRam}m ";
            // 默认高性能 JVM 参数（用户自定义优先）
            string defaultJvm = "-XX:+UseG1GC -XX:MaxGCPauseMillis=75 -XX:+ParallelRefProcEnabled -XX:+DisableExplicitGC ";
            string jvmArgs = string.IsNullOrWhiteSpace(_versionService.CurrentVersionConfig.CustomJvmArgs)
                ? defaultJvm
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

            // 强制 NVIDIA 接管 OpenGL（禁止 Intel 核显抢上下文）
            pInfo.EnvironmentVariables["__GL_SYNC_TO_VBLANK"] = "0";
            pInfo.EnvironmentVariables["__GL_THREADED_OPTIMIZATIONS"] = "1";
            pInfo.EnvironmentVariables["__GL_SHADER_DISK_CACHE"] = "1";
            // AMD 等效变量
            pInfo.EnvironmentVariables["RADEON_TELEMETRY_DISABLE"] = "1";

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
