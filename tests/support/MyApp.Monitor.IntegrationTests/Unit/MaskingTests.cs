using System.Text.Json.Nodes;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Sources;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Unit;

public class MaskingPatternTests
{
    private readonly Masker _m = Fx.Masker();

    // each shipped pattern: a positive case and a negative case
    [TestCase("login failed, password=hunter2 rejected", "login failed, password=[secret] rejected")]
    [TestCase("sent token: abc123 to api", "sent token=[secret] to api")]
    [TestCase("api_key=XYZ&x=1", "api_key=[secret]&x=1")]
    public void Secret_assignment_positive(string input, string expected) => Assert.That(_m.MaskFreeText(input), Is.EqualTo(expected));

    [TestCase("the token count is high")]
    [TestCase("passwords are rotated monthly")]
    public void Secret_assignment_negative(string input) => Assert.That(_m.MaskFreeText(input), Is.EqualTo(input));

    [Test]
    public void Bearer_positive() => Assert.That(_m.MaskFreeText("Authorization: Bearer abc.DEF-123_x=="), Is.EqualTo("Authorization: Bearer [secret]"));

    [Test]
    public void Bearer_negative() => Assert.That(_m.MaskFreeText("a carrier pigeon arrived"), Is.EqualTo("a carrier pigeon arrived"));

    [Test]
    public void Jwt_positive() =>
        Assert.That(_m.MaskFreeText("jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dBjftJeZ4CVPmB92K27uh end"), Is.EqualTo("jwt [jwt] end"));

    [Test]
    public void Jwt_negative() => Assert.That(_m.MaskFreeText("prefix eyJ only"), Is.EqualTo("prefix eyJ only"));

    [TestCase("write to john.doe+x@example.co.uk now", "write to [email] now")]
    [TestCase("a@b.io", "[email]")]
    public void Email_positive(string input, string expected) => Assert.That(_m.MaskFreeText(input), Is.EqualTo(expected));

    [Test]
    public void Email_negative() => Assert.That(_m.MaskFreeText("user at example dot com"), Is.EqualTo("user at example dot com"));

    [TestCase("pay IT60 X054 2811 1010 0000 0123 456 today", "pay [iban] today")]
    [TestCase("pay GB82WEST12345698765432 today", "pay [iban] today")]
    public void Iban_positive(string input, string expected) => Assert.That(_m.MaskFreeText(input), Is.EqualTo(expected));

    [Test]
    public void Iban_negative() => Assert.That(_m.MaskFreeText("release v1.2.3 shipped"), Is.EqualTo("release v1.2.3 shipped"));

    [Test]
    public void Tax_code_positive() => Assert.That(_m.MaskFreeText("owner RSSMRA85T10A562S ok"), Is.EqualTo("owner [tax_code] ok"));

    [Test]
    public void Tax_code_negative() => Assert.That(_m.MaskFreeText("owner RSSMRA85T10A56 ok"), Is.EqualTo("owner RSSMRA85T10A56 ok"));

    [TestCase("card 4111 1111 1111 1111 declined", "card [card] declined")]
    [TestCase("card 4111-1111-1111-1111 declined", "card [card] declined")]
    public void Card_number_positive(string input, string expected) => Assert.That(_m.MaskFreeText(input), Is.EqualTo(expected));

    [Test]
    public void Card_number_negative() => Assert.That(_m.MaskFreeText("room 1234 5678"), Is.EqualTo("room 1234 5678"));

    [Test]
    public void Ipv4_positive() => Assert.That(_m.MaskFreeText("from 192.168.1.10 refused"), Is.EqualTo("from [ip] refused"));

    [Test]
    public void Ipv4_negative() => Assert.That(_m.MaskFreeText("version 1.2.3 refused"), Is.EqualTo("version 1.2.3 refused"));

    [TestCase("call +39 333 1234567 please", "call [phone] please")]
    [TestCase("call +1 (415) 555-0100 please", "call [phone] please")]
    public void Phone_international_positive(string input, string expected) => Assert.That(_m.MaskFreeText(input), Is.EqualTo(expected));

    [Test]
    public void Phone_grouped_positive() => Assert.That(_m.MaskFreeText("call 333 123 4567 please"), Is.EqualTo("call [phone] please"));

    [Test]
    public void Phone_negative() => Assert.That(_m.MaskFreeText("took 12 ms in 3 steps"), Is.EqualTo("took 12 ms in 3 steps"));

    [Test]
    public void Long_digits_positive() => Assert.That(_m.MaskFreeText("account 123456789012 closed"), Is.EqualTo("account [digits] closed"));

