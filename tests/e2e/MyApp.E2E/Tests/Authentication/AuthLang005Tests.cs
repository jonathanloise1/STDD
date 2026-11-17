using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LANG-005: Saving the language when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LANG-005")]
[Property("UserStory", "US-AUTH-08")]
public class AuthLang005Tests : LanguageTestBase
{
    [Test]
    [Description("When the service does not answer while the chosen language is saved, the user is told so; the app still switches for the current session.")]
    public async Task SavingTheLanguageWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var address = new AddressPage(Page);
        var menus = new HeaderMenusPage(Page);

        // The service does not answer when the language choice is saved.
        ServiceDoesNotAnswer();
        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Click the "Change language" button.
        await menus.ClickLanguageButton();
        // 3. Click the "Italiano" button.
        await menus.ClickLanguage("Italiano");
        // The message appears in Italian, the language the app has just switched to.
        await Expect(address.Message("Il servizio non ha risposto. Riprova tra qualche minuto.")).ToBeVisibleAsync();
        // The language button shows "IT".
        await Expect(menus.LanguageButton).ToContainTextAsync("IT");
    }
}
