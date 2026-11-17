using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-UPDATE-002: An editor cannot open the Update Organization page
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-UPDATE-002")]
[Property("UserStory", "US-ORG-02")]
public class OrgUpdate002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An editor who opens the Update Organization page by its address is taken to the Dashboard with a message, and no field is shown.")]
    public async Task OpeningTheUpdatePageLeadsToTheDashboard()
    {
        var edit = new UpdateOrganizationPage(Page);
        var noAccess = new NoAccessPage(Page);

        // 1. Open the Update Organization page.
        await edit.Open();

        // The Dashboard page opens with the message "You don't have access to this page."
        await noAccess.ExpectDashboardOpen();
        await Expect(noAccess.NoAccessMessage).ToBeVisibleAsync();
        // The page does not show the fields "Organization Name" or "Legal Name".
        await Expect(edit.FormField("Organization Name")).ToHaveCountAsync(0);
        await Expect(edit.FormField("Legal Name")).ToHaveCountAsync(0);
    }
}