    [Test]
    public void Long_digits_negative() => Assert.That(_m.MaskFreeText("account 12345678 closed"), Is.EqualTo("account 12345678 closed"));

    [Test]
    public void Patterns_apply_in_order_so_an_earlier_replacement_is_not_matched_again()
    {
        var masked = _m.MaskFreeText("mail a@b.io from 10.0.0.1 and 4111 1111 1111 1111");
        Assert.That(masked, Is.EqualTo("mail [email] from [ip] and [card]"));
    }

    // protected spans
    [TestCase("user 6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72 failed")]
    [TestCase("trace 4bf92f3577b34da6a3ce929d0e0e4736 failed")]
    [TestCase("span 00f067aa0ba902b7 failed")]
    [TestCase("at 2026-11-06T10:42:01.123Z failed")]
    [TestCase("on 2026-11-06 failed")]
    [TestCase("template t-2bb5dcdc1949 failed")]
    [TestCase("incident INC-20261106-0007 failed")]
    public void Protected_spans_are_left_alone(string input) => Assert.That(_m.MaskFreeText(input), Is.EqualTo(input));

    // F6 item 7: an unspaced run of digits is a number, not a trace or span id, whatever its length
    [TestCase("card 4111111111111111 declined", "card [card] declined")]
    [TestCase("card 5500005555555559 declined", "card [card] declined")]
    [TestCase("order 12345678901234567890123456789012 failed", "order [digits] failed")]
    public void An_unspaced_digit_run_of_trace_or_span_length_is_masked_not_protected(string input, string expected) =>
        Assert.That(_m.MaskFreeText(input), Is.EqualTo(expected));

    [TestCase("trace 4bf92f3577b34da6a3ce929d0e0e4736 failed")]
    [TestCase("span 00f067aa0ba902b7 failed")]
    [TestCase("span 1234567890abcdef failed")]
    [TestCase("trace 00000000000000000000000000000a01 failed")]
    public void A_real_trace_or_span_id_with_a_hex_letter_is_still_protected_in_free_text(string input) =>
        Assert.That(_m.MaskFreeText(input), Is.EqualTo(input));

    [Test]
    public void A_trace_and_a_span_id_in_their_structured_fields_pass_the_id_class_even_when_all_digits()
    {
        var record = Fx.Record(message: "x", traceId: "4bf92f3577b34da6a3ce929d0e0e4736") with { SpanId = "1234567890123456" };
        var masked = _m.MaskRecord(record, out _);
        Assert.That(masked.TraceId, Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"));
        Assert.That(masked.SpanId, Is.EqualTo("1234567890123456"));
    }

    [Test]
    public void A_pattern_still_applies_to_text_around_a_protected_span() =>
        Assert.That(_m.MaskFreeText("6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72 mailed a@b.io"), Is.EqualTo("6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72 mailed [email]"));

    [Test]
    public void Free_text_is_cut_to_the_policy_length_after_masking()
    {
        var masked = _m.MaskFreeText(new string('x', 800));
        Assert.That(masked, Has.Length.EqualTo(500));
        Assert.That(_m.MaskFreeText("abcdef", maxChars: 3), Is.EqualTo("abc"));
    }
}

public class MaskingClassTests
{
    private readonly Masker _m = Fx.Masker();

    private string? Mask(MaskClass cls, object? value) => MonitorJson.ScalarText(_m.MaskScalar(cls, value is null ? null : JsonValue.Create(value)));

    [TestCase("6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72", true)]
    [TestCase("12345", true)]
    [TestCase("-7", true)]
    [TestCase("4bf92f3577b34da6a3ce929d0e0e4736", true)]
    [TestCase("00f067aa0ba902b7", true)]
    [TestCase("not-an-id", false)]
    [TestCase("john@example.com", false)]
    [TestCase("4bf92f3577b34da6a3ce929d0e0e47", false)]
    public void Id_class(string value, bool passes) => Assert.That(Mask(MaskClass.Id, value), Is.EqualTo(passes ? value : "[masked]"));

    [TestCase("MyApp.Services.TaskItemService.Create", true)]
    [TestCase("api/organizations/{organizationId}/tasks", true)]
    [TestCase("ns:type<T>[0]", true)]
    [TestCase("has a space", false)]
    [TestCase("semi;colon", false)]
    [TestCase("john@example.com", false)]
    public void Code_class(string value, bool passes) => Assert.That(Mask(MaskClass.Code, value), Is.EqualTo(passes ? value : "[masked]"));

