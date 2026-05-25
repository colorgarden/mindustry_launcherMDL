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
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                string log = $"崩溃时间: {DateTime.Now}\n异常: {ex.Message}\n堆栈:\n{ex.StackTrace}";
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), log); } catch { }
                MessageBox.Show($"程序崩溃！详细原因：\n\n{ex.Message}\n\n堆栈追踪：\n{ex.StackTrace}", "致命错误");
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                string log = $"UI崩溃时间: {DateTime.Now}\n异常: {args.Exception.Message}\n堆栈:\n{args.Exception.StackTrace}";
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), log); } catch { }
                MessageBox.Show($"UI线程崩溃！详细原因：\n\n{args.Exception.Message}\n\n堆栈追踪：\n{args.Exception.StackTrace}", "致命错误");
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
