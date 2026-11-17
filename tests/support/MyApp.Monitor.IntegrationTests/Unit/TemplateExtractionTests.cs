using MyApp.Monitor.Analysis;
using MyApp.Monitor.Sources;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Unit;

public class TemplateExtractionTests
{
    [Test]
    public void Normalises_variable_parts_in_the_specified_order()
    {
        var text = TemplateExtractor.Normalise("User 6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72 logged in from 10.0.0.1 at 2026-11-06T10:42:01Z with token abc12345def");
        Assert.That(text, Is.EqualTo("User <guid> logged in from <ip> at <ts> with token <hex>"));
    }

    [TestCase("Mail to john.doe@example.com failed", "Mail to <email> failed")]
    [TestCase("GET https://example.com/a?b=1 failed", "GET <url> failed")]
    [TestCase("Peer 2001:db8::1 refused", "Peer <ip> refused")]
    [TestCase("Peer ::1 refused", "Peer <ip> refused")]
    [TestCase("Failed to open file \"C:\\data\\x.txt\" after 3 retries", "Failed to open file <str> after <num> retries")]
    [TestCase("Took 12.5 ms for 4 items", "Took <num> ms for <num> items")]
    [TestCase("Blob deadbeef01 missing", "Blob <hex> missing")]
    [TestCase("user's data   \t is   here", "user's data is here")]
    [TestCase("Used utf8 encoding in v2", "Used utf8 encoding in v2")]
    public void Normalises_each_kind(string input, string expected) =>
        Assert.That(TemplateExtractor.Normalise(input), Is.EqualTo(expected));

    [Test]
    public void Normalised_text_is_cut_at_300_characters() =>
        Assert.That(TemplateExtractor.Normalise(new string('x', 500)), Has.Length.EqualTo(300));

    [Test]
    public void Golden_canonical_string_and_id_for_a_free_message()
    {
        var record = Fx.Record(message: "User 6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72 logged in from 10.0.0.1 at 2026-11-06T10:42:01Z with token abc12345def");
        var t = TemplateExtractor.Extract(record);
        Assert.That(t.Canonical, Is.EqualTo("\nMyApp.Test\n\n\nUser <guid> logged in from <ip> at <ts> with token <hex>"));
        Assert.That(t.Id, Is.EqualTo("t-9ca22cc87a82"));
    }

    [Test]
    public void Golden_canonical_string_and_id_for_a_quoted_message()
    {
        var t = TemplateExtractor.Extract(Fx.Record(message: "Failed to open file \"C:\\data\\x.txt\" after 3 retries"));
        Assert.That(t.Canonical, Is.EqualTo("\nMyApp.Test\n\n\nFailed to open file <str> after <num> retries"));
        Assert.That(t.Id, Is.EqualTo("t-410891daaf9c"));
    }

    [Test]
    public void Platform_events_use_their_discriminator_properties_in_table_order()
    {
        var record = Fx.Record(
            level: RecordLevel.Error, eventName: "platform.request.unhandled", category: "MyApp.WebApi.Middleware.ExceptionHandlingMiddleware",
            message: "Unhandled exception somewhere", exceptionType: "System.InvalidOperationException",
            topFrame: "MyApp.Application.Services.TaskItemService.Create",
            props: new Dictionary<string, string>
            {
                ["Route"] = "api/tasks", ["TopFrame"] = "MyApp.Application.Services.TaskItemService.Create", ["ExceptionType"] = "System.InvalidOperationException",
            });
        var t = TemplateExtractor.Extract(record);
        Assert.That(t.Discriminator, Is.EqualTo("System.InvalidOperationException|MyApp.Application.Services.TaskItemService.Create|api/tasks"));
        Assert.That(t.Canonical, Is.EqualTo(
            "platform.request.unhandled\nMyApp.WebApi.Middleware.ExceptionHandlingMiddleware\nSystem.InvalidOperationException\nMyApp.Application.Services.TaskItemService.Create\n" +
            "System.InvalidOperationException|MyApp.Application.Services.TaskItemService.Create|api/tasks"));
        Assert.That(t.Id, Is.EqualTo("t-28a840784043"));
    }

    [TestCase("platform.request.rate_limited", "Policy", "DefaultPerUser", "DefaultPerUser")]
    [TestCase("platform.health.unhealthy", "Check", "sqlserver", "sqlserver")]
    [TestCase("monitor.api.unreachable", "Url", "https://x/health", "https://x/health")]
    public void Single_property_discriminators(string eventName, string property, string value, string expected)
    {
        var record = Fx.Record(eventName: eventName, props: new Dictionary<string, string> { [property] = value, ["Other"] = "ignored" });
        Assert.That(TemplateExtractor.Extract(record).Discriminator, Is.EqualTo(expected));
    }

    [Test]
    public void A_missing_discriminator_property_counts_as_empty()
    {
        var record = Fx.Record(eventName: "platform.job.failed", props: new Dictionary<string, string> { ["Job"] = "Cleanup" });
        Assert.That(TemplateExtractor.Extract(record).Discriminator, Is.EqualTo("Cleanup||"));
    }

    [Test]
    public void The_template_wins_over_the_message_and_the_id_ignores_variable_data()
    {
        var a = Fx.Record(message: "Loaded 3 items for 6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72", template: "Loaded {Count} items for {UserId}");
        var b = Fx.Record(message: "Loaded 99 items for 0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91", template: "Loaded {Count} items for {UserId}");
        Assert.That(TemplateExtractor.Extract(a).Id, Is.EqualTo(TemplateExtractor.Extract(b).Id));
        Assert.That(TemplateExtractor.Extract(a).Discriminator, Is.EqualTo("Loaded {Count} items for {UserId}"));
    }

    [Test]
    public void The_same_input_always_gives_the_same_id_and_different_inputs_differ()
    {
        var one = TemplateExtractor.Extract(Fx.Record(message: "disk is full")).Id;
        Assert.That(TemplateExtractor.Extract(Fx.Record(message: "disk is full")).Id, Is.EqualTo(one));
        Assert.That(TemplateExtractor.Extract(Fx.Record(message: "disk is nearly full")).Id, Is.Not.EqualTo(one));
        Assert.That(TemplateExtractor.Extract(Fx.Record(message: "disk is full", category: "Other")).Id, Is.Not.EqualTo(one));
        Assert.That(one, Does.Match("^t-[0-9a-f]{12}$"));
    }

    [Test]
    public void Version_host_and_timestamp_do_not_change_the_id()
    {
        var a = Fx.Record(message: "disk is full", version: "1.0.0");
        var b = Fx.Record(message: "disk is full", version: "2.0.0", at: DateTimeOffset.UnixEpoch);
        Assert.That(TemplateExtractor.Extract(a).Id, Is.EqualTo(TemplateExtractor.Extract(b).Id));
    }
}
