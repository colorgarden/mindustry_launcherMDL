using Microsoft.Maui.Controls;
using MindustryShared;

namespace MindustryLauncher.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly ConfigService _config;

    public SettingsPage()
    {
        InitializeComponent();
        _config = IPlatformApplication.Current!.Services.GetRequiredService<ConfigService>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshUI();
    }

    private void RefreshUI()
    {
        var cfg = _config.GetConfig();

        ProxyPicker.SelectedIndex = Math.Clamp(cfg.ProxyNodeIndex, 0, 5);
        UrlHelper.ProxyIndex = cfg.ProxyNodeIndex;

        LanguagePicker.SelectedIndex = cfg.Language switch
        {
            "zh-CN" => 1,
            "en-US" => 2,
            _ => 0
        };

        NicknameEntry.Text = cfg.PlayerNickname;
        FolderList.ItemsSource = cfg.ManagedFolders;
    }

    private void OnProxyChanged(object sender, EventArgs e)
    {
        _config.GetConfig().ProxyNodeIndex = ProxyPicker.SelectedIndex;
        UrlHelper.ProxyIndex = ProxyPicker.SelectedIndex;
        _config.SaveConfig();
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        var lang = LanguagePicker.SelectedIndex switch
        {
            1 => "zh-CN",
            2 => "en-US",
            _ => "auto"
        };

        if (lang == "auto")
        {
            _config.GetConfig().Language = "";
            L.LoadLanguage(L.AutoDetect());
        }
        else
        {
            _config.GetConfig().Language = lang;
            L.LoadLanguage(lang);
        }

        _config.SaveConfig();
    }

    private void OnNicknameChanged(object sender, TextChangedEventArgs e)
    {
        _config.GetConfig().PlayerNickname = e.NewTextValue ?? "";
        _config.SaveConfig();
    }

    private async void OnAddFolder(object sender, EventArgs e)
    {
        var path = await DisplayPromptAsync("Add Folder",
            "Enter a root folder path for game storage:", "Add", "Cancel");
        if (!string.IsNullOrWhiteSpace(path)
            && !_config.GetConfig().ManagedFolders.Contains(path))
        {
            _config.GetConfig().ManagedFolders.Add(path);
            _config.SaveConfig();
            RefreshUI();
        }
    }

    private async void OnOpenWiki(object sender, EventArgs e)
    {
        try
        {
            await Browser.Default.OpenAsync("https://mdt.zone", BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            await DisplayAlert("Error", "Cannot open link.", "OK");
        }
    }
}
