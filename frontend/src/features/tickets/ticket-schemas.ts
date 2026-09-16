import { z } from "zod";
import { TICKET_PRIORITIES } from "@/types/domain";

/*
  Mirrors FlowDesk.Application CreateTicketValidator and UpdateTicketValidator.
  The server validates independently; this exists so a mistake becomes an inline
  message rather than a round trip.
*/

export const ticketSchema = z.object({
  customerId: z.string().min(1, "Müşteri seçimi zorunludur."),
  subject: z.string().min(1, "Konu zorunludur.").max(200, "Konu en fazla 200 karakter olabilir."),
  description: z.string().max(8000, "Açıklama en fazla 8000 karakter olabilir."),
  priority: z.enum(TICKET_PRIORITIES),
  // Carries either a member's id or the {@link UNASSIGNED} sentinel, which
  // becomes null on submit.
  assignedUserId: z.string(),
});

export type TicketFormValues = z.infer<typeof ticketSchema>;

export const ticketCommentSchema = z.object({
  body: z
    .string()
    .trim()
    .min(1, "Yorum boş olamaz.")
    .max(4000, "Yorum en fazla 4000 karakter olabilir."),
});

export type TicketCommentFormValues = z.infer<typeof ticketCommentSchema>;

/**
 * The value the assignee picker carries when nobody holds the ticket.
 *
 * A named sentinel rather than an empty string, because a select treats an
 * empty value as "nothing selected" and would fall back to the placeholder —
 * leaving the control blank where it should read "Atanmamış". No user id can
 * collide with it; ids are GUIDs.
 */
export const UNASSIGNED = "unassigned";
