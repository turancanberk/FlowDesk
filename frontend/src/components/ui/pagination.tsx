"use client";

import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { cn } from "cn";
import { Button } from "@/components/ui/button";

/*
  Sayfalama.

  Sayfa numaralarının tamamı basılmaz; operasyonel tablolarda kayıt sayısı
  binlere çıkabilir ve numara şeridi hem yer kaplar hem de tarama yapmayı
  zorlaştırır. Bunun yerine kaç kayıttan hangilerinin gösterildiği yazılır ve
  ileri/geri düğmeleri verilir.
*/

type PaginationProps = {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  className?: string;
};

function Pagination({ page, pageSize, totalCount, onPageChange, className }: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const firstOnPage = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastOnPage = Math.min(page * pageSize, totalCount);

  const canGoBack = page > 1;
  const canGoForward = page < totalPages;

  return (
    <nav
      aria-label="Sayfalama"
      className={cn(
        "border-border flex items-center justify-between gap-4 border-t px-3 py-2",
        className,
      )}
    >
      <p className="text-muted-foreground text-xs">
        {totalCount === 0 ? (
          "Kayıt yok"
        ) : (
          <>
            <span className="font-mono tabular-nums">
              {firstOnPage}–{lastOnPage}
            </span>{" "}
            / <span className="font-mono tabular-nums">{totalCount}</span> kayıt
          </>
        )}
      </p>

      <div className="flex items-center gap-2">
        <span className="text-muted-foreground text-xs">
          Sayfa <span className="font-mono tabular-nums">{page}</span> /{" "}
          <span className="font-mono tabular-nums">{totalPages}</span>
        </span>

        <Button
          variant="outline"
          size="icon-sm"
          aria-label="Önceki sayfa"
          disabled={!canGoBack}
          onClick={() => {
            onPageChange(page - 1);
          }}
        >
          <ChevronLeftIcon />
        </Button>

        <Button
          variant="outline"
          size="icon-sm"
          aria-label="Sonraki sayfa"
          disabled={!canGoForward}
          onClick={() => {
            onPageChange(page + 1);
          }}
        >
          <ChevronRightIcon />
        </Button>
      </div>
    </nav>
  );
}

export { Pagination };
