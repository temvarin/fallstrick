using System.Net.Http.Json;
using System.Text.Json;
using Gram.Domain;

namespace Gram.App.Services;

/// <summary>Loads the content written by the pipeline (wwwroot/data).</summary>
internal static class CatalogLoader
{
    /// <returns>null if the data can't be loaded – the app was never opened online, or a release is incomplete.</returns>
    public static async Task<Catalog?> TryLoadAsync(HttpClient http)
    {
        try
        {
            var data = await http.GetFromJsonAsync("data/catalog.json", DataJson.Default.CatalogData);
            return data?.ToCatalog();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException)
        {
            Console.Error.WriteLine($"Loading the catalog failed: {e.Message}");
            return null;
        }
    }
}