    [Test]
    public void Code_class_rejects_values_over_200_characters() => Assert.That(Mask(MaskClass.Code, new string('a', 201)), Is.EqualTo("[masked]"));

    [TestCase("12", true)]
    [TestCase("12.5", true)]
    [TestCase("-3", true)]
    [TestCase("1e3", true)]
    [TestCase("twelve", false)]
    public void Number_class(string value, bool passes) => Assert.That(Mask(MaskClass.Number, value), Is.EqualTo(passes ? value : "[masked]"));

    [Test]
    public void Number_class_keeps_json_numbers_as_numbers()
    {
        var node = _m.MaskScalar(MaskClass.Number, JsonValue.Create(42));
        Assert.That(node!.GetValueKind(), Is.EqualTo(System.Text.Json.JsonValueKind.Number));
    }

    [TestCase("true", true)]
    [TestCase("False", true)]
    [TestCase("yes", false)]
    [TestCase("1", false)]
    public void Boolean_class(string value, bool passes) => Assert.That(Mask(MaskClass.Boolean, value), Is.EqualTo(passes ? value : "[masked]"));

    [TestCase("2026-11-06T10:42:01.123Z", true)]
    [TestCase("2026-11-06", true)]
    [TestCase("2026-11-06T10:42:01+01:00", true)]
    [TestCase("yesterday", false)]
    [TestCase("2026-13-45", false)]
    public void Timestamp_class(string value, bool passes) => Assert.That(Mask(MaskClass.Timestamp, value), Is.EqualTo(passes ? value : "[masked]"));

    [Test]
    public void Free_text_class_applies_patterns() => Assert.That(Mask(MaskClass.FreeText, "write a@b.io"), Is.EqualTo("write [email]"));

    [Test]
    public void Hash_class_is_h_prefix_and_12_hex_and_correlatable()
    {
        var a = Mask(MaskClass.Hash, "Jane.Doe@example.com");
        var b = Mask(MaskClass.Hash, "  jane.doe@EXAMPLE.com ");
        Assert.That(a, Does.Match("^h:[0-9a-f]{12}$"));
        Assert.That(b, Is.EqualTo(a), "trimmed and lower-cased before hashing");
        Assert.That(Mask(MaskClass.Hash, "other@example.com"), Is.Not.EqualTo(a));
    }

    [Test]
    public void Hash_class_depends_on_the_key()
    {
        var other = new Masker(Fx.Policy(), System.Text.Encoding.UTF8.GetBytes("a-completely-different-key-of-32-bytes!!"));
        Assert.That(other.Hash("x@y.z"), Is.Not.EqualTo(_m.Hash("x@y.z")));
    }

    [Test]
    public void Pack_class_passes_unchanged_and_null_stays_null()
    {
        Assert.That(Mask(MaskClass.Pack, "anything a@b.io"), Is.EqualTo("anything a@b.io"));
        Assert.That(_m.MaskScalar(MaskClass.Id, null), Is.Null);
    }

    [Test]
    public void A_structured_value_in_a_scalar_class_is_masked() =>
        Assert.That(MonitorJson.ScalarText(_m.MaskScalar(MaskClass.Code, JsonNode.Parse("{\"a\":1}"))), Is.EqualTo("[masked]"));
}

public class MaskingRecordAndDataTests
{
    private readonly Masker _m = Fx.Masker();

    [Test]
    public void A_record_is_masked_with_the_signal_section()
    {
        var record = Fx.Record(
            message: "contact a@b.io", eventName: "task.create.access_lost", template: "contact {Email}", exceptionType: "System.Exception",
            props: new()
            {
                ["OrganizationId"] = "0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91", ["UserId"] = "john@example.com", ["Unlisted"] = "secret", ["Count"] = "oops",
                ["Route"] = "api/tasks", ["Url"] = "https://10.0.0.1/health",
            });
        var masked = _m.MaskRecord(record, out var dropped);
        Assert.That(masked.Message, Is.EqualTo("contact [email]"));
        Assert.That(masked.Properties["OrganizationId"], Is.EqualTo("0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91"));
        Assert.That(masked.Properties["UserId"], Is.EqualTo("[masked]"), "*Id is the id class");
        Assert.That(masked.Properties["Count"], Is.EqualTo("[masked]"));
        Assert.That(masked.Properties["Route"], Is.EqualTo("api/tasks"));
        Assert.That(masked.Properties["Url"], Is.EqualTo("https://[ip]/health"));
        Assert.That(masked.Properties, Does.Not.ContainKey("Unlisted"), "a property with no class is dropped");
        Assert.That(dropped, Is.EqualTo(1));
        Assert.That(masked.LogKey, Is.EqualTo("task.create.access_lost"));
        Assert.That(masked.ExceptionType, Is.EqualTo("System.Exception"));
    }

