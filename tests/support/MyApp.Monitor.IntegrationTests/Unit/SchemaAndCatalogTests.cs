using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Unit;

public class SchemaSubsetValidatorTests
{
    private static List<string> Validate(string schema, string instance, Func<string, IReadOnlyCollection<string>?>? enumFrom = null) =>
        new SchemaSubsetValidator(JsonNode.Parse(schema)!, enumFrom).Validate(JsonNode.Parse(instance));

    [TestCase("""{"type":"string"}""", "\"a\"", true)]
    [TestCase("""{"type":"string"}""", "1", false)]
    [TestCase("""{"type":"integer"}""", "3", true)]
    [TestCase("""{"type":"integer"}""", "3.5", false)]
    [TestCase("""{"type":"number"}""", "3.5", true)]
    [TestCase("""{"type":"boolean"}""", "true", true)]
    [TestCase("""{"type":["string","null"]}""", "null", true)]
    [TestCase("""{"type":["string","null"]}""", "2", false)]
    [TestCase("""{"type":"object"}""", "[]", false)]
    [TestCase("""{"type":"array"}""", "{}", false)]
    public void Type(string schema, string instance, bool valid) => Assert.That(Validate(schema, instance).Count == 0, Is.EqualTo(valid));

    [TestCase("""{"enum":["a","b"]}""", "\"a\"", true)]
    [TestCase("""{"enum":["a","b"]}""", "\"c\"", false)]
    [TestCase("""{"pattern":"^[0-9a-f]{4}$"}""", "\"beef\"", true)]
    [TestCase("""{"pattern":"^[0-9a-f]{4}$"}""", "\"beefs\"", false)]
    [TestCase("""{"minLength":2}""", "\"a\"", false)]
    [TestCase("""{"maxLength":2}""", "\"abc\"", false)]
    [TestCase("""{"maxLength":3}""", "\"abc\"", true)]
    [TestCase("""{"minimum":1}""", "0", false)]
    [TestCase("""{"minimum":1,"maximum":5}""", "5", true)]
    [TestCase("""{"maximum":5}""", "6", false)]
    [TestCase("""{"maxItems":1}""", "[1,2]", false)]
    [TestCase("""{"items":{"type":"integer"}}""", "[1,\"x\"]", false)]
    [TestCase("""{"anyOf":[{"type":"null"},{"type":"string"}]}""", "null", true)]
    [TestCase("""{"anyOf":[{"type":"null"},{"type":"string"}]}""", "1", false)]
    public void Keywords(string schema, string instance, bool valid) => Assert.That(Validate(schema, instance).Count == 0, Is.EqualTo(valid));

    [Test]
    public void Objects_check_required_properties_and_additional_properties()
    {
        const string schema = """{"type":"object","additionalProperties":false,"required":["a"],"properties":{"a":{"type":"string"},"b":{"type":"integer"}}}""";
        Assert.That(Validate(schema, """{"a":"x","b":1}"""), Is.Empty);
        Assert.That(Validate(schema, """{"b":1}""").Single(), Does.Contain("missing required 'a'"));
        Assert.That(Validate(schema, """{"a":"x","c":1}""").Single(), Does.Contain("unknown property 'c'"));
        Assert.That(Validate(schema, """{"a":1}""").Single(), Does.Contain("$.a"));
    }

    [Test]
    public void Ref_to_defs_is_resolved()
    {
        const string schema = """{"$defs":{"id":{"type":"string","pattern":"^E\\d+$"}},"type":"object","properties":{"x":{"$ref":"#/$defs/id"}}}""";
        Assert.That(Validate(schema, """{"x":"E1"}"""), Is.Empty);
        Assert.That(Validate(schema, """{"x":"F1"}"""), Is.Not.Empty);
        Assert.That(Validate("""{"$ref":"#/$defs/missing"}""", "1").Single(), Does.Contain("unresolved"));
    }

    [Test]
    public void The_x_enum_from_extension_takes_the_names_from_the_source()
    {
        const string schema = """{"type":"string","x-enum-from":"entities"}""";
        IReadOnlyCollection<string>? Names(string s) => s == "entities" ? new[] { "task", "user" } : null;
        Assert.That(Validate(schema, "\"task\"", Names), Is.Empty);
        Assert.That(Validate(schema, "\"nope\"", Names).Single(), Does.Contain("not a known entities name"));
        Assert.That(Validate(schema, "\"task\"").Single(), Does.Contain("not available"));
    }

