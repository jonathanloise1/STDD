namespace MyApp.Monitor;

/// <summary>One failed check. <see cref="Check"/> is a check id of operations.md section 16 (or another stable id).</summary>
public sealed record Finding(string Check, string Message, string? Location = null)
{
    public override string ToString() => Location is null ? $"[{Check}] {Message}" : $"[{Check}] {Message} ({Location})";
}

public sealed class FindingList : List<Finding>
{
    public void Add(string check, string message, string? location = null) => Add(new Finding(check, message, location));
}

public class MonitorException(string message, Exception? inner = null) : Exception(message, inner);
