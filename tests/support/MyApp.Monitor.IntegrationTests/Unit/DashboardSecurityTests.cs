using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Dashboard;
using MyApp.Monitor.Pack;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Unit;

public class DashboardAuthenticationTests
{
    private static IConfiguration Config(params (string, string)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Item1, v => (string?)v.Item2)).Build();

    private static string? Validate(string auth, string? urls, string? group = null, IConfiguration? config = null, bool enabled = true) =>
        DashboardAuthentication.Validate(new DashboardSettings { Enabled = enabled, Auth = auth, AllowedGroupId = group }, urls, config ?? Config());

    [TestCase("http://localhost:5310")]
    [TestCase("http://127.0.0.1:0")]
    [TestCase("http://[::1]:5310")]
    [TestCase("http://localhost:5310;http://127.0.0.1:5311")]
    public void Local_authentication_accepts_loopback_addresses_only(string urls) => Assert.That(Validate("local", urls), Is.Null);

    [TestCase("http://0.0.0.0:5310")]
    [TestCase("http://+:5310")]
    [TestCase("http://*:5310")]
    [TestCase("http://192.168.1.20:5310")]
    [TestCase("http://monitor.example.test:5310")]
    [TestCase("http://localhost:5310;http://+:5311")]
    public void Local_authentication_refuses_anything_that_is_not_loopback(string urls) =>
        Assert.That(Validate("local", urls), Does.Contain("loopback"));

    [Test]
    public void Entra_authentication_needs_its_tenant_client_and_the_allowed_group()
    {
        Assert.That(Validate("entra", "http://+:8080", "g1"), Does.Contain("TenantId"));
        var config = Config(("Monitor:Dashboard:AzureAd:TenantId", "t"), ("Monitor:Dashboard:AzureAd:ClientId", "c"));
        Assert.That(Validate("entra", "http://+:8080", null, config), Does.Contain("allowed_group_id"));
        Assert.That(Validate("entra", "http://+:8080", "g1", config), Is.Null, "any address is fine once people sign in");
    }

    [Test]
    public void An_unknown_mode_is_refused_and_a_disabled_dashboard_needs_nothing()
    {
        Assert.That(Validate("basic", "http://localhost:5310"), Does.Contain("'local' or 'entra'"));
        Assert.That(Validate("local", "http://0.0.0.0:1", enabled: false), Is.Null);
    }

    [TestCase("localhost", true)]
    [TestCase("LOCALHOST", true)]
    [TestCase("127.0.0.1", true)]
    [TestCase("127.0.0.2", true)]
    [TestCase("[::1]", true)]
    [TestCase("::1", true)]
    [TestCase("monitor.example.test", false)]
    [TestCase("10.0.0.5", false)]
    [TestCase("localhost.evil.test", false)]
    public void Loopback_hosts_are_recognised(string host, bool loopback) => Assert.That(DashboardAuthentication.IsLoopbackHost(host), Is.EqualTo(loopback));

    private static ClaimsPrincipal User(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    private static IAuthorizationService Authorization(string group)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthorizationHandler, GroupRequirementHandler>();
        DashboardAuthentication.AddPolicies(services, new DashboardSettings { Auth = "entra", AllowedGroupId = group });
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    [Test]
    public async Task The_dashboard_policy_lets_in_the_members_of_the_allowed_group_only()
    {
        var auth = Authorization("11111111-1111-4111-8111-111111111111");
        Assert.That((await auth.AuthorizeAsync(User(new Claim("groups", "11111111-1111-4111-8111-111111111111")), DashboardRoles.DashboardPolicy)).Succeeded, Is.True);
        Assert.That((await auth.AuthorizeAsync(User(new Claim("groups", "11111111-1111-4111-8111-111111111111".ToUpperInvariant())), DashboardRoles.DashboardPolicy)).Succeeded, Is.True);
        Assert.That((await auth.AuthorizeAsync(User(new Claim("groups", "22222222-2222-4222-8222-222222222222")), DashboardRoles.DashboardPolicy)).Succeeded, Is.False);
        Assert.That((await auth.AuthorizeAsync(User(), DashboardRoles.DashboardPolicy)).Succeeded, Is.False, "signed in but in no group");
        Assert.That((await auth.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), DashboardRoles.DashboardPolicy)).Succeeded, Is.False, "not signed in");
    }

    [Test]
    public async Task The_ingest_policy_needs_the_role_incidents_ingest()
    {
        var auth = Authorization("g");
        Assert.That((await auth.AuthorizeAsync(User(new Claim(ClaimTypes.Role, "Incidents.Ingest")), DashboardRoles.IngestPolicy)).Succeeded, Is.True);
        Assert.That((await auth.AuthorizeAsync(User(new Claim(ClaimTypes.Role, "Incidents.Read")), DashboardRoles.IngestPolicy)).Succeeded, Is.False);
        Assert.That((await auth.AuthorizeAsync(User(new Claim("groups", "g")), DashboardRoles.IngestPolicy)).Succeeded, Is.False, "a group member is not an ingest client");
    }

    [Test]
    public void The_user_name_is_the_signed_in_person_or_the_developer_in_local_mode()
    {
        Assert.That(DashboardAuthentication.UserName(User(new Claim("preferred_username", "ann@example.test"))), Is.EqualTo("ann@example.test"));
        Assert.That(DashboardAuthentication.UserName(new ClaimsPrincipal(new ClaimsIdentity())), Is.EqualTo(Environment.UserName));
    }
}

