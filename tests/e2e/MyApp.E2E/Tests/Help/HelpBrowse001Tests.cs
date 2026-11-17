using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-BROWSE-001: Open the manual and read a page
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-BROWSE-001")]
[Property("UserStory", "US-HELP-01")]
public class HelpBrowse001Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A user opens the Manual page, goes to an area and reads one of its pages.")]
    public async Task OpenTheManualAndReadAPage()
    {
        var manual = new ManualPage(Page);

        // 1. Open the Manual page.
        await manual.Open();
        // The Manual page shows the home page with a list of areas that includes "Tasks".
        await Expect(manual.ListedLink("Tasks")).ToBeVisibleAsync();
        // 2. Click the "Tasks" link.
        await manual.ClickLink("Tasks");
        // The area page lists the pages of the Tasks area as links.
        await Expect(manual.ListedLinks).ToHaveTextAsync(new[] { "Create a task", "Edit a task", "Change the status of a task", "Delete a task", "See the list of tasks" });
        // 3. Click the "Create a task" link.
        await manual.ClickLink("Create a task");
        // The page "Create a task" shows its title, an introduction and the sections "Steps" and "What you will see".
        await Expect(manual.PageTitle("Create a task")).ToBeVisibleAsync();
        await Expect(manual.Introduction).ToBeVisibleAsync();
        await Expect(manual.SectionHeading("Steps")).ToBeVisibleAsync();
        await Expect(manual.SectionHeading("What you will see")).ToBeVisibleAsync();
    }
}
