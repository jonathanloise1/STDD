using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Onboarding;

/// <summary>
/// ONBOARD-WIZ-003: A name made only of spaces counts as empty
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("TestCase", "ONBOARD-WIZ-003")]
[Property("UserStory", "US-ONBOARD-02")]
public class OnboardWiz003Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.NewUser;

    [Test]
    [Description("Spaces alone are not accepted as an organization name.")]
    public async Task ANameMadeOnlyOfSpacesCountsAsEmpty()
    {
        var visitor = new VisitorPage(Page);
        var setup = new SetupPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as "New User".
        await visitor.SignInAs(TestUsers.NewUser);
        // 3. Type "   " in the "Organization Name" field.
        await setup.TypeOrganizationName("   ");
        // 4. Click the "Submit" button.
        await setup.ClickSubmit();
        // The message "Organization name is required" appears.
        await Expect(setup.FieldMessage("Organization name is required")).ToBeVisibleAsync();
        // The text "Creating your organization..." is not shown.
        await Expect(setup.CreatingText).ToHaveCountAsync(0);
        // The "Set Up Your Organization" page stays open.
        await Expect(setup.Title).ToBeVisibleAsync();
    }
}