    [Test]
    public void An_exact_property_name_wins_over_a_glob()
    {
        var policy = MaskingPolicyLoader.Load("""
        { "schema_version": 1, "free_text_max_chars": 50, "protected": [], "patterns": [],
          "signal": { "fields": {}, "properties": { "*Id": "id", "CorrelationId": "code" } }, "tools": {}, "entities": {} }
        """);
        Assert.That(policy.TryGetPropertyClass("CorrelationId", out var exact), Is.True);
        Assert.That(exact, Is.EqualTo(MaskClass.Code));
        Assert.That(policy.TryGetPropertyClass("userid", out var glob), Is.True, "case-insensitive");
        Assert.That(glob, Is.EqualTo(MaskClass.Id));
        Assert.That(policy.TryGetPropertyClass("Name", out _), Is.False);
    }

    [Test]
    public void Tool_data_keeps_classed_fields_masks_them_and_counts_dropped_ones()
    {
        var data = Fx.Obj("""
        { "records": [ { "timestamp": "2026-11-06T10:00:00Z", "level": "Warning", "message": "mail a@b.io", "span_id": "00f067aa0ba902b7", "secret": "x", "extra": 1 } ] }
        """);
        var result = _m.MaskToolData(data, "tools.logs_by_correlation", null);
        var record = (JsonObject)result.Data!["records"]![0]!;
        Assert.That(MonitorJson.ScalarText(record["message"]), Is.EqualTo("mail [email]"));
        Assert.That(record.ContainsKey("secret"), Is.False);
        Assert.That(record.ContainsKey("extra"), Is.False);
        Assert.That(result.FieldsDropped, Is.EqualTo(2));
    }

    [Test]
    public void Tool_data_with_an_unknown_section_is_withheld_entirely()
    {
        var result = _m.MaskToolData(Fx.Obj("""{ "a": 1, "b": 2 }"""), "tools.nothing", null);
        Assert.That(result.Data, Is.Null);
        Assert.That(result.FieldsDropped, Is.EqualTo(2));
    }

    [Test]
    public void Array_items_use_the_bracket_path()
    {
        var result = _m.MaskToolData(Fx.Obj("""{ "changes": [ { "changed_properties": ["IsDeleted", "bad value"], "action": "Update" } ] }"""), "tools.audit_trail_by_entity", "organization");
        var props = (JsonArray)result.Data!["changes"]![0]!["changed_properties"]!;
        Assert.That(props[0]!.GetValue<string>(), Is.EqualTo("IsDeleted"));
        Assert.That(props[1]!.GetValue<string>(), Is.EqualTo("[masked]"));
    }

    [Test]
    public void The_entity_class_filters_by_the_entity_named_in_the_call_and_accepts_json_text()
    {
        var data = Fx.Obj("""{ "changes": [ { "old_values": "{\"IsDeleted\":false,\"InternalNote\":\"x\"}", "new_values": { "Name": "Acme a@b.io", "Unknown": 1 } } ] }""");
        var result = _m.MaskToolData(data, "tools.audit_trail_by_entity", "organization");
        var change = result.Data!["changes"]![0]!;
        Assert.That(change["old_values"]!.AsObject().ContainsKey("InternalNote"), Is.False);
        Assert.That(MonitorJson.ScalarText(change["old_values"]!["IsDeleted"]), Is.EqualTo("false"));
        Assert.That(MonitorJson.ScalarText(change["new_values"]!["Name"]), Is.EqualTo("Acme [email]"));
        Assert.That(change["new_values"]!.AsObject().ContainsKey("Unknown"), Is.False);
        Assert.That(result.FieldsDropped, Is.EqualTo(2));
    }

    [Test]
    public void An_entity_object_with_no_known_entity_keeps_nothing()
    {
        var data = Fx.Obj("""{ "changes": [ { "old_values": { "Name": "x" } } ] }""");
        var result = _m.MaskToolData(data, "tools.audit_trail_by_entity", "nonexistent");
        Assert.That(result.Data!["changes"]![0]!["old_values"]!.AsObject(), Is.Empty);
    }

