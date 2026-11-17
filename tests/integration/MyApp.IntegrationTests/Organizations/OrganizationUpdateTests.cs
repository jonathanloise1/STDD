using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Organizations;

/// <summary>
/// Admins edit the organization details; other roles cannot. Moved from the old OrgUpdate001Tests and
/// OrgUpdate002Tests; the Editor case now runs on a new organization instead of "MyApp Dev Org".
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("UserStory", "US-ORG-02")]
public class OrganizationUpdateTests
{
    private static object Body(string name, string city, string email) => new
    {
        name,
        legalName = "Updated Org S.p.A.",
        vatNumber = "IT00000000002",
        fiscalCode = "00000000002",
        billingAddress = "Via After 10",
        billingCity = city,
        billingProvince = "RM",
        billingZipCode = "00100",
        billingCountryCode = "IT",
        billingEmail = email
    };

    [Test]
    [Description("An Admin changes the name and billing fields and the change is stored")]
    public async Task Admin_ChangesTheDetails()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var newName = $"Updated {Guid.NewGuid():N}"[..20];

        var update = await Api.Put($"/api/organizations/{organizationId}", Body(newName, "Roma", "after@myapp.test"), admin.Id);

        ApiAssert.IsSuccess(update);
        Assert.That(update.String("name"), Is.EqualTo(newName));
        Assert.That(update.String("legalName"), Is.EqualTo("Updated Org S.p.A."));
        Assert.That(update.String("billingCity"), Is.EqualTo("Roma"));
        Assert.That(update.String("billingEmail"), Is.EqualTo("after@myapp.test"));

        var stored = await Api.Get($"/api/organizations/{organizationId}", admin.Id);
        Assert.That(stored.String("name"), Is.EqualTo(newName));
    }

    [TestCase("Editor")]
    [TestCase("Viewer")]
    [Description("A member who is not an Admin is refused, and the stored details do not change")]
    public async Task NonAdmin_IsRefusedAndNothingChanges(string role)
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var member = await TestWorld.AddMember(organizationId, admin, role);
        var before = await Api.Get($"/api/organizations/{organizationId}", admin.Id);

        var update = await Api.Put($"/api/organizations/{organizationId}", Body("Tampered Name", "Nowhere", "tampered@myapp.test"), member.Id);

        ApiAssert.IsProblem(update, 403);
        var after = await Api.Get($"/api/organizations/{organizationId}", admin.Id);
        Assert.That(after.String("name"), Is.EqualTo(before.String("name")));
        Assert.That(after.String("billingCity"), Is.EqualTo(before.String("billingCity")));
    }
}
