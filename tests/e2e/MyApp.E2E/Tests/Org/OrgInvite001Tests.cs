using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Org;

/// <summary>
/// ORG-INVITE-001: Admin Invites a Collaborator
///
/// Verifies that an Admin can invite a new collaborator via
/// POST /api/organizations/{id}/collaborators/invite and the
/// invitation appears in GET .../invitations/pending.
///
/// Uses a freshly-created org to avoid concurrency issues on DevOrg.
/// </summary>
[TestFixture]
[Category("Org")]
[Property("TestCase", "ORG-INVITE-001")]
[Property("UserStory", "US-ORG-07")]
public class OrgInvite001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    private string? _testOrgId;
    private string _inviteEmail = null!;

    [SetUp]
    public void GenerateUniqueEmail()
    {
        _inviteEmail = $"invite-{TestRunId}@MyApp.test";
    }

    /// <summary>Creates a fresh org for invite testing and returns its ID.</summary>
    private async Task<string> CreateTestOrgAsync()
    {
        var response = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new
                {
                    name = UniqueTestName("Invite Org"),
                    legalName = "Invite Test S.r.l.",
                    vatNumber = "IT55555555555",
                    fiscalCode = "55555555555",
                    billingAddress = "Via Invite 1",
                    billingCity = "Milano",
                    billingProvince = "MI",
                    billingZipCode = "20100",
                    billingCountryCode = "IT",
                    billingEmail = "invite-org@MyApp.test"
                },
                IgnoreHTTPSErrors = true
            });

        Assert.That(response.Status, Is.EqualTo(201), "Should create test org for invite");
        var json = await response.JsonAsync();
        _testOrgId = json?.GetProperty("id").GetString();
        return _testOrgId!;
    }

    [Test]
    [Description("Admin invites a collaborator and invitation appears in pending list")]
    public async Task Invite_ShouldCreatePendingInvitation()
    {
        var orgId = await CreateTestOrgAsync();

        // Step 1: Send invite
        var inviteResponse = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{orgId}/collaborators/invite",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new
                {
                    fullName = "Invited Tester",
                    email = _inviteEmail,
                    role = "Editor"
                },
                IgnoreHTTPSErrors = true
            });

        Console.WriteLine($"[ORG-INVITE-001] Invite response status: {inviteResponse.Status}");
        if (inviteResponse.Status != 204)
        {
            var body = await inviteResponse.TextAsync();
            Console.WriteLine($"[ORG-INVITE-001] Response body: {body}");
        }
        Assert.That(inviteResponse.Status, Is.EqualTo(204),
            "Invite collaborator should return 204 NoContent");

        // Step 2: Verify the pending invitation exists
        var pendingResponse = await Page.APIRequest.GetAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{orgId}/invitations/pending",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                IgnoreHTTPSErrors = true
            });

        Assert.That(pendingResponse.Status, Is.EqualTo(200));

        var pendingJson = await pendingResponse.JsonAsync();
        Assert.That(pendingJson, Is.Not.Null);
        Assert.That(pendingJson?.GetArrayLength(), Is.GreaterThanOrEqualTo(1),
            "Pending invitations list should contain at least one entry");

        // Find our specific invite (email is lowered server-side)
        bool found = false;
        for (int i = 0; i < pendingJson?.GetArrayLength(); i++)
        {
            var item = pendingJson.Value[i];
            if (string.Equals(item.GetProperty("email").GetString(), _inviteEmail, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                Assert.That(item.GetProperty("fullName").GetString(), Is.EqualTo("Invited Tester"));
                Assert.That(item.GetProperty("role").GetString(), Is.EqualTo("Editor"));
                Assert.That(item.TryGetProperty("id", out _), Is.True, "Should have an id field");
                Assert.That(item.TryGetProperty("createdAt", out _), Is.True, "Should have a createdAt field");
                break;
            }
        }

        Assert.That(found, Is.True,
            $"Pending invitations should contain an entry for {_inviteEmail}");
    }

    [Test]
    [Description("Non-admin should receive 403 when trying to invite")]
    public async Task Invite_AsEditor_ShouldReturn403()
    {
        var devOrgId = await ResolveDevOrgIdAsync();

        var inviteResponse = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{devOrgId}/collaborators/invite",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.MemberUser.Id
                },
                DataObject = new
                {
                    fullName = "Should Fail",
                    email = "shouldfail@MyApp.test",
                    role = "Viewer"
                },
                IgnoreHTTPSErrors = true
            });

        Console.WriteLine($"[ORG-INVITE-001] Non-admin invite status: {inviteResponse.Status}");
        Assert.That(inviteResponse.Status, Is.EqualTo(403),
            "Editor should receive 403 Forbidden when inviting");
    }

    [TearDown]
    public async Task CleanupTestOrg()
    {
        if (_testOrgId is not null)
        {
            await Page.APIRequest.DeleteAsync(
                $"{TestConfiguration.ApiBaseUrl}/api/organizations/{_testOrgId}",
                new()
                {
                    Headers = new Dictionary<string, string>
                    {
                        [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                    },
                    IgnoreHTTPSErrors = true
                });
            _testOrgId = null;
        }
    }
}
