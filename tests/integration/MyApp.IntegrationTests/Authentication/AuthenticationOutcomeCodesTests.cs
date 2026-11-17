using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Authentication;

/// <summary>
/// The keyed outcomes of the authentication area (operations.md 4.2), reached through the fault switch
/// (development.md 16.2): the problem document carries the log key as <c>code</c> and the service message as
/// <c>detail</c>, and the operation does not run.
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("UserStory", "US-AUTH-01")]
[Property("UserStory", "US-AUTH-08")]
[Property("TestCase", "AUTH-LANG-005")]
[Property("TestCase", "AUTH-LOGIN-006")]
public class AuthenticationOutcomeCodesTests
{
    private const string Unavailable = "The service did not answer. Try again in a few minutes.";

    [Test]
    public async Task auth_login_unavailable()
    {
        var person = await TestWorld.CreateUser();

        var response = await Api.Post("/api/auth/sync", null, person.Id, Api.Fault("auth.login.unavailable"));

        ApiAssert.IsProblem(response, 503, "auth.login.unavailable", Unavailable);
    }

    [Test]
    public async Task auth_lang_unavailable()
    {
        var person = await TestWorld.CreateUser();

        var response = await Api.Put("/api/users/me/language", new { language = "it" }, person.Id, Api.Fault("auth.lang.unavailable"));

        ApiAssert.IsProblem(response, 503, "auth.lang.unavailable", Unavailable);
        var profile = await Api.Get("/api/users/me/profile", person.Id);
        Assert.That(profile.String("preferredLanguage"), Is.EqualTo("en"), "The operation did not run");
    }
}
