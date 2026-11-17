using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Onboarding;

/// <summary>
/// ONBOARD-WIZ-004: Creating when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("TestCase", "ONBOARD-WIZ-004")]
[Property("UserStory", "US-ONBOARD-02")]
public class OnboardWiz004Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.NewUser;

    [Test]
    [Description("When the service does not answer while the organization is created, the user is told so and the wizard keeps what was typed.")]
    public async Task CreatingWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var visitor = new VisitorPage(Page);
        var setup = new SetupPage(Page);
        var address = new AddressPage(Page);

        // The service does not answer when an organization is created.
        ServiceDoesNotAnswer();
        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as "New User".
        await visitor.SignInAs(TestUsers.NewUser);
        // 3. Type "Initech" in the "Organization Name" field.
        await setup.TypeOrganizationName("Initech");
        // 4. Click the "Submit" button.
        await setup.ClickSubmit();
        // The message "The service did not answer. Try again in a few minutes." appears.
        await Expect(address.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The "Set Up Your Organization" page stays open and the "Organization Name" field still shows "Initech".
        await Expect(setup.Title).ToBeVisibleAsync();
        await Expect(setup.OrganizationNameField).ToHaveValueAsync("Initech");
    }
}
