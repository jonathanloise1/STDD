using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Organizations;

/// <summary>
/// The keyed outcomes of the organizations area (operations.md 4.2). Conflicts and missing members come from
/// real situations in a new organization; "the service did not answer" comes from the fault switch
/// (development.md 16.2) and leaves the stored data as it was.
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("UserStory", "US-ORG-01")]
[Property("UserStory", "US-ORG-02")]
[Property("UserStory", "US-ORG-03")]
[Property("UserStory", "US-ORG-04")]
[Property("UserStory", "US-ORG-05")]
[Property("UserStory", "US-ORG-06")]
[Property("UserStory", "US-ORG-07")]
[Property("TestCase", "ORG-INVITE-005")]
[Property("TestCase", "ORG-INVITE-006")]
[Property("TestCase", "ORG-LOGO-004")]
[Property("TestCase", "ORG-REMOVE-004")]
[Property("TestCase", "ORG-ROLE-005")]
[Property("TestCase", "ORG-ROLE-006")]
[Property("TestCase", "ORG-TEAM-005")]
[Property("TestCase", "ORG-UPDATE-007")]
[Property("TestCase", "ORG-VIEW-003")]
public class OrganizationsOutcomeCodesTests
{
    private const string Unavailable = "The service did not answer. Try again in a few minutes.";

    private Guid _organizationId;
    private Person _admin = null!;

    [SetUp]
    public async Task NewOrganization() => (_organizationId, _admin) = await TestWorld.CreateOrganization();

    private string Org => $"/api/organizations/{_organizationId}";

    [Test]
    public async Task org_invite_already_member()
    {
        var response = await Api.Post($"{Org}/collaborators/invite", new { fullName = _admin.FullName, email = _admin.Email, role = "Viewer" }, _admin.Id);

        ApiAssert.IsProblem(response, 409, "org.invite.already_member", "User is already a member of this organization.");
    }

    [Test]
    public async Task org_invite_already_invited()
    {
        var email = $"twice-{Guid.NewGuid():N}@myapp.test";
        ApiAssert.IsSuccess(await Api.Post($"{Org}/collaborators/invite", new { fullName = "Twice Invited", email, role = "Viewer" }, _admin.Id), 204);

        var response = await Api.Post($"{Org}/collaborators/invite", new { fullName = "Twice Invited", email, role = "Viewer" }, _admin.Id);

        ApiAssert.IsProblem(response, 409, "org.invite.already_invited", "A pending invitation for this email already exists.");
    }

    [Test]
    public async Task org_role_last_admin()
    {
        var response = await Api.Patch($"{Org}/collaborators/{_admin.RowId}", new { role = "Viewer" }, _admin.Id);

        ApiAssert.IsProblem(response, 409, "org.role.last_admin", "An organization needs at least one Admin.");
        var team = await Api.Get($"{Org}/users", _admin.Id);
        Assert.That(team.Json.EnumerateArray().Single().GetProperty("role").GetString(), Is.EqualTo("Admin"));
    }

    [Test]
    public async Task org_role_not_found()
    {
        var response = await Api.Patch($"{Org}/collaborators/{Guid.NewGuid()}", new { role = "Viewer" }, _admin.Id);

        ApiAssert.IsProblem(response, 404, "org.role.not_found", "This member is no longer in the team.");
    }

    [Test]
    public async Task org_role_not_found_for_a_removed_member()
    {
        var member = await TestWorld.AddMember(_organizationId, _admin, "Viewer");
        ApiAssert.IsSuccess(await Api.Delete($"{Org}/collaborators/{member.RowId}", _admin.Id), 204);

        var response = await Api.Patch($"{Org}/collaborators/{member.RowId}", new { role = "Editor" }, _admin.Id);

        ApiAssert.IsProblem(response, 404, "org.role.not_found", "This member is no longer in the team.");
    }

    [Test]
    public async Task org_remove_not_found()
    {
        var response = await Api.Delete($"{Org}/collaborators/{Guid.NewGuid()}", _admin.Id);

        ApiAssert.IsProblem(response, 404, "org.remove.not_found", "This member is no longer in the team.");
    }

    [Test]
    public async Task org_remove_not_found_for_a_removed_member()
    {
        var member = await TestWorld.AddMember(_organizationId, _admin, "Viewer");
        ApiAssert.IsSuccess(await Api.Delete($"{Org}/collaborators/{member.RowId}", _admin.Id), 204);

        var response = await Api.Delete($"{Org}/collaborators/{member.RowId}", _admin.Id);

        ApiAssert.IsProblem(response, 404, "org.remove.not_found", "This member is no longer in the team.");
    }

    [Test]
    public async Task org_view_unavailable()
    {
        var response = await Api.Get(Org, _admin.Id, Api.Fault("org.view.unavailable"));

        ApiAssert.IsProblem(response, 503, "org.view.unavailable", Unavailable);
    }

    [Test]
    public async Task org_team_unavailable()
    {
        var members = await Api.Get($"{Org}/users", _admin.Id, Api.Fault("org.team.unavailable"));
        var invitations = await Api.Get($"{Org}/invitations/pending", _admin.Id, Api.Fault("org.team.unavailable"));

        ApiAssert.IsProblem(members, 503, "org.team.unavailable", Unavailable);
        ApiAssert.IsProblem(invitations, 503, "org.team.unavailable", Unavailable);
    }

    [Test]
    public async Task org_update_unavailable()
    {
        var before = await Api.Get(Org, _admin.Id);
        var body = new
        {
            name = "Never saved", legalName = "Never Saved S.r.l.", vatNumber = "IT00000000003", fiscalCode = "00000000003",
            billingAddress = "Via Mai 1", billingCity = "Nowhere", billingProvince = "XX", billingZipCode = "00000",
            billingCountryCode = "IT", billingEmail = "never@myapp.test"
        };

        var response = await Api.Put(Org, body, _admin.Id, Api.Fault("org.update.unavailable"));

        ApiAssert.IsProblem(response, 503, "org.update.unavailable", Unavailable);
        var after = await Api.Get(Org, _admin.Id);
        Assert.That(after.String("name"), Is.EqualTo(before.String("name")), "The operation did not run");
    }

    [Test]
    public async Task org_logo_unavailable()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var response = await Api.PostFile($"{Org}/logo", png, "image/png", _admin.Id, Api.Fault("org.logo.unavailable"));

        ApiAssert.IsProblem(response, 503, "org.logo.unavailable", Unavailable);
        var after = await Api.Get(Org, _admin.Id);
        Assert.That(after.Json.GetProperty("logoUrl").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null), "The operation did not run");
    }
}
