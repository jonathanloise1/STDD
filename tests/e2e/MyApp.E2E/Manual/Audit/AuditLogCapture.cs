using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;

namespace MyApp.E2E.Manual.Audit;

/// <summary>Screenshots of the Audit Log page for the manual (manual.md 4).</summary>
[TestFixture]
[Category("audit")]
[Category("manual-capture")]
[Property("UserStory", "US-AUD-01")]
[Property("UserStory", "US-AUD-02")]
[Property("UserStory", "US-AUD-03")]
public class AuditLogCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) => TestUsers.AdminUser;

    [Test]
    public async Task Default()
    {
        var audit = new AuditLogPage(Page);
        await audit.Open();
        await Expect(audit.Table).ToBeVisibleAsync();
        await ShootAsync(audit);
    }

    [Test]
    public async Task FilteredByType()
    {
        var audit = new AuditLogPage(Page);
        await audit.Open();
        await Expect(audit.Table).ToBeVisibleAsync();
        // The second choice of the "Type" field is "Task".
        await audit.SelectTypeNumber(1);
        await ShootAsync(audit);
    }

    [Test]
    public async Task DetailsDialogOpen()
    {
        var audit = new AuditLogPage(Page);
        await audit.Open();
        await Expect(audit.FirstRow).ToBeVisibleAsync();
        await audit.ClickDetailsOfFirstRow();
        await ShootAsync(audit);
    }

    [Test]
    public async Task ListUnavailable()
    {
        var audit = new AuditLogPage(Page);
        ServiceDoesNotAnswer("AUD-LIST-007");
        await audit.Open();
        await WaitForMessageAsync();
        await ShootAsync(audit);
    }
}
