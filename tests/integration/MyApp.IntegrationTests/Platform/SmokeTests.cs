using MyApp.Infrastructure.Seed;
using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Platform;

/// <summary>
/// The quickest check that the backend is up and the impersonation works: a signed-in call answers 200 with
/// data. Moved from the API check of the old SmokeTests; its UI checks (the page loads, the menu is shown)
/// belong to E2E test cases of the areas.
/// </summary>
[TestFixture]
[Category("authentication")]
[Category("Smoke")]
[Property("UserStory", "US-AUTH-01")]
public class SmokeTests
{
    [Test]
    [Description("The list of organizations answers 200 with data for a signed-in user")]
    public async Task Organizations_AnswerForASignedInUser()
    {
        var response = await Api.Get("/api/organizations", SeedDataSet.DevUser);

        ApiAssert.IsSuccess(response);
        Assert.That(response.Body, Is.Not.Empty);
    }
}
