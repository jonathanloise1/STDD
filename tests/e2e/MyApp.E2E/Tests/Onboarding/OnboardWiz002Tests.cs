using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Onboarding;

/// <summary>
/// ONBOARD-WIZ-002: Submitting without a name shows a message
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("TestCase", "ONBOARD-WIZ-002")]
[Property("UserStory", "US-ONBOARD-02")]
public class OnboardWiz002Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.NewUser;

    [Test]
    [Description("The wizard does not create an organization when the name is empty.")]
    public async Task SubmittingWithoutANameShowsAMessage()
    {
        var visitor = new VisitorPage(Page);
        var setup = new SetupPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as "New User".
        await visitor.SignInAs(TestUsers.NewUser);
        // 3. Type "billing@initech.test" in the "Billing Email" field.
        await setup.TypeBillingEmail("billing@initech.test");
        // 4. Click the "Submit" button.
        await setup.ClickSubmit();
        // The message "Organization name is required" appears.
        await Expect(setup.FieldMessage("Organization name is required")).ToBeVisibleAsync();
        // The "Set Up Your Organization" page stays open.
        await Expect(setup.Title).ToBeVisibleAsync();
    }
}
