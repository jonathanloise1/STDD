using System.Text.RegularExpressions;

namespace MyApp.DocTools.Model;

/// <summary>Id grammar of development.md section 3.1 and naming helpers.</summary>
public static class Ids
{
    public static readonly Regex AreaFolder = new(@"^[a-z]+(-[a-z]+)*$");
    public static readonly Regex AreaCode = new(@"^[A-Z]{2,8}$");
    public static readonly Regex Feature = new(@"^F-[A-Z]{2,8}-\d{2}$");
    public static readonly Regex Story = new(@"^US-[A-Z]{2,8}-\d{2}$");
    public static readonly Regex TestCase = new(@"^[A-Z]{2,8}-[A-Z]{2,12}-\d{3}$");
    public static readonly Regex LogKey = new(@"^[a-z]{2,8}\.[a-z]{2,12}\.[a-z]+(_[a-z]+)*$");
    /// <summary>Finds story ids in free text, including malformed ones (any number of digits).</summary>
    public static readonly Regex StoryInText = new(@"\bUS-[A-Z]{2,8}-\d+\b");

    /// <summary>Runbook anchor: the log key with '.' and '_' replaced by '-'.</summary>
    public static string RunbookAnchor(string logKey) => logKey.Replace('.', '-').Replace('_', '-');

    /// <summary>TASK-CREATE-005 -> TaskCreate005</summary>
    public static string TestCasePascal(string id) =>
        string.Concat(id.Split('-').Select(Cap));

    /// <summary>tasks -> Tasks, user-management -> UserManagement</summary>
    public static string AreaPascal(string folder) =>
        string.Concat(folder.Split('-').Select(Cap));

    /// <summary>Audit Trail -> AuditTrail</summary>
    public static string TitlePascal(string title) =>
        string.Concat(title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Cap));

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
}
