using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;
using System.Text.RegularExpressions;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-GREET-001: The dashboard greets the user by name
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-GREET-001")]
[Property("UserStory", "US-DASH-04")]
public class DashGreet001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The page starts with a greeting and the name, then the overview line.")]
    public async Task TheDashboardGreetsTheUserByName()
    {
        var dashboard = new DashboardPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The page starts with "Good morning", "Good afternoon" or "Good evening", followed by a comma and "Dev User".
        await Expect(dashboard.Greeting).ToHaveTextAsync(new Regex(@"^(Good morning|Good afternoon|Good evening), Dev User$"));
        // Under the greeting the page reads "Here is your task overview".
        await Expect(dashboard.OverviewLine).ToBeVisibleAsync();
    }
}