    [Test]
    public void Check_subset_flags_keywords_outside_the_subset_and_allows_property_names_that_look_like_keywords()
    {
        var problems = SchemaSubsetValidator.CheckSubset(JsonNode.Parse("""
        { "type": "object", "properties": { "type": { "type": "string", "format": "date" }, "items": { "type": "array", "items": { "oneOf": [] } } } }
        """));
        Assert.That(problems, Has.Count.EqualTo(2));
        Assert.That(problems[0], Does.Contain("'format'"));
        Assert.That(problems[1], Does.Contain("'oneOf'"));
    }

    [TestCase("signature.schema.json")]
    [TestCase("incident-report.schema.json")]
    public void The_shipped_schemas_use_only_the_subset(string file) =>
        Assert.That(SchemaSubsetValidator.CheckSubset(JsonNode.Parse(Fx.Support(file))), Is.Empty);

    [Test]
    public void The_shipped_example_report_validates_against_the_report_schema()
    {
        var errors = new SchemaSubsetValidator(JsonNode.Parse(Fx.Support("incident-report.schema.json"))!).Validate(JsonNode.Parse(PackVerifier.ReadEmbeddedExample()!));
        Assert.That(errors, Is.Empty);
    }
}

public class CatalogTests
{
    [Test]
    public void The_shipped_catalog_has_five_read_tools_ordered_by_name()
    {
        var catalog = Fx.Catalog();
        Assert.That(catalog.Tools.Select(t => t.Name), Is.EqualTo(new[] { "audit_trail_by_entity", "health_status", "logs_by_correlation", "signature_lookup", "sql_entity_read" }));
        Assert.That(catalog.Tools.All(t => t.Access == "read"), Is.True);
        Assert.That(catalog.Find("logs_by_correlation")!.Limits.LookbackHours, Is.EqualTo(72));
        Assert.That(catalog.Find("sql_entity_read")!.MaskingSection, Is.EqualTo("entities"));
    }

    private static FindingList Parse(Action<JsonObject> change)
    {
        var node = (JsonObject)JsonNode.Parse(Fx.Support("tools.json"))!;
        change(node);
        var findings = new FindingList();
        ToolCatalog.Parse(node, findings);
        return findings;
    }

    private static JsonObject Tool(JsonObject root, string name) =>
        root["tools"]!.AsArray().Select(t => t!.AsObject()).Single(t => t["name"]!.GetValue<string>() == name);

    [Test]
    public void A_closed_family_list_is_enforced() =>
        Assert.That(Parse(r => Tool(r, "health_status")["family"] = "weather").Single().Message, Does.Contain("family 'weather'"));

    [Test]
    public void Access_must_be_read() =>
        Assert.That(Parse(r => Tool(r, "health_status")["access"] = "write").Select(f => f.Message), Has.Some.Contain("access must be 'read'"));

    [Test]
    public void Names_must_follow_the_pattern_be_unique_and_ordered()
    {
        Assert.That(Parse(r => Tool(r, "health_status")["name"] = "Health-Status").Select(f => f.Message), Has.Some.Contain("must match"));
        Assert.That(Parse(r => r["tools"]!.AsArray().Add(r["tools"]![0]!.DeepClone())).Select(f => f.Message), Has.Some.Contain("duplicated"));
        Assert.That(Parse(r => { var a = r["tools"]!.AsArray(); var first = a[0]!.DeepClone(); a.RemoveAt(0); a.Add(first); }).Select(f => f.Message), Has.Some.Contain("ordered by name"));
    }

    [Test]
    public void Parameters_must_be_an_object_schema_that_forbids_extra_properties_in_the_subset()
    {
        Assert.That(Parse(r => Tool(r, "health_status")["parameters"]!["additionalProperties"] = true).Select(f => f.Message), Has.Some.Contain("additionalProperties must be false"));
        Assert.That(Parse(r => Tool(r, "health_status")["parameters"]!["format"] = "x").Select(f => f.Message), Has.Some.Contain("outside the schema subset"));
    }

    [Test]
    public void Limits_and_implementations_and_masking_name_are_checked()
    {
        Assert.That(Parse(r => Tool(r, "health_status")["limits"]!.AsObject().Remove("max_rows")).Select(f => f.Message), Has.Some.Contain("limits.max_rows"));
        Assert.That(Parse(r => Tool(r, "health_status")["limits"]!["surprise"] = 1).Select(f => f.Message), Has.Some.Contain("not a known limit"));
        Assert.That(Parse(r => Tool(r, "health_status")["implementations"] = new JsonObject()).Select(f => f.Message), Has.Some.Contain("implementations"));
        Assert.That(Parse(r => Tool(r, "health_status")["output"]!["masking"] = "tools.other").Select(f => f.Message), Has.Some.Contain("output.masking"));
    }

