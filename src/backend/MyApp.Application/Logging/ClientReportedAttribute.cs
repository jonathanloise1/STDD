namespace MyApp.Application.Logging;

/// <summary>
/// Marks a log definition method that the frontend may trigger through <c>POST /api/client-events</c>
/// (operations.md 3.6). The method has exactly one parameter after the logger: <c>string route</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ClientReportedAttribute : Attribute
{
}
