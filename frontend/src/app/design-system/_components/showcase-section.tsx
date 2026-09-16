import { cn } from "cn";

/*
  Vitrin bölümü sarmalayıcısı. Her bölüm bir başlık, kısa bir gerekçe ve
  örnekleri taşır. Gerekçe metni önemlidir: tasarım sistemi bir bileşen
  galerisi değil, kararların kaydıdır.
*/

type ShowcaseSectionProps = {
  id: string;
  title: string;
  description: string;
  children: React.ReactNode;
  className?: string;
};

function ShowcaseSection({ id, title, description, children, className }: ShowcaseSectionProps) {
  return (
    <section id={id} className={cn("border-border scroll-mt-16 border-b pb-10", className)}>
      <h2 className="text-foreground text-base font-semibold">{title}</h2>
      <p className="text-muted-foreground mt-1 max-w-2xl text-sm">{description}</p>
      <div className="mt-5">{children}</div>
    </section>
  );
}

/** Tek bir örneği başlığıyla birlikte gösterir. */
function Specimen({
  label,
  children,
  className,
}: {
  label: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("flex flex-col gap-2", className)}>
      <span className="text-muted-foreground font-mono text-[11px] tracking-wide uppercase">
        {label}
      </span>
      <div>{children}</div>
    </div>
  );
}

/** Örnekleri çerçeve içinde gösteren yüzey. */
function Surface({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={cn("border-border bg-card rounded-xl border p-4", className)}>{children}</div>
  );
}

export { ShowcaseSection, Specimen, Surface };
