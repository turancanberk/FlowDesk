using FlowDesk.Application.Common;

namespace FlowDesk.Application.Authentication;

/// <summary>
/// Failures the authentication use cases can return.
/// </summary>
/// <remarks>
/// Collected in one place so that the messages stay consistent and so that the
/// deliberate vagueness of <see cref="InvalidCredentials"/> is visible rather
/// than scattered. Telling a caller which half of the credentials was wrong
/// turns the sign-in endpoint into an account enumeration oracle.
/// </remarks>
public static class AuthenticationErrors
{
    public static ApplicationError EmailAlreadyRegistered =>
        ApplicationError.Conflict(
            "auth.email_already_registered",
            "Bu e-posta adresi zaten kayıtlı.");

    public static ApplicationError InvalidCredentials =>
        ApplicationError.Unauthorized(
            "auth.invalid_credentials",
            "E-posta veya parola hatalı.");

    public static ApplicationError SessionNotFound =>
        ApplicationError.Unauthorized(
            "auth.session_not_found",
            "Oturumunuz geçerli değil. Lütfen tekrar giriş yapın.");

    public static ApplicationError SessionExpired =>
        ApplicationError.Unauthorized(
            "auth.session_expired",
            "Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.");

    public static ApplicationError SessionRevoked =>
        ApplicationError.Unauthorized(
            "auth.session_revoked",
            "Oturumunuz sonlandırıldı. Lütfen tekrar giriş yapın.");

    /// <summary>
    /// Another request exchanged the same token a moment earlier.
    /// </summary>
    /// <remarks>
    /// Not a revocation: two tabs refreshing at once is ordinary, and the other
    /// tab now holds a working session in the shared cookie.
    /// </remarks>
    public static ApplicationError SessionSuperseded =>
        ApplicationError.Unauthorized(
            "auth.session_superseded",
            "Oturumunuz başka bir sekmede yenilendi. Lütfen sayfayı yenileyin.");

    public static ApplicationError AccountNotFound =>
        ApplicationError.Unauthorized(
            "auth.account_not_found",
            "Hesabınıza ulaşılamadı. Lütfen tekrar giriş yapın.");

    public static ApplicationError WeakPassword(string reason) =>
        ApplicationError.Validation("auth.weak_password", reason);
}
