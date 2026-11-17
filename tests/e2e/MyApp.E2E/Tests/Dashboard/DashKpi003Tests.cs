using NUnit.Framework;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-KPI-003: A visitor is asked to sign in before seeing the dashboard
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-KPI-003")]
[Property("UserStory", "US-DASH-01")]
public class DashKpi003Tests : SignedOutTestBase
{
    [Test]
    [Description("A visitor who is not signed in is sent to the sign-in page and sees no counts.")]
    public async Task AVisitorIsAskedToSignInBeforeSeeingTheDashboard()
    {
        var dashboard = new DashboardPage(Page);
        var signIn = new SignInPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The sign-in page opens.
        await signIn.ExpectOpen();
        // No task counts are shown.
        await Expect(dashboard.Card("Total Tasks")).ToHaveCountAsync(0);
    }
}
