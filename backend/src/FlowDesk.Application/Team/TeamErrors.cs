using FlowDesk.Application.Common;

namespace FlowDesk.Application.Team;

public static class TeamErrors
{
    public static ApplicationError MemberNotFound =>
        ApplicationError.NotFound("member.not_found", "Ekip üyesi bulunamadı.");

    public static ApplicationError AlreadyAMember =>
        ApplicationError.Conflict(
            "member.already_exists",
            "Bu kişi zaten çalışma alanının üyesi.");

    public static ApplicationError LastOwner =>
        ApplicationError.Conflict(
            "member.last_owner",
            "Çalışma alanının son sahibi bu işlemi yapamaz. Önce başka bir sahip belirleyin.");

    public static ApplicationError CannotManageOwner =>
        ApplicationError.Forbidden(
            "member.cannot_manage_owner",
            "Bir sahibin rolünü yalnızca başka bir sahip değiştirebilir.");

    /// <summary>
    /// Returned for every failed acceptance: unknown token, expired, already
    /// used, withdrawn, or addressed to someone else.
    /// </summary>
    /// <remarks>
    /// One message for all of them on purpose. Telling a caller that a token
    /// exists but has expired, or that it belongs to a different address, hands
    /// them information they did not have — and the person holding a valid link
    /// never sees this error anyway.
    /// </remarks>
    public static ApplicationError InvitationNotUsable =>
        ApplicationError.NotFound(
            "invitation.not_usable",
            "Bu davet bağlantısı geçerli değil. Süresi dolmuş, kullanılmış veya iptal edilmiş olabilir.");

    public static ApplicationError InvitationNotFound =>
        ApplicationError.NotFound("invitation.not_found", "Davet bulunamadı.");

    public static ApplicationError InvitationAlreadyPending =>
        ApplicationError.Conflict(
            "invitation.already_pending",
            "Bu adrese gönderilmiş bekleyen bir davet zaten var.");
}
