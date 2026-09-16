"use client";

import { useRouter } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldControl, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api/api-error";
import { useLogin } from "./auth-queries";
import { loginSchema, type LoginFormValues } from "./auth-schemas";

export function LoginForm() {
  const router = useRouter();
  const loginMutation = useLogin();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  /*
    The server returns one message for a wrong password and for an unknown
    address, so it is shown at form level rather than pinned to a field.
    Attaching it to "password" would tell the visitor the address was
    recognised — exactly the enumeration the shared message prevents.
  */
  const formError =
    loginMutation.error instanceof ApiError
      ? loginMutation.error.message
      : loginMutation.error
        ? "Giriş yapılamadı. Lütfen tekrar deneyin."
        : null;

  const submit = handleSubmit((values) => {
    loginMutation.mutate(values, {
      onSuccess: () => {
        router.replace("/");
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

      <Field invalid={errors.password !== undefined}>
        <FieldLabel required>Parola</FieldLabel>
        <FieldControl>
          <Input type="password" autoComplete="current-password" {...register("password")} />
        </FieldControl>
        <FieldError>{errors.password?.message}</FieldError>
      </Field>

      <Button type="submit" size="lg" disabled={loginMutation.isPending}>
        {loginMutation.isPending ? "Giriş yapılıyor…" : "Giriş yap"}
      </Button>
    </form>
  );
}
