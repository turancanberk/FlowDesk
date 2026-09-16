using FlowDesk.Application.Common;

namespace FlowDesk.Application.Customers;

public static class CustomerErrors
{
    /// <summary>
    /// Used both when no such customer exists and when it belongs to another
    /// workspace (ADR-0007).
    /// </summary>
    public static ApplicationError NotFound =>
        ApplicationError.NotFound("customer.not_found", "Müşteri bulunamadı.");

    public static ApplicationError Archived =>
        ApplicationError.Conflict(
            "customer.archived",
            "Arşivlenmiş müşteri düzenlenemez. Önce arşivden çıkarın.");
}
