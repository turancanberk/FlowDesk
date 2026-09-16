"use client";

import * as React from "react";
import { cn } from "cn";
import { Label } from "@/components/ui/label";

/*
  Form alanı: etiket, girdi, yardımcı metin ve hata mesajını tek bir erişilebilir
  birim olarak bağlar (docs/DESIGN_SYSTEM.md bölüm 10).

  Yapı her zaman aynıdır:
    Etiket
    [Girdi]
    Yardımcı metin
    Hata mesajı

  Dekoratif kayan etiket kullanılmaz ve yer tutucu etiketin yerine geçmez.
  Bağlama işi bileşende yapılır; çağıran tarafın her seferinde `id`,
  `aria-describedby` ve `aria-invalid` eşleştirmesini elle kurması gerekmez.
*/

type FieldContextValue = {
  controlId: string;
  descriptionId: string;
  errorId: string;
  hasError: boolean;
  hasDescription: boolean;
};

const FieldContext = React.createContext<FieldContextValue | null>(null);

function useFieldContext(componentName: string): FieldContextValue {
  const context = React.useContext(FieldContext);

  if (context === null) {
    throw new Error(`${componentName} bir <Field> içinde kullanılmalıdır.`);
  }

  return context;
}

type FieldProps = React.ComponentProps<"div"> & {
  /** Alanın bir doğrulama hatası taşıyıp taşımadığı. */
  invalid?: boolean;
  /** Yardımcı metnin var olup olmadığı; aria-describedby bağlamasını belirler. */
  described?: boolean;
};

function Field({ className, invalid = false, described = false, children, ...props }: FieldProps) {
  const reactId = React.useId();

  const value = React.useMemo<FieldContextValue>(
    () => ({
      controlId: `${reactId}-control`,
      descriptionId: `${reactId}-description`,
      errorId: `${reactId}-error`,
      hasError: invalid,
      hasDescription: described,
    }),
    [reactId, invalid, described],
  );

  return (
    <FieldContext.Provider value={value}>
      <div data-slot="field" className={cn("flex flex-col gap-1.5", className)} {...props}>
        {children}
      </div>
    </FieldContext.Provider>
  );
}

type FieldLabelProps = React.ComponentProps<typeof Label> & {
  /** Zorunlu alanlar görsel ve ekran okuyucu için işaretlenir. */
  required?: boolean;
};

function FieldLabel({ className, children, required = false, ...props }: FieldLabelProps) {
  const { controlId } = useFieldContext("FieldLabel");

  return (
    <Label htmlFor={controlId} className={cn("text-xs", className)} {...props}>
      {children}
      {required ? (
        <span className="text-danger" aria-hidden="true">
          *
        </span>
      ) : null}
      {required ? <span className="sr-only">(zorunlu)</span> : null}
    </Label>
  );
}

/**
 * Girdiyi etiket ve açıklamalarla bağlar. Tek bir form kontrolü sarmalar.
 */
function FieldControl({ children }: { children: React.ReactElement }) {
  const { controlId, descriptionId, errorId, hasError, hasDescription } =
    useFieldContext("FieldControl");

  const describedBy =
    [hasDescription ? descriptionId : null, hasError ? errorId : null].filter(Boolean).join(" ") ||
    undefined;

  return React.cloneElement(children, {
    id: controlId,
    "aria-describedby": describedBy,
    "aria-invalid": hasError || undefined,
  } as React.HTMLAttributes<HTMLElement>);
}

function FieldDescription({ className, ...props }: React.ComponentProps<"p">) {
  const { descriptionId } = useFieldContext("FieldDescription");

  return (
    <p
      id={descriptionId}
      data-slot="field-description"
      className={cn("text-muted-foreground text-xs", className)}
      {...props}
    />
  );
}

function FieldError({ className, children, ...props }: React.ComponentProps<"p">) {
  const { errorId } = useFieldContext("FieldError");

  if (children === undefined || children === null || children === false) {
    return null;
  }

  return (
    <p
      id={errorId}
      data-slot="field-error"
      // role="alert" yerine aria-live: hata metni odak kaymadan da duyurulur.
      aria-live="polite"
      className={cn("text-danger text-xs", className)}
      {...props}
    >
      {children}
    </p>
  );
}

export { Field, FieldControl, FieldDescription, FieldError, FieldLabel };