    [Test]
    public void A_catalog_can_be_reduced_to_the_profile_tools()
    {
        var reduced = ToolCatalog.Reduce(JsonNode.Parse(Fx.Support("tools.json"))!, new HashSet<string> { "health_status", "signature_lookup" });
        Assert.That(reduced["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()), Is.EqualTo(new[] { "health_status", "signature_lookup" }));
    }

    [Test]
    public void The_shipped_entities_load_and_check_identifiers()
    {
        var entities = Fx.Entities();
        Assert.That(entities.Names, Is.EquivalentTo(new[] { "membership", "organization", "task", "user" }));
        Assert.That(entities.Find("organization")!.Table, Is.EqualTo("dbo.Organizations"));
        Assert.That(entities.Find("user")!.AuditEntityType, Is.EqualTo("User"));
    }

    [Test]
    public void Entity_problems_are_findings()
    {
        var findings = new FindingList();
        EntityCatalog.Parse(MonitorJson.Parse("""
        { "schema_version": 1, "entities": [
          { "name": "Bad Name", "description": "", "source_kind": "", "table": "Users; DROP TABLE x", "key": "I d", "key_type": "float" },
          { "name": "ok", "description": "d", "source_kind": "sqlserver", "table": "dbo.Ok", "key": "Id", "key_type": "guid", "audit_entity_type": null },
          { "name": "ok", "description": "d", "source_kind": "sqlserver", "table": "dbo.Ok", "key": "Id", "key_type": "guid", "audit_entity_type": null }
        ] }
        """), findings);
        var all = string.Join("\n", findings.Select(f => f.Message));
        Assert.That(all, Does.Contain("must match ^[a-z]+(_[a-z]+)*$"));
        Assert.That(all, Does.Contain("schema.table"));
        Assert.That(all, Does.Contain("not a plain identifier"));
        Assert.That(all, Does.Contain("key_type"));
        Assert.That(all, Does.Contain("audit_entity_type is missing"));
        Assert.That(all, Does.Contain("duplicated"));
    }

    [Test]
    public void The_shipped_constraints_load_with_their_parameters()
    {
        var c = Fx.Constraints();
        Assert.That(c.Constraints, Has.Count.EqualTo(10));
        Assert.That(c.MaxToolCalls, Is.EqualTo(12));
        Assert.That(c.MaxOutputChars, Is.EqualTo(60000));
        Assert.That(c.MaxLookbackHours, Is.EqualTo(72));
        Assert.That(c.AllowedAccess, Is.EqualTo(new[] { "read" }));
        Assert.That(c.ApplicationIntent, Is.EqualTo("ReadOnly"));
    }

    [Test]
    public void Constraint_problems_are_findings()
    {
        var findings = new FindingList();
        ConstraintSet.Parse(MonitorJson.Parse("""
        { "schema_version": 1, "constraints": [
          { "id": "X1", "type": "mystery", "text": "", "params": {}, "enforced_by": [] },
          { "id": "C01", "type": "access", "text": "t", "params": {}, "enforced_by": ["somebody"] },
          { "id": "C01", "type": "access", "text": "t", "params": {}, "enforced_by": ["executor"] }
        ] }
        """), findings);
        var all = string.Join("\n", findings.Select(f => f.Message));
        Assert.That(all, Does.Contain("C and two digits"));
        Assert.That(all, Does.Contain("type 'mystery'"));
        Assert.That(all, Does.Contain("text is empty"));
        Assert.That(all, Does.Contain("enforced_by must name"));
        Assert.That(all, Does.Contain("'somebody'"));
        Assert.That(all, Does.Contain("duplicated"));
    }
}

public class ProfileTests
{
    private static string Yaml(string name) => File.ReadAllText(Path.Combine(FixtureRepo.FixturesRoot, "repo", "docs", "support", "profiles", name + ".yaml"));

    private static FindingList Validate(string name, Action<JsonObject>? change = null, bool withCatalog = true)
    {
        var raw = ProfileLoader.ParseYaml(Yaml(name));
        change?.Invoke(raw);
        var findings = new FindingList();
        ProfileLoader.Validate(raw, name, findings, withCatalog ? Fx.Catalog() : null);
        return findings;
    }

    [TestCase("onprem")]
    [TestCase("cloud")]
    public void The_shipped_profiles_are_valid(string name) => Assert.That(Validate(name), Is.Empty);

    [Test]
    public void Placeholders_resolve_from_settings_with_defaults()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Monitor:Settings:ApiBaseUrl"] = "https://api.example" }).Build();
        var profile = ProfileLoader.Load(Yaml("onprem"), settings, "onprem");
        Assert.That(profile.Environment, Is.EqualTo("prod"), "default");
        Assert.That(profile.FindSource("api")!.GetString("base_url"), Is.EqualTo("https://api.example"));
        Assert.That(profile.FindSource("applogs")!.GetStrings("paths").Single(), Does.EndWith("myapp-api-*.jsonl"));
        Assert.That(profile.Analyser.RateMultiplier, Is.EqualTo(3));
        Assert.That(profile.ToolSources["signature_lookup"], Is.EqualTo("pack"));
        Assert.That(profile.Store.RetentionDays, Is.EqualTo(90));
        Assert.That(profile.Gates.TicketAllowModelVerdict, Is.False);
    }

    [Test]
    public void A_placeholder_without_value_or_default_is_reported_not_guessed()
    {
        var profile = ProfileLoader.Load(Yaml("cloud"), new ConfigurationBuilder().Build(), "cloud");
        Assert.That(profile.UnresolvedSettings, Does.Contain("Monitor:Settings:ApiBaseUrl"));
    }

    [Test]
    public void The_profile_name_must_equal_the_file_name() =>
        Assert.That(Validate("onprem", r => r["name"] = "other").Select(f => f.Message), Has.Some.Contain("must equal the profile name"));

    [Test]
    public void A_world_source_is_never_allowed_in_a_profile() =>
        Assert.That(Validate("onprem", r => r["sources"]!.AsArray().Add(new JsonObject { ["id"] = "w", ["kind"] = "world" })).Select(f => f.Message),
            Has.Some.Contain("world source"));

    [Test]
    public void A_literal_under_a_secret_key_is_refused_and_a_placeholder_is_fine()
    {
        var findings = Validate("onprem", r => r["notifications"]!["email"]!.AsObject().Add("password", "hunter2"));
        Assert.That(findings.Select(f => f.Message), Has.Some.Contain("notifications.email.password"));
        Assert.That(Validate("onprem", r => r["notifications"]!["email"]!.AsObject().Add("password", "${setting:Monitor:Smtp:Password}")), Is.Empty);
    }

    [TestCase("secret"), TestCase("token"), TestCase("key"), TestCase("connection"), TestCase("api_key")]
    public void Every_secret_key_name_is_checked(string key) =>
        Assert.That(Validate("onprem", r => r["perimeter"]!.AsObject().Add(key, "literal")).Select(f => f.Message), Has.Some.Contain(key));

    [Test]
    public void A_setting_name_under_a_key_that_only_contains_a_secret_word_is_not_a_secret() =>
        Assert.That(Validate("onprem"), Is.Empty, "connection_setting and password_setting hold setting names");

    [Test]
    public void A_tool_bound_to_an_unknown_source_or_tool_or_kind_is_refused()
    {
        Assert.That(Validate("onprem", r => r["tool_sources"]!["health_status"] = "nowhere").Select(f => f.Message), Has.Some.Contain("not defined in sources"));
        Assert.That(Validate("onprem", r => r["tool_sources"]!["no_such_tool"] = "api").Select(f => f.Message), Has.Some.Contain("no such tool"));
        Assert.That(Validate("onprem", r => r["tool_sources"]!["health_status"] = "main-db").Select(f => f.Message), Has.Some.Contain("no implementation for source kind 'sqlserver'"));
        Assert.That(Validate("onprem", r => r["tool_sources"]!["signature_lookup"] = "api").Select(f => f.Message), Has.Some.Contain("must be 'pack'"));
    }

    [Test]
    public void The_model_verdict_gate_must_stay_false() =>
        Assert.That(Validate("onprem", r => r["gates"]!["ticket"]!["allow_model_verdict"] = true).Select(f => f.Message), Has.Some.Contain("allow_model_verdict"));

    [Test]
    public void Source_settings_are_checked_by_kind()
    {
        Assert.That(Validate("onprem", r => r["sources"]![0]!.AsObject().Remove("paths")).Select(f => f.Message), Has.Some.Contain("paths"));
        Assert.That(Validate("onprem", r => r["sources"]![0]!["format"] = "text").Select(f => f.Message), Has.Some.Contain("line_regex"));
        Assert.That(Validate("cloud", r => r["sources"]![0]!.AsObject().Remove("app_role_names")).Select(f => f.Message), Has.Some.Contain("app_role_names"));
        Assert.That(Validate("cloud", r => { var s = r["sources"]![0]!.AsObject(); s.Remove("workspace_id"); s.Remove("discovery"); }).Select(f => f.Message), Has.Some.Contain("workspace_id or discovery"));
        Assert.That(Validate("onprem", r => r["sources"]![2]!.AsObject().Remove("base_url")).Select(f => f.Message), Has.Some.Contain("base_url"));
    }

    [Test]
    public void Missing_sections_are_findings()
    {
        foreach (var section in new[] { "analyser", "investigator", "gates", "notifications", "store", "dashboard", "perimeter", "tool_sources" })
            Assert.That(Validate("onprem", r => r.Remove(section)).Select(f => f.Message), Has.Some.Contain(section), section);
    }
}
