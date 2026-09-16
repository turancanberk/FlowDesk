using FlowDesk.Application.Common;

namespace FlowDesk.Application.Tasks;

public static class TaskErrors
{
    /// <summary>
    /// Used both when no such task exists and when it belongs to another
    /// workspace (ADR-0007).
    /// </summary>
    public static ApplicationError NotFound =>
        ApplicationError.NotFound("task.not_found", "Görev bulunamadı.");

    /// <remarks>
    /// Deliberately not distinguished from "no such customer": telling a caller
    /// that an id exists but belongs elsewhere would confirm another
    /// organisation's records (docs/SECURITY.md).
    /// </remarks>
    public static ApplicationError CustomerNotFound =>
        ApplicationError.Validation(
            "task.customer_not_found",
            "Seçilen müşteri bu çalışma alanında bulunamadı.");

    public static ApplicationError AssigneeNotAMember =>
        ApplicationError.Validation(
            "task.assignee_not_a_member",
            "Görev yalnızca çalışma alanının üyelerine atanabilir.");
}
