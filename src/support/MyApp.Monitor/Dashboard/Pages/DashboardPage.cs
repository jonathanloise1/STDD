using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Dashboard.Pages;

public static class Fmt
{
    public static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
    public static string Time(DateTimeOffset? value) => value is null ? "" : Time(value.Value);
    public static string Text(JsonNode? node) => MonitorJson.ScalarText(node) ?? "";
    public static string At(JsonNode? node, string path) => Text(MonitorJson.Get(node, path));
    public static string Pretty(JsonNode? node) => node is null ? "" : node.ToJsonString(MonitorJson.Snake);
    public static string Rate(double rate) => rate.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>Base of the dashboard pages: the signed-in person, and the message shown after an action.</summary>
public abstract class DashboardPage : PageModel
{
    public string CurrentUser => DashboardAuthentication.UserName(User);

    [BindProperty(SupportsGet = true, Name = "message")] public string? Message { get; set; }
    [BindProperty(SupportsGet = true, Name = "ok")] public string? MessageOk { get; set; }

    public bool MessageIsError => MessageOk == "0";

    protected IActionResult Back(string path, ActionResult result) =>
        Redirect($"{path}{(path.Contains('?') ? '&' : '?')}ok={(result.Ok ? 1 : 0)}&message={Uri.EscapeDataString(result.Message)}");
}
