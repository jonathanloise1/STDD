using MyApp.Application.Help;

namespace MyApp.WebApi.Help;

public static class HelpServiceCollectionExtensions
{
    /// <summary>
    /// Wires the help pack and lock (read once, at start-up), the model host client and the server tier
    /// (models.md 7.4, 7.5, 7.7). Missing files never stop the application.
    /// </summary>
    public static IServiceCollection AddHelp(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddSingleton(sp => HelpRuntime.Load(
            sp.GetRequiredService<HelpSettings>(), environment.ContentRootPath,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<HelpRuntime>()));

        services.AddHttpClient<IModelHost, ModelHostClient>((sp, client) =>
        {
            var endpoint = sp.GetRequiredService<HelpSettings>().ServerEndpoint.TrimEnd('/') + "/";
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                client.BaseAddress = uri;
            // The limit per question is the cancellation token of the answer, not the client
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddSingleton(sp => new HelpAssistant(sp.GetRequiredService<HelpRuntime>(), sp.GetRequiredService<IModelHost>()));
        return services;
    }
}
