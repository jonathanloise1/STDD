using MyApp.Infrastructure.Seed;
using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Authentication;

/// <summary>
/// What the app learns about the user when it signs in: who the user is and which organizations the user
/// belongs to with which role. Moved from the old AuthSync001Tests and AuthLogin001Tests.
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("UserStory", "US-AUTH-01")]
[Property("UserStory", "US-AUTH-04")]
public class SignInSyncTests
{
    [Test]
    [Description("The first sign-in answer carries the user's id, email and language preference")]
    public async Task Sync_ReturnsTheUser()
    {
        var response = await Api.Post("/api/auth/sync", null, SeedDataSet.DevUser);

        ApiAssert.IsSuccess(response);
        Assert.That(Guid.Parse(response.String("id")!), Is.EqualTo(SeedDataSet.RowId(SeedDataSet.DevUser))); // the user row, not the sign-in identity
        Assert.That(response.String("email"), Is.EqualTo("dev@myapp.local").IgnoreCase);
        Assert.That(response.String("preferredLanguage"), Is.Not.Empty);
        Assert.That(response.Json.TryGetProperty("hasCompletedOnboarding", out _), Is.True);
    }

    [Test]
    [Description("The answer lists the organizations of the user with the organization id, name and role")]
    public async Task Sync_ListsTheOrganizationsWithTheRole()
    {
        var response = await Api.Post("/api/auth/sync", null, SeedDataSet.DevUser);

        ApiAssert.IsSuccess(response);
        var organizations = response.Json.GetProperty("organizations").EnumerateArray().ToList();
        Assert.That(organizations, Is.Not.Empty, "Dev User belongs to an organization");

        var devOrg = organizations.Single(o => Guid.Parse(o.GetProperty("organizationId").GetString()!) == SeedDataSet.DevOrg);
        Assert.That(devOrg.GetProperty("organizationName").GetString(), Is.EqualTo("MyApp Dev Org"));
        Assert.That(devOrg.GetProperty("role").GetString(), Is.EqualTo("Admin"));
    }

    [Test]
    [Description("Each seeded role is told its own role in the same organization")]
    public async Task Sync_TellsEachMemberTheirRole()
    {
        foreach (var (user, role) in new[] { (SeedDataSet.DevMember, "Editor"), (SeedDataSet.DevViewer, "Viewer") })
        {
            var response = await Api.Post("/api/auth/sync", null, user);

            ApiAssert.IsSuccess(response);
            var devOrg = response.Json.GetProperty("organizations").EnumerateArray()
                .Single(o => Guid.Parse(o.GetProperty("organizationId").GetString()!) == SeedDataSet.DevOrg);
            Assert.That(devOrg.GetProperty("role").GetString(), Is.EqualTo(role));
        }
    }
}
