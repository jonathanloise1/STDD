using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-OPEN-004: A screen without a manual page shows the manual home
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-OPEN-004")]
[Property("UserStory", "US-HELP-03")]
public class HelpOpen004Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A screen with no page of its own and no area page lists the manual home page.")]
    public async Task AScreenWithoutAManualPageShowsTheManualHome()
    {
        var billing = ScreenPage.Billing(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Billing page.
        await billing.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();

        // The list shows one entry, the home page of the manual.
        await Expect(help.PageLinks).ToHaveCountAsync(1);
        await Expect(help.PageLinks.First).ToHaveAttributeAsync("href", "/manual");
    }
}
