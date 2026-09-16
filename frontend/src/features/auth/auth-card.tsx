import Link from "next/link";
import { cn } from "cn";

/*
  Shell for the sign-in and registration screens.

  A centred single-column layout is right here and only here: these are the two
  screens where the person has exactly one thing to do. Operational pages keep
  the dense left-aligned layout (docs/DESIGN_SYSTEM.md).
*/

type AuthCardProps = {
  title: string;
  description: string;
  children: React.ReactNode;
  footer: { question: string; linkLabel: string; href: string };
  className?: string;
};

export function AuthCard({ title, description, children, footer, className }: AuthCardProps) {
  return (
    <main className={cn("flex flex-1 items-center justify-center px-4 py-12", className)}>
      <div className="w-full max-w-sm">
        <p className="text-muted-foreground font-mono text-[11px] tracking-widest uppercase">
          FlowDesk
        </p>

        <h1 className="text-foreground mt-2 text-xl font-semibold">{title}</h1>
        <p className="text-muted-foreground mt-1 text-sm">{description}</p>

        <div className="border-border bg-card mt-6 rounded-xl border p-5">{children}</div>

        <p className="text-muted-foreground mt-4 text-center text-sm">
          {footer.question}{" "}
          <Link
            href={footer.href}
            className="text-primary rounded-sm font-medium underline-offset-4 hover:underline"
          >
            {footer.linkLabel}
          </Link>
        </p>
      </div>
    </main>
  );
}
