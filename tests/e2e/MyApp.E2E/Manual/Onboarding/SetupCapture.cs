using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Manual.Onboarding;

/// <summary>Screenshots of the "Set Up Your Organization" page for the manual (manual.md 4).</summary>
[TestFixture]
[Category("onboarding")]
[Category("manual-capture")]
[Property("UserStory", "US-ONBOARD-01")]
[Property("UserStory", "US-ONBOARD-02")]
public class SetupCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) => TestUsers.NewUser;

    [Test]
    public async Task Default()
    {
        var setup = new SetupPage(Page);
        await setup.Open();
        await Expect(setup.Title).ToBeVisibleAsync();
        await ShootAsync(setup);
    }

    [Test]
    public async Task NameMissing()
    {
        var setup = new SetupPage(Page);
        await setup.Open();
        await setup.ClickSubmit();
        await ShootAsync(setup);
    }

    [Test]
    public async Task CreateUnavailable()
    {
        var setup = new SetupPage(Page);
        ServiceDoesNotAnswer("ONBOARD-WIZ-004");
        await setup.Open();
        await setup.TypeOrganizationName("Initech");
        await setup.ClickSubmit();
        await WaitForMessageAsync();
        await ShootAsync(setup);
    }
}
