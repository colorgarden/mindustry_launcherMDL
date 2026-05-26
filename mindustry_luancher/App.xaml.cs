using System;
using System.Diagnostics;
using System.IO;
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
            // 提前加载语言文件，确保崩溃处理也能使用正确的语言
            try
            {
                string langPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang");
                if (Directory.Exists(langPath))
                {
                    var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");
                    string lang = "";
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
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to load language on startup: {ex.Message}"); }

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                string log = $"崩溃时间: {DateTime.Now}\n异常: {ex.Message}\n堆栈:\n{ex.StackTrace}";
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), log); } catch (Exception ex2) { Debug.WriteLine($"Failed to write crash log: {ex2.Message}"); }
                MessageBox.Show(L.T("crash.fatal_msg", ex.Message, ex.StackTrace), L.Get("crash.fatal_title"));
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                string log = $"UI崩溃时间: {DateTime.Now}\n异常: {args.Exception.Message}\n堆栈:\n{args.Exception.StackTrace}";
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), log); } catch (Exception ex2) { Debug.WriteLine($"Failed to write UI crash log: {ex2.Message}"); }
                MessageBox.Show(L.T("crash.ui_fatal_msg", args.Exception.Message, args.Exception.StackTrace), L.Get("crash.fatal_title"));
                args.Handled = true;
            };

            ConfigureServices();

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
    }
}
