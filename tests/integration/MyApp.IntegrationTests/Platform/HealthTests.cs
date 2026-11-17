using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Platform;

/// <summary>
/// The health endpoint the monitor reads (operations.md 6.4): JSON with the overall status and one entry per
/// check. Anyone can call it.
/// </summary>
[TestFixture]
[Category("authentication")]
[Category("Smoke")]
[Property("UserStory", "US-AUTH-01")]
public class HealthTests
{
    [Test]
    [Description("/health answers JSON with the status and the checks, with no identity")]
    public async Task Health_AnswersJson()
    {
        var response = await Api.Get("/health");

        Assert.That(response.Status, Is.EqualTo(200), response.Body);
        Assert.That(response.ContentType, Is.EqualTo("application/json"));
        Assert.That(response.String("status"), Is.EqualTo("Healthy"));

        var checks = response.Json.GetProperty("checks").EnumerateArray().ToList();
        Assert.That(checks, Is.Not.Empty);
        foreach (var check in checks)
        {
            Assert.That(check.GetProperty("name").GetString(), Is.Not.Empty);
            Assert.That(check.GetProperty("status").GetString(), Is.Not.Empty);
            Assert.That(check.GetProperty("durationMs").GetInt64(), Is.GreaterThanOrEqualTo(0));
        }
    }
}
