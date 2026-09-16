import { z } from "zod";

/*
  Mirrors FlowDesk.Application CreateTaskValidator and UpdateTaskValidator. The
  server validates independently; this exists so a mistake becomes an inline
  message rather than a round trip.
*/

export const taskSchema = z.object({
  title: z
    .string()
    .min(1, "Görev başlığı zorunludur.")
    .max(200, "Başlık en fazla 200 karakter olabilir."),
  description: z.string().max(4000, "Açıklama en fazla 4000 karakter olabilir."),
  /** Empty string is the {@link NONE} sentinel's own value for date inputs. */
  dueAt: z.string(),
  customerId: z.string(),
  assignedUserId: z.string(),
});

export type TaskFormValues = z.infer<typeof taskSchema>;

/**
 * The value a picker carries when nothing is chosen.
 *
 * A named sentinel rather than an empty string, because a select treats an
 * empty value as "nothing selected" and falls back to its placeholder, leaving
 * the control blank where it should read "Atanmamış" or "Müşteri yok". No id
 * can collide with it; ids are GUIDs.
 */
export const NONE = "none";
