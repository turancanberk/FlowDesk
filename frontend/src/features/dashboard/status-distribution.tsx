"use client";

import { TicketStatusBadge } from "@/components/product/status-badge";
import { formatNumber } from "@/lib/format";
import type { Tone } from "@/lib/domain-labels";
import { ticketStatusLabels } from "@/lib/domain-labels";
import type { TicketStatusCount } from "./dashboard-types";

/*
  A stacked bar built from divs rather than a charting library.

  Five categories that always sum to a known total is a proportion, and a
  proportion reads fine as a single bar. Pulling in a charting dependency for
  this would add hundreds of kilobytes, a second rendering model and an
  accessibility surface of its own, for a picture the browser already draws.

  The bar is decorative: every figure beside it is also written out in the
  legend, so nothing here depends on seeing colour (docs/DESIGN_SYSTEM.md).
*/

const toneBackground: Record<Tone, string> = {
  neutral: "bg-neutral-400",
  info: "bg-info",
  accent: "bg-accent-text",
  warning: "bg-warning",
  success: "bg-success",
  danger: "bg-danger",
};

export function StatusDistribution({ counts }: { counts: TicketStatusCount[] }) {
  const total = counts.reduce((sum, entry) => sum + entry.count, 0);

  if (total === 0) {
    return (
      <p className="text-muted-foreground text-sm">
        Henüz talep yok. İlk talep açıldığında dağılım burada görünecek.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div
        className="bg-muted flex h-2 w-full overflow-hidden rounded-full"
        // The legend below carries the same information as text, so the bar
        // itself has nothing to announce.
        aria-hidden="true"
      >
        {counts
          .filter((entry) => entry.count > 0)
          .map((entry) => (
            <div
              key={entry.status}
              className={toneBackground[ticketStatusLabels[entry.status].tone]}
              style={{ width: `${(entry.count / total) * 100}%` }}
            />
          ))}
      </div>

      <dl className="flex flex-col gap-2">
        {counts.map((entry) => (
          <div key={entry.status} className="flex items-center justify-between gap-3">
            <dt>
              <TicketStatusBadge status={entry.status} />
            </dt>
            <dd className="text-foreground text-sm font-medium tabular-nums">
              {formatNumber(entry.count)}
            </dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