public class ReportTextTests
{
    [Test]
    public void A_report_from_another_monitor_is_masked_again_in_its_text_fields_and_its_identifiers_are_left_alone()
    {
        var report = (JsonObject)JsonNode.Parse("""
            {
              "id": "INC-20261106-0001", "fingerprint": "9c41d27a05be3f18",
              "correlation": { "trace_ids": ["4bf92f3577b34da6a3ce929d0e0e4736"], "entities": [{ "entity": "user", "id": "1234567890123" }] },
              "evidence": [ { "id": "E1", "source": "signal", "reference": "applogs: jane.doe@example.com", "observed_at": "2026-11-06T10:00:00Z", "excerpt": "mail jane.doe@example.com ip 10.1.2.3" } ],
              "rule_verdict": { "cause": "Card 4111111111111111 declined", "evidence_refs": ["E1"] },
              "outcome": { "note": "call +39 333 1234567", "ticket": "T-1" }
            }
            """)!;
        IncidentReports.MaskAllText(report, Fx.Masker());

        Assert.That(report["evidence"]![0]!["excerpt"]!.GetValue<string>(), Is.EqualTo("mail [email] ip [ip]"));
        Assert.That(report["evidence"]![0]!["reference"]!.GetValue<string>(), Is.EqualTo("applogs: [email]"));
        Assert.That(report["rule_verdict"]!["cause"]!.GetValue<string>(), Is.EqualTo("Card [card] declined"));
        Assert.That(report["outcome"]!["note"]!.GetValue<string>(), Does.Contain("[phone]"));
        Assert.That(report["id"]!.GetValue<string>(), Is.EqualTo("INC-20261106-0001"));
        Assert.That(report["fingerprint"]!.GetValue<string>(), Is.EqualTo("9c41d27a05be3f18"));
        Assert.That(report["correlation"]!["trace_ids"]![0]!.GetValue<string>(), Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"));
        Assert.That(report["correlation"]!["entities"]![0]!["id"]!.GetValue<string>(), Is.EqualTo("1234567890123"));
        Assert.That(report["evidence"]![0]!["observed_at"]!.GetValue<string>(), Is.EqualTo("2026-11-06T10:00:00Z"));
    }

    [TestCase("fixed", true)]
    [TestCase("noise", true)]
    [TestCase("reclassified", true)]
    [TestCase("unknown_resolved", true)]
    [TestCase("wontfix", true)]
    [TestCase("duplicate", false)]
    [TestCase(null, false)]
    [TestCase("other", false)]
    public void Only_the_outcomes_that_change_knowledge_go_back_as_a_pull_request(string? outcome, bool needs) =>
        Assert.That(Outcomes.NeedsRepositoryChange(outcome), Is.EqualTo(needs));

    [Test]
    public void Every_outcome_of_the_schema_has_its_repository_change_text()
    {
        var schema = JsonNode.Parse(Fx.Support("incident-report.schema.json"))!;
        var values = schema["properties"]!["outcome"]!["anyOf"]![1]!["properties"]!["value"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>());
        Assert.That(Outcomes.All, Is.EquivalentTo(values));
        Assert.That(Outcomes.RepositoryChange.Keys, Is.EquivalentTo(values));
    }
}
