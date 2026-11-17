using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// Base class of the E2E tests of the help area for a signed-in user.
///
/// Precondition of every test of the area: the manual and the help pack are built
/// (<see cref="HelpPackFiles.BuildCommand"/> in <c>src/frontend</c>). They are generated and git-ignored, so
/// until the owner approves that step the tests fail at once (or, while docs/manual has no pages, are skipped with a message that says so) with a message that says so, instead of timing out.
///
/// The tests need no model. By default the session is told that no assistant writes answers; a test case that
/// needs the server tier answering calls one of the methods below, which stand in for it in the browser
/// session (<see cref="HelpStandIns"/>). A test class never names a route.
/// </summary>
public abstract class HelpTestBase : AuthenticatedTestBase
{
    [OneTimeSetUp]
    public void TheManualAndThePackAreBuilt()
    {
        // No manual pages yet: there is nothing to build a pack from. The tests are skipped, which a run reports as
        // skipped, not passed: a green run does not mean the help area was covered.
        if (!HelpPackFiles.ManualHasPages())
            Assert.Ignore(
                "HELP TESTS SKIPPED, NOT PASSED: docs/manual has no pages, so no help pack can be built. " +
                "A green run does not cover the help area until the manual has pages and " +
                $"`{HelpPackFiles.BuildCommand}` has run.");

        var missing = HelpPackFiles.MissingFiles();
        if (missing.Count > 0)
            Assert.Fail(
                $"The help tests need the generated manual and help pack, and these files are missing under " +
                $"{HelpPackFiles.Folder}: {string.Join(", ", missing)}. Build them with " +
                $"`{HelpPackFiles.BuildCommand}` in src/frontend (the owner approves that step), then run the tests again.");
    }

    [SetUp]
    public async Task NoAssistantWritesAnswers()
    {
        await HelpStandIns.AssistantWritesNoAnswers(AuthContext);
    }

    /// <summary>
    /// A question that no other run has asked: the text with a word made of letters after the run's suffix in
    /// front of it. Letters only, so the masking of numbers in the downloaded file never touches it.
    /// </summary>
    protected string UniqueQuestion(string text)
    {
        var word = new string(TestRunId[^6..].Select(c => (char)('a' + c % 26)).ToArray());
        return $"{word} {text}";
    }

    // ----- "The assistant is available on the server and ..." -----

    /// <summary>"... answers {question} citing the page "Create a task" as its source."</summary>
    protected Task ServerAssistantAnswersCitingCreateATask()
    {
        return HelpStandIns.ServerAssistantAnswersCiting(
            AuthContext, "Open the Tasks page, click New Task, type the title and click Create Task.",
            "tasks/US-TASK-01");
    }

    /// <summary>"... answers {question} with no source."</summary>
    protected Task ServerAssistantAnswersWithNoSource()
    {
        return HelpStandIns.ServerAssistantAnswersWithoutSource(
            AuthContext, "You create a task from the Tasks page with the New Task button.");
    }

    /// <summary>"... declines to answer {question}."</summary>
    protected Task ServerAssistantDeclines()
    {
        return HelpStandIns.ServerAssistantDeclines(AuthContext, DeclineSentence);
    }

    /// <summary>"... takes too long to answer {question}."</summary>
    protected Task ServerAssistantTakesTooLong()
    {
        return HelpStandIns.ServerAssistantIsTooSlow(AuthContext);
    }

    // ----- "The device can run the assistant and ..." (the browser tier) -----

    /// <summary>
    /// "The device can run the assistant and the first download is {size} MB." The panel asks before a download
    /// larger than 100 MB (the default of the status).
    /// </summary>
    protected Task TheDeviceCanRunTheAssistantAndTheFirstDownloadIs(int sizeMb)
    {
        return HelpStandIns.BrowserAssistantOnDevice(
            AuthContext, HelpStandIns.BrowserDevice.Capable, sizeMb, askBeforeDownloadMb: 100);
    }

    /// <summary>"The device can run the assistant and the download is at {done} of {total} MB."</summary>
    protected Task TheDeviceCanRunTheAssistantAndTheDownloadIsAt(int doneMb, int totalMb)
    {
        // The download is under way, so the panel has nothing left to ask
        return HelpStandIns.BrowserAssistantOnDevice(
            AuthContext, HelpStandIns.BrowserDevice.Capable, totalMb, askBeforeDownloadMb: totalMb * 10, downloadedMb: doneMb);
    }

    /// <summary>The sentence the assistant uses to decline, which the panel must never show.</summary>
    protected static string DeclineSentence => HelpPackFiles.RefusalSentence("en");

    // ----- "The help content cannot be loaded." -----

    /// <summary>"The content of the manual cannot be loaded."</summary>
    protected Task TheManualContentCannotBeLoaded() => HelpStandIns.ManualContentFails(AuthContext);

    /// <summary>"The content of the manual cannot be loaded for the search."</summary>
    protected Task TheContentForTheSearchCannotBeLoaded() => HelpStandIns.PackFails(AuthContext);

    /// <summary>"The help content cannot be loaded."</summary>
    protected async Task TheHelpContentCannotBeLoaded()
    {
        await HelpStandIns.ManualContentFails(AuthContext);
        await HelpStandIns.PackFails(AuthContext);
    }
}
