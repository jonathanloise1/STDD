extern alias WebApi;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using WebApi::MyApp.WebApi.Middlewares;

namespace MyApp.UnitTests.Backend;

/// <summary>Impersonation signs a request in as anyone, so it can only be on in Development.</summary>
[TestFixture]
public class ImpersonationTests
{
    private const string Header = "X-MyApp-Impersonate-UserId";

    private static IConfiguration Config(bool? enabled)
    {
        var values = new Dictionary<string, string?>();
        if (enabled is not null) values[ImpersonationSettings.EnabledKey] = enabled.ToString();
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [TestCase("Development", true, true)]
    [TestCase("Development", false, false)]
    [TestCase("Production", true, false)]
    [TestCase("Staging", true, false)]
    [TestCase("Test", true, false)]
    public void IsOnOnlyInDevelopmentWhenTurnedOn(string environment, bool enabled, bool expected) =>
        Assert.That(ImpersonationSettings.IsEnabled(Config(enabled), new TestHostEnvironment(environment)), Is.EqualTo(expected));

    [Test]
    public void IsOffByDefault() =>
        Assert.That(ImpersonationSettings.IsEnabled(Config(null), new TestHostEnvironment("Development")), Is.False);

    [Test]
    public async Task InProduction_TheHeaderSignsNobodyIn_EvenWhenTheSettingIsOn()
    {
        var nextCalled = false;
        var middleware = new ImpersonationMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            Config(true), new TestHostEnvironment("Production"), NullLogger<ImpersonationMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Headers[Header] = Guid.NewGuid().ToString();

        // No database is given: a request that was honoured would need it
        await middleware.InvokeAsync(context, null!);

        Assert.That(nextCalled, Is.True);
        Assert.That(context.User.Identity?.IsAuthenticated, Is.Not.True);
    }

    [TestCase("Production", false)]
    [TestCase("Development", true)]
    public void TheRateLimitBypassFollowsTheSameRule(string environment, bool bypassed)
    {
        var services = new ServiceCollection()
            .AddSingleton(Config(true))
            .AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new TestHostEnvironment(environment))
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers[Header] = Guid.NewGuid().ToString();

        Assert.That(ImpersonationSettings.IsRequested(context), Is.EqualTo(bypassed));
    }
}
