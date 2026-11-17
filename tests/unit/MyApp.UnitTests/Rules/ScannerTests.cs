using MyApp.DocTools.CSharp;
using MyApp.DocTools.Markdown;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

[TestFixture]
public class ScannerTests
{
    [Test]
    public void CSharp_ReadsClassLevelTags()
    {
        var cs = CSharpScanner.Parse("x.cs", """
using NUnit.Framework;
// [Category("ignored")]
[TestFixture]
[Category("tasks"), Property("TestCase", "TASK-CREATE-005")]
[NUnit.Framework.Property("UserStory", "US-TASK-01")]
public sealed class Sample : Base
{
    [Test]
    public void T() { var s = "[Category(\"x\")] class Fake"; }
}
""");
        var c = cs.Classes.Single();
        Assert.That(c.Name, Is.EqualTo("Sample"));
        Assert.That(c.Categories, Is.EqualTo(new[] { "tasks" }));
        Assert.That(c.TestCaseTags, Is.EqualTo(new[] { "TASK-CREATE-005" }));
        Assert.That(c.UserStoryTags, Is.EqualTo(new[] { "US-TASK-01" }));
        Assert.That(c.IsTestClass);
        Assert.That(c.MethodTags, Is.Empty);
    }

    [Test]
    public void CSharp_FindsMethodTagsAndIgnoresStringsAndComments()
    {
        var cs = CSharpScanner.Parse("x.cs", "[TestFixture] class A { [Test, Category(\"a\")] public void T() { var x = new[] { 1 }; } /* [Category(\"b\")] */ }");
        Assert.That(cs.Classes.Single().MethodCategories, Is.EqualTo(new[] { "a" }));
        Assert.That(cs.Classes.Single().MethodTags, Is.Empty);
    }

    [Test]
    public void CSharp_AbstractBaseIsNotATestClass()
    {
        var cs = CSharpScanner.Parse("x.cs", "public abstract class Base { [SetUp] public void S() {} }");
        Assert.That(cs.Classes.Single().IsAbstract);
        Assert.That(cs.Classes.Single().IsTestClass, Is.False);
    }

    [Test]
    public void CSharp_HandlesVerbatimAndRawStringsWithBraces()
    {
        var cs = CSharpScanner.Parse("x.cs", "[TestFixture] class A { string a = @\"}{\"; string b = \"\"\"}}{\"\"\"; [Test] public void T() {} }\nclass B { }");
        Assert.That(cs.Classes.Select(c => c.Name), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(cs.Classes[0].IsTestClass);
    }

    [Test]
    public void Markdown_ParsesHeadingsTablesAndLists()
    {
        var doc = MarkdownDocument.Parse("# T\n\n```\n# not a heading\n```\n\n## A\n\n| X | Y |\n|---|---|\n| 1 | a \\| b |\n\n1. one\n2. two\n\n- b1\n\n## B\n\nNone.\n");
        Assert.That(doc.Headings.Select(h => h.Text), Is.EqualTo(new[] { "T", "A", "B" }));
        var a = doc.Section("A")!;
        var t = MarkdownDocument.ParseTables(a).Single();
        Assert.That(t.Rows[0], Is.EqualTo(new[] { "1", "a | b" }));
        Assert.That(MarkdownDocument.NumberedItems(a), Is.EqualTo(new[] { "one", "two" }));
        Assert.That(MarkdownDocument.Bullets(a), Is.EqualTo(new[] { "b1" }));
        Assert.That(MarkdownDocument.IsNone(doc.Section("B")!));
        Assert.That(MarkdownDocument.IsNone(a), Is.False);
    }

    [Test]
    public void Markdown_SlugFollowsGitHub()
    {
        Assert.That(MarkdownDocument.Slug("3.3 Roadmap features"), Is.EqualTo("33-roadmap-features"));
        Assert.That(MarkdownDocument.Slug("11. Traceability tags in code and tests"), Is.EqualTo("11-traceability-tags-in-code-and-tests"));
        Assert.That(MarkdownDocument.Slug("task-create-access-lost"), Is.EqualTo("task-create-access-lost"));
        Assert.That(MarkdownDocument.Slug("5.4 Runbook format (area `operations.md`)"), Is.EqualTo("54-runbook-format-area-operationsmd"));
    }
}