    [Test]
    public void The_entities_section_masks_the_fields_of_an_entity_read()
    {
        var data = Fx.Obj("""{ "entity": "user", "id": "6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72", "found": true, "fields": { "Id": "6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72", "Email": "Jane@Example.com", "PasswordHash": "x" } }""");
        var result = _m.MaskToolData(data, "entities", "user");
        var fields = (JsonObject)result.Data!["fields"]!;
        Assert.That(MonitorJson.ScalarText(fields["Email"]), Does.Match("^h:[0-9a-f]{12}$"));
        Assert.That(fields.ContainsKey("PasswordHash"), Is.False);
        Assert.That(result.FieldsDropped, Is.EqualTo(1));
    }

    [Test]
    public void The_pack_class_passes_a_whole_subtree_unchanged()
    {
        var data = Fx.Obj("""{ "found": true, "signature": { "id": "x", "runbook": { "text": "mail a@b.io" } } }""");
        var result = _m.MaskToolData(data, "tools.signature_lookup", null);
        Assert.That(MonitorJson.ScalarText(result.Data!["signature"]!["runbook"]!["text"]), Is.EqualTo("mail a@b.io"));
    }

    [Test]
    public void Tool_arguments_are_masked_by_name_for_the_audit_row()
    {
        var masked = _m.MaskArguments(Fx.Obj("""{ "trace_id": "4bf92f3577b34da6a3ce929d0e0e4736", "id": "john@example.com", "entity": "user", "note": "mail a@b.io" }"""));
        Assert.That(MonitorJson.ScalarText(masked["trace_id"]), Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"));
        Assert.That(MonitorJson.ScalarText(masked["id"]), Is.EqualTo("[masked]"), "'id' matches the *Id id class");
        Assert.That(MonitorJson.ScalarText(masked["note"]), Is.EqualTo("mail [email]"));
    }

    [Test]
    public void Report_text_is_masked_by_patterns_as_a_second_line_without_a_cut()
    {
        var text = "mail a@b.io " + new string('x', 700);
        var masked = _m.MaskReportText(text);
        Assert.That(masked, Does.StartWith("mail [email] "));
        Assert.That(masked.Length, Is.GreaterThan(500));
    }
}

public class MaskingPolicyLoaderTests
{
    private const string Minimal = """{ "schema_version": 1, "free_text_max_chars": 10, "protected": [], "patterns": [], "signal": { "fields": {}, "properties": {} }, "tools": {}, "entities": {} }""";

    [Test]
    public void The_shipped_policy_loads() => Assert.That(Fx.Policy().Patterns, Has.Count.EqualTo(11));

    [Test]
    public void Problems_are_reported_as_O08_findings()
    {
        var findings = new FindingList();
        MaskingPolicyLoader.Parse(MonitorJson.Parse("""
        { "schema_version": 2, "free_text_max_chars": 0,
          "protected": [ { "name": "bad", "regex": "(" } ],
          "patterns": [ { "name": "a", "regex": "x", "replacement": "y" }, { "name": "a", "regex": "(", "replacement": "y" }, { "name": "b" } ],
          "signal": { "fields": { "X": "nonsense" }, "properties": {} },
          "tools": {}, "entities": { "e": { "Col": "entity" } } }
        """), findings);
        Assert.That(findings.Select(f => f.Check).Distinct(), Is.EqualTo(new[] { "O08" }));
        var all = string.Join("\n", findings.Select(f => f.Message));
        Assert.That(all, Does.Contain("schema_version"));
        Assert.That(all, Does.Contain("free_text_max_chars"));
        Assert.That(all, Does.Contain("duplicated"));
        Assert.That(all, Does.Contain("invalid regex"));
        Assert.That(all, Does.Contain("needs name, regex and replacement"));
        Assert.That(all, Does.Contain("unknown class 'nonsense'"));
        Assert.That(all, Does.Contain("not allowed in an entity section"));
    }

    [Test]
    public void A_missing_section_is_a_finding() =>
        Assert.Throws<MonitorException>(() => MaskingPolicyLoader.Load("""{ "schema_version": 1, "free_text_max_chars": 5, "patterns": [], "protected": [] }"""));

    [Test]
    public void A_minimal_policy_loads() => Assert.That(MaskingPolicyLoader.Load(Minimal).FreeTextMaxChars, Is.EqualTo(10));

    [Test]
    public void The_masking_key_must_exist_and_have_at_least_32_bytes()
    {
        Assert.Throws<MonitorException>(() => new MonitorOptions().RequireHashKey());
        Assert.Throws<MonitorException>(() => new MonitorOptions { Masking = { HashKey = "short" } }.RequireHashKey());
        Assert.That(new MonitorOptions { Masking = { HashKey = new string('k', 32) } }.RequireHashKey(), Has.Length.EqualTo(32));
    }
}
