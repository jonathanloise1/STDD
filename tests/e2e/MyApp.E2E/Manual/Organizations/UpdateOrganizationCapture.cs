using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Manual.Organizations;

/// <summary>Screenshots of the Update Organization page for the manual (manual.md 4).</summary>
[TestFixture]
[Category("organizations")]
[Category("manual-capture")]
[Property("UserStory", "US-ORG-02")]
[Property("UserStory", "US-ORG-03")]
public class UpdateOrganizationCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) => TestUsers.AdminUser;

    [Test]
    public async Task Default()
    {
        var edit = new UpdateOrganizationPage(Page);
        await edit.Open();
        await ShootAsync(edit);
    }

    [Test]
    public async Task UnsavedChanges()
    {
        var edit = new UpdateOrganizationPage(Page);
        await edit.Open();
        await edit.ClearField("City");
        await edit.TypeInField("City", "Bergamo");
        await ShootAsync(edit);
    }

    [Test]
    public async Task RequiredFieldMissing()
    {
        var edit = new UpdateOrganizationPage(Page);
        await edit.Open();
        await edit.ClearField("Organization Name");
        await edit.ClickSaveChanges();
        await ShootAsync(edit);
    }

    [Test]
    public async Task SaveUnavailable()
    {
        var edit = new UpdateOrganizationPage(Page);
        ServiceDoesNotAnswer("ORG-UPDATE-007");
        await edit.Open();
        await edit.ClearField("City");
        await edit.TypeInField("City", "Bergamo");
        await edit.ClickSaveChanges();
        await WaitForMessageAsync();
        await ShootAsync(edit);
    }
}
