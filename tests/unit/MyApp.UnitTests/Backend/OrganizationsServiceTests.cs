using MyApp.Application.DTOs.Organizations;
using MyApp.Application.Exceptions;
using MyApp.Application.Logging;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
[Property("UserStory", "US-ORG-06")]
[Property("UserStory", "US-ORG-07")]
public class OrganizationsServiceTests
{
    private static OrganizationsService Service(FakeOrganizationsRepository repo, CapturingLogger<OrganizationsService> log) =>
        new(log, repo, null!);

    [Test]
    public void LoweringTheRoleOfTheLastAdmin_IsRefusedWithTheKey()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("editor", OrganizationRole.Editor));
        var log = new CapturingLogger<OrganizationsService>();
        var repo = new FakeOrganizationsRepository(org);

        var ex = Assert.ThrowsAsync<ConflictException>(() => Service(repo, log).UpdateOrganizationUsersAsync(
            m["admin"].User!.AadId, m["admin"].User!.Id, org.Id, new UpdateOrganizationUserRequest { Role = "Editor" }));

        Assert.That(ex!.Key, Is.EqualTo(OrganizationsLog.Keys.OrgRoleLastAdmin));
        Assert.That(ex.Detail, Is.EqualTo("An organization needs at least one Admin."));
        Assert.That(m["admin"].Role, Is.EqualTo(OrganizationRole.Admin));
        Assert.That(log.Entries.Single().LogKey, Is.EqualTo(OrganizationsLog.Keys.OrgRoleLastAdmin));
        Assert.That(log.Entries.Single()["MembershipId"], Is.EqualTo(m["admin"].Id));
        Assert.That(repo.Updates, Is.Zero);
    }

    [Test]
    public async Task LoweringTheRoleOfAnAdmin_IsAllowedWhenAnotherAdminRemains()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("second", OrganizationRole.Admin));
        var repo = new FakeOrganizationsRepository(org);

        await Service(repo, new CapturingLogger<OrganizationsService>()).UpdateOrganizationUsersAsync(
            m["admin"].User!.AadId, m["admin"].User!.Id, org.Id, new UpdateOrganizationUserRequest { Role = "Viewer" });

        Assert.That(m["admin"].Role, Is.EqualTo(OrganizationRole.Viewer));
    }

    [Test]
    public void ChangingTheRoleOfARemovedMember_IsNotFound()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("gone", OrganizationRole.Viewer));
        m["gone"].Status = OrganizationUserStatus.Disabled;
        var log = new CapturingLogger<OrganizationsService>();

        var ex = Assert.ThrowsAsync<ResourceNotFoundException>(() => Service(new FakeOrganizationsRepository(org), log).UpdateOrganizationUsersAsync(
            m["admin"].User!.AadId, m["gone"].User!.Id, org.Id, new UpdateOrganizationUserRequest { Role = "Editor" }));

        Assert.That(ex!.Key, Is.EqualTo(OrganizationsLog.Keys.OrgRoleNotFound));
        Assert.That(ex.Detail, Is.EqualTo("This member is no longer in the team."));
        Assert.That(log.Entries.Single()["MembershipId"], Is.EqualTo(m["gone"].Id));
    }

    [Test]
    public void RemovingAMemberAlreadyRemoved_IsNotFound()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("gone", OrganizationRole.Viewer));
        m["gone"].Status = OrganizationUserStatus.Disabled;

        var ex = Assert.ThrowsAsync<ResourceNotFoundException>(() => Service(new FakeOrganizationsRepository(org), new CapturingLogger<OrganizationsService>())
            .RemoveCollaboratorAsync(m["admin"].User!.AadId, m["gone"].User!.Id, org.Id));

        Assert.That(ex!.Key, Is.EqualTo(OrganizationsLog.Keys.OrgRemoveNotFound));
    }

    [Test]
    public void InvitingAMember_OrAPendingInvitation_IsRefusedWithTheKeysAtInformation()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("editor", OrganizationRole.Editor));
        org.Users.Add(new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = org.Id, Email = "pending@test", Status = OrganizationUserStatus.Pending });
        var log = new CapturingLogger<OrganizationsService>();
        var service = Service(new FakeOrganizationsRepository(org), log);

        var member = Assert.ThrowsAsync<ConflictException>(() => service.InviteCollaboratorAsync(
            m["admin"].User!.AadId, org.Id, new InviteCollaboratorRequest { Email = "EDITOR@test", FullName = "x", Role = "Viewer" }));
        var pending = Assert.ThrowsAsync<ConflictException>(() => service.InviteCollaboratorAsync(
            m["admin"].User!.AadId, org.Id, new InviteCollaboratorRequest { Email = "pending@test", FullName = "x", Role = "Viewer" }));

        Assert.That(member!.Key, Is.EqualTo(OrganizationsLog.Keys.OrgInviteAlreadyMember));
        Assert.That(pending!.Key, Is.EqualTo(OrganizationsLog.Keys.OrgInviteAlreadyInvited));
        Assert.That(log.Entries.Select(e => e.Level), Is.All.EqualTo(Microsoft.Extensions.Logging.LogLevel.Information));
    }

    // The user row id and the AadId of a user differ (Build gives each its own Guid), as they do for a real user.

    [Test]
    public async Task AnAdminRemovesAMember_ByTheirOwnAadId()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("viewer", OrganizationRole.Viewer));
        Assert.That(m["admin"].User!.AadId, Is.Not.EqualTo(m["admin"].User!.Id));
        var repo = new FakeOrganizationsRepository(org);

        await Service(repo, new CapturingLogger<OrganizationsService>())
            .RemoveCollaboratorAsync(m["admin"].User!.AadId, m["viewer"].User!.Id, org.Id);

        Assert.That(m["viewer"].Status, Is.EqualTo(OrganizationUserStatus.Disabled));
        Assert.That(repo.Updates, Is.EqualTo(1));
    }

    [Test]
    public void ARequesterIsKnownByTheAadId_NotByTheUserRowId()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("viewer", OrganizationRole.Viewer));

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(new FakeOrganizationsRepository(org), new CapturingLogger<OrganizationsService>())
            .RemoveCollaboratorAsync(m["admin"].User!.Id, m["viewer"].User!.Id, org.Id));
        Assert.That(m["viewer"].Status, Is.EqualTo(OrganizationUserStatus.Active));
    }

    [Test]
    public void AMemberWhoIsNotAnAdmin_CannotRemoveAnyone()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("editor", OrganizationRole.Editor));

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(new FakeOrganizationsRepository(org), new CapturingLogger<OrganizationsService>())
            .RemoveCollaboratorAsync(m["editor"].User!.AadId, m["admin"].User!.Id, org.Id));
        Assert.That(m["admin"].Status, Is.EqualTo(OrganizationUserStatus.Active));
    }

    [Test]
    public void AnAdminCannotRemoveThemselves()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("second", OrganizationRole.Admin));

        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() => Service(new FakeOrganizationsRepository(org), new CapturingLogger<OrganizationsService>())
            .RemoveCollaboratorAsync(m["admin"].User!.AadId, m["admin"].User!.Id, org.Id).GetAwaiter().GetResult());
    }

    [Test]
    public async Task OnlyAnAdmin_ReadsTheOrganizationAndItsTeam()
    {
        var (org, m) = FakeOrganizationsRepository.Build(
            ("admin", OrganizationRole.Admin), ("editor", OrganizationRole.Editor), ("viewer", OrganizationRole.Viewer));
        var service = new OrganizationsService(new CapturingLogger<OrganizationsService>(), new FakeOrganizationsRepository(org), null!);

        Assert.That((await service.GetByIdAsync(m["admin"].User!.AadId, org.Id)).Id, Is.EqualTo(org.Id));
        Assert.That((await service.GetOrganizationUsersAsync(m["admin"].User!.AadId, org.Id)).Count(), Is.EqualTo(3));
        foreach (var role in new[] { "editor", "viewer" })
        {
            Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetByIdAsync(m[role].User!.AadId, org.Id));
            Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetOrganizationUsersAsync(m[role].User!.AadId, org.Id));
        }
    }

    [Test]
    public async Task TheListOfOrganizations_CarriesNoMembers()
    {
        var (org, m) = FakeOrganizationsRepository.Build(("admin", OrganizationRole.Admin), ("viewer", OrganizationRole.Viewer));
        var service = new OrganizationsService(new CapturingLogger<OrganizationsService>(), new FakeOrganizationsRepository(org), null!);

        var list = (await service.GetByUserAadIdAsync(m["viewer"].User!.AadId)).ToList();

        Assert.That(list.Single().Id, Is.EqualTo(org.Id));
        Assert.That(typeof(MyApp.Application.DTOs.Organizations.OrganizationDto).GetProperty("Users"), Is.Null);
    }
}
