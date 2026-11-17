using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Onboarding;

/// <summary>
/// The keyed outcome of the setup wizard (operations.md 4.2): when the service does not answer, no
/// organization is created and the problem document carries the key.
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("UserStory", "US-ONBOARD-02")]
[Property("TestCase", "ONBOARD-WIZ-004")]
public class OnboardingOutcomeCodesTests
{
    [Test]
    public async Task onboard_wiz_unavailable()
    {
        var person = await TestWorld.CreateUser(completedOnboarding: false);

        var response = await Api.Post("/api/organizations", TestWorld.OrganizationBody(), person.Id, Api.Fault("onboard.wiz.unavailable"));

        ApiAssert.IsProblem(response, 503, "onboard.wiz.unavailable", "The service did not answer. Try again in a few minutes.");
        var organizations = await Api.Get("/api/organizations", person.Id);
        Assert.That(organizations.Json.GetArrayLength(), Is.EqualTo(0), "The operation did not run");
    }
}
