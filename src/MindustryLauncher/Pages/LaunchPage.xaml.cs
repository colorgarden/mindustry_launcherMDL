using System.Diagnostics;
using Microsoft.Maui.Controls;
using MindustryShared;

namespace MindustryLauncher.Pages;

public partial class LaunchPage : ContentPage
{
    private readonly ConfigService _config;
    private readonly VersionManagementService _versions;
    private readonly RemoteDownloadService _downloader;

    public LaunchPage()
    {
        InitializeComponent();

        _config = IPlatformApplication.Current!.Services.GetRequiredService<ConfigService>();
        _versions = IPlatformApplication.Current!.Services.GetRequiredService<VersionManagementService>();
        _downloader = IPlatformApplication.Current!.Services.GetRequiredService<RemoteDownloadService>();

        _config.SetConfigFilePath(Path.Combine(FileSystem.AppDataDirectory, "launcher_config.json"));
        _config.LoadConfig();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshUI();
    }

    private void RefreshUI()
    {
        var instances = _versions.GetAllInstalledInstances();
        VersionList.ItemsSource = instances;
        VersionNameText.Text = _versions.CurrentInstance?.Name ?? "No version selected";
        StatusLabel.Text = $"{instances.Count} version(s) installed";
    }

    private void OnVersionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is GameInstanceInfo instance)
        {
            _versions.CurrentInstance = instance;
            _versions.LoadVersionConfig(instance.FullPath);
            VersionNameText.Text = instance.Name;
        }
    }

    private async void OnLaunchClicked(object sender, EventArgs e)
    {
        var instance = _versions.CurrentInstance;
        if (instance == null)
        {
            await DisplayAlert("MDL", "Please select a version first!", "OK");
            return;
        }

        if (_versions.IsInstanceRunning(instance.FullPath))
        {
            await DisplayAlert("MDL", "This instance is already running!", "OK");
            return;
        }

        try
        {
#if ANDROID
            var intent = new Android.Content.Intent("io.colorgarden.mdl.LAUNCH_GAME");
            intent.PutExtra("version_path", instance.FullPath);
            intent.PutExtra("version_name", instance.Name);
            intent.PutExtra("isolated", _versions.CurrentVersionConfig.UseIsolation);
            Android.App.Application.Context.StartActivity(intent);
#else
            await DisplayAlert("MDL", "Game launching is only supported on Android.", "OK");
            return;
#endif

            _versions.RunningInstancePaths.Add(instance.FullPath);
            StatusLabel.Text = $"Running: {instance.Name}";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to launch: {ex.Message}", "OK");
            Debug.WriteLine($"Launch failed: {ex}");
        }
    }

    private async void OnDownloadNew(object sender, EventArgs e)
    {
        string repo = await DisplayActionSheet("Download Source", "Cancel", null,
            "Official (Anuke)", "X-Client (TinyLake)", "Foo-Client (Antigrief)");

        if (string.IsNullOrEmpty(repo) || repo == "Cancel") return;

        _downloader.CurrentDownloadRepo = repo switch
        {
            "Official (Anuke)" => "Anuken/Mindustry",
            "X-Client (TinyLake)" => "TinyLake/Mindustry",
            "Foo-Client (Antigrief)" => "Antigrief/Mindustry",
            _ => "Anuken/Mindustry"
        };

        await FetchAndShowReleases();
    }

    private async Task FetchAndShowReleases()
    {
        DownloadPanel.IsVisible = true;
        DownloadStatusText.Text = "Fetching versions...";
        DownloadProgress.Progress = 0;

        try
        {
            var releases = await _downloader.FetchFilteredReleasesAsync();
            if (releases.Count == 0)
            {
                await DisplayAlert("MDL", "No applicable client files found.", "OK");
                return;
            }

            var tag = await DisplayActionSheet("Select Version", "Cancel", null,
                releases.Select(r => r.TagName).ToArray());

            if (string.IsNullOrEmpty(tag) || tag == "Cancel") return;

            var selected = releases.First(r => r.TagName == tag);
            var asset = RemoteDownloadService.SelectBestAsset(selected.Assets!, _downloader.CurrentDownloadRepo);
            if (asset == null)
            {
                await DisplayAlert("MDL", "Cannot determine which file to download.", "OK");
                return;
            }

            var root = _config.GetConfig().ManagedFolders.FirstOrDefault()
                       ?? Path.Combine(FileSystem.AppDataDirectory, "Versions");
            var folder = _downloader.GetDownloadFolderName(tag, root);

            DownloadStatusText.Text = "Downloading...";
            var progress = new Progress<double>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() => DownloadProgress.Progress = p / 100);
            });
            await _downloader.DownloadFileAsync(asset.BrowserDownloadUrl,
                Path.Combine(folder, "Mindustry.jar"), progress);

            await DisplayAlert("MDL", "Download successful!", "OK");
            RefreshUI();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Download failed: {ex.Message}", "OK");
        }
        finally
        {
            DownloadPanel.IsVisible = false;
        }
    }

    private async void OnImport(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Mindustry.jar",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/java-archive", "application/octet-stream" } },
                })
            });

            if (result == null) return;

            string name = await DisplayPromptAsync("Import", "Enter version name:", "Import", "Cancel",
                placeholder: "e.g., Mindustry v146");
            if (string.IsNullOrWhiteSpace(name)) return;

            var root = _config.GetConfig().ManagedFolders.FirstOrDefault()
                       ?? Path.Combine(FileSystem.AppDataDirectory, "Versions");
            var destDir = Path.Combine(root, name);
            Directory.CreateDirectory(destDir);
            var srcStream = await result.OpenReadAsync();
            using var destStream = File.Create(Path.Combine(destDir, "Mindustry.jar"));
            await srcStream.CopyToAsync(destStream);

            await DisplayAlert("MDL", $"Version imported: {name}", "OK");
            RefreshUI();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Import failed: {ex.Message}", "OK");
        }
    }

    private async void OnVersionSettings(object sender, EventArgs e)
    {
        if (_versions.CurrentInstance == null)
        {
            await DisplayAlert("MDL", "Select a version first!", "OK");
            return;
        }
        // Will navigate to VSettingsPage once built
        await DisplayAlert("MDL", "Version settings coming soon.", "OK");
    }

    private async void OnOpenFolder(object sender, EventArgs e)
    {
        if (_versions.CurrentInstance == null) return;
        // Open file browser on the version folder (platform-specific)
        await DisplayAlert("MDL", $"Path: {_versions.CurrentInstance.FullPath}", "OK");
    }

    private async void OnManageToolbar(object sender, EventArgs e)
    {
        string action = await DisplayActionSheet("Manage", "Cancel", null,
            "Add folder", "Delete version", "Rename version");

        if (action == "Add folder")
        {
            var result = await DisplayPromptAsync("Add Folder",
                "Enter a root folder path for game storage:", "Add", "Cancel");
            if (!string.IsNullOrWhiteSpace(result) && !_config.GetConfig().ManagedFolders.Contains(result))
            {
                _config.GetConfig().ManagedFolders.Add(result);
                _config.SaveConfig();
                RefreshUI();
            }
        }
        else if (action == "Delete version" && _versions.CurrentInstance != null)
        {
            bool delete = await DisplayAlert("Delete",
                $"Delete {_versions.CurrentInstance.Name}?\nThis is irreversible!",
                "Delete", "Cancel");
            if (delete)
            {
                try
                {
                    Directory.Delete(_versions.CurrentInstance.FullPath, true);
                    _versions.CurrentInstance = null;
                    RefreshUI();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
                }
            }
        }
        else if (action == "Rename version" && _versions.CurrentInstance != null)
        {
            string name = await DisplayPromptAsync("Rename", "New name:", "OK", "Cancel",
                initialValue: _versions.CurrentInstance.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                var parent = Path.GetDirectoryName(_versions.CurrentInstance.FullPath)!;
                var newPath = Path.Combine(parent, name);
                Directory.Move(_versions.CurrentInstance.FullPath, newPath);
                _versions.CurrentInstance = new GameInstanceInfo { Name = name, FullPath = newPath };
                RefreshUI();
            }
        }
    }
}
