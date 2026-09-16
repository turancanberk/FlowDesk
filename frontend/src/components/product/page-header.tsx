import { cn } from "cn";

/*
  Sayfa başlığı.

  Başlık solda, birincil eylem sağda, altında ince ayraç. Başlığın yanına
  dekoratif ikon konmaz (docs/DESIGN_SYSTEM.md bölüm 11).

  Hiyerarşi punto büyüterek değil, ağırlık ve renkle kurulur; 20px başlık bir
  operasyon ekranı için yeterlidir.
*/

type PageHeaderProps = {
  title: string;
  /** Başlığın altında tek cümlelik bağlam. Zorunlu değildir; doldurmak için yazılmaz. */
  description?: string;
  /** Sayfanın birincil eylemi. Bir sayfada tek birincil eylem bulunur. */
  actions?: React.ReactNode;
  className?: string;
};

function PageHeader({ title, description, actions, className }: PageHeaderProps) {
  return (
    <header className={cn("border-border border-b pb-4", className)}>
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-foreground truncate text-xl font-semibold">{title}</h1>
          {description ? <p className="text-muted-foreground mt-1 text-sm">{description}</p> : null}
        </div>

        {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
      </div>
    </header>
  );
}

export { PageHeader };
