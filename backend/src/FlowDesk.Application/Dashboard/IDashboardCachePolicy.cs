namespace FlowDesk.Application.Dashboard;

/// <summary>
/// How long a dashboard may be out of date.
/// </summary>
/// <remarks>
/// A contract rather than an options type, so the application states what it
/// needs without reaching for infrastructure's configuration (ADR-0021).
/// </remarks>
public interface IDashboardCachePolicy
{
    TimeSpan DashboardTtl { get; }
}
