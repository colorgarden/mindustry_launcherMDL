using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace mindustry_launcher
{
    public partial class App : Application
    {
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
                        catch { }
                    }
                    if (string.IsNullOrEmpty(lang))
                        lang = L.AutoDetect();
                    L.LoadLanguage(lang);
                }
            }
            catch { }

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                string log = $"崩溃时间: {DateTime.Now}\n异常: {ex.Message}\n堆栈:\n{ex.StackTrace}";
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), log); } catch { }
                MessageBox.Show(L.T("crash.fatal_msg", ex.Message, ex.StackTrace), L.Get("crash.fatal_title"));
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                string log = $"UI崩溃时间: {DateTime.Now}\n异常: {args.Exception.Message}\n堆栈:\n{args.Exception.StackTrace}";
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), log); } catch { }
                MessageBox.Show(L.T("crash.ui_fatal_msg", args.Exception.Message, args.Exception.StackTrace), L.Get("crash.fatal_title"));
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
