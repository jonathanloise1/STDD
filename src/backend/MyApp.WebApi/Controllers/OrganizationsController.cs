using MyApp.Application.DTOs.Organizations;
using MyApp.Application.Interfaces;
using MyApp.Application.Logging;
using MyApp.WebApi.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// Controller responsible for managing organizations and collaborators. Failures become problem documents
/// in <c>ExceptionHandlingMiddleware</c>; the services raise the keyed outcomes.
/// </summary>
[Route("api/[controller]")]
public class OrganizationsController(
    ILogger<OrganizationsController> logger,
    IOrganizationsService organizationService) : AuthenticatedControllerBase
{
    /// <summary>
    /// Returns all organizations the authenticated user is a member of.
    /// </summary>
    // US-ORG-01
    [HttpGet]
    public async Task<IActionResult> GetMyOrganizations(CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId
        });

        logger.LogDebug("Fetching organizations for authenticated user");

        var orgs = await organizationService.GetByUserAadIdAsync(
            AuthenticatedUserId,
            cancellationToken);

        return Ok(orgs);
    }

    /// <summary>
    /// Creates a new organization and assigns the authenticated user as Admin.
    /// </summary>
    // US-ONBOARD-02
    [HttpPost]
    [ServiceUnavailable(OnboardingLog.Keys.OnboardWizUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId
        });

        logger.LogDebug("Create organization request received");

        var result = await organizationService.CreateAsync(
            AuthenticatedUserId,
            request,
            cancellationToken);

        logger.LogInformation("Organization created with ID {OrganizationId}", result.Id);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates organization details. Only Admins can perform this action.
    /// </summary>
    // US-ORG-02
    [HttpPut("{id}")]
    [ServiceUnavailable(OrganizationsLog.Keys.OrgUpdateUnavailable)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = id
        });

        logger.LogDebug("Update organization request received");

        var result = await organizationService.UpdateAsync(
            id,
            request,
            AuthenticatedUserId,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Uploads the organization logo (multipart field <c>file</c>, an image of at most 5 MB). Admins only.
    /// </summary>
    // US-ORG-03
    [HttpPost("{id}/logo")]
    [EnableRateLimiting("FileUpload")]
    [ServiceUnavailable(OrganizationsLog.Keys.OrgLogoUnavailable)]
    public async Task<IActionResult> UploadLogo(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = id
        });

        await using var stream = file.OpenReadStream();
        var result = await organizationService.UploadLogoAsync(
            AuthenticatedUserId, id, file.ContentType, file.Length, stream, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Gets organization details by ID.
    /// </summary>
    // US-ORG-01
    [HttpGet("{id}")]
    [ServiceUnavailable(OrganizationsLog.Keys.OrgViewUnavailable)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = id
        });

        logger.LogDebug("Fetching organization details");

        var organization = await organizationService.GetByIdAsync(AuthenticatedUserId, id, cancellationToken);
        return Ok(organization);
    }

    /// <summary>
    /// Gets all users (members) of an organization.
    /// </summary>
    // US-ORG-04
    [HttpGet("{organizationId}/users")]
    [ServiceUnavailable(OrganizationsLog.Keys.OrgTeamUnavailable)]
    public async Task<IActionResult> GetOrganizationUsers(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId
        });

        logger.LogDebug("Fetching organization users");

        var users = await organizationService.GetOrganizationUsersAsync(
            AuthenticatedUserId,
            organizationId,
            cancellationToken);

        return Ok(users);
    }

    /// <summary>
    /// Sends an invitation to a new collaborator for the specified organization.
    /// </summary>
    // US-ORG-05
    [HttpPost("{organizationId}/collaborators/invite")]
    public async Task<IActionResult> InviteCollaborator(
        Guid organizationId,
        [FromBody] InviteCollaboratorRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId
        });

        logger.LogDebug("Inviting a collaborator");

        await organizationService.InviteCollaboratorAsync(
            AuthenticatedUserId,
            organizationId,
            request,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Removes a collaborator from an organization. Only Admins can perform this action.
    /// </summary>
    // US-ORG-07
    [HttpDelete("{organizationId}/collaborators/{userId}")]
    public async Task<IActionResult> RemoveCollaborator(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId,
            ["TargetUserId"] = userId
        });

        logger.LogDebug("Removing collaborator from organization");

        await organizationService.RemoveCollaboratorAsync(
            AuthenticatedUserId,
            userId,
            organizationId,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Updates the role of a collaborator in an organization.
    /// </summary>
    // US-ORG-06
    [HttpPatch("{organizationId}/collaborators/{userId}")]
    public async Task<IActionResult> UpdateOrganizationUser(
        Guid organizationId,
        Guid userId,
        [FromBody] UpdateOrganizationUserRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId,
            ["TargetUserId"] = userId
        });

        logger.LogDebug("Updating collaborator");

        await organizationService.UpdateOrganizationUsersAsync(
            AuthenticatedUserId,
            userId,
            organizationId,
            request,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Returns a list of all pending invitations in the specified organization.
    /// Only accessible by organization Admins.
    /// </summary>
    // US-ORG-04
    [HttpGet("{organizationId}/invitations/pending")]
    [ServiceUnavailable(OrganizationsLog.Keys.OrgTeamUnavailable)]
    public async Task<IActionResult> GetPendingInvitations(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId
        });

        logger.LogDebug("Fetching pending invitations");

        var result = await organizationService.GetPendingInvitationsAsync(
            AuthenticatedUserId,
            organizationId,
            cancellationToken);

        return Ok(result);
    }
}
