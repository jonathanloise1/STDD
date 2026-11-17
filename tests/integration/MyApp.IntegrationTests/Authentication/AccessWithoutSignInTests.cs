using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Authentication;

/// <summary>
/// The backend refuses a caller who is not signed in and tells nothing about the users. Moved from the old
/// AuthLogin002Tests and from the API part of AuthLogout001Tests.
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("UserStory", "US-AUTH-02")]
[Property("UserStory", "US-AUTH-03")]
public class AccessWithoutSignInTests
{
    [Test]
    [Description("The sign-in sync without an identity is refused with 401")]
    public async Task Sync_WithoutIdentity_Returns401()
    {
        var response = await Api.Post("/api/auth/sync", null);

        ApiAssert.IsProblem(response, 401, detail: "Authentication is required.");
    }

    [Test]
    [Description("The list of organizations without an identity is refused with 401 and leaks no user data")]
    public async Task Organizations_WithoutIdentity_Returns401AndLeaksNothing()
    {
        var response = await Api.Get("/api/organizations");

        ApiAssert.IsProblem(response, 401, detail: "Authentication is required.");
        Assert.That(response.Body, Does.Not.Contain("email"));
        Assert.That(response.Body, Does.Not.Contain("@"));
    }

    [Test]
    [Description("An identity that matches no user is treated as no identity")]
    public async Task Organizations_AsAnUnknownUser_Returns401()
    {
        var response = await Api.Get("/api/organizations", Guid.NewGuid());

        ApiAssert.IsProblem(response, 401);
    }
}
