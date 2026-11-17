using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Authentication;

/// <summary>
/// The language a user chooses is stored and comes back at the next sign-in. Moved from the old
/// AuthLang001Tests and AuthLang002Tests (their API checks; the language button and the translated menu are
/// covered by E2E tests). A new user is used so the seed users keep English.
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("UserStory", "US-AUTH-08")]
public class LanguagePreferenceTests
{
    [Test]
    [Description("Choosing Italian is stored and the next sign-in and the profile report it")]
    public async Task ChoosingItalian_IsStoredAndReturned()
    {
        var person = await TestWorld.CreateUser();

        var change = await Api.Put("/api/users/me/language", new { language = "it" }, person.Id);
        ApiAssert.IsSuccess(change, 204);

        var sync = await Api.Post("/api/auth/sync", null, person.Id);
        ApiAssert.IsSuccess(sync);
        Assert.That(sync.String("preferredLanguage"), Is.EqualTo("it"));

        var profile = await Api.Get("/api/users/me/profile", person.Id);
        ApiAssert.IsSuccess(profile);
        Assert.That(profile.String("preferredLanguage"), Is.EqualTo("it"));
    }

    [Test]
    [Description("A language the app does not offer is refused and the stored language stays")]
    public async Task ChoosingAnUnknownLanguage_IsRefused()
    {
        var person = await TestWorld.CreateUser();

        var change = await Api.Put("/api/users/me/language", new { language = "xx" }, person.Id);

        ApiAssert.IsProblem(change, 400, detail: "Language must be one of: en, it.");
        var profile = await Api.Get("/api/users/me/profile", person.Id);
        Assert.That(profile.String("preferredLanguage"), Is.EqualTo("en"));
    }
}
