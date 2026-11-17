using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Authentication;

/// <summary>
/// A user who has just signed in for the first time belongs to no organization and has not completed the
/// setup. Moved from the old AuthProvision001Tests.
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("UserStory", "US-AUTH-04")]
public class FirstSignInTests
{
    [Test]
    [Description("A new user gets the user data with no organization and the setup not completed")]
    public async Task Sync_ForANewUser_HasNoOrganization()
    {
        var person = await TestWorld.CreateUser(completedOnboarding: false);

        var response = await Api.Post("/api/auth/sync", null, person.Id);

        ApiAssert.IsSuccess(response);
        Assert.That(Guid.Parse(response.String("id")!), Is.EqualTo(person.RowId)); // the user row, not the sign-in identity
        Assert.That(response.String("email"), Is.EqualTo(person.Email));
        Assert.That(response.Json.GetProperty("organizations").GetArrayLength(), Is.EqualTo(0));
        Assert.That(response.Json.GetProperty("hasCompletedOnboarding").GetBoolean(), Is.False);
    }
}
