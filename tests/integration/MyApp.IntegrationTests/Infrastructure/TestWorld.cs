using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Persistence;

namespace MyApp.IntegrationTests.Infrastructure;

/// <summary>
/// A user created for one test: <see cref="Id"/> is its identity (the AadId, which the impersonation header
/// carries) and <see cref="RowId"/> is the id of its user row, which the team endpoints name. They differ on
/// purpose, as they do for a real user, so a mix-up of the two fails a test.
/// </summary>
public sealed record Person(Guid Id, string FirstName, string LastName, string Email, Guid RowId)
{
    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>
/// Builds the data a test needs. A test that changes data never changes the seed data (the users and
/// organizations of <c>SeedDataSet</c>), which the E2E tests rely on: it makes its own user, organization
/// and members, whose names no other run has used.
/// </summary>
public static class TestWorld
{
    /// <summary>
    /// A new user who belongs to no organization. The user row is written straight to the database, because
    /// the sign-in that creates it in production needs a real identity provider.
    /// </summary>
    public static async Task<Person> CreateUser(string firstName = "Fresh", bool completedOnboarding = true)
    {
        var id = Guid.NewGuid();
        var person = new Person(id, firstName, "Tester", $"it-{id:N}@myapp.test", Guid.NewGuid());

        using var scope = BackendHost.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyAppDbContext>();
        db.Users.Add(new User
        {
            Id = person.RowId,
            AadId = id,
            FirstName = person.FirstName,
            LastName = person.LastName,
            Email = person.Email,
            PreferredLanguage = "en",
            HasCompletedOnboarding = completedOnboarding
        });
        await db.SaveChangesAsync();
        return person;
    }

    /// <summary>The body of "create an organization" (the setup wizard), with a name no other run has used.</summary>
    public static object OrganizationBody(string? name = null) => new
    {
        name = name ?? $"IT Org {Guid.NewGuid().ToString("N")[..8]}",
        legalName = "Integration Test S.r.l.",
        vatNumber = "IT00000000001",
        fiscalCode = "00000000001",
        billingAddress = "Via Test 1",
        billingCity = "Milano",
        billingProvince = "MI",
        billingZipCode = "20100",
        billingCountryCode = "IT",
        billingEmail = "billing@myapp.test"
    };

    /// <summary>A new organization with a new Admin; the Admin is the user that created it.</summary>
    public static async Task<(Guid OrganizationId, Person Admin)> CreateOrganization()
    {
        var admin = await CreateUser("Admin");
        var response = await Api.Post("/api/organizations", OrganizationBody(), admin.Id);
        ApiAssert.IsSuccess(response, 201);
        return (Guid.Parse(response.String("id")!), admin);
    }

    /// <summary>
    /// A new member of the organization with this role ("Admin", "Editor" or "Viewer"): the Admin invites the
    /// email and the new user's first sign-in accepts the invitation (US-AUTH-05).
    /// </summary>
    public static async Task<Person> AddMember(Guid organizationId, Person admin, string role)
    {
        var member = await CreateUser(role);
        var invite = await Api.Post(
            $"/api/organizations/{organizationId}/collaborators/invite",
            new { fullName = member.FullName, email = member.Email, role },
            admin.Id);
        ApiAssert.IsSuccess(invite, 204);

        var sync = await Api.Post("/api/auth/sync", null, member.Id);
        ApiAssert.IsSuccess(sync);
        return member;
    }

    /// <summary>Creates a task as this user and returns its id. The due date is written yyyy-MM-dd.</summary>
    public static async Task<Guid> CreateTask(
        Guid organizationId, Person user, string title, string? description = null, string? dueDate = null)
    {
        var body = new Dictionary<string, object?> { ["title"] = title, ["description"] = description, ["dueDate"] = dueDate };
        var response = await Api.Post(TasksPath(organizationId), body, user.Id);
        ApiAssert.IsSuccess(response, 201);
        return Guid.Parse(response.String("id")!);
    }

    public static string TasksPath(Guid organizationId) => $"/api/organizations/{organizationId}/tasks";
}
