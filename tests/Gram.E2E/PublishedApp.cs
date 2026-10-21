using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Playwright;

namespace Gram.E2E;

/// <summary>
/// Serves the published app the way GitHub Pages does (static files, index.html for unknown paths) and starts a browser.
/// Locally the installed Edge is used; CI sets PLAYWRIGHT_CHANNEL to an empty value for Playwright's own Chromium.
/// </summary>
public sealed class PublishedApp : IAsyncLifetime
{
    private WebApplication? _server;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public string Url { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var root = PublishFolder();
        var files = new PhysicalFileProvider(root);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _server = builder.Build();
        _server.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true });
        _server.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files });
        await _server.StartAsync();
        Url = _server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/";

        _playwright = await Playwright.CreateAsync();
        var channel = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHANNEL") ?? "msedge";
        Browser = await _playwright.Chromium.LaunchAsync(new() { Channel = channel.Length == 0 ? null : channel });
    }

    /// <summary>A fresh phone-sized browser profile: no stored progress, no service worker yet.</summary>
    public async Task<IPage> NewPageAsync()
    {
        var context = await Browser.NewContextAsync(new()
        {
            BaseURL = Url,
            ViewportSize = new() { Width = 390, Height = 844 },
            Locale = "de-DE",
            AcceptDownloads = true,
        });
        return await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
        if (_server is not null) await _server.DisposeAsync();
    }

    private static string PublishFolder()
    {
        var folder = Environment.GetEnvironmentVariable("GRAM_PUBLISH");
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); folder is null && dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Gram.slnx"))) folder = Path.Combine(dir.FullName, "publish", "wwwroot");

        return folder is not null && File.Exists(Path.Combine(folder, "index.html"))
            ? folder
            : throw new InvalidOperationException("No published app: run `dotnet publish src/Gram.App -c Release -o publish` first.");
    }
}

[CollectionDefinition(Name)]
public sealed class UsesPublishedApp : ICollectionFixture<PublishedApp>
{
    public const string Name = "published app";
}
