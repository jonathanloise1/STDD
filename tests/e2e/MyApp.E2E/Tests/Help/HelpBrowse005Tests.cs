using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-BROWSE-005: The manual shows a message when its content cannot be loaded
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-BROWSE-005")]
[Property("UserStory", "US-HELP-01")]
public class HelpBrowse005Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When the content of the manual cannot be loaded, the page says so instead of staying blank.")]
    public async Task TheManualShowsAMessageWhenItsContentCannotBeLoaded()
    {
        var manual = new ManualPage(Page);
        // The content of the manual cannot be loaded.
        await TheManualContentCannotBeLoaded();

        // 1. Open the Manual page.
        await manual.Open();

        // The message "The manual could not be loaded." appears.
        await Expect(manual.LoadFailedMessage).ToBeVisibleAsync();
        // The list of areas is not shown.
        await Expect(manual.ListedLinks).ToHaveCountAsync(0);
    }
}
