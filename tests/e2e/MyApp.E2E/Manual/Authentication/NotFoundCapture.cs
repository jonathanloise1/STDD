using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;

namespace MyApp.E2E.Manual.Authentication;

/// <summary>Screenshot of the "Page not found" page for the manual (manual.md 4).</summary>
[TestFixture]
[Category("authentication")]
[Category("manual-capture")]
[Property("UserStory", "US-AUTH-06")]
public class NotFoundCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) => TestUsers.AdminUser;

    [Test]
    public async Task Default()
    {
        var address = new AddressPage(Page);
        var notFound = new NotFoundPage(Page);
        await address.OpenAsync("/this-page-does-not-exist");
        await ShootAsync(notFound);
    }
}
