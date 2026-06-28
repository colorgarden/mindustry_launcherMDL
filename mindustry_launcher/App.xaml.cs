using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace mindustry_launcher
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 强制使用独立显卡（Windows GPU 偏好注册表，重启后生效）
            EnsureHighPerformanceGpu();

            // 提前加载语言文件，确保崩溃处理也能使用正确的语言
            try
            {
                string lang = "";
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");
                if (File.Exists(configPath))
                {
                    try
                    {
                        var json = File.ReadAllText(configPath);
                        var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("Language", out var prop))
                            lang = prop.GetString() ?? "";
                    }
                    catch (Exception ex) { Debug.WriteLine($"Failed to parse config for language detection: {ex.Message}"); }
                }
                if (string.IsNullOrEmpty(lang))
                    lang = L.AutoDetect();
                L.LoadLanguage(lang);
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to load language on startup: {ex.Message}"); }

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                string log = L.T("crash.log_format", DateTime.Now, ex.Message, ex.StackTrace);
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), log); } catch (Exception ex2) { Debug.WriteLine($"Failed to write crash log: {ex2.Message}"); }
                MessageBox.Show(L.T("crash.fatal_msg", ex.Message, ex.StackTrace), L.Get("crash.fatal_title"));
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                string log = L.T("crash.ui_log_format", DateTime.Now, args.Exception.Message, args.Exception.StackTrace);
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), log); } catch (Exception ex2) { Debug.WriteLine($"Failed to write UI crash log: {ex2.Message}"); }
                MessageBox.Show(L.T("crash.ui_fatal_msg", args.Exception.Message, args.Exception.StackTrace), L.Get("crash.fatal_title"));
                args.Handled = true;
            };

            ConfigureServices();
            SmoothScrollHelper.Initialize();

            base.OnStartup(e);
        }

        private static void ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<HttpClient>(sp =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (m, c, ch, e2) => true
                };
                var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                return client;
            });

            services.AddSingleton<ConfigService>();
            services.AddSingleton<VersionManagementService>();
            services.AddSingleton<GameLaunchService>();
            services.AddSingleton<RemoteDownloadService>();
            services.AddSingleton<ModService>();
            services.AddSingleton<SchematicService>();
            services.AddSingleton<MultiplayerService>();

            Services = services.BuildServiceProvider();
        }

        // 通过 Windows GPU 偏好注册表强制使用高性能显卡
        // 参考 HMCL: 导出 NvOptimusEnablement=1 / AmdPowerXpressRequestHighPerformance=1
        // 但 .NET 无法直接导出原生符号，改用 Windows 10+ 官方 GPU 偏好 API
        public static void EnsureHighPerformanceGpu()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;
                exePath = Path.GetFullPath(exePath);

                const string keyPath = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences";
                var existing = Registry.GetValue(keyPath, exePath, null) as string;
                if (!string.Equals(existing, "GpuPreference=2;", StringComparison.Ordinal))
                {
                    Registry.SetValue(keyPath, exePath, "GpuPreference=2;", RegistryValueKind.String);
                    // 设置后自动重启，新进程由系统分配到高性能 GPU
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                    });
                    Environment.Exit(0);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"GPU preference setup failed: {ex.Message}"); }
        }
    }
}
