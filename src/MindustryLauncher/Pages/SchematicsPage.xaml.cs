using Microsoft.Maui.Controls;
using MindustryShared;

namespace MindustryLauncher.Pages;

public partial class SchematicsPage : ContentPage
{
    private readonly SchematicService _schematicService;
    private readonly VersionManagementService _versions;
    private List<SchematicEntry> _allSchematics = new();
    private string _cacheDir = "";

    public SchematicsPage()
    {
        InitializeComponent();
        _schematicService = IPlatformApplication.Current!.Services.GetRequiredService<SchematicService>();
        _versions = IPlatformApplication.Current!.Services.GetRequiredService<VersionManagementService>();
        _cacheDir = Path.Combine(FileSystem.CacheDirectory, "schematics");

        // Default source is MinRi2
        SourceMinriBtn.Style = (Style)Resources["PrimaryButton"];
        SourceDesignitBtn.Style = (Style)Resources["OutlinedButton"];
    }

    private async void OnSelectMinri(object sender, EventArgs e)
    {
        _schematicService.CurrentRepo = "MinRi2/schematics-archives";
        _schematicService.CurrentBranch = "master";
        SourceMinriBtn.Style = (Style)Resources["PrimaryButton"];
        SourceDesignitBtn.Style = (Style)Resources["OutlinedButton"];
        await FetchSchematics();
    }

    private async void OnSelectDesignit(object sender, EventArgs e)
    {
        _schematicService.CurrentRepo = "DesignItOSS/Mindustry-Schematics";
        _schematicService.CurrentBranch = "main";
        SourceMinriBtn.Style = (Style)Resources["OutlinedButton"];
        SourceDesignitBtn.Style = (Style)Resources["PrimaryButton"];
        await FetchSchematics();
    }

    private async Task FetchSchematics()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        StatusLabel.Text = "Downloading repository...";

        try
        {
            // Cancel any existing fetch
            _schematicService.FetchCts?.Cancel();
            _schematicService.FetchCts = new CancellationTokenSource();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_schematicService.FetchCts.Token);

            var zipPath = _schematicService.GetCacheZipPath(_cacheDir);

            // Download if not cached
            if (!File.Exists(zipPath))
                await _schematicService.DownloadRepoZipAsync(zipPath, cts.Token);

            StatusLabel.Text = "Parsing schematics...";
            _allSchematics = SchematicService.ParseSchematicsFromZip(zipPath, cts.Token);
            SchematicList.ItemsSource = _allSchematics;
            StatusLabel.Text = $"{_allSchematics.Count} schematics loaded";
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "";
            await DisplayAlert("Error", $"Failed to fetch: {ex.Message}\nTry switching proxy node.", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.ToLowerInvariant() ?? "";
        SchematicList.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _allSchematics
            : _allSchematics.Where(s =>
                (s.RealName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (s.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
    }

    private async void OnDownload(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is SchematicEntry entry)
        {
            var instance = _versions.CurrentInstance;
            if (instance == null)
            {
                await DisplayAlert("MDL", "Select a version first!", "OK");
                return;
            }

            try
            {
                var schematicsDir = Path.Combine(instance.FullPath, "schematics");
                Directory.CreateDirectory(schematicsDir);
                var dest = Path.Combine(schematicsDir, entry.FileName);

                // Extract from cached ZIP
                var zipPath = _schematicService.GetCacheZipPath(_cacheDir);
                using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
                var zipEntry = zip.GetEntry(entry.ZipEntryFullName);
                if (zipEntry != null)
                {
                    using var stream = zipEntry.Open();
                    using var file = File.Create(dest);
                    await stream.CopyToAsync(file);
                }

                await DisplayAlert("MDL", "Schematic installed!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Install failed: {ex.Message}", "OK");
            }
        }
    }
}
