using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LANG-003: The chosen language is kept at the next sign-in
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LANG-003")]
[Property("UserStory", "US-AUTH-08")]
public class AuthLang003Tests : LanguageTestBase
{
    protected override bool KeepsSavedLanguage => true;

    [Test]
    [Description("A user who chose Italian and signs in again finds the app in Italian.")]
    public async Task TheChosenLanguageIsKeptAtTheNextSignIn()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var menus = new HeaderMenusPage(Page);
        var visitor = new VisitorPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Click the "Change language" button.
        await menus.ClickLanguageButton();
        // 3. Click the "Italiano" button.
        await menus.ClickLanguage("Italiano");
        // 4. Click the "Dev User" button.
        await menus.ClickAvatar("Dev User");
        // 5. Click the "Logout" button.
        await menus.ClickLogout();
        // 6. Sign in as Admin.
        await visitor.SignInAs(TestUsers.AdminUser);
        // After step 6, the Dashboard page opens and the language button shows "IT".
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(menus.LanguageButton).ToContainTextAsync("IT");
        // After step 6, the menu entry "Tasks" is shown in Italian.
        await Expect(menu.Entry("Attività")).ToBeVisibleAsync();
        // 7. Click the "Change language" button.
        await menus.ClickLanguageButton();
        // 8. Click the "English" button.
        await menus.ClickLanguage("English");
        // After step 8, the language button shows "EN".
        await Expect(menus.LanguageButton).ToContainTextAsync("EN");
    }
}
