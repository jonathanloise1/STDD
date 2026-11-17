using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Onboarding;

/// <summary>
/// The setup state of a user: whether the user has completed it, and the reset that support uses. Moved
/// from the old OnboardSkip001Tests (the skip link is gone from the product; the state it set is checked
/// here).
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("UserStory", "US-ONBOARD-01")]
public class OnboardingCompletionTests
{
    [Test]
    [Description("Completing the setup is stored and the next sign-in reports it; the language is kept")]
    public async Task Completing_IsStoredAndReported()
    {
        var person = await TestWorld.CreateUser(completedOnboarding: false);
        var before = await Api.Post("/api/auth/sync", null, person.Id);
        Assert.That(before.Json.GetProperty("hasCompletedOnboarding").GetBoolean(), Is.False);

        var complete = await Api.Post("/api/users/me/complete-onboarding", null, person.Id);
        ApiAssert.IsSuccess(complete, 204);

        var after = await Api.Post("/api/auth/sync", null, person.Id);
        Assert.That(after.Json.GetProperty("hasCompletedOnboarding").GetBoolean(), Is.True);
        Assert.That(after.String("preferredLanguage"), Is.EqualTo("en"));
    }

    [Test]
    [Description("The reset used by support sets the setup state and the language back to their start")]
    public async Task Reset_RestoresTheStart()
    {
        var person = await TestWorld.CreateUser(completedOnboarding: true);
        await Api.Put("/api/users/me/language", new { language = "it" }, person.Id);

        var reset = await Api.Delete("/api/users/me/complete-onboarding", person.Id);
        ApiAssert.IsSuccess(reset, 204);

        var sync = await Api.Post("/api/auth/sync", null, person.Id);
        Assert.That(sync.Json.GetProperty("hasCompletedOnboarding").GetBoolean(), Is.False);
        Assert.That(sync.String("preferredLanguage"), Is.EqualTo("en"));
    }
}
