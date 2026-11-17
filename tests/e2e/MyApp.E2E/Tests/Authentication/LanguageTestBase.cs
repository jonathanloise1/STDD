using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// Base class of the language test cases, which all start as Admin of "MyApp Dev Org" with English. After the
/// test, the same user puts the language back to English through the language menu (development.md 10.3:
/// restore the state through the UI), so a test that fails half-way does not leave Italian behind for the
/// next one.
/// </summary>
public abstract class LanguageTestBase : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    protected override async Task CleanUpAsync()
    {
        try
        {
            ServiceAnswersAgain();
            await new HeaderMenusPage(Page).SwitchToEnglishIfNeeded();
        }
        catch (Exception ex)
        {
            // Clean-up is best effort: a failed test may leave the page where the menu is not reachable
            Console.WriteLine($"[E2E] Clean-up of the language failed: {ex.Message}");
        }
    }
}
