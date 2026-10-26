using Gram.Domain;

namespace Gram.App.Services;

public static class ServiceRegistration
{
    /// <summary>Everything the components use. Clock and randomness are services too, so tests can replace them.</summary>
    public static IServiceCollection AddAppServices(this IServiceCollection services, Catalog catalog) => services
        .AddSingleton(catalog)
        .AddSingleton<IKeyValueStore, LocalStorage>()
        .AddSingleton<ProgressStore>()
        .AddSingleton<ProgressService>()
        .AddSingleton<Dialogs>()
        .AddSingleton<Browser>()
        .AddSingleton(TimeProvider.System)
        .AddSingleton(Random.Shared);
}
