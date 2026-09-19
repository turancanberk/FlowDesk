import { z } from "zod";

/*
  Mirrors FlowDesk.Application CreateWorkspaceValidator. The server validates
  independently; this exists so a mistake becomes an inline message instead of a
  round trip.
*/

const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

const workspaceName = z
  .string()
  .min(1, "Çalışma alanı adı zorunludur.")
  .max(120, "Çalışma alanı adı en fazla 120 karakter olabilir.");

export const createWorkspaceSchema = z.object({
  name: workspaceName,
  slug: z
    .string()
    .trim()
    .refine(
      (value) =>
        value.length === 0 || (value.length >= 3 && value.length <= 40 && SLUG_PATTERN.test(value)),
      "Adres 3-40 karakter olmalı; yalnızca küçük harf, rakam ve tek tire içerebilir.",
    ),
});

export type CreateWorkspaceFormValues = z.infer<typeof createWorkspaceSchema>;

/** Mirrors FlowDesk.Application UpdateWorkspaceValidator: the name is all that changes. */
export const updateWorkspaceSchema = z.object({ name: workspaceName });

export type UpdateWorkspaceFormValues = z.infer<typeof updateWorkspaceSchema>;
