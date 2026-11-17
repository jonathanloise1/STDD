using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LANG-004: Only English and Italiano are offered
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LANG-004")]
[Property("UserStory", "US-AUTH-08")]
public class AuthLang004Tests : LanguageTestBase
{
    [Test]
    [Description("The language menu lists exactly two languages and a user with no choice sees English.")]
    public async Task OnlyEnglishAndItalianoAreOffered()
    {
        var address = new AddressPage(Page);
        var menus = new HeaderMenusPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Click the "Change language" button.
        await menus.ClickLanguageButton();
        // The language button shows "EN".
        await Expect(menus.LanguageButton).ToContainTextAsync("EN");
        // The language menu lists "English" and "Italiano" and no other language.
        await Expect(menus.LanguageOptions).ToHaveTextAsync(new[] { "English", "Italiano" });
    }
}
