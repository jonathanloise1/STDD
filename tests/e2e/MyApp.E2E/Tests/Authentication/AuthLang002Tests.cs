using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LANG-002: Menu entries change language with the app
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LANG-002")]
[Property("UserStory", "US-AUTH-08")]
public class AuthLang002Tests : LanguageTestBase
{
    [Test]
    [Description("The menu entries and the language button follow the language chosen.")]
    public async Task MenuEntriesChangeLanguageWithTheApp()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var menus = new HeaderMenusPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Click the "Change language" button.
        await menus.ClickLanguageButton();
        // 3. Click the "Italiano" button.
        await menus.ClickLanguage("Italiano");
        // After step 3, the menu entries are shown in Italian, and the language button shows "IT".
        await Expect(menu.Entry("Dashboard")).ToBeVisibleAsync();
        await Expect(menu.Entry("Attività")).ToBeVisibleAsync();
        await Expect(menu.Entry("Team")).ToBeVisibleAsync();
        await Expect(menu.Entry("Registro di audit")).ToBeVisibleAsync();
        await Expect(menu.Entry("Impostazioni")).ToBeVisibleAsync();
        await Expect(menus.LanguageButton).ToContainTextAsync("IT");
        // 4. Click the "Change language" button.
        await menus.ClickLanguageButton();
        // 5. Click the "English" button.
        await menus.ClickLanguage("English");
        // After step 5, the menu entries are "Dashboard", "Tasks", "Team", "Audit Log" and "Settings", and the language button shows "EN".
        await Expect(menu.Entry("Dashboard")).ToBeVisibleAsync();
        await Expect(menu.Entry("Tasks")).ToBeVisibleAsync();
        await Expect(menu.Entry("Team")).ToBeVisibleAsync();
        await Expect(menu.Entry("Audit Log")).ToBeVisibleAsync();
        await Expect(menu.Entry("Settings")).ToBeVisibleAsync();
        await Expect(menus.LanguageButton).ToContainTextAsync("EN");
    }
}
