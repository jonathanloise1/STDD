using MyApp.Application.Help;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
[Property("UserStory", "US-HELP-07")]
public class GapMaskingTests
{
    [Test]
    public void TheQuestionOfTheTestCase_IsMasked()
    {
        var masked = GapMasking.Mask("can i pay invoice 12345678 from here, write to anna.rossi@example.com or see https://example.com/pay");

        Assert.That(masked, Is.EqualTo("can i pay invoice [number] from here, write to [email] or see [url]"));
    }

    [TestCase("call +39 333 123 4567 now", "call +[number] now")]
    [TestCase("card 1234-5678-9012-3456 failed", "card [number] failed")]
    [TestCase("code 1.234 is wrong", "code [number] is wrong")]
    [TestCase("room 123 is free", "room 123 is free")]
    [TestCase("see www.example.com/a?b=1234, please", "see [url], please")]
    [TestCase("is http://localhost:3147/tasks/6f1c ok?", "is [url] ok?")]
    [TestCase("mail a.b+c@sub.example.co.uk and d@e.it", "mail [email] and [email]")]
    [TestCase("a @ b and 12 34", "a @ b and [number]")]
    public void AddressesAndNumbers_AreReplaced(string question, string expected) =>
        Assert.That(GapMasking.Mask(question), Is.EqualTo(expected));

    [Test]
    public void TheQuestion_IsCutAtThreeHundredCodePointsAfterMasking()
    {
        var question = "https://example.com/" + new string('a', 500) + " " + new string('x', 400);

        var masked = GapMasking.Mask(question);

        Assert.That(masked, Does.StartWith("[url] x"));
        Assert.That(AssistantProtocol.CodePointLength(masked), Is.EqualTo(300));
    }

    [Test]
    public void ACutNeverSplitsASurrogatePair()
    {
        var masked = GapMasking.Mask(new string('a', 299) + "\U0001F600" + "tail");

        Assert.That(AssistantProtocol.CodePointLength(masked), Is.EqualTo(300));
        Assert.That(masked, Does.EndWith("\U0001F600"));
    }
}
