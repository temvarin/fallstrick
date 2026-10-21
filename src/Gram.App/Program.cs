using Gram.App;
using Gram.App.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<HeadOutlet>("head::after"); // the tab title set by each page's TopBar

// The catalog is loaded before the UI starts: every screen needs it, and offline it comes from the service worker's cache.
var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
var catalog = await CatalogLoader.TryLoadAsync(http);
if (catalog is null)
{
    builder.RootComponents.Add<LoadError>("#app");
}
else
{
    builder.RootComponents.Add<App>("#app");
    builder.Services.AddAppServices(catalog);
}

await builder.Build().RunAsync();
