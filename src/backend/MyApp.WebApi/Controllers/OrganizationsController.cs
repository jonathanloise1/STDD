using MyApp.Application.DTOs.Organizations;
using MyApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// Controller responsible for managing organizations and collaborators.
/// </summary>
/// <userstory ref="US-ORG-01, US-ORG-02, US-ORG-03, US-ORG-04, US-ORG-05, US-ORG-06, US-TEAM-01, US-TEAM-02, US-TEAM-04, US-TEAM-05, US-TEAM-06, US-TEAM-07, US-TEAM-11, US-TEAM-12, US-TEAM-13, US-TEAM-14, US-TEAM-15" />
[Route("api/[controller]")]
public class OrganizationsController(
    ILogger<OrganizationsController> logger,
    IOrganizationsService organizationService) : AuthenticatedControllerBase
{
    /// <summary>
    /// Returns all organizations the authenticated user is a member of.
    /// </summary>
    /// <userstory ref="US-ORG-01" />
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

        logger.LogInformation("User is member of {Count} organizations", orgs.Count());

        return Ok(orgs);
    }

    /// <summary>
    /// Creates a new organization and assigns the authenticated user as owner.
    /// </summary>
    /// <userstory ref="US-ORG-02" />
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId
        });

        logger.LogDebug("Create organization request received: {Request}", 
            JsonConvert.SerializeObject(request));

        var result = await organizationService.CreateAsync(
            AuthenticatedUserId,
            request,
            cancellationToken);

        logger.LogInformation("Organization created with ID {OrganizationId}", result.Id);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates organization details. Only owners can perform this action.
    /// </summary>
    /// <userstory ref="US-ORG-04" />
    [HttpPut("{id}")]
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

        logger.LogDebug("Update organization request: {Request}", 
            JsonConvert.SerializeObject(request));

        try
        {
            var result = await organizationService.UpdateAsync(
                id,
                request,
                AuthenticatedUserId,
                cancellationToken);

            logger.LogInformation("Organization updated successfully");

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized update attempt: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Update failed — not found: {Message}", ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error updating organization");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Marks the organization as deleted (soft delete).
    /// </summary>
    /// <userstory ref="US-ORG-06" />
    [HttpDelete("{organizationId}")]
    public async Task<IActionResult> SoftDeleteOrganization(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId
        });

        logger.LogDebug("Soft delete organization requested");

        try
        {
            await organizationService.SoftDeleteAsync(
                AuthenticatedUserId,
                organizationId,
                cancellationToken);

            logger.LogInformation("Organization soft deleted");

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized soft delete attempt: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Soft delete failed — not found: {Message}", ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error soft deleting organization");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Gets organization details by ID.
    /// </summary>
    /// <userstory ref="US-ORG-03" />
    [HttpGet("{id}")]
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

        var organization = await organizationService.GetByIdAsync(id, cancellationToken);

        if (organization is null)
        {
            logger.LogWarning("Organization not found");
            return NotFound();
        }

        logger.LogDebug("Organization retrieved successfully");

        return Ok(organization);
    }

    /// <summary>
    /// Gets all users (members) of an organization.
    /// </summary>
    [HttpGet("{organizationId}/users")]
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

        try
        {
            var users = await organizationService.GetOrganizationUsersAsync(
                AuthenticatedUserId,
                organizationId,
                cancellationToken);

            logger.LogDebug("Returned {Count} users for organization", users.Count());

            return Ok(users);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized access to organization users: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Organization not found: {Message}", ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error fetching organization users");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Sends an invitation to a new collaborator for the specified organization.
    /// </summary>
    /// <userstory ref="US-ORG-08" />
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

        logger.LogDebug("Inviting collaborator: {Request}", JsonConvert.SerializeObject(request));

        try
        {
            await organizationService.InviteCollaboratorAsync(
                AuthenticatedUserId,
                organizationId,
                request,
                cancellationToken);

            logger.LogInformation("Invitation sent to {Email}", request.Email);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized collaborator invitation: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Invitation failed: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error inviting collaborator");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Removes a collaborator from an organization. Only owners can perform this action.
    /// </summary>
    /// <userstory ref="US-ORG-12" />
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

        try
        {
            await organizationService.RemoveCollaboratorAsync(
                AuthenticatedUserId,
                userId,
                organizationId,
                cancellationToken);

            logger.LogInformation("Collaborator removed successfully");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Removal failed: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized removal attempt: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error removing collaborator");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Updates the role and permissions of a collaborator in an organization.
    /// </summary>
    /// <userstory ref="US-ORG-11, US-ORG-13" />
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

        logger.LogDebug("Updating collaborator: {Request}", JsonConvert.SerializeObject(request));

        try
        {
            await organizationService.UpdateOrganizationUsersAsync(
                AuthenticatedUserId,
                userId,
                organizationId,
                request,
                cancellationToken
            );

            logger.LogInformation("Collaborator updated successfully");

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized update attempt on collaborator: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Update collaborator failed: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error updating collaborator");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Returns a list of all pending invitations in the specified organization.
    /// Only accessible by organization Owners.
    /// </summary>
    /// <userstory ref="US-ORG-09" />
    [HttpGet("{organizationId}/invitations/pending")]
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

        try
        {
            var result = await organizationService.GetPendingInvitationsAsync(
                AuthenticatedUserId,
                organizationId,
                cancellationToken);

            logger.LogDebug("Returned {Count} pending invitations", result.Count());

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized access to pending invitations: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Organization not found: {Message}", ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error fetching pending invitations");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

}
