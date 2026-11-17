using MyApp.Monitor.Analysis;
using MyApp.Monitor.Pack;

namespace MyApp.Monitor.Notifications;

public static class Channels
{
    public const string Email = "email";
    public const string TicketWebhook = "ticket_webhook";
}

/// <summary>The incident events a notification can answer. The idempotency key of a notification is the incident id and the event.</summary>
public static class NotificationEvents
{
    public const string Created = "created";
    public const string SeverityRaised = "severity_raised";
    public const string Digest = "digest";
}

public static class NotificationKinds
{
    public const string Sent = "sent";
    public const string Draft = "draft";
    public const string Suppressed = "suppressed";
    public const string Failed = "failed";
}

/// <summary>What the gates need to know about an incident.</summary>
public sealed record IncidentFacts(string Severity, string Trigger, string SignatureKind, bool HasRuleVerdict, bool HasModelVerdict);

public sealed record PlannedNotification(string Channel, bool Draft, string Event, string Reason);

/// <summary>
/// The gates of operations.md 10.3. A ticket is created automatically only from a rule verdict on a matched signature; every other
/// escalation gets a ticket draft for a person to approve. A model verdict never creates a ticket on its own (C09), and nothing
/// remediates (C08).
/// </summary>
public sealed class Gates(GateSettings settings)
{
    public bool TicketIsAutomatic(IncidentFacts f) =>
        f.HasRuleVerdict
        && f.SignatureKind is SignatureKinds.Keyed or SignatureKinds.Template
        && f.Trigger is Triggers.Severity or Triggers.Regression or Triggers.Health
        && Severities.Rank(f.Severity) >= Severities.Rank(settings.TicketMinSeverity);

    public bool EmailIsDue(string severity) => Severities.Rank(severity) >= Severities.Rank(settings.EmailMinSeverity);

    /// <summary>
    /// What to do for one incident event. <c>created</c> covers a regression too (it opens an incident); a severity raise mails again
    /// when the new severity passes the gate; tickets are decided once, at creation.
    /// </summary>
    public IReadOnlyList<PlannedNotification> Plan(string eventName, IncidentFacts facts)
    {
        var plan = new List<PlannedNotification>();
        if (eventName == NotificationEvents.Created)
        {
            var automatic = TicketIsAutomatic(facts);
            plan.Add(new PlannedNotification(Channels.TicketWebhook, !automatic, eventName,
                automatic ? "rule verdict on a matched signature at or above the ticket gate" : "not eligible for an automatic ticket"));
        }
        if (eventName is NotificationEvents.Created or NotificationEvents.SeverityRaised && EmailIsDue(facts.Severity))
            plan.Add(new PlannedNotification(Channels.Email, false, eventName, "severity at or above the e-mail gate"));
        return plan;
    }
}
