using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LANG-001: Choosing a language switches the app at once
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LANG-001")]
[Property("UserStory", "US-AUTH-08")]
public class AuthLang001Tests : LanguageTestBase
{
    [Test]
    [Description("A user switches the app to Italian and back to English, and each choice changes the text and shows a message.")]
    public async Task ChoosingALanguageSwitchesTheAppAtOnce()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var menus = new HeaderMenusPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // The page is marked to tell later whether it was reloaded.
        await address.MarkThePage();
        // 2. Click the "Change language" button.
        await menus.ClickLanguageButton();
        // 3. Click the "Italiano" button.
        await menus.ClickLanguage("Italiano");
        // After step 3, the menu entry "Tasks" is shown in Italian and a message confirms the change in Italian.
        await Expect(menu.Entry("Attività")).ToBeVisibleAsync();
        await Expect(address.Message("Lingua cambiata in Italiano")).ToBeVisibleAsync();
        // 4. Click the "Change language" button.
        await menus.ClickLanguageButton();
        // 5. Click the "English" button.
        await menus.ClickLanguage("English");
        // After step 5, the message "Language changed to English" appears.
        await Expect(address.Message("Language changed to English")).ToBeVisibleAsync();
        // After step 5, the menu entry "Tasks" is shown in English again.
        await Expect(menu.Entry("Tasks")).ToBeVisibleAsync();
        // The page was not reloaded between the steps.
        await Expect(address.MarkedPage).ToHaveCountAsync(1);
    }
}
