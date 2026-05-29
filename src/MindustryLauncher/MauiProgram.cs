using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using MindustryShared;

namespace MindustryLauncher;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("font.ttf", "GameFont");
            });

        // Services
        builder.Services.AddSingleton<HttpClient>(_ =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
            client.DefaultRequestHeaders.Add("User-Agent", "MDL-Mobile/0.3");
            return client;
        });

        builder.Services.AddSingleton<ConfigService>();
        builder.Services.AddSingleton<VersionManagementService>();
        builder.Services.AddSingleton<RemoteDownloadService>();
        builder.Services.AddSingleton<ModService>();
        builder.Services.AddSingleton<SchematicService>();

        return builder.Build();
    }
}
