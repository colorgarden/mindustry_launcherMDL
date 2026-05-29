using Microsoft.Maui.Controls;
using MindustryShared;

namespace MindustryLauncher.Pages;

public partial class ModsPage : ContentPage
{
    private readonly ModService _modService;
    private readonly VersionManagementService _versions;
    private List<ModRegistryEntry> _allMods = new();

    public ModsPage()
    {
        InitializeComponent();
        _modService = IPlatformApplication.Current!.Services.GetRequiredService<ModService>();
        _versions = IPlatformApplication.Current!.Services.GetRequiredService<VersionManagementService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_allMods.Count == 0)
            await LoadMods();
    }

    private async Task LoadMods()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            _allMods = await _modService.FetchModRegistryAsync();
            ModList.ItemsSource = _allMods;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to fetch mod list: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnRefresh(object sender, EventArgs e) => await LoadMods();

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.ToLowerInvariant() ?? "";
        ModList.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _allMods
            : _allMods.Where(m =>
                m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || m.Author.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (m.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
    }

    private async void OnModSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ModRegistryEntry mod) return;

        string author = string.IsNullOrEmpty(mod.Author) ? "Unknown" : mod.Author;
        string desc = string.IsNullOrEmpty(mod.Description) ? "No description" : mod.Description;
        await DisplayAlert(mod.Name, $"Author: {author}\nStars: {mod.Stars}\n\n{desc}", "OK");
    }

    private async void OnInstallMod(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is ModRegistryEntry mod)
        {
            _modService.SelectedModToInstall = mod;
            var instance = _versions.CurrentInstance;
            if (instance == null)
            {
                await DisplayAlert("MDL", "Select a version first!", "OK");
                return;
            }

            try
            {
                var releases = await _modService.FetchModReleasesAsync(mod.Repo);
                if (releases == null || releases.Count == 0)
                {
                    await DisplayAlert("MDL", "No releases available for this mod.", "OK");
                    return;
                }

                var tags = releases.Select(r => r.TagName).ToArray();
                var tag = await DisplayActionSheet("Select Version", "Cancel", null, tags);
                if (string.IsNullOrEmpty(tag) || tag == "Cancel") return;

                var selected = releases.First(r => r.TagName == tag);
                var asset = selected.Assets?.FirstOrDefault(a =>
                    a.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                    && !a.Name.Contains("sources", StringComparison.OrdinalIgnoreCase));

                if (asset == null)
                {
                    await DisplayAlert("MDL", "No compiled asset in this release.", "OK");
                    return;
                }

                var modsDir = Path.Combine(instance.FullPath, "mods");
                Directory.CreateDirectory(modsDir);
                var dest = Path.Combine(modsDir, asset.Name);

                using var http = IPlatformApplication.Current!.Services.GetRequiredService<HttpClient>();
                using var resp = await http.GetAsync(asset.BrowserDownloadUrl);
                resp.EnsureSuccessStatusCode();
                await using var stream = await resp.Content.ReadAsStreamAsync();
                await using var file = File.Create(dest);
                await stream.CopyToAsync(file);

                await DisplayAlert("MDL", "Mod installed successfully!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Install failed: {ex.Message}", "OK");
            }
        }
    }

    private async void OnShowLocal(object sender, EventArgs e)
    {
        var instance = _versions.CurrentInstance;
        if (instance == null)
        {
            await DisplayAlert("MDL", "Select a version first!", "OK");
            return;
        }

        var modsDir = Path.Combine(instance.FullPath, "mods");
        var localMods = ModService.ScanMods(modsDir);

        if (localMods.Count == 0)
        {
            await DisplayAlert("Local Mods", "No mods installed.", "OK");
            return;
        }

        var names = localMods.Select(m => $"{m.DisplayName} ({m.FileSize})").ToArray();
        var choice = await DisplayActionSheet("Local Mods (tap to delete)", "Cancel", null, names);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var selected = localMods[Array.IndexOf(names, choice)];
        bool del = await DisplayAlert("Delete", $"Delete {selected.FileName}?", "Delete", "Cancel");
        if (del)
        {
            try
            {
                File.Delete(selected.FullPath);
                await DisplayAlert("MDL", "Deleted.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Delete failed: {ex.Message}", "OK");
            }
        }
    }
}
