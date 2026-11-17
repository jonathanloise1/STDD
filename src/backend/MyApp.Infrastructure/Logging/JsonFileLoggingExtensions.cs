using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Logging;

public static class JsonFileLoggingExtensions
{
    /// <summary>
    /// Adds the JSON lines file sink when <c>MyApp:Logging:File:Enabled</c> is true (operations.md 3.4).
    /// </summary>
    public static ILoggingBuilder AddJsonFile(this ILoggingBuilder builder, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = new FileLoggerOptions();
        var section = configuration.GetSection(FileLoggerOptions.SectionName);
        section.Bind(options);
        if (!options.Enabled)
            return builder;

        var version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var plus = version.IndexOf('+');
        if (plus >= 0)
            version = version[..plus]; // drop the build metadata so the version stays a plain semantic version

        builder.Services.AddSingleton<ILoggerProvider>(_ =>
            new JsonFileLoggerProvider(options, environment.ContentRootPath, environment.EnvironmentName, version));
        return builder;
    }
}
