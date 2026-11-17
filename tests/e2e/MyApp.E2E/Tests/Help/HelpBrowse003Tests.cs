using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-BROWSE-003: The manual follows the language of the application
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-BROWSE-003")]
[Property("UserStory", "US-HELP-01")]
public class HelpBrowse003Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("After the language of the application is changed to Italian, the manual is shown in Italian.")]
    public async Task TheManualFollowsTheLanguageOfTheApplication()
    {
        var dashboard = new DashboardPage(Page);
        var header = new HeaderPage(Page);
        var manual = new ManualPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();
        // 2. Click the "Change language" button.
        await header.ClickChangeLanguage();
        // 3. Click the "Italiano" button.
        await header.ClickLanguage("Italiano");
        // 4. Open the Manual page. It is opened from the menu: loading the application again would set English again.
        await manual.OpenFromMenu();

        // The list of areas shows "Attività" instead of "Tasks".
        await Expect(manual.ListedLink("Attività")).ToBeVisibleAsync();
        await Expect(manual.ListedLink("Tasks")).ToHaveCountAsync(0);
        // The Manual page shows no English page titles.
        await Expect(manual.SectionHeading("Steps")).ToHaveCountAsync(0);
    }

    /// <summary>The language of the user goes back to English through the same buttons a user clicks.</summary>
    protected override async Task CleanUpAsync()
    {
        try
        {
            var header = new HeaderPage(Page);
            await header.ClickLanguageSelector();
            await header.ClickLanguage("English");
        }
        catch (Exception ex)
        {
            // Clean-up is best effort: a failed test may leave the page in a state the clean-up cannot use
            Console.WriteLine($"[E2E] Clean-up of the language failed: {ex.Message}");
        }
    }
}
