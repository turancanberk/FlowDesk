import { cn } from "cn";

/*
  Boş durum.

  Kısa başlık, bir cümlelik açıklama, tek eylem. Büyük illüstrasyon
  kullanılmaz (docs/DESIGN_SYSTEM.md bölüm 11).

  "Hiç kayıt yok" ile "filtreye uyan kayıt yok" ayrı durumlardır ve ayrı metin
  gösterirler; ikisini tek bileşene yıkmak kullanıcıya yanlış şey söyler.
  Bu yüzden metin çağıran taraftan gelir.
*/

type EmptyStateProps = {
  title: string;
  description: string;
  /** Kullanıcıyı ileri taşıyan tek eylem. */
  action?: React.ReactNode;
  /** Durumu netleştiren küçük bir ikon. Süs amacıyla kullanılmaz. */
  icon?: React.ReactNode;
  className?: string;
};

function EmptyState({ title, description, action, icon, className }: EmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center gap-2 px-6 py-12 text-center",
        className,
      )}
    >
      {icon ? (
        <div className="text-muted-foreground mb-1 [&_svg]:size-5" aria-hidden="true">
          {icon}
        </div>
      ) : null}

      <h2 className="text-foreground text-sm font-semibold">{title}</h2>
      <p className="text-muted-foreground max-w-sm text-sm">{description}</p>

      {action ? <div className="mt-3">{action}</div> : null}
    </div>
  );
}

export { EmptyState };
