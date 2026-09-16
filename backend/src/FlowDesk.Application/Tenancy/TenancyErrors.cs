using FlowDesk.Application.Common;

namespace FlowDesk.Application.Tenancy;

public static class TenancyErrors
{
    /// <summary>
    /// Returned for both "no such workspace" and "you are not a member".
    /// </summary>
    /// <remarks>
    /// Distinguishing the two would confirm that a workspace exists, letting
    /// anyone enumerate organisations by guessing slugs. A caller who is not a
    /// member is told the same thing as a caller asking about nothing
    /// (ADR-0007).
    /// </remarks>
    public static ApplicationError WorkspaceNotFound =>
        ApplicationError.NotFound(
            "workspace.not_found",
            "Çalışma alanı bulunamadı.");

    public static ApplicationError SlugAlreadyTaken =>
        ApplicationError.Conflict(
            "workspace.slug_taken",
            "Bu çalışma alanı adresi zaten kullanılıyor. Başka bir adres deneyin.");

    public static ApplicationError InsufficientRole(string action) =>
        ApplicationError.Forbidden(
            "workspace.insufficient_role",
            $"{action} için yetkiniz yok.");
}
