using MindustryShared;

namespace MindustryLauncher;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

#if ANDROID
        var langDir = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "Lang");
        L.LangDirPath = langDir;
        Directory.CreateDirectory(langDir);

        // Copy bundled language files to writable FilesDir on first launch.
        // Android raw resource names can only contain [a-zA-Z0-9_.], so
        // zh-CN → zh_CN, en-US → en_US in Resources/Raw, restored on copy.
        var langMappings = new[] { ("zh_CN", "zh-CN"), ("en_US", "en-US") };
        foreach (var (resName, langCode) in langMappings)
        {
            var destPath = Path.Combine(langDir, $"{langCode}.json");
            if (!File.Exists(destPath))
            {
                try
                {
                    using var stream = FileSystem.OpenAppPackageFileAsync($"{resName}.json")
                        .GetAwaiter().GetResult();
                    using var fs = File.Create(destPath);
                    stream.CopyTo(fs);
                }
                catch { /* keep empty dict fallback */ }
            }
        }
#else
        L.LangDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang");
#endif
        L.LoadLanguage(L.AutoDetect());
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
