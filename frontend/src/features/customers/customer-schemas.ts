import { z } from "zod";

/*
  Mirrors FlowDesk.Application CreateCustomerValidator. The server validates
  independently; this exists so a mistake becomes an inline message rather than
  a round trip.

  Optional fields accept an empty string and are turned into null on submit: a
  blank field means "not provided", and sending "" would store an empty string
  that sorts and filters differently from a missing value.
*/

export const customerSchema = z.object({
  name: z
    .string()
    .min(1, "Müşteri adı zorunludur.")
    .max(160, "Müşteri adı en fazla 160 karakter olabilir."),
  email: z
    .string()
    .max(256, "E-posta adresi en fazla 256 karakter olabilir.")
    .refine(
      (value) => value.trim().length === 0 || z.string().email().safeParse(value.trim()).success,
      "Geçerli bir e-posta adresi girin.",
    ),
  phone: z.string().max(40, "Telefon en fazla 40 karakter olabilir."),
  company: z.string().max(160, "Şirket adı en fazla 160 karakter olabilir."),
  status: z.enum(["Active", "Inactive"]),
  notes: z.string().max(4000, "Notlar en fazla 4000 karakter olabilir."),
});

export type CustomerFormValues = z.infer<typeof customerSchema>;

/** Turns blank optional fields into null before sending. */
export function toCustomerInput(values: CustomerFormValues) {
  const emptyToNull = (value: string) => (value.trim().length === 0 ? null : value.trim());

  return {
    name: values.name.trim(),
    email: emptyToNull(values.email),
    phone: emptyToNull(values.phone),
    company: emptyToNull(values.company),
    status: values.status,
    notes: emptyToNull(values.notes),
  };
}
