/*
  Türkçe biçimlendirme yardımcıları.

  Tarihler veritabanında ve API'de UTC olarak tipli taşınır; yerelleştirme
  yalnızca burada, sunum sınırında yapılır (CLAUDE.md, "Locale").

  Intl biçimlendiricileri her çağrıda yeniden oluşturulmaz; kurulum maliyeti
  bir listede yüzlerce hücre için tekrarlanacak kadar yüksektir.
*/

const LOCALE = "tr-TR";

const dateFormatter = new Intl.DateTimeFormat(LOCALE, {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
});

const dateTimeFormatter = new Intl.DateTimeFormat(LOCALE, {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

const relativeTimeFormatter = new Intl.RelativeTimeFormat(LOCALE, { numeric: "auto" });

const numberFormatter = new Intl.NumberFormat(LOCALE);

/** 16.09.2026 */
export function formatDate(value: Date | string): string {
  return dateFormatter.format(toDate(value));
}

/** 16.09.2026 14:35 */
export function formatDateTime(value: Date | string): string {
  return dateTimeFormatter.format(toDate(value));
}

/** 1.248 */
export function formatNumber(value: number): string {
  return numberFormatter.format(value);
}

/**
 * "3 gün önce", "dün", "2 saat sonra".
 *
 * En büyük anlamlı birime yuvarlar; "72 saat önce" yerine "3 gün önce" demek
 * bir etkinlik akışında daha okunur.
 */
export function formatRelativeTime(value: Date | string, now: Date = new Date()): string {
  const target = toDate(value);
  const diffInSeconds = (target.getTime() - now.getTime()) / 1000;

  const thresholds: { limit: number; unit: Intl.RelativeTimeFormatUnit; inSeconds: number }[] = [
    { limit: 60, unit: "second", inSeconds: 1 },
    { limit: 3600, unit: "minute", inSeconds: 60 },
    { limit: 86_400, unit: "hour", inSeconds: 3600 },
    { limit: 604_800, unit: "day", inSeconds: 86_400 },
    { limit: 2_629_800, unit: "week", inSeconds: 604_800 },
    { limit: 31_557_600, unit: "month", inSeconds: 2_629_800 },
  ];

  const magnitude = Math.abs(diffInSeconds);

  for (const threshold of thresholds) {
    if (magnitude < threshold.limit) {
      return relativeTimeFormatter.format(
        Math.round(diffInSeconds / threshold.inSeconds),
        threshold.unit,
      );
    }
  }

  return relativeTimeFormatter.format(Math.round(diffInSeconds / 31_557_600), "year");
}

function toDate(value: Date | string): Date {
  return value instanceof Date ? value : new Date(value);
}
