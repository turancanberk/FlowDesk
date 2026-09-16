"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Field,
  FieldControl,
  FieldDescription,
  FieldError,
  FieldLabel,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api/api-error";
import { useRegister } from "./auth-queries";
import { registerSchema, type RegisterFormValues } from "./auth-schemas";

export function RegisterForm() {
  const router = useRouter();
  const registerMutation = useRegister();

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { displayName: "", email: "", password: "" },
  });

  const [formError, setFormError] = React.useState<string | null>(null);

  const submit = handleSubmit((values) => {
    setFormError(null);

    registerMutation.mutate(values, {
      onSuccess: () => {
        router.replace("/");
      },
      onError: (error) => {
        if (!(error instanceof ApiError)) {
          setFormError("Hesap oluşturulamadı. Lütfen tekrar deneyin.");
          return;
        }

        // A taken address is about one field, so it belongs on that field
        // where the person can fix it.
        if (error.code === "auth.email_already_registered") {
          setError("email", { message: error.message });
          return;
        }

        if (error.code === "auth.weak_password") {
          setError("password", { message: error.message });
          return;
        }

        // Field-level messages from server-side validation take precedence
        // over the summary line.
        const fieldNames = Object.keys(error.fieldErrors);

        if (fieldNames.length > 0) {
          for (const field of fieldNames) {
            const messages = error.fieldErrors[field];

            if (messages !== undefined && messages.length > 0 && isFormField(field)) {
              setError(field, { message: messages[0] });
            }
          }

          return;
        }

        setFormError(error.message);
      },
    });
  });

  return (
    <form
      // handleSubmit returns a promise and React Hook Form already handles its
      // rejection, so the result is discarded explicitly rather than left
      // floating.
      onSubmit={(event) => {
        void submit(event);
      }}
      noValidate
      className="flex flex-col gap-4"
    >
      {formError === null ? null : (
        <Alert variant="destructive">
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      <Field invalid={errors.displayName !== undefined}>
        <FieldLabel required>Ad soyad</FieldLabel>
        <FieldControl>
          <Input autoComplete="name" placeholder="Ayşe Demir" {...register("displayName")} />
        </FieldControl>
        <FieldError>{errors.displayName?.message}</FieldError>
      </Field>

      <Field invalid={errors.email !== undefined}>
        <FieldLabel required>E-posta</FieldLabel>
        <FieldControl>
          <Input
            type="email"
            autoComplete="email"
            placeholder="ad.soyad@sirket.com"
            {...register("email")}
          />
        </FieldControl>
        <FieldError>{errors.email?.message}</FieldError>
      </Field>

      <Field invalid={errors.password !== undefined} described>
        <FieldLabel required>Parola</FieldLabel>
        <FieldControl>
          <Input type="password" autoComplete="new-password" {...register("password")} />
        </FieldControl>
        <FieldDescription>En az 10 karakter.</FieldDescription>
        <FieldError>{errors.password?.message}</FieldError>
      </Field>

      <Button type="submit" size="lg" disabled={registerMutation.isPending}>
        {registerMutation.isPending ? "Hesap oluşturuluyor…" : "Hesap oluştur"}
      </Button>
    </form>
  );
}

function isFormField(field: string): field is keyof RegisterFormValues {
  return field === "displayName" || field === "email" || field === "password";
}
