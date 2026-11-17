using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-BROWSE-002: The pages of an area follow the order of the stories
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-BROWSE-002")]
[Property("UserStory", "US-HELP-01")]
public class HelpBrowse002Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The area page lists its pages in the order of the stories they describe.")]
    public async Task ThePagesOfAnAreaFollowTheOrderOfTheStories()
    {
        var manual = new ManualPage(Page);

        // 1. Open the Manual page.
        await manual.Open();
        // 2. Click the "Tasks" link.
        await manual.ClickLink("Tasks");
        // The list shows the pages in this order: "Create a task", "Edit a task", "Change the status of a task", "Delete a task", "See the list of tasks".
        await Expect(manual.ListedLinks).ToHaveTextAsync(new[] { "Create a task", "Edit a task", "Change the status of a task", "Delete a task", "See the list of tasks" });
    }
}
