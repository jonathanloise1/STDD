using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;
using System.Text.RegularExpressions;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-GREET-002: Italian users read the greeting in Italian
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-GREET-002")]
[Property("UserStory", "US-DASH-04")]
public class DashGreet002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Switching to Italian translates the greeting, switching back restores English.")]
    public async Task ItalianUsersReadTheGreetingInItalian()
    {
        var dashboard = new DashboardPage(Page);
        var header = new HeaderPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();
        // 2. Click the "Change language" button.
        await header.ClickLanguageSelector();
        // 3. Click the "Italiano" button.
        await header.ClickLanguage("Italiano");

        // After step 3, the greeting and the line "Here is your task overview" are shown in Italian, and the greeting is still followed by a comma and "Dev User".
        await Expect(dashboard.Greeting).ToHaveTextAsync(new Regex(@"^(Buongiorno|Buon pomeriggio|Buonasera), Dev User$"));
        await Expect(dashboard.OverviewLineInItalian).ToBeVisibleAsync();

        // 4. Click the "Change language" button.
        await header.ClickLanguageSelector();
        // 5. Click the "English" button.
        await header.ClickLanguage("English");

        // After step 5, the line "Here is your task overview" is shown in English again.
        await Expect(dashboard.OverviewLine).ToBeVisibleAsync();
    }
}
