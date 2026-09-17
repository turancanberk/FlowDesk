using FlowDesk.Application.Common;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets;

public static class TicketErrors
{
    /// <summary>
    /// Used both when no such ticket exists and when it belongs to another
    /// workspace (ADR-0007).
    /// </summary>
    public static ApplicationError NotFound =>
        ApplicationError.NotFound("ticket.not_found", "Talep bulunamadı.");

    /// <summary>
    /// The chosen customer does not exist in this workspace.
    /// </summary>
    /// <remarks>
    /// Deliberately not distinguished from "no such customer": telling a caller
    /// that an id exists but belongs elsewhere would confirm another
    /// organisation's records (docs/SECURITY.md).
    /// </remarks>
    public static ApplicationError CustomerNotFound =>
        ApplicationError.Validation(
            "ticket.customer_not_found",
            "Seçilen müşteri bu çalışma alanında bulunamadı.");

    public static ApplicationError AssigneeNotAMember =>
        ApplicationError.Validation(
            "ticket.assignee_not_a_member",
            "Talep yalnızca çalışma alanının üyelerine atanabilir.");

    public static ApplicationError InvalidTransition(TicketStatus from, TicketStatus to) =>
        ApplicationError.Conflict(
            "ticket.invalid_transition",
            $"Talep {Describe(from)} durumundan {Describe(to)} durumuna geçirilemez.");

    /// <summary>
    /// Turkish name of a status, for error messages.
    /// </summary>
    /// <remarks>
    /// Kept out of <see cref="TicketStatus"/> itself: the domain should not know
    /// which language its reader speaks. Error text is already written in
    /// Turkish throughout this layer, so this is where the mapping belongs. The
    /// switch is exhaustive, so adding a status makes this a compile error
    /// rather than a silently English message.
    /// </remarks>
    private static string Describe(TicketStatus status) => status switch
    {
        TicketStatus.Open => "açık",
        TicketStatus.InProgress => "işlemde",
        TicketStatus.Waiting => "beklemede",
        TicketStatus.Resolved => "çözüldü",
        TicketStatus.Closed => "kapalı",
        _ => status.ToString(),
    };

    /// <summary>
    /// Someone else changed the ticket between the read and the write.
    /// </summary>
    /// <remarks>
    /// Returned as a conflict rather than silently winning, so the person who
    /// is about to overwrite a colleague's change finds out before it happens.
    /// </remarks>
    public static ApplicationError ConcurrencyConflict =>
        ApplicationError.Conflict(
            "ticket.concurrency_conflict",
            "Bu talep siz düzenlerken başka biri tarafından güncellendi. Sayfayı yenileyip değişikliklerinizi tekrar uygulayın.");

    public static ApplicationError AttachmentNotFound =>
        ApplicationError.NotFound("attachment.not_found", "Dosya bulunamadı.");

    /// <summary>
    /// The stored bytes are gone although the record is still here.
    /// </summary>
    /// <remarks>
    /// Reported as its own failure rather than a 404. The record exists and the
    /// person is entitled to it, so "not found" would send them looking for a
    /// mistake they did not make; this says the file is unavailable, which is
    /// what an operator needs to hear too.
    /// </remarks>
    public static ApplicationError AttachmentContentMissing =>
        ApplicationError.Conflict(
            "attachment.content_missing",
            "Dosyanın içeriği depolamada bulunamadı. Lütfen yeniden yükleyin.");

    public static ApplicationError AttachmentTooLarge(long maximumBytes) =>
        ApplicationError.Validation(
            "attachment.too_large",
            $"Dosya boyutu en fazla {maximumBytes / (1024 * 1024)} MB olabilir.");

    public static ApplicationError AttachmentTypeNotAllowed =>
        ApplicationError.Validation(
            "attachment.type_not_allowed",
            "Bu dosya türü yüklenemiyor. Görsel, PDF, metin, tablo ve arşiv dosyaları kabul edilir.");

    public static ApplicationError AttachmentEmpty =>
        ApplicationError.Validation("attachment.empty", "Boş dosya yüklenemez.");

    public static ApplicationError CommentNotAllowed =>
        ApplicationError.Forbidden("ticket.comment_forbidden", "Talebe yorum eklemek için yetkiniz yok.");
}
