import type {
  CustomerStatus,
  MembershipRole,
  TaskStatus,
  TicketPriority,
  TicketStatus,
} from "@/types/domain";

/*
  Domain kodlarının Türkçe karşılıkları ve görsel tonları.

  Renk tek başına anlam taşımaz; her rozet metin de içerir. Renk körlüğü olan
  kullanıcılar ayrımı metinden okur (docs/DESIGN_SYSTEM.md bölüm 3).

  Record<...> kullanımı bilinçlidir: domain'e yeni bir durum eklendiğinde
  buradaki eşlemenin eksik kalması derleme hatası üretir.
*/

/** Rozet tonu; docs/DESIGN_SYSTEM.md içindeki anlamsal renk ailelerine karşılık gelir. */
export type Tone = "neutral" | "info" | "accent" | "warning" | "success" | "danger";

export type DomainLabel = {
  label: string;
  tone: Tone;
};

export const ticketStatusLabels: Record<TicketStatus, DomainLabel> = {
  Open: { label: "Açık", tone: "info" },
  InProgress: { label: "Devam Ediyor", tone: "accent" },
  Waiting: { label: "Bekliyor", tone: "warning" },
  Resolved: { label: "Çözüldü", tone: "success" },
  Closed: { label: "Kapalı", tone: "neutral" },
};

export const ticketPriorityLabels: Record<TicketPriority, DomainLabel> = {
  Low: { label: "Düşük", tone: "neutral" },
  Medium: { label: "Orta", tone: "info" },
  High: { label: "Yüksek", tone: "warning" },
  Urgent: { label: "Acil", tone: "danger" },
};

export const taskStatusLabels: Record<TaskStatus, DomainLabel> = {
  Todo: { label: "Yapılacak", tone: "neutral" },
  InProgress: { label: "Devam Ediyor", tone: "accent" },
  Done: { label: "Tamamlandı", tone: "success" },
};

export const customerStatusLabels: Record<CustomerStatus, DomainLabel> = {
  Active: { label: "Aktif", tone: "success" },
  Inactive: { label: "Pasif", tone: "neutral" },
};

/** Sıralama seçeneklerinin görünen adları. */
export const customerSortLabels: Record<
  "RecentlyUpdated" | "NameAscending" | "NameDescending" | "RecentlyCreated",
  string
> = {
  RecentlyUpdated: "Son güncellenen",
  NameAscending: "Ada göre (A-Z)",
  NameDescending: "Ada göre (Z-A)",
  RecentlyCreated: "Son eklenen",
};

export const membershipRoleLabels: Record<MembershipRole, DomainLabel> = {
  Owner: { label: "Sahip", tone: "accent" },
  Admin: { label: "Yönetici", tone: "info" },
  Agent: { label: "Temsilci", tone: "neutral" },
  Viewer: { label: "İzleyici", tone: "neutral" },
};
