using Microsoft.Extensions.Logging;

namespace MyApp.Application.Logging;

/// <summary>
/// The keyed entries for "the service did not answer" (operations.md 3.1). The web layer catches the
/// database or dependency failure of an operation once, in one filter, and asks this class to write the
/// entry of the operation's key; each key has exactly one call here.
/// </summary>
public static class UnavailableOutcomes
{
    /// <summary>Writes the entry of <paramref name="key"/>; false when the key is not an "unavailable" key.</summary>
    public static bool Emit(ILogger logger, string key, Exception exception, string exceptionType, string topFrame)
    {
        switch (key)
        {
            case AuthenticationLog.Keys.AuthLoginUnavailable: logger.AuthLoginUnavailable(exception, exceptionType, topFrame); return true;
            case AuthenticationLog.Keys.AuthLangUnavailable: logger.AuthLangUnavailable(exception, exceptionType, topFrame); return true;
            case OnboardingLog.Keys.OnboardWizUnavailable: logger.OnboardWizUnavailable(exception, exceptionType, topFrame); return true;
            case DashboardLog.Keys.DashKpiUnavailable: logger.DashKpiUnavailable(exception, exceptionType, topFrame); return true;
            case DashboardLog.Keys.DashRecentUnavailable: logger.DashRecentUnavailable(exception, exceptionType, topFrame); return true;
            case TasksLog.Keys.TaskCreateUnavailable: logger.TaskCreateUnavailable(exception, exceptionType, topFrame); return true;
            case TasksLog.Keys.TaskListUnavailable: logger.TaskListUnavailable(exception, exceptionType, topFrame); return true;
            case AuditTrailLog.Keys.AudListUnavailable: logger.AudListUnavailable(exception, exceptionType, topFrame); return true;
            case OrganizationsLog.Keys.OrgViewUnavailable: logger.OrgViewUnavailable(exception, exceptionType, topFrame); return true;
            case OrganizationsLog.Keys.OrgUpdateUnavailable: logger.OrgUpdateUnavailable(exception, exceptionType, topFrame); return true;
            case OrganizationsLog.Keys.OrgLogoUnavailable: logger.OrgLogoUnavailable(exception, exceptionType, topFrame); return true;
            case OrganizationsLog.Keys.OrgTeamUnavailable: logger.OrgTeamUnavailable(exception, exceptionType, topFrame); return true;
            case HelpLog.Keys.HelpDownloadFailed: logger.HelpDownloadFailed(exception, exceptionType, topFrame); return true;
            default: return false;
        }
    }
}
