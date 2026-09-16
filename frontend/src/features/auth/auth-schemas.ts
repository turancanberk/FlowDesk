import { z } from "zod";

/*
  Client-side form schemas.

  These exist for the person filling in the form: they turn a mistake into an
  inline message without a round trip. They are not a security control and the
  server does not trust them — it runs its own validation on every request
  (docs/SECURITY.md).

  The rules mirror the server's so that the two do not contradict each other in
  front of the user.
*/

/** Matches FlowDesk.Application RegisterUserValidator. */
const MINIMUM_PASSWORD_LENGTH = 10;

export const loginSchema = z.object({
  email: z.string().min(1, "E-posta adresi zorunludur.").email("Geçerli bir e-posta adresi girin."),
  password: z.string().min(1, "Parola zorunludur."),
});

export type LoginFormValues = z.infer<typeof loginSchema>;

export const registerSchema = z.object({
  displayName: z
    .string()
    .min(1, "Ad soyad zorunludur.")
    .max(120, "Ad soyad en fazla 120 karakter olabilir."),
  email: z
    .string()
    .min(1, "E-posta adresi zorunludur.")
    .max(256, "E-posta adresi en fazla 256 karakter olabilir.")
    .email("Geçerli bir e-posta adresi girin."),
  password: z
    .string()
    .min(MINIMUM_PASSWORD_LENGTH, `Parola en az ${MINIMUM_PASSWORD_LENGTH} karakter olmalıdır.`),
});

export type RegisterFormValues = z.infer<typeof registerSchema>;
