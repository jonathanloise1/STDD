namespace MyApp.DocTools;

/// <summary>One violation of a rule. Subject is a repository-relative path with '/' separators.</summary>
public sealed record Finding(string Rule, string Subject, string Message);
